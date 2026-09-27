using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// Shared by melee and shaped attacks: scheduling, proc dispatch, ownership and chain budgets.
public sealed class AttackExecutionRuntime : IDisposable
{
    public const int Capacity = 256;
    private static long nextId;
    private readonly Transform owner;
    private readonly LayerMask layers;
    private readonly CombatEffectStateStore effects;
    private readonly List<Scheduled> pending = new();
    private readonly List<LiveBody> bodies = new();
    private int generation;
    private int dispatchDepth;
    private bool disposed;
    private double clock;
    public int ActiveBodyCount => bodies.Count;
    public int PendingCount => pending.Count;

    private sealed class Chain
    {
        public long Root;
        public int Remaining;
        public int MaxDepth;
    }
    public sealed class AttackContext
    {
        internal AttackRecipe Recipe;
        internal AttackSnapshot Shot;
        internal CombatEffectRuntime[] Effects;
        internal long Id;
        internal int Depth;
        internal int Generation;
        internal bool Finished;
        internal object ChainState;
        public long AttackId => Id;
    }
    private sealed class Scheduled
    {
        public double Due;
        public AttackContext Context;
        public Vector2 Origin;
        public bool Channel;
        public float AimOffset;
    }
    private sealed class LiveBody
    {
        public AttackBodyRuntime Body;
        public AttackContext Context;
        public double Born;
        public float AimOffset;
    }
    public AttackExecutionRuntime(Transform owner, LayerMask layers, CombatEffectStateStore effects)
    { this.owner = owner; this.layers = layers; this.effects = effects; }

    public AttackContext CreateAttack(AttackRecipe recipe, AttackSnapshot shot)
    {
        if (disposed) throw new ObjectDisposedException(nameof(AttackExecutionRuntime));
        var context = CreateContext(recipe, shot, null, 0);
        var chain = (Chain)context.ChainState;
        chain.Root = context.Id;
        return context;
    }
    private AttackContext CreateContext(AttackRecipe recipe, AttackSnapshot shot, Chain chain, int depth)
    {
        return new AttackContext { Recipe = recipe.Copy(), Shot = shot, Id = Interlocked.Increment(ref nextId),
            Effects = effects.Bind(recipe.effects), Depth = depth, Generation = generation,
            ChainState = chain ?? new Chain { Remaining = recipe.maxChildAttacks, MaxDepth = recipe.maxChainDepth } };
    }
    private bool Alive(AttackContext context) => !disposed && context != null && !context.Finished && context.Generation == generation;

    public void Activate(AttackContext context, Vector2 origin)
    { Dispatch(context, origin, null, 0, default, (effect, evt) => effect.OnAttack(evt)); }

    public void Emit(AttackContext context, Vector2 origin, bool channel = false)
    {
        if (!Alive(context)) return;
        var schedule = new List<AttackEmission>();
        if (context.Recipe.emission != null) context.Recipe.emission.Build(context.Recipe, context.Shot, schedule);
        else AttackEmissionDefinition.Fan(context.Recipe, schedule);
        if (schedule.Count == 0 || schedule.Count > Capacity) throw new InvalidOperationException("Emission schedule must have 1..256 entries.");
        // Validate the whole schedule before mutating the queue.
        foreach (var entry in schedule)
            if (!Finite(entry.Delay) || entry.Delay < 0f || !Finite(entry.Angle) || !Finite(entry.Offset.x) || !Finite(entry.Offset.y))
                throw new ArgumentException("Invalid emission schedule.");
        if (schedule.Count + pending.Count + bodies.Count > Capacity) return;
        foreach (var entry in schedule)
        {
            var rotation = Quaternion.Euler(0, 0, entry.Angle);
            Vector2 aim = rotation * context.Shot.Direction;
            var shot = new AttackSnapshot(aim, context.Shot.Damage, context.Shot.Range,
                context.Shot.Duration, context.Shot.Charge, context.Shot.ProjectileSpeedMultiplier);
            var child = CreateContext(context.Recipe, shot, (Chain)context.ChainState, context.Depth);
            float yaw = Mathf.Atan2(context.Shot.Direction.y, context.Shot.Direction.x) * Mathf.Rad2Deg;
            pending.Add(new Scheduled { Due = clock + entry.Delay, Context = child,
                Origin = origin + (Vector2)(Quaternion.Euler(0, 0, yaw) * entry.Offset), Channel = channel, AimOffset = entry.Angle });
        }
        FlushDue();
    }

    private void FlushDue()
    {
        // Callbacks can enqueue descendants, but work per tick remains bounded.
        int count = 0;
        while (!disposed && pending.Count > 0 && count++ < Capacity)
        {
            int index = -1;
            double earliest = double.PositiveInfinity;
            for (int i = 0; i < pending.Count; i++)
                if (pending[i].Due <= clock && pending[i].Due < earliest) { index = i; earliest = pending[i].Due; }
            if (index < 0) return;
            var entry = pending[index];
            pending.RemoveAt(index);
            if (!Alive(entry.Context)) continue;
            var body = new AttackBodyRuntime(entry.Context.Recipe, entry.Context.Shot, entry.Context.Shot.Direction,
                owner, layers, entry.Origin, target => Hit(entry.Context, target), entry.Channel);
            bodies.Add(new LiveBody { Body = body, Context = entry.Context, Born = entry.Due, AimOffset = entry.AimOffset });
            Dispatch(entry.Context, entry.Origin, null, 0, default, (effect, evt) => effect.OnEmit(evt));
        }
    }

    public void Tick(float deltaTime)
    {
        if (disposed) return;
        clock += deltaTime;
        FlushDue();
        int epoch = generation;
        for (int i = bodies.Count - 1; i >= 0; i--)
        {
            var entry = bodies[i];
            float elapsed = (float)Math.Max(0d, clock - entry.Born);
            float dt = Mathf.Max(0f, elapsed - entry.Body.Age);
            bool alive = dt <= 0f || entry.Body.Tick(dt);
            if (disposed || epoch != generation) return;
            if (!alive)
            {
                Vector2 position = entry.Body.Position;
                entry.Body.Dispose();
                bodies.RemoveAt(i);
                if (entry.Body.ExpiredNaturally) Complete(entry.Context, position);
                else entry.Context.Finished = true;
                if (disposed || epoch != generation) return;
            }
        }
    }

    public DamageResult Hit(AttackContext context, EnemyActor target)
    {
        if (!Alive(context) || target == null || target.IsDead || !target.isActiveAndEnabled) return default;
        uint version = target.SpawnVersion;
        Vector2 position = target.transform.position;
        float damage = context.Shot.Damage;
        var before = Event(context, position, target, version, default);
        foreach (var effect in context.Effects)
        {
            damage = effect.ModifyDamage(before, damage);
            if (!Alive(context) || target == null || target.SpawnVersion != version || target.IsDead || !target.isActiveAndEnabled) return default;
        }
        var chain = (Chain)context.ChainState;
        var result = target.TakeDamage(new DamageContext(owner, damage, context.Shot.Direction,
            false, context.Depth, context.Id, chain.Root));
        if (result.AppliedDamage > 0f)
        {
            Dispatch(context, position, target, version, result, (effect, evt) => effect.OnHit(evt));
            if (result.Killed) Dispatch(context, position, target, version, result, (effect, evt) => effect.OnKill(evt));
        }
        return result;
    }
    public void Complete(AttackContext context, Vector2 position)
    {
        if (!Alive(context)) return;
        try { Dispatch(context, position, null, 0, default, (effect, evt) => effect.OnExpire(evt)); }
        finally { context.Finished = true; }
    }
    private CombatEvent Event(AttackContext context, Vector2 position, EnemyActor target, uint version, DamageResult result)
    {
        var chain = (Chain)context.ChainState;
        return new CombatEvent(owner, context.Shot, target, version, position, result, context.Id, chain.Root,
            context.Depth, effects.Time, (recipe, direction, scale) => QueueChild(context, recipe, direction, position, scale));
    }
    private void Dispatch(AttackContext context, Vector2 position, EnemyActor target, uint version,
        DamageResult result, Action<CombatEffectRuntime, CombatEvent> callback)
    {
        if (!Alive(context)) return;
        var evt = Event(context, position, target, version, result);
        dispatchDepth++;
        try { foreach (var effect in context.Effects) { if (!Alive(context)) break; callback(effect, evt); } }
        finally { dispatchDepth--; }
    }
    private bool QueueChild(AttackContext parent, AttackRecipe recipe, Vector2 direction, Vector2 position, float scale)
    {
        if (dispatchDepth == 0 || !Alive(parent) || recipe == null || recipe.shape == null ||
            !Finite(direction.x) || !Finite(direction.y) || direction.sqrMagnitude < 0.0001f || !Finite(scale) || scale <= 0f) return false;
        var chain = (Chain)parent.ChainState;
        if (parent.Depth >= chain.MaxDepth || chain.Remaining <= 0 || pending.Count + bodies.Count >= Capacity) return false;
        recipe = recipe.Copy();
        recipe.Validate();
        float damage = parent.Shot.Damage * scale * recipe.damageMultiplier;
        if (!Finite(damage) || damage <= 0f) return false;
        var shot = new AttackSnapshot(direction.normalized, damage, parent.Shot.Range,
            recipe.lifetime, parent.Shot.Charge, parent.Shot.ProjectileSpeedMultiplier);
        var child = CreateContext(recipe, shot, chain, parent.Depth + 1);
        chain.Remaining--;
        pending.Add(new Scheduled { Due = clock, Context = child, Origin = position });
        return true;
    }
    public void Cancel()
    {
        generation++;
        pending.Clear();
        foreach (var entry in bodies) entry.Body.Dispose();
        bodies.Clear();
    }
    public void SetChannelAim(Vector2 aim)
    {
        if (!Finite(aim.x) || !Finite(aim.y) || aim.sqrMagnitude < 0.0001f) return;
        foreach (var entry in bodies)
        {
            if (!entry.Body.IsChannel) continue;
            Vector2 direction = Quaternion.Euler(0, 0, entry.AimOffset) * aim.normalized;
            entry.Body.SetAim(direction);
            var shot = entry.Context.Shot;
            entry.Context.Shot = new AttackSnapshot(direction, shot.Damage, shot.Range, shot.Duration,
                shot.Charge, shot.ProjectileSpeedMultiplier);
        }
        foreach (var entry in pending)
        {
            if (!entry.Channel) continue;
            Vector2 direction = Quaternion.Euler(0, 0, entry.AimOffset) * aim.normalized;
            var shot = entry.Context.Shot;
            entry.Context.Shot = new AttackSnapshot(direction, shot.Damage, shot.Range, shot.Duration,
                shot.Charge, shot.ProjectileSpeedMultiplier);
        }
    }
    public void Dispose() { if (disposed) return; disposed = true; Cancel(); }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

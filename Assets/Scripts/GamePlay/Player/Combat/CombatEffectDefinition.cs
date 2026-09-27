using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class CombatEffectDefinition : ScriptableObject
{
    // Assets are immutable configuration; each owner gets a separate runtime.
    public abstract CombatEffectRuntime CreateRuntime();
}

public abstract class CombatEffectRuntime : IDisposable
{
    public virtual void OnAttack(CombatEvent context) { }
    public virtual void OnEmit(CombatEvent context) { }
    public virtual float ModifyDamage(CombatEvent context, float damage) => damage;
    public virtual void OnHit(CombatEvent context) { }
    public virtual void OnKill(CombatEvent context) { }
    public virtual void OnExpire(CombatEvent context) { }
    public virtual void Dispose() { }
}

public sealed class CombatEffectStateStore : IDisposable
{
    private readonly Dictionary<CombatEffectDefinition, CombatEffectRuntime> states = new();
    private bool disposed;
    public double Time { get; private set; }
    public void Advance(float deltaTime) { if (!disposed) Time += deltaTime; }
    public CombatEffectRuntime[] Bind(IReadOnlyList<CombatEffectDefinition> definitions)
    {
        if (disposed) throw new ObjectDisposedException(nameof(CombatEffectStateStore));
        var result = new List<CombatEffectRuntime>();
        var seen = new HashSet<CombatEffectDefinition>();
        if (definitions != null) foreach (var definition in definitions)
        {
            if (definition == null || !seen.Add(definition)) continue;
            if (!states.TryGetValue(definition, out var runtime))
            {
                runtime = definition.CreateRuntime() ?? throw new InvalidOperationException("Effect returned no runtime.");
                states.Add(definition, runtime);
            }
            result.Add(runtime);
        }
        return result.ToArray();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var runtime in states.Values)
        {
            try { runtime.Dispose(); }
            catch (Exception error) { Debug.LogException(error); }
        }
        states.Clear();
    }
}

public readonly struct CombatEvent
{
    public readonly Transform Owner;
    public readonly AttackSnapshot Attack;
    public readonly EnemyActor Target;
    public readonly uint TargetVersion;
    public readonly Vector2 Position;
    public readonly DamageResult Result;
    public readonly long AttackId;
    public readonly long RootAttackId;
    public readonly int ChainDepth;
    public readonly double Time;
    private readonly Func<AttackRecipe, Vector2, float, bool> spawn;
    internal CombatEvent(Transform owner, AttackSnapshot attack, EnemyActor target, uint targetVersion,
        Vector2 position, DamageResult result, long attackId, long rootAttackId, int depth, double time,
        Func<AttackRecipe, Vector2, float, bool> spawn)
    {
        Owner = owner; Attack = attack; Target = target; TargetVersion = targetVersion; Position = position;
        Result = result; AttackId = attackId; RootAttackId = rootAttackId; ChainDepth = depth; Time = time;
        this.spawn = spawn;
    }
    // Queued, never recursive. False means cancellation or a chain/capacity limit rejected it.
    public bool SpawnChild(AttackRecipe recipe, Vector2 direction, float damageScale = 1f) =>
        spawn != null && spawn(recipe, direction, damageScale);
}

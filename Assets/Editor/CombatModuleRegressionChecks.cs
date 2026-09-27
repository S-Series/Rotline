using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CombatModuleRegressionChecks
{
    private static readonly List<Object> garbage = new();
    private static int checks;
    [MenuItem("ROTLINE/Tests/Combat Modules")]
    public static void Run()
    {
        checks = 0;
        try
        {
            BurstAndCancel(); Channel(); EffectsAndMelee(); ChainLimits(); Combination(); Reentry(); Homing(); CustomTrigger();
            PeriodicProcAndExpiry(); ProceduralMelee();
            Debug.Log($"[CombatModules] All {checks} extension assertions passed.");
        }
        finally
        {
            for (int i = garbage.Count - 1; i >= 0; i--) if (garbage[i] != null) Object.DestroyImmediate(garbage[i]);
            garbage.Clear();
        }
    }
    private static void BurstAndCancel()
    {
        var stats = Owner();
        var enemy = Enemy(stats.transform.position + Vector3.right);
        var recipe = Recipe();
        recipe.emission = Asset<BurstEmissionDefinition>();
        using (var runtime = Runtime(stats, recipe))
        {
            runtime.PrimaryStarted(Vector2.right);
            Need(runtime.ActiveBodyCount == 1 && runtime.PendingEmissionCount == 2, "Burst must schedule, not emit all at once.");
            runtime.Tick(0.25f);
            Need(enemy.CurrentHealth == 70f && runtime.ActiveBodyCount == 3, "Long frame must process every due wave.");
            runtime.CancelAttack();
            Need(runtime.ActiveBodyCount == 0 && runtime.PendingEmissionCount == 0, "Cancel must clear bodies and queue.");
        }
        enemy.gameObject.SetActive(false);
        var fine = Enemy(stats.transform.position + Vector3.right);
        using (var runtime = Runtime(stats, recipe))
        {
            runtime.PrimaryStarted(Vector2.right);
            for (int i = 0; i < 5; i++) runtime.Tick(0.05f);
            Need(fine.CurrentHealth == 70f, "Burst result must be stable across frame subdivisions.");
        }
        using (var runtime = Runtime(stats, recipe))
        {
            runtime.PrimaryStarted(Vector2.right);
            runtime.CancelAttack(); runtime.Tick(2f);
            Need(runtime.ActiveBodyCount == 0 && runtime.PendingEmissionCount == 0, "Canceled delayed waves must not return.");
        }
    }
    private static void Channel()
    {
        var stats = Owner();
        var right = Enemy(stats.transform.position + Vector3.right);
        var up = Enemy(stats.transform.position + Vector3.up);
        var recipe = Recipe(); recipe.trigger = AttackTrigger.Channel; recipe.lifetime = 0.05f;
        recipe.trajectory = Asset<AttachedTrajectoryDefinition>();
        using (var runtime = Runtime(stats, recipe))
        {
            runtime.PrimaryStarted(Vector2.right); runtime.Tick(0.25f);
            Need(runtime.IsAttacking && runtime.ActiveBodyCount == 1 && right.CurrentHealth == 90f,
                "Channel must outlive ordinary lifetime without duplicating the beam.");
            runtime.PrimaryHeld(Vector2.up); runtime.Tick(0.02f);
            Need(up.CurrentHealth == 90f && runtime.ActiveBodyCount == 1, "Channel must follow aim while held.");
            runtime.PrimaryReleased(Vector2.up);
            Need(!runtime.IsAttacking && runtime.ActiveBodyCount == 0, "Channel release must stop immediately.");
        }
    }
    private static void EffectsAndMelee()
    {
        var stats = Owner();
        var probe = Asset<CombatProbeEffect>();
        var recipe = Recipe(); recipe.effects.Add(probe);
        using (var store = new CombatEffectStateStore())
        {
            using (var runtime = Runtime(stats, recipe, store)) runtime.PrimaryStarted(Vector2.right);
            using (var runtime = Runtime(stats, recipe, store)) runtime.PrimaryStarted(Vector2.right);
            Need(probe.Created.Count == 1 && probe.Created[0].Attacks == 2, "Effect state must survive runtime/loadout rebuilds.");
            using (var other = Runtime(Owner(), recipe)) other.PrimaryStarted(Vector2.right);
            Need(probe.Created.Count == 2 && probe.Created[1].Attacks == 1, "Two owners must not share proc counters.");
            Need(probe.Created[1].Disposed && !probe.Created[0].Disposed, "State disposal must respect owner lifetime.");
        }
        Need(probe.Created[0].Disposed, "Owner state must be disposed at session end.");
        var target = Enemy(stats.transform.position + Vector3.right);
        var sword = AssetDatabase.LoadAssetAtPath<WeaponAttackDefinition>("Assets/Data/Attack_Sword_Default..asset");
        var melee = new AttackRecipe(); melee.effects.Add(probe);
        using (var runtime = sword.CreateRuntime(stats, stats.transform, 1 << 30, melee))
        {
            runtime.PrimaryStarted(Vector2.right); runtime.Tick(2f);
            Need(probe.Created.Last().Attacks == 1 && probe.Created.Last().Hits == 1,
                "Melee must use the same attack/hit effect contract.");
            Need(target.CurrentHealth == 90f, "Melee adapter must preserve existing damage.");
        }
        var copy = recipe.Copy(); copy.effects.Clear();
        Need(recipe.effects.Count == 1, "Recipe copies must own their effect lists.");
    }
    private static void ChainLimits()
    {
        var stats = Owner();
        var effect = Asset<CombatProbeEffect>();
        var recipe = Recipe(); recipe.effects.Add(effect); recipe.maxChainDepth = 2; recipe.maxChildAttacks = 3;
        effect.OnEmit = evt => { evt.SpawnChild(recipe, Vector2.right); evt.SpawnChild(recipe, Vector2.left); };
        using (var store = new CombatEffectStateStore())
        using (var world = new AttackExecutionRuntime(stats.transform, 1 << 30, store))
        {
            var context = world.CreateAttack(recipe, Shot()); world.Emit(context, stats.transform.position);
            var events = effect.Created[0].Emitted;
            Need(world.ActiveBodyCount == 4, "A whole proc tree must share one descendant budget.");
            Need(events.Max(evt => evt.ChainDepth) == 2 && events.Select(evt => evt.RootAttackId).Distinct().Count() == 1,
                "Descendants must retain root identity and obey maximum depth.");
            Need(events.Select(evt => evt.AttackId).Distinct().Count() == 4, "Each body needs its own attack identity.");
            Need(!events[0].SpawnChild(recipe, Vector2.right), "Retained callbacks must not spawn outside dispatch.");
            world.Cancel();
            Need(world.ActiveBodyCount == 0 && world.PendingCount == 0, "Cancel must terminate the entire tree.");
        }
        var cancel = Asset<CombatProbeEffect>();
        recipe.effects.Clear(); recipe.effects.Add(cancel);
        using (var store = new CombatEffectStateStore())
        using (var world = new AttackExecutionRuntime(stats.transform, 1 << 30, store))
        {
            bool rejected = false;
            cancel.OnEmit = evt => { world.Cancel(); rejected = !evt.SpawnChild(recipe, Vector2.right); };
            world.Emit(world.CreateAttack(recipe, Shot()), stats.transform.position);
            Need(rejected && world.ActiveBodyCount == 0, "Reentrant cancellation must block descendants and clear visuals.");
        }
        var onHit = Asset<CombatProbeEffect>(); recipe.effects.Clear(); recipe.effects.Add(onHit);
        var enemy = Enemy(stats.transform.position + Vector3.right);
        onHit.OnHit = evt => evt.SpawnChild(Recipe(), Vector2.up);
        using (var store = new CombatEffectStateStore())
        using (var world = new AttackExecutionRuntime(stats.transform, 1 << 30, store))
        {
            world.Emit(world.CreateAttack(recipe, Shot()), stats.transform.position); world.Tick(0.1f);
            Need(world.PendingCount == 1 && world.ActiveBodyCount == 1, "Hit procs must queue children, not recurse in collision callbacks.");
            Need(onHit.Created[0].LastHit.Result.AppliedDamage == 10f && onHit.Created[0].LastHit.Target == enemy,
                "Hit effects need actual damage and target identity.");
            world.Tick(0.01f);
            Need(world.ActiveBodyCount == 2, "Queued hit child must execute on the next scheduler pass.");
        }
    }
    private static void Combination()
    {
        var a = Asset<AttackRecipeModifier>(); var b = Asset<AttackRecipeModifier>();
        Set(a, "id", "source-a"); Set(b, "id", "source-b");
        var wrapped = Asset<BeamShapeDefinition>(); Set(wrapped, "wrapAroundOwner", true);
        var rule = Asset<ModuleCombinationRule>(); Set(rule, "id", "wrap-rule");
        Set(rule, "requiredSourceIds", new[] { "source-a", "source-b" }); Set(rule, "shape", wrapped);
        var inventory = new Dictionary<AttackRecipeModifier, int> { [b] = 1, [a] = 1 };
        var rules = new[] { rule };
        Need(AttackRecipeResolver.Resolve(new AttackRecipe(), inventory, rules).shape == wrapped,
            "Explicit combination must override the final shape after ordinary modifiers.");
        inventory.Remove(a);
        Need(AttackRecipeResolver.Resolve(new AttackRecipe(), inventory, rules).shape == null,
            "Removing a required source must remove its synergy.");
    }
    private static void Reentry()
    {
        var stats = Owner(); var target = Enemy(stats.transform.position + Vector3.right);
        var recipe = Recipe(); recipe.hitPolicy = Asset<ReentryHitPolicyDefinition>();
        using (var body = new AttackBodyRuntime(recipe, Shot(), Vector2.right, stats.transform, 1 << 30))
        {
            body.Tick(0.1f); body.Tick(0.1f);
            Need(target.CurrentHealth == 90f, "Reentry policy must not hit continuously overlapping colliders.");
            target.transform.position += Vector3.up * 10f; body.Tick(0.1f);
            target.transform.position -= Vector3.up * 10f; body.Tick(0.1f);
            Need(target.CurrentHealth == 80f, "Reentry policy must hit again on a new contact.");
        }
    }
    private static void Homing()
    {
        var stats = Owner(); Enemy(stats.transform.position + Vector3.up * 3f);
        var definition = Asset<HomingTrajectoryDefinition>();
        Vector2 origin = stats.transform.position;
        var first = definition.CreateRuntime(stats.transform, 1 << 30, origin, Vector2.right, 4f);
        var second = definition.CreateRuntime(stats.transform, 1 << 30, origin, Vector2.right, 4f);
        var pose = first.Evaluate(0.1f);
        Need(pose.Position.y > origin.y, "Homing module must steer toward an enemy.");
        Need(second.Evaluate(0f).Position == origin, "Trajectory state must be independent per attack body.");
    }
    private static void CustomTrigger()
    {
        var stats = Owner(); var recipe = Recipe(); recipe.triggerModule = Asset<ReleaseOnlyProbeTrigger>();
        using (var runtime = Runtime(stats, recipe))
        {
            runtime.PrimaryStarted(Vector2.right); runtime.PrimaryHeld(Vector2.right);
            Need(runtime.ActiveBodyCount == 0, "A custom trigger must override legacy trigger selection.");
            runtime.PrimaryReleased(Vector2.right);
            Need(runtime.ActiveBodyCount == 1, "A custom trigger must fire without modifying the weapon runner.");
        }
    }
    private static void PeriodicProcAndExpiry()
    {
        var stats = Owner();
        var proc = Asset<SpawnOnEventEffectDefinition>();
        Set(proc, "onEvent", CombatEffectEvent.Attack); Set(proc, "everyNth", 3); Set(proc, "child", Recipe());
        var recipe = Recipe(); recipe.effects.Add(proc);
        using (var store = new CombatEffectStateStore())
        using (var world = new AttackExecutionRuntime(stats.transform, 1 << 30, store))
        {
            for (int i = 0; i < 2; i++) world.Activate(world.CreateAttack(recipe, Shot()), stats.transform.position);
            Need(world.PendingCount == 0, "Every-N proc must wait for the correct activation.");
            world.Activate(world.CreateAttack(recipe, Shot()), stats.transform.position);
            Need(world.PendingCount == 1, "Every-N proc must spawn at its threshold.");
        }
        var expired = Asset<CombatProbeEffect>(); recipe = Recipe(); recipe.effects.Add(expired); recipe.lifetime = 0.05f;
        using (var runtime = Runtime(stats, recipe))
        {
            runtime.PrimaryStarted(Vector2.right); runtime.Tick(0.1f);
            Need(expired.Created.Last().Expirations == 1, "Natural expiry must dispatch once.");
        }
        using (var runtime = Runtime(stats, recipe))
        {
            runtime.PrimaryStarted(Vector2.right); runtime.CancelAttack(); runtime.Tick(0.1f);
            Need(expired.Created.Last().Expirations == 0, "Cancellation must not trigger expiry procs.");
        }
    }
    private static void ProceduralMelee()
    {
        var stats = Owner(); var enemy = Enemy(stats.transform.position + Vector3.right);
        var recipe = Recipe(); recipe.trajectory = Asset<SwingTrajectoryDefinition>(); recipe.lifetime = 0.2f;
        recipe.emission = Asset<BurstEmissionDefinition>();
        using (var runtime = Runtime(stats, recipe))
        {
            runtime.PrimaryStarted(Vector2.right); runtime.Tick(0.5f);
            Need(enemy.CurrentHealth == 70f, "Procedural melee must compose with scheduled emission through the common runner.");
        }
        bool rejected = false;
        try { new AttackRecipe { emission = recipe.emission }.Validate(); } catch (ArgumentException) { rejected = true; }
        Need(rejected, "Unsupported legacy emission must report a configuration error instead of silently ignoring it.");
    }
    private static AttackRecipe Recipe() => new AttackRecipe { shape = Asset<BeamShapeDefinition>(), lifetime = 2f };
    private static AttackSnapshot Shot() => new AttackSnapshot(Vector2.right, 10f, 3f, 2f);
    private static ModularWeaponAttackRuntime Runtime(PlayerStats stats, AttackRecipe recipe, CombatEffectStateStore store = null) =>
        (ModularWeaponAttackRuntime)Asset<WeaponAttackDefinition>().CreateRuntime(stats, stats.transform, 1 << 30, recipe, store);
    private static PlayerStats Owner()
    {
        var go = new GameObject("ModuleTestOwner"); garbage.Add(go);
        go.transform.position = new Vector3(3000f + garbage.Count * 30f, -3000f);
        var stats = go.AddComponent<PlayerStats>();
        stats.Initialize(JsonUtility.FromJson<CharacterStats>("{\"attackPower\":10,\"attackSpeed\":2,\"projectileSpeedMultiplier\":1}"));
        return stats;
    }
    private static EnemyActor Enemy(Vector3 position)
    {
        var go = new GameObject("ModuleTestEnemy"); garbage.Add(go); go.layer = 30; go.transform.position = position;
        go.AddComponent<CircleCollider2D>().radius = 0.1f;
        var enemy = go.AddComponent<EnemyActor>(); go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var definition = Asset<EnemyDefinition>(); Set(definition, "maxHealth", 100f);
        Set(definition, "movement", Asset<ChaseMovementDefinition>()); enemy.Initialize(definition, null, null);
        return enemy;
    }
    private static T Asset<T>() where T : ScriptableObject
    { var value = ScriptableObject.CreateInstance<T>(); garbage.Add(value); return value; }
    private static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new MissingFieldException(name);
    }
    private static void Need(bool success, string message)
    { checks++; if (!success) throw new InvalidOperationException(message); }
}

public sealed class CombatProbeEffect : CombatEffectDefinition
{
    public readonly List<ProbeRuntime> Created = new();
    public Action<CombatEvent> OnEmit;
    public Action<CombatEvent> OnHit;
    public override CombatEffectRuntime CreateRuntime()
    { var runtime = new ProbeRuntime(this); Created.Add(runtime); return runtime; }
    public sealed class ProbeRuntime : CombatEffectRuntime
    {
        private readonly CombatProbeEffect definition;
        public int Attacks; public int Hits; public int Expirations; public bool Disposed;
        public readonly List<CombatEvent> Emitted = new();
        public CombatEvent LastHit;
        public ProbeRuntime(CombatProbeEffect definition) { this.definition = definition; }
        public override void OnAttack(CombatEvent context) { Attacks++; }
        public override void OnEmit(CombatEvent context) { Emitted.Add(context); definition.OnEmit?.Invoke(context); }
        public override void OnHit(CombatEvent context) { Hits++; LastHit = context; definition.OnHit?.Invoke(context); }
        public override void OnExpire(CombatEvent context) { Expirations++; }
        public override void Dispose() { Disposed = true; }
    }
}
public sealed class ReleaseOnlyProbeTrigger : AttackTriggerDefinition
{
    public override AttackTriggerRuntime CreateRuntime(AttackRecipe recipe, Func<AttackActivation, bool> fire, Action stop) => new Runtime(fire);
    private sealed class Runtime : AttackTriggerRuntime
    {
        private readonly Func<AttackActivation, bool> fire;
        public Runtime(Func<AttackActivation, bool> fire) { this.fire = fire; }
        public override void Released(Vector2 aim) { fire(new AttackActivation(aim)); }
        public override void Cancel() { }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CombatRegressionChecks
{
    private static readonly List<Object> objects = new();
    private static int assertions;

    [MenuItem("ROTLINE/Tests/Combat and Lifecycle")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        assertions = 0;
        try
        {
            TestRecipes();
            TestChargeAndLifetime();
            TestRingAndDamage();
            TestFrameIntervals();
            TestReentry();
            TestMelee();
            TestGauge();
            TestBerserk();
            Debug.Log($"[Combat] All {assertions} regression assertions passed.");
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }
    }

    public static void RunBatch()
    {
        Run();
        CombatModuleRegressionChecks.Run();
        foreach (Type type in new[] { typeof(PlayerStatsRegressionChecks),
            typeof(ModifierScopeRegressionChecks), typeof(ModifierCollectionRegressionChecks) })
            type.GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
    }

    private static void TestRecipes()
    {
        var beam = New<BeamShapeDefinition>();
        var ring = New<RingShapeDefinition>();
        var first = New<AttackRecipeModifier>();
        JsonUtility.FromJsonOverwrite("{\"id\":\"beam\",\"priority\":10}", first);
        Set(first, "shape", beam);
        var second = New<AttackRecipeModifier>();
        JsonUtility.FromJsonOverwrite("{\"id\":\"ring\",\"priority\":20,\"extraProjectiles\":2,\"maximumStacks\":3}", second);
        Set(second, "shape", ring);
        var a = new Dictionary<AttackRecipeModifier, int> { [first] = 1, [second] = 2 };
        var b = new Dictionary<AttackRecipeModifier, int> { [second] = 2, [first] = 1 };
        var basis = new AttackRecipe();
        var x = AttackRecipeResolver.Resolve(basis, a);
        var y = AttackRecipeResolver.Resolve(basis, b);
        Require(x.shape == ring && y.shape == ring, "Shape priority must not depend on acquisition order.");
        Require(x.projectileCount == 5 && y.projectileCount == 5, "Item stacks must affect emission count.");
        a.Remove(second);
        Require(AttackRecipeResolver.Resolve(basis, a).shape == beam, "Removing ring must restore beam.");
        a.Clear();
        Require(AttackRecipeResolver.Resolve(basis, a).shape == null, "Removing all items must restore melee.");
        Require(basis.shape == null && basis.projectileCount == 1, "Resolver must not mutate weapon assets.");
        JsonUtility.FromJsonOverwrite("{\"priority\":10}", second);
        Require(AttackRecipeResolver.Resolve(basis, b).shape == ring, "Stable ID must break equal-priority ties.");
        JsonUtility.FromJsonOverwrite("{\"id\":\"beam\"}", second);
        bool threw = false;
        try { AttackRecipeResolver.Resolve(basis, b); } catch (ArgumentException) { threw = true; }
        Require(threw, "Duplicate stable IDs must be rejected.");
    }

    private static void TestChargeAndLifetime()
    {
        var stats = Stats();
        var weapon = New<WeaponAttackDefinition>();
        var recipe = new AttackRecipe { shape = New<BeamShapeDefinition>(),
            trigger = AttackTrigger.ChargeRelease, chargeSeconds = 1f, minimumCharge = 0.2f, lifetime = 2f };
        using (var runtime = weapon.CreateRuntime(stats, stats.transform, 1 << 30, recipe))
        {
            int emitted = 0;
            float charge = 0;
            runtime.AttackActiveStarted += shot => { emitted++; charge = shot.Charge; };
            runtime.PrimaryStarted(Vector2.right);
            runtime.Tick(0.5f);
            Require(runtime.IsCharging && emitted == 0, "Charging must not emit before release.");
            runtime.PrimaryReleased(Vector2.right);
            Require(emitted == 1 && Mathf.Abs(charge - 0.5f) < 0.001f, "Release must snapshot charge.");
            runtime.Tick(0.6f);
            Require(!runtime.IsAttacking && ((ModularWeaponAttackRuntime)runtime).ActiveBodyCount == 1,
                "Entity lifetime must be independent of attack recovery.");
            runtime.CancelAttack();
            Require(((ModularWeaponAttackRuntime)runtime).ActiveBodyCount == 0, "Cancel must remove entities after recovery.");
        }
        using (var runtime = weapon.CreateRuntime(stats, stats.transform, 1 << 30, recipe))
        {
            int emitted = 0;
            runtime.AttackActiveStarted += _ => emitted++;
            runtime.PrimaryStarted(Vector2.right);
            runtime.Tick(0.1f);
            runtime.PrimaryReleased(Vector2.right);
            Require(emitted == 0 && !runtime.IsCharging, "Early release must not emit.");
            runtime.PrimaryStarted(Vector2.right);
            runtime.Tick(0.5f);
            runtime.CancelAttack();
            runtime.PrimaryReleased(Vector2.right);
            Require(emitted == 0, "Canceled charge must not emit on release.");
        }
        Require(Mathf.Approximately(stats.AttackRange, 0f), "Dispose must release weapon modifiers.");
    }

    private static void TestRingAndDamage()
    {
        var stats = Stats();
        var center = Enemy(stats.transform.position);
        var rim = Enemy(stats.transform.position + Vector3.right * 2f);
        rim.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        var recipe = new AttackRecipe { shape = New<RingShapeDefinition>(), radius = 2f,
            thickness = 0.1f, lifetime = 1f, speed = 0f };
        using (var body = new AttackBodyRuntime(recipe, new AttackSnapshot(Vector2.right, 10f, 3f, 1f),
            Vector2.right, stats.transform, 1 << 30)) body.Tick(0.1f);
        Require(center.CurrentHealth == 100f, "Ring interior must remain empty.");
        Require(rim.CurrentHealth == 90f, "Ring rim must hit exactly once.");
        int deaths = 0;
        rim.Died += (_, __) => deaths++;
        rim.Damaged += (enemy, context, result) =>
        {
            if (result.Killed) Require(enemy.TakeDamage(context).AppliedDamage == 0f, "Lethal callback reentry must be rejected.");
        };
        var overkill = rim.TakeDamage(new DamageContext(stats.transform, 1000f, Vector2.right));
        Require(overkill.AppliedDamage == 90f && overkill.Killed && deaths == 1, "Damage result must report actual damage and one death.");
    }

    private static void TestFrameIntervals()
    {
        float coarse = RepeatedBeam(new[] { 0.31f });
        float fine = RepeatedBeam(new[] { 0.05f, 0.04f, 0.12f, 0.1f });
        Require(coarse == 60f && fine == coarse, "Repeated hit count must survive variable frame intervals.");
        var stats = Stats();
        var enemy = Enemy(stats.transform.position + Vector3.right * 3f);
        var recipe = new AttackRecipe { shape = New<ProjectileShapeDefinition>(),
            trajectory = New<LinearTrajectoryDefinition>(), speed = 10f, lifetime = 0.4f, radius = 0.1f };
        using (var body = new AttackBodyRuntime(recipe, new AttackSnapshot(Vector2.right, 10f, 3f, 0.4f),
            Vector2.right, stats.transform, 1 << 30))
            Require(!body.Tick(1f), "A frame beyond lifetime must finish the projectile.");
        Require(enemy.CurrentHealth == 90f, "Final lifetime movement must still detect a crossed enemy.");
    }

    private static float RepeatedBeam(float[] deltas)
    {
        var stats = Stats();
        var enemy = Enemy(stats.transform.position + Vector3.right);
        var recipe = new AttackRecipe { shape = New<BeamShapeDefinition>(), repeatHitSeconds = 0.1f, lifetime = 1f };
        using (var body = new AttackBodyRuntime(recipe, new AttackSnapshot(Vector2.right, 10f, 3f, 1f),
            Vector2.right, stats.transform, 1 << 30))
            foreach (float dt in deltas) body.Tick(dt);
        float health = enemy.CurrentHealth;
        enemy.gameObject.SetActive(false);
        return health;
    }

    private static void TestReentry()
    {
        var stats = Stats();
        var enemy = Enemy(stats.transform.position + Vector3.right);
        Vector3 hitPosition = enemy.transform.position;
        var recipe = new AttackRecipe { shape = New<BeamShapeDefinition>(), repeatHitSeconds = 0.1f, lifetime = 1f };
        using (var body = new AttackBodyRuntime(recipe, new AttackSnapshot(Vector2.right, 10f, 3f, 1f),
            Vector2.right, stats.transform, 1 << 30))
        {
            body.Tick(0.15f);
            Require(enemy.CurrentHealth == 80f, "Initial beam contacts must deliver two pulses.");
            enemy.transform.position += Vector3.up * 10f;
            body.Tick(0.5f);
            Require(enemy.CurrentHealth == 80f, "No damage while outside the beam.");
            enemy.transform.position = hitPosition;
            body.Tick(0.01f);
            Require(enemy.CurrentHealth == 70f, "Re-entry must not retroactively apply absent pulses.");
        }
    }

    private static void TestMelee()
    {
        var stats = Stats();
        var enemy = Enemy(stats.transform.position + Vector3.right);
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponAttackDefinition>("Assets/Data/Attack_Sword_Default..asset");
        Require(weapon != null, "Existing sword asset must load.");
        using (var runtime = weapon.CreateRuntime(stats, stats.transform, 1 << 30))
        {
            int active = 0;
            runtime.AttackActiveStarted += _ => active++;
            Require(runtime.RequestAttack(Vector2.right), "Existing sword must start.");
            runtime.Tick(2f);
            runtime.Tick(2f);
            Require(active == 1 && !runtime.IsAttacking, "Long frame must emit melee activation exactly once.");
            Require(enemy.CurrentHealth == 90f, "Long frame must scan sword sweep.");
        }
        using (var runtime = weapon.CreateRuntime(stats, stats.transform, 1 << 30))
        {
            runtime.AttackActiveStarted += _ => runtime.CancelAttack();
            runtime.PrimaryStarted(Vector2.right);
            runtime.Tick(2f);
            Require(enemy.CurrentHealth == 90f && !runtime.IsAttacking, "Cancellation inside activation must stop melee hits.");
        }
    }

    private static void TestGauge()
    {
        var gauge = new SpecialGaugeRuntime(100f, 50f);
        gauge.SetMaximum(200f);
        Require(gauge.Current == 50f, "Increasing capacity must not refill gauge.");
        gauge.SetMaximum(20f);
        Require(gauge.Current == 20f, "Capacity shrink must clamp current gauge.");
        gauge.SetMaximum(0f);
        Require(gauge.Ratio == 0f && gauge.Current == 0f, "Zero capacity must be finite and empty.");
    }

    private static void TestBerserk()
    {
        var stats = Stats();
        var weapon = New<WeaponAttackDefinition>();
        var recipe = new AttackRecipe { shape = New<BeamShapeDefinition>() };
        var definition = AssetDatabase.LoadAssetAtPath<BerserkAbilityDefinition>("Assets/Data/Characters/BerserkAbility.asset");
        using (var runtime = weapon.CreateRuntime(stats, stats.transform, 0, recipe))
        using (var ability = new BerserkAbilityRuntime(definition, stats))
        {
            ability.BindGauge(new SpecialGaugeRuntime());
            ability.RequestToggle();
            Require(Mathf.Abs(stats.AttackRange - 3.6f) < 0.001f, "Berserk must scale weapon's base range.");
        }
        Require(stats.AttackRange == 0f, "Weapon and ability scopes must both be released.");
    }

    private static PlayerStats Stats()
    {
        var go = new GameObject("CombatRegressionOwner");
        objects.Add(go);
        go.transform.position = new Vector3(1000f + objects.Count * 20f, -1000f);
        var stats = go.AddComponent<PlayerStats>();
        stats.Initialize(JsonUtility.FromJson<CharacterStats>("{\"attackPower\":10,\"attackSpeed\":2,\"projectileSpeedMultiplier\":1,\"maxResource\":100}"));
        return stats;
    }

    private static EnemyActor Enemy(Vector3 position)
    {
        var go = new GameObject("CombatRegressionEnemy");
        objects.Add(go);
        go.layer = 30;
        go.transform.position = position;
        go.AddComponent<CircleCollider2D>().radius = 0.1f;
        var enemy = go.AddComponent<EnemyActor>();
        go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var definition = New<EnemyDefinition>();
        JsonUtility.FromJsonOverwrite("{\"maxHealth\":100}", definition);
        Set(definition, "movement", New<ChaseMovementDefinition>());
        enemy.Initialize(definition, null, null);
        return enemy;
    }

    private static T New<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        objects.Add(value);
        return value;
    }

    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("[Combat] " + message);
    }
}

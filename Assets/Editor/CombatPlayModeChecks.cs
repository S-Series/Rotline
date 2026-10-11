using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Batch-only integration check: opens the scene without saving it and exits after validation.
[InitializeOnLoad]
public static class CombatPlayModeChecks
{
    private const string RunningKey = "Rotline.CombatPlayModeChecks";
    private static double readyAt;
    private static bool failed;
    private static WeaponAttackRuntime chargeRuntime;
    private static Action checkChargeAndPool;

    static CombatPlayModeChecks()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        BatchCheckGuard.RestoreProfile();
        readyAt = EditorApplication.timeSinceStartup + 3f;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Update;
    }

    public static void RunBatch()
    {
        BatchCheckGuard.Run(RunningKey, 60, () =>
        {
            BatchCheckGuard.Gameplay();
            EditorApplication.isPlaying = true;
        });
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            if (checkChargeAndPool == null)
            {
                CheckScene();
                return;
            }
            // Automatic attacks may still be recovering when the item changes the trigger.
            // Wait for that preserved recovery instead of requiring an immediate charge.
            if (Time.time < chargeRuntime.NextAttackTime || Time.deltaTime <= 0f) return;
            checkChargeAndPool();
            if (failed) throw new InvalidOperationException("Play Mode logged an error; inspect this log.");
            Debug.Log("[CombatPlayMode] Scene, input routing, item restoration, gauge and pool checks passed.");
            Exit();
        }
        catch (Exception exception)
        {
            failed = true;
            Debug.LogException(exception);
            Exit();
        }
    }

    private static void Exit()
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= OnLog;
        checkChargeAndPool = null;
        chargeRuntime = null;
        BatchCheckGuard.Finish(RunningKey, failed ? 1 : 0);
    }

    private static void CheckScene()
    {
        var combat = Object.FindFirstObjectByType<PlayerCombatController>();
        Require(combat != null && AppRoot.Instance != null, "Gameplay scene must initialize player and input.");
        Require(AppRoot.Instance.transform.parent == null && AppRoot.Instance.gameObject.scene.name == "DontDestroyOnLoad",
            "AppRoot must persist independently of scene containers.");
        var character = combat.GetComponent<PlayerCharacter>();
        var actions = combat.GetComponent<PlayerActionController>();
        var mode = combat.GetComponent<PlayerModeController>();
        var stats = character.Stats;
        ItemRegressionChecks.CheckInventory(combat.GetComponent<PlayerItemInventory>(), stats);
        float range = stats.AttackRange;
        foreach (string eventName in new[] { "PrimaryStarted", "PrimaryHeld", "PrimaryReleased", "PrimaryCanceled" })
            Require(Subscriptions(actions, eventName, combat) == 1, eventName + " must have one combat subscription.");
        combat.enabled = false;
        foreach (string eventName in new[] { "PrimaryStarted", "PrimaryHeld", "PrimaryReleased", "PrimaryCanceled" })
            Require(Subscriptions(actions, eventName, combat) == 0, eventName + " must unsubscribe on disable.");
        Require(stats.AttackRange < range, "Disabling combat must remove weapon modifiers.");
        combat.enabled = true;
        Require(Mathf.Approximately(stats.AttackRange, range), "Re-enable must equip weapon exactly once.");

        var item = AssetDatabase.LoadAssetAtPath<AttackRecipeModifier>("Assets/Data/CombatExamples/Item_ChargedBeam.asset");
        Require(combat.EquipAttackItem(item), "Beam transformation item must equip.");
        var runtime = Field<WeaponAttackRuntime>(combat, "weaponRuntime");
        Require(runtime is ModularWeaponAttackRuntime, "Beam item must replace melee runtime.");
        mode.SetMode(PlayerMode.Combat);
        chargeRuntime = runtime;
        checkChargeAndPool = () => CheckChargeAndPool(combat, actions, mode, character, stats, item, runtime);
    }

    private static void CheckChargeAndPool(PlayerCombatController combat, PlayerActionController actions,
        PlayerModeController mode, PlayerCharacter character, PlayerStats stats, AttackRecipeModifier item,
        WeaponAttackRuntime runtime)
    {
        var mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            Vector2 aim = Camera.main.WorldToScreenPoint(combat.transform.position + Vector3.right * 3f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = aim });
            InputSystem.Update();
            Field<Action<PlayerMode>>(actions, "PrimaryStarted").Invoke(PlayerMode.Combat);
            runtime.Tick(0.5f);
            Require(runtime.IsCharging, "Started event must reach charge input.");
            Field<Action<PlayerMode>>(actions, "PrimaryReleased").Invoke(PlayerMode.Combat);
            Require(((ModularWeaponAttackRuntime)runtime).ActiveBodyCount == 1, "Released event must fire beam.");
            mode.SetMode(PlayerMode.Purification);
            Require(((ModularWeaponAttackRuntime)runtime).ActiveBodyCount == 0, "Mode change must clear attack entities.");
            Require(combat.UnequipAttackItem(item), "Transformation must be removable.");
            Require(Field<WeaponAttackRuntime>(combat, "weaponRuntime") is ComposedWeaponAttackRuntime,
                "Removing transformation must restore existing sword.");
        }
        finally { InputSystem.RemoveDevice(mouse); }

        var gauge = character.SpecialGauge;
        Require(gauge != null, "Example character must have a gauge.");
        float current = gauge.Current;
        int handle = stats.AddModifier(StatKey.MaxResource, ModifierType.BaseFlat, 50f);
        Require(gauge.Maximum == stats.MaxResource && gauge.Current == current, "Gauge capacity must follow stats without refill.");
        stats.RemoveModifier(StatKey.MaxResource, handle);
        Require(gauge.Maximum == stats.MaxResource, "Gauge capacity must restore on modifier removal.");

        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Def/EnemySlime.asset");
        var container = new GameObject("PoolRegressionContainer");
        using (var pool = new EnemyPool(definition, container.transform))
        {
            var enemy = pool.Get();
            Require(!enemy.gameObject.activeInHierarchy, "Pool must return an inactive enemy.");
            enemy.transform.position = combat.transform.position + Vector3.right * 5f;
            enemy.Initialize(definition, combat.transform, Object.FindFirstObjectByType<NavigationService>());
            uint version = enemy.SpawnVersion;
            enemy.gameObject.SetActive(true);
            Require(enemy.CurrentHealth == definition.MaxHealth, "Enemy must have valid data before activation.");
            pool.Release(enemy);
            var reused = pool.Get();
            Require(reused == enemy && !reused.gameObject.activeInHierarchy, "Pool must reuse inactive instance.");
            reused.Initialize(definition, combat.transform, null);
            Require(reused.SpawnVersion != version, "Reused enemy must have a new hit identity.");
            pool.Release(reused);
        }
        Object.Destroy(container);
        combat.CancelCombat();
    }

    private static int Subscriptions(object source, string name, object target)
    {
        var callback = Field<Delegate>(source, name);
        return callback?.GetInvocationList().Count(entry => entry.Target == target) ?? 0;
    }
    private static T Field<T>(object source, string name) =>
        (T)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

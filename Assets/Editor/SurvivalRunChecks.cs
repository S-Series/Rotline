using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SurvivalRunChecks
{
    private const string Key = "Rotline.SurvivalChecks";
    private static int phase;
    private static double next;
    private static bool errors;
    private static int checks;
    private static SurvivalRunController firstRun;
    private static float healthBeforeContact;
    private static int uiChildCount;

    static SurvivalRunChecks()
    {
        if (!SessionState.GetBool(Key, false)) return;
        BatchCheckGuard.RestoreProfile();
        next = EditorApplication.timeSinceStartup + 4;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Update;
    }
    public static void RunBatch()
    {
        BatchCheckGuard.Run(Key, 75, () =>
        {
            BatchCheckGuard.Gameplay();
            CombatRegressionChecks.RunBatch();
            EditorApplication.isPlaying = true;
        });
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors = true;
    }
    private static void Update()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            var run = Object.FindFirstObjectByType<SurvivalRunController>();
            Require(run != null && run.Health != null, "Run must initialize.");
            if (phase == 0)
            {
                firstRun = run;
                Require(run.IsRunning && run.Health.Current > 0f, "Scene must be playable.");
                var hud = run.transform.Find("Run HUD");
                var pages = run.transform.Find("Alpha System Pages");
                Require(hud != null && pages != null && run.GetComponent<SurvivalRunHUD>() != null,
                    "Run UI must be saved in Gameplay before Play.");
                uiChildCount = hud.GetComponentsInChildren<Transform>(true).Length +
                    pages.GetComponentsInChildren<Transform>(true).Length;
                ItemRegressionChecks.Run();
                ItemRegressionChecks.CheckInventory(run.Player.GetComponent<PlayerItemInventory>(), run.Player.Stats);
                var enemy = Object.FindFirstObjectByType<EnemyActor>();
                Require(enemy != null, "Spawner must create an enemy.");
                CheckEnemySteering(run);
                healthBeforeContact = run.Health.Current;
                enemy.transform.position = run.Player.transform.position;
                Physics2D.SyncTransforms();
                phase++; next = EditorApplication.timeSinceStartup + 0.3;
            }
            else if (phase == 1)
            {
                Require(run.Health.Current < healthBeforeContact, "Ignored collision layers must still cause contact damage.");
                Require(!run.Health.TakeDamage(1f), "Contact hit must grant temporary immunity.");
                var enemy = Object.FindFirstObjectByType<EnemyActor>();
                int before = run.Kills;
                enemy.TakeDamage(float.MaxValue);
                Require(run.Kills == before + 1 && run.Pickups.ActiveCount > 0, "Kill must spawn world loot exactly once.");
                run.Player.GetComponent<PlayerItemInventory>().AddItem(run.Definition.levelRewards[0]);
                run.Collect(0, 4f);
                Require(run.Experience.Level == 2 && run.State == SurvivalRunState.ChoosingReward, "XP +50% must trigger level reward.");
                Require(run.Experience.Current >= 1d, "XP overflow must be retained.");
                Require(Time.timeScale == 0f && run.Choices.Count == 3 && run.Choices.Select(c => c.item).Distinct().Count() == 3, "Three distinct choices must pause simulation.");
                Require(!run.ChooseReward(-1), "Invalid reward selection must not consume a reward.");
                phase++; next = EditorApplication.timeSinceStartup + 0.8;
            }
            else if (phase == 2)
            {
                Capture("SurvivalRewards");
                CheckCombatStats(run);
                var selected = run.Choices[0].item;
                int before = run.Player.GetComponent<PlayerItemInventory>().Count(selected);
                var button = run.GetComponent<SurvivalRunHUD>().GetComponentsInChildren<UnityEngine.UI.Button>()[0];
                var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                pointer.position = RectTransformUtility.WorldToScreenPoint(Camera.main, button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
                Require(hits.Any(hit => hit.gameObject == button.gameObject), "Reward button must be reachable through the real UI raycaster while paused.");
                Require(uiChildCount == run.transform.Find("Run HUD").GetComponentsInChildren<Transform>(true).Length +
                    run.transform.Find("Alpha System Pages").GetComponentsInChildren<Transform>(true).Length,
                    "Reward display must retain the authored UI hierarchy.");
                button.onClick.Invoke();
                Require(run.Player.GetComponent<PlayerItemInventory>().Count(selected) == before + 1, "Reward must enter inventory.");
                Require(run.IsRunning && Time.timeScale > 0f, "Choice must resume simulation.");
                run.QueueReward(true);
                int attackIndex = -1;
                for (int i = 0; i < run.Choices.Count; i++) if (run.Choices[i].attack != null) { attackIndex = i; break; }
                Require(attackIndex >= 0 && run.ChooseReward(attackIndex), "Treasure must offer and grant an attack transformation.");
                Require(run.Player.GetComponent<PlayerCombatController>().HasModularAttack, "Treasure must change the live weapon.");
                run.Collect(0, 50f);
                int rewards = 0;
                while (run.State == SurvivalRunState.ChoosingReward && rewards++ < 32) Require(run.ChooseReward(0), "Overflow rewards must be selectable.");
                Require(rewards > 1 && run.IsRunning, "Multiple levels must queue all rewards and resume.");
                run.TogglePause(); Require(run.State == SurvivalRunState.Paused && Time.timeScale == 0f, "Pause must freeze run.");
                run.TogglePause(); Require(run.IsRunning, "Resume must restore run.");
                // Advance through the normal Update path to test stage escalation and victory.
                typeof(SurvivalRunController).GetField("<Elapsed>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(run, 61f);
                Require(run.Stage == 2, "Stage must advance with elapsed time.");
                phase++; next = EditorApplication.timeSinceStartup + 1;
            }
            else if (phase == 3)
            {
                Capture("SurvivalHUD");
                run.Health.TakeDamage(float.MaxValue);
                Require(run.State == SurvivalRunState.Lost && Time.timeScale == 0f, "Lethal damage must show loss and stop time.");
                run.Restart(); phase++; next = EditorApplication.timeSinceStartup + 3;
            }
            else if (phase == 4)
            {
                Require(run != firstRun && run.IsRunning && run.Kills == 0 && run.Experience.Level == 1 && run.Acquired.Count == 0,
                    "Restart must reset run progression and preserve usable input.");
                Require(run.Player.GetComponent<PlayerItemInventory>().ItemCount == 0 && run.Health.Current == run.Health.Maximum,
                    "Restart must clear acquired modifiers and restore health.");
                typeof(SurvivalRunController).GetField("<Elapsed>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(run, run.Definition.durationSeconds);
                phase++; next = EditorApplication.timeSinceStartup + 0.4;
            }
            else
            {
                Require(run.State == SurvivalRunState.Won && Time.timeScale == 0f, "Duration must trigger victory.");
                Require(!errors, "Runtime logged errors.");
                Debug.Log($"[SurvivalChecks] {checks} checks passed: contact, XP, choices, item effects, treasure, pause, death, restart and victory.");
                Exit(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Exit(1); }
    }
    private static void Require(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
    private static void CheckEnemySteering(SurvivalRunController run)
    {
        Require(Physics2D.GetIgnoreLayerCollision(LayerMask.NameToLayer("Enemy"), LayerMask.NameToLayer("Enemy")),
            "Enemies must not collide physically with other enemies.");
        Require(!Physics2D.GetIgnoreLayerCollision(LayerMask.NameToLayer("Enemy"), LayerMask.NameToLayer("World")),
            "Enemies must still collide with the world.");
        Vector2 center = run.Player.transform.position;
        var targeting = new EnemyTargeting(run.Player.transform, 1f, 1.5f);
        Require(targeting.Approach(center + Vector2.right * 3f).x < 0f,
            "Enemies outside the band must approach the player.");
        Require(targeting.Approach(center + Vector2.right * 1.25f) == Vector2.zero,
            "Enemies inside the band must stop approaching.");
        Require(targeting.Approach(center + Vector2.right * 0.5f).x > 0f,
            "Enemies inside the minimum distance must retreat.");
        Require(targeting.Approach(center).sqrMagnitude > 0f,
            "An enemy exactly on the player must leave the center.");
        Require(EnemyRangeBands.DefaultDistances(EnemyRangeBand.Sniper).x >
            EnemyRangeBands.DefaultDistances(EnemyRangeBand.Long).y,
            "Sniper band must extend beyond long range.");
        var self = new GameObject("Steering check A", typeof(BoxCollider2D));
        var other = new GameObject("Steering check B", typeof(BoxCollider2D));
        try
        {
            self.layer = other.layer = LayerMask.NameToLayer("Enemy");
            self.transform.position = new Vector2(1000f, 1000f);
            other.transform.position = new Vector2(1000.3f, 1000f);
            Physics2D.SyncTransforms();
            var steering = new EnemySteering(self.transform, self.GetComponent<Collider2D>(), null, 1f, 0.5f);
            Require(steering.Direction(self.transform.position, Vector2.zero, false).x < 0f,
                "Nearby enemies must steer apart without physical collision.");
        }
        finally { Object.Destroy(self); Object.Destroy(other); }
    }
    private static void CheckCombatStats(SurvivalRunController run)
    {
        // The reward menu deliberately pauses health damage. Verify that boundary first.
        Require(!run.Health.TakeDamage(10f), "Reward menu must block incoming damage.");
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:EnemyDefinition")[0]));
        var enemy = Object.Instantiate(definition.Prefab);
        try
        {
            enemy.Initialize(definition, run.Player.transform, Object.FindFirstObjectByType<NavigationService>());
            enemy.ScaleDifficulty(100f, 1f);
            using var scope = new ModifierScope(run.Player.Stats);
            scope.AddModifier(StatKey.CriticalChance, ModifierType.BaseFlat, 1f);
            scope.AddModifier(StatKey.CriticalDamage, ModifierType.BaseFlat, 2f);
            scope.AddModifier(StatKey.KnockbackPower, ModifierType.BaseFlat, 3f);
            bool critical = false;
            enemy.Damaged += (_, context, result) => critical = context.IsCritical;
            var result = enemy.TakeDamage(new DamageContext(run.Player.transform, 2f, Vector2.right));
            Require(critical && Mathf.Approximately(result.AppliedDamage, 4f), "Critical chance and multiplier must affect real enemy damage.");
        }
        finally { Object.Destroy(enemy.gameObject); }
    }
    private static void Capture(string name)
    {
        var camera = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var oldCameras = canvases.Select(c => c.worldCamera).ToArray();
        int width = camera.pixelWidth, height = camera.pixelHeight;
        var target = new RenderTexture(width, height, 24);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f; }
            Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Logs/" + name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; RenderTexture.active = active;
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = oldCameras[i]; }
            target.Release(); Object.Destroy(target); Object.Destroy(texture);
        }
    }
    private static void Exit(int code)
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= OnLog;
        BatchCheckGuard.Finish(Key, code);
    }
}

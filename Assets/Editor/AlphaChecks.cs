using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AlphaChecks
{
    private const string Key = "Rotline.AlphaChecks";
    private static int phase;
    private static int checks;
    private static double next;
    private static double deadline;
    private static bool errors;
    private static int walletBefore;
    static AlphaChecks()
    {
        if (!SessionState.GetBool(Key, false)) return;
        AlphaSession.UseProfileForChecks(SessionState.GetString(Key + "Path", ""));
        next = EditorApplication.timeSinceStartup + 4; deadline = EditorApplication.timeSinceStartup + 100;
        Application.logMessageReceived += OnLog; EditorApplication.update += Update;
    }
    public static void RunBatch()
    {
        CombatRegressionChecks.RunBatch();
        AlphaSetup.Install();
        string path = Path.GetFullPath("Logs/AlphaChecks/" + Guid.NewGuid().ToString("N") + ".json");
        CheckPersistence(path + ".probe");
        SessionState.SetString(Key + "Path", path); SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); EditorApplication.isPlaying = true;
    }
    private static void CheckPersistence(string path)
    {
        var store = new AlphaProfileStore(path); store.Data.shards = 100; Require(store.Save(), "Profile must save.");
        Require(store.BuyUpgrade(1) && store.Data.power == 1 && store.Data.shards == 85, "Upgrade must deduct currency once.");
        Require(!store.BuyUpgrade(-1), "Invalid upgrade must be rejected.");
        int reward = store.Settle("unit-run", true, 20, 30);
        Require(reward == 29 && store.Settle("unit-run", true, 20, 30) == 0, "Settlement must be idempotent.");
        var reload = new AlphaProfileStore(path); Require(reload.Data.shards == 114 && reload.Data.power == 1, "Profile must survive reload.");
        File.WriteAllText(path, "corrupt");
        var recovered = new AlphaProfileStore(path); Require(recovered.Data.power == 1, "Corrupt primary must recover backup.");
        string blocker = path + ".blocked";
        File.WriteAllText(blocker, "not a directory");
        try
        {
            var failed = new AlphaProfileStore(Path.Combine(blocker, "profile.json"));
            Require(failed.Settle("failed-run", true, 20, 30) == -1 && failed.Data.shards == 0 &&
                failed.Data.runs == 0 && failed.Data.settledRuns.Count == 0 && failed.Error != null,
                "Failed settlement must not look paid or mutate the profile.");
            Require(!failed.Discover("failed-item") && failed.Data.discoveries.Count == 0,
                "Failed discovery must not remain in memory.");
            File.Delete(blocker);
            Require(failed.Settle("failed-run", true, 20, 30) == 29 &&
                failed.Data.discoveries.Contains("failed-item") &&
                new AlphaProfileStore(Path.Combine(blocker, "profile.json")).Data.discoveries.Contains("failed-item"),
                "A later successful save must recover the settlement and pending discovery.");
        }
        finally
        {
            if (File.Exists(blocker)) File.Delete(blocker);
            else if (Directory.Exists(blocker)) Directory.Delete(blocker, true);
        }
        Debug.Log("[AlphaProfile] Saving, upgrade purchase, duplicate settlement and backup recovery passed.");
    }
    private static void OnLog(string message, string trace, LogType type) { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors = true; }
    private static void Update()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Alpha test timed out.");
            var run = Object.FindFirstObjectByType<SurvivalRunController>();
            var alpha = Object.FindFirstObjectByType<AlphaRunController>();
            var arena = Object.FindFirstObjectByType<AlphaArena>();
            if (phase == 0)
            {
                var menu = Object.FindFirstObjectByType<AlphaMainMenu>(); Require(menu != null && !AlphaSession.Active, "Application must start at menu.");
                AlphaSession.Profile.Data.shards = 100; AlphaSession.Profile.Save(); Require(AlphaSession.Profile.BuyUpgrade(1), "Persistent upgrade must be purchasable.");
                walletBefore = AlphaSession.Profile.Data.shards;
                Capture("AlphaMenu"); menu.Show("select"); phase++; next = EditorApplication.timeSinceStartup + 0.4;
            }
            else if (phase == 1)
            {
                var menu = Object.FindFirstObjectByType<AlphaMainMenu>();
                var ranger = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == "Ranger"); ranger.onClick.Invoke();
                var mode = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == "Mode");
                Require(mode.GetComponentInChildren<Text>().text.Contains("Gameplay 필드"), "Menu must default to the authored Gameplay field.");
                mode.onClick.Invoke();
                Capture("AlphaCharacters"); menu.Launch(); phase++; next = EditorApplication.timeSinceStartup + 3;
            }
            else if (phase == 2)
            {
                Require(run != null && run.IsRunning && alpha.Room == 0 && arena.RoomCount == 5, "Menu must launch a five-room expedition.");
                Require(run.Player.Definition.CharacterId == "Ranger" && run.Player.Stats.AttackPower == 9f, "Selected character and persistent upgrade must apply once.");
                Require(!arena.IsDoorOpen(0), "First room must initially lock its exit.");
                Physics2D.SyncTransforms();
                Require(Physics2D.Linecast(arena.Centers[0] + Vector2.one * 0.5f, arena.Centers[1] + Vector2.one * 0.5f, 1 << 8).collider != null,
                    "Locked door must physically block its corridor.");
                foreach (var center in arena.Centers) Require(Object.FindFirstObjectByType<NavigationService>().IsWalkable(center + Vector2.one * 0.5f), "Every generated room must be navigable.");
                run.Health.GrantInvulnerability(90);
                KillWave(run, 15); Require(alpha.RoomCleared && arena.IsDoorOpen(0), "Combat quota must unlock next room.");
                Require(Physics2D.Linecast(arena.Centers[0] + Vector2.one * 0.5f, arena.Centers[1] + Vector2.one * 0.5f, 1 << 8).collider == null,
                    "Cleared corridor must physically allow travel.");
                Move(run, arena, 1); phase++; next = EditorApplication.timeSinceStartup + 0.4;
            }
            else if (phase == 3)
            {
                Require(alpha.Room == 1, "Walking into next room must change encounter.");
                alpha.Interact(); Require(run.State == SurvivalRunState.ChoosingReward && arena.IsDoorOpen(1), "Treasure interaction must offer reward and unlock exit.");
                var choice = run.Choices[0]; string id = choice.attack != null ? choice.attack.Id : choice.item.Id;
                Require(run.ChooseReward(0) && AlphaSession.Profile.Data.discoveries.Contains(id), "Selected treasure must enter persistent collection.");
                Move(run, arena, 2); phase++; next = EditorApplication.timeSinceStartup + 0.4;
            }
            else if (phase == 4)
            {
                Require(alpha.Room == 2, "Second combat encounter must begin.");
                KillWave(run, 25); Require(alpha.RoomCleared && arena.IsDoorOpen(2), "Second quota must open shop route.");
                Move(run, arena, 3); phase++; next = EditorApplication.timeSinceStartup + 0.4;
            }
            else if (phase == 5)
            {
                Require(alpha.Room == 3, "Shop room must be entered."); alpha.Interact();
                Require(run.State == SurvivalRunState.SystemMenu && Time.timeScale == 0, "Shop must pause gameplay.");
                var pages = run.transform.Find("Alpha System Pages");
                Require(pages != null && pages.gameObject.activeSelf, "Shop must activate the authored page.");
                int pageObjects = pages.GetComponentsInChildren<Transform>(true).Length;
                Capture("AlphaShop");
                float power = run.Player.Stats.AttackPower; int coins = alpha.Coins;
                var powerButton = pages.GetComponentsInChildren<Button>(true).First(b => b.name == "Power");
                powerButton.onClick.Invoke();
                Require(run.Player.Stats.AttackPower == power + 2 && alpha.Coins == coins - 12, "Authored shop button must grant item and deduct exact cost.");
                Require(!alpha.Buy(1), "Sold offer cannot be purchased again.");
                Require(pageObjects == pages.GetComponentsInChildren<Transform>(true).Length, "Shop refresh must retain its authored hierarchy.");
                alpha.ClosePage(); Require(arena.IsDoorOpen(3) && run.IsRunning, "Leaving shop must unlock boss and resume.");
                Move(run, arena, 4); phase++; next = EditorApplication.timeSinceStartup + 3;
            }
            else if (phase == 6)
            {
                Require(alpha.Room == 4, "Final encounter must start.");
                var boss = Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None).First(e => e.Definition != null && e.Definition.rangedStyle == 2);
                Require(AlphaHostileShots.Instance.ActiveCount > 0, "Boss must fire real hostile projectiles.");
                Capture("AlphaBoss"); boss.TakeDamage(float.MaxValue);
                Require(run.State == SurvivalRunState.Won && AlphaSession.Profile.Data.wins == 1 && AlphaSession.Profile.Data.shards > walletBefore, "Boss defeat must settle victory once.");
                int wallet = AlphaSession.Profile.Data.shards; run.Finish(true); Require(AlphaSession.Profile.Data.shards == wallet, "Repeated finish must not duplicate payout.");
                phase++; next = EditorApplication.timeSinceStartup + 0.4;
            }
            else if (phase == 7)
            {
                Capture("AlphaResults"); alpha.ReturnToMenu(); phase++; next = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 8)
            {
                Require(Object.FindFirstObjectByType<AlphaMainMenu>() != null && Time.timeScale == 1 && !AlphaSession.Active, "Result must return to a working menu.");
                var reloaded = new AlphaProfileStore(SessionState.GetString(Key + "Path", ""));
                Require(reloaded.Data.wins == 1 && reloaded.Data.power == 1 && reloaded.Data.discoveries.Count > 0, "Meta progress and discoveries must survive disk reload.");
                var menu = Object.FindFirstObjectByType<AlphaMainMenu>(); menu.Show("select");
                menu.Launch(); phase++; next = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 9)
            {
                Require(run.IsRunning && !AlphaSession.Expedition && run.Player.Definition.CharacterId == "Berserker", "Menu must also launch the survival mode and melee character.");
                var pages = run.transform.Find("Alpha System Pages");
                int pageObjects = pages.GetComponentsInChildren<Transform>(true).Length;
                run.TogglePause(); alpha.OpenPage("inventory"); Require(run.State == SurvivalRunState.SystemMenu, "Paused inventory must open safely.");
                alpha.OpenPage("settings");
                Require(pageObjects == pages.GetComponentsInChildren<Transform>(true).Length, "Equipment and settings must retain authored rows and controls.");
                alpha.ClosePage(); Require(run.State == SurvivalRunState.Paused, "Closing a system page must preserve the prior pause state.");
                run.TogglePause(); run.Health.TakeDamage(float.MaxValue);
                Require(run.State == SurvivalRunState.Lost && AlphaSession.Profile.Data.runs == 2, "Death must settle failure once.");
                run.Restart(); phase++; next = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 10)
            {
                Require(run.IsRunning && run.Kills == 0 && run.Player.Stats.AttackPower == 11f && run.Health.Current == run.Health.Maximum,
                    "Restart must reset health, kills and run modifiers while applying meta upgrades once.");
                typeof(SurvivalRunController).GetField("<Elapsed>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(run, run.Definition.durationSeconds);
                phase++; next = EditorApplication.timeSinceStartup + 0.4;
            }
            else
            {
                Require(run.State == SurvivalRunState.Won && AlphaSession.Profile.Data.runs == 3 && AlphaSession.Profile.Data.wins == 2,
                    "Survival time limit must settle its own victory.");
                Require(!errors, "Alpha playthrough logged runtime errors.");
                Debug.Log($"[AlphaChecks] {checks} checks passed: menu, selection, seeded rooms, doors, combat, treasure, shop, boss, result, profile and return."); Exit(0);
            }
        }
        catch (Exception ex) { Debug.LogException(ex); Exit(1); }
    }
    private static void KillWave(SurvivalRunController run, int count)
    {
        var spawner = Object.FindFirstObjectByType<SpawnDirector>(); spawner.SuspendSpawning = true;
        for (int i = 0; i < count; i++) spawner.SpawnSpecific(AlphaSession.Catalog.enemies[0], (Vector2)run.Player.transform.position + Vector2.right * 5).TakeDamage(float.MaxValue);
    }
    private static void Move(SurvivalRunController run, AlphaArena arena, int room)
    { run.Player.transform.position = arena.Centers[room] + Vector2.one * 0.5f; run.Player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); var camera = Camera.main;
        var target = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.GetFullPath("Logs/" + name + ".png"), texture.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; target.Release(); Object.Destroy(target); Object.Destroy(texture); }
    }
    private static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    private static void Exit(int code) { SessionState.SetBool(Key, false); EditorApplication.update -= Update; EditorApplication.Exit(code); }
}

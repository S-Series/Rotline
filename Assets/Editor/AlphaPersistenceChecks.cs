using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Isolated store failures plus real scene transitions; never installs or saves authored scenes.
[InitializeOnLoad]
public static class AlphaPersistenceChecks
{
    private const string Key = "Rotline.AlphaPersistenceChecks";
    private static int phase;
    private static int assertions;
    private static double next;
    private static bool errors;
    private static string blockedDirectory;
    private static string profilePath;
    private static string firstRunId;
    private static string secondRunId;

    static AlphaPersistenceChecks()
    {
        if (!SessionState.GetBool(Key, false)) return;
        BatchCheckGuard.RestoreProfile();
        next = EditorApplication.timeSinceStartup + 3;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Update;
    }

    public static void RunBatch()
    {
        BatchCheckGuard.Run(Key, 80, () =>
        {
            BatchCheckGuard.Gameplay();
            BatchCheckGuard.Menu();
            CheckStore(BatchCheckGuard.ProfilePath + ".store");
            EditorApplication.isPlaying = true;
        });
    }

    private static void CheckStore(string path)
    {
        string blocker = path + ".blocked";
        File.WriteAllText(blocker, "blocked parent");
        var failed = new AlphaProfileStore(Path.Combine(blocker, "profile.json"));
        Require(failed.Settle("one", true, 20, 30) == -1, "Blocked settlement must fail.");
        Require(failed.Settle("one", false, 999, 999) == -1 && failed.PendingSettlementCount == 1,
            "Conflicting retries must retain one original result.");
        Require(failed.Settle("two", false, 10, 60) == -1 && failed.PendingSettlementCount == 2,
            "Several failed results must survive in the store.");
        Require(failed.Data.shards == 0 && failed.Data.runs == 0 && failed.Data.settledRuns.Count == 0,
            "Failed saves must not commit rewards or records in memory.");
        Require(!failed.Discover("item") && failed.Data.discoveries.Count == 0,
            "Discovery must roll back while pending for retry.");
        Require(!failed.RetryPendingSettlements() && failed.PendingSettlementCount == 2,
            "Repeated failed retries must preserve both results.");
        File.Delete(blocker);
        // An ordinary settings save also flushes pending results exactly once.
        failed.Data.volume = 0.4f;
        Require(failed.Save() && failed.PendingSettlementCount == 0, "Any successful save must commit pending results.");
        Require(failed.Data.shards == 31 && failed.Data.runs == 2 && failed.Data.wins == 1 &&
            failed.Data.bestKills == 20 && failed.Data.bestSeconds == 60 && failed.Data.discoveries.Contains("item"),
            "Original rewards, records and discovery must commit together.");
        Require(failed.RetryPendingSettlements() && failed.Settle("one", true, 20, 30) == 0 && failed.Data.shards == 31,
            "Repeated successful retries must not pay twice.");
        var reload = new AlphaProfileStore(Path.Combine(blocker, "profile.json"));
        Require(reload.Data.shards == 31 && reload.Data.runs == 2 && reload.Data.volume == 0.4f,
            "Retried results and settings must survive a disk reload.");

        var store = new AlphaProfileStore(path);
        store.Data.shards = 100;
        Require(store.Save(), "Backup fixture initial save must work.");
        store.Data.shards = 120;
        Require(store.Save(), "Backup fixture rotation must work.");
        string backup = File.ReadAllText(path + ".bak");
        File.WriteAllText(path, "corrupt primary");
        var recovered = new AlphaProfileStore(path);
        Require(recovered.Data.shards == 100, "Corrupt primary must load the good backup.");
        Directory.CreateDirectory(path + ".tmp");
        Require(recovered.Settle("recovered", true, 20, 30) == -1 && recovered.Data.shards == 100,
            "A failed recovery save must roll back its result.");
        Require(File.ReadAllText(path + ".bak") == backup, "Failed recovery save must preserve the backup bytes.");
        Directory.Delete(path + ".tmp");
        Require(recovered.RetryPendingSettlements() && recovered.Data.shards == 129,
            "Recovery-save retry must commit exactly once.");
        Require(File.ReadAllText(path + ".bak") == backup, "Recovery save must not replace the backup with corruption.");
        Require(new AlphaProfileStore(path).Data.shards == 129, "Recovery save must write a valid primary.");
        File.WriteAllText(path, "second corruption");
        var secondRecovered = new AlphaProfileStore(path);
        Require(secondRecovered.Data.shards == 100, "Second primary corruption must still recover the good backup.");
        secondRecovered.Data.shards = 129;
        Require(secondRecovered.Save() && new AlphaProfileStore(path + ".bak").Data.shards == 100,
            "A second recovered store must also preserve the good backup during repair.");
        // Reload after repair, then exercise normal rotation with a healthy primary.
        var healthy = new AlphaProfileStore(path);
        healthy.Data.shards = 140;
        Require(healthy.Save() && new AlphaProfileStore(path + ".bak").Data.shards == 129,
            "Later normal saves must rotate the repaired healthy primary.");
        Debug.Log("[AlphaPersistence] Store rollback, pending results, discovery and recovery backup checks passed.");
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            var run = Object.FindFirstObjectByType<SurvivalRunController>();
            var menu = Object.FindFirstObjectByType<AlphaMainMenu>();
            if (phase == 0)
            {
                blockedDirectory = BatchCheckGuard.ProfilePath + ".scene-blocked";
                profilePath = Path.Combine(blockedDirectory, "profile.json");
                File.WriteAllText(blockedDirectory, "blocked parent");
                AlphaSession.UseProfileForChecks(profilePath);
                AlphaSession.Begin(0, false, 1234);
                firstRunId = AlphaSession.RunId;
                SceneManager.LoadScene("Gameplay");
                Advance(2);
            }
            else if (phase == 1)
            {
                PrepareResult(run, 20, 30);
                run.Finish(true);
                var alpha = run.GetComponent<AlphaRunController>();
                Require(alpha.SettlementFailed && AlphaSession.Profile.PendingSettlementCount == 1 &&
                    AlphaSession.Profile.Data.runs == 0, "Failed run result must remain pending and unpaid.");
                run.Finish(false);
                alpha.ReturnToMenu();
                Advance(1);
            }
            else if (phase == 2)
            {
                Require(menu != null && !AlphaSession.Active && AlphaSession.Profile.IsSettlementPending(firstRunId),
                    "Pending result must survive real scene destruction and menu entry.");
                var label = (Text)typeof(AlphaMainMenu).GetField("errorLabel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
                Require(label.gameObject.activeSelf && label.text.Contains("저장 대기 중인 결과 1건"),
                    "The existing menu error label must expose pending settlement status.");
                Canvas.ForceUpdateCanvases();
                Require(label.preferredHeight <= label.rectTransform.rect.height,
                    "Pending status must fit the existing authored menu error label.");
                File.Delete(blockedDirectory);
                Advance(3);
            }
            else if (phase == 3)
            {
                Require(AlphaSession.Profile.PendingSettlementCount == 0 && AlphaSession.Profile.Data.shards == 29 &&
                    AlphaSession.Profile.Data.runs == 1 && AlphaSession.Profile.Data.wins == 1,
                    "Menu must retry automatically after storage recovers and pay once.");
                var label = (Text)typeof(AlphaMainMenu).GetField("errorLabel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
                Require(!label.gameObject.activeSelf, "Successful retry must clear the menu failure status.");
                Require(AlphaSession.Profile.RetryPendingSettlements() &&
                    AlphaSession.Profile.Settle(firstRunId, false, 999, 999) == 0 && new AlphaProfileStore(profilePath).Data.shards == 29,
                    "Repeated retry must preserve the disk balance.");
                menu.Launch();
                secondRunId = AlphaSession.RunId;
                Require(secondRunId != firstRunId, "New runs must retain distinct settlement IDs.");
                Advance(2);
            }
            else if (phase == 4)
            {
                Directory.CreateDirectory(profilePath + ".tmp");
                PrepareResult(run, 10, 60);
                run.Finish(false);
                Require(AlphaSession.Profile.IsSettlementPending(secondRunId), "Second failed result must be pending before restart.");
                run.Restart();
                Advance(2);
            }
            else
            {
                Require(run.IsRunning && AlphaSession.RunId != secondRunId &&
                    AlphaSession.Profile.IsSettlementPending(secondRunId) && AlphaSession.Profile.Data.shards == 29,
                    "Restart and its failed Begin retry must preserve the prior result without paying it.");
                Directory.Delete(profilePath + ".tmp");
                PrepareResult(run, 0, 0);
                run.Restart();
                Require(AlphaSession.Profile.PendingSettlementCount == 0 && AlphaSession.Profile.Data.shards == 31 &&
                    AlphaSession.Profile.Data.runs == 3 && AlphaSession.Profile.Data.wins == 1,
                    "Restart must flush the old failed result and settle its own zero-kill run once.");
                Require(AlphaSession.Profile.RetryPendingSettlements() && new AlphaProfileStore(profilePath).Data.shards == 31,
                    "Run-start retry must remain idempotent on disk.");
                Require(!errors, "Persistence scene checks must not log runtime errors.");
                Debug.Log($"[AlphaPersistenceChecks] {assertions} Play Mode assertions passed: failed result, menu, automatic retry, repeated retry and restart boundaries.");
                Exit(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Exit(1); }
    }

    private static void PrepareResult(SurvivalRunController run, int kills, float seconds)
    {
        Require(run != null && run.IsRunning, "Fixture needs a ready run.");
        var spawner = Object.FindFirstObjectByType<SpawnDirector>();
        spawner.SuspendSpawning = true;
        spawner.ClearEnemies();
        run.Health.GrantInvulnerability(30);
        typeof(SurvivalRunController).GetField("<Kills>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(run, kills);
        typeof(SurvivalRunController).GetField("<Elapsed>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(run, seconds);
    }
    private static void Advance(double seconds) { phase++; next = EditorApplication.timeSinceStartup + seconds; }
    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors = true;
    }
    private static void Require(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
    private static void Exit(int code)
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= OnLog;
        BatchCheckGuard.Finish(Key, code);
    }
}

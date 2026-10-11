using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Exercises real projectile updates and the run's synchronous death/settlement callbacks.
[InitializeOnLoad]
public static class AlphaHostileShotsChecks
{
    private const string Key = "Rotline.AlphaHostileShotsChecks";
    private static double next;
    private static int phase;
    private static int assertions;
    private static int deaths;
    private static bool errors;
    private static float healthBefore;
    private static Vector2 missPosition;
    private static Action clearAndFire;

    static AlphaHostileShotsChecks()
    {
        if (!SessionState.GetBool(Key, false)) return;
        BatchCheckGuard.RestoreProfile();
        next = EditorApplication.timeSinceStartup + 3;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Update;
    }

    public static void RunBatch()
    {
        BatchCheckGuard.Run(Key, 60, () =>
        {
            BatchCheckGuard.Gameplay();
            BatchCheckGuard.Menu();
            EditorApplication.isPlaying = true;
        });
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors = true;
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            var run = Object.FindFirstObjectByType<SurvivalRunController>();
            var shots = AlphaHostileShots.Instance;
            if (phase == 0)
            {
                AlphaSession.Begin(0, false, 12345);
                SceneManager.LoadScene("Gameplay");
                Advance(2);
            }
            else if (phase == 1)
            {
                Prepare(run, shots);
                var spawner = Object.FindFirstObjectByType<SpawnDirector>();
                spawner.SpawnSpecific(AlphaSession.Catalog.enemies[0], missPosition).TakeDamage(float.MaxValue);
                Require(run.Kills == 1, "A real kill must provide a nonzero settlement fixture.");
                run.Health.Died += CountDeath;
                healthBefore = run.Health.Current;
                shots.Fire(missPosition, Vector2.right, 0, 2);
                shots.Fire(missPosition, Vector2.up, 0, 2);
                // An expired miss must recycle without affecting the other live shots.
                shots.Fire(missPosition, Vector2.right, 0, 2);
                var active = (IList)Field(shots, "active");
                active[active.Count - 1].GetType().GetField("life").SetValue(active[active.Count - 1], 0f);
                shots.Fire(run.Player.transform.position, Vector2.right, 7, 2);
                Advance(1);
            }
            else if (phase == 2)
            {
                Require(run.IsRunning && deaths == 0, "Nonlethal hit must keep the run alive.");
                Require(Mathf.Approximately(run.Health.Current, healthBefore - 2), "One normal hit must apply exactly its damage.");
                Require(shots.ActiveCount == 2, "Hit and expiry must recycle while both non-hitting shots remain active.");
                CheckPool(shots);
                Require(AlphaSession.Profile.Data.runs == 0, "A normal hit must not settle the run.");
                // Other shots remain in the list when this last-added shot causes death.
                shots.Fire(run.Player.transform.position, Vector2.right, 7, float.MaxValue);
                Advance(.4);
            }
            else if (phase == 3)
            {
                Require(!errors, "Lethal projectile update must not log an exception.");
                Require(run.State == SurvivalRunState.Lost && deaths == 1 && Time.timeScale == 0,
                    "Actual projectile death must end the run once and stop time.");
                Require(shots.ActiveCount == 0, "Run end must clear all remaining hostile shots.");
                CheckPool(shots);
                var profile = AlphaSession.Profile.Data;
                Require(profile.runs == 1 && profile.wins == 0 && profile.shards == 1 && profile.settledRuns.Count == 1,
                    "Failure with one kill must settle exactly once.");
                var saved = new AlphaProfileStore(BatchCheckGuard.ProfilePath).Data;
                Require(saved.runs == 1 && saved.shards == 1 && saved.settledRuns.Count == 1,
                    "The isolated profile must persist the single settlement.");
                run.Finish(false); run.Finish(true);
                Require(profile.runs == 1 && profile.shards == 1 && profile.settledRuns.Count == 1,
                    "Repeated finish requests must not repeat settlement.");
                Debug.Log("[HostileShots] Nonlethal hit, expiry, misses and lethal projectile settlement passed.");
                run.Restart();
                Advance(2);
            }
            else if (phase == 4)
            {
                Prepare(run, shots);
                healthBefore = run.Health.Current;
                clearAndFire = () =>
                {
                    shots.Clear();
                    // May immediately reuse the shot that was being processed.
                    shots.Fire(missPosition, Vector2.right, 0, 2);
                };
                run.Health.Changed += clearAndFire;
                shots.Fire(missPosition, Vector2.up, 0, 2);
                shots.Fire(missPosition, Vector2.right, 0, 2);
                shots.Fire(run.Player.transform.position, Vector2.right, 7, 2);
                Advance(.4);
            }
            else
            {
                run.Health.Changed -= clearAndFire;
                Require(!errors && run.IsRunning && Mathf.Approximately(run.Health.Current, healthBefore - 2),
                    "Clear-and-fire inside a damage callback must preserve the nonlethal hit.");
                Require(shots.ActiveCount == 1, "A shot fired during Clear callback must remain active exactly once.");
                CheckPool(shots);
                Require(AlphaSession.Profile.Data.runs == 1, "Restart and nonlethal reentry must not settle another run.");
                shots.Clear(); shots.Clear();
                CheckPool(shots);
                Require(shots.ActiveCount == 0, "Repeated Clear must remain safe.");
                Debug.Log($"[HostileShots] All {assertions} assertions passed: real projectile death, normal hits, misses, expiry, pool reuse, callback Clear/Fire and one persisted settlement.");
                Finish(0);
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }

    private static void Prepare(SurvivalRunController run, AlphaHostileShots shots)
    {
        Require(run != null && run.IsRunning && shots != null, "Gameplay must initialize an active alpha run and hostile shots.");
        var spawner = Object.FindFirstObjectByType<SpawnDirector>();
        spawner.SuspendSpawning = true; spawner.ClearEnemies();
        run.Player.GetComponent<PlayerCombatController>().enabled = false;
        run.Player.GetComponent<AlphaCorruptionHazard>().enabled = false;
        shots.Clear();
        var navigation = Object.FindFirstObjectByType<NavigationService>();
        bool found = false;
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * 2 / 32;
            missPosition = (Vector2)run.Player.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5;
            if (navigation.IsWalkable(missPosition) && Physics2D.OverlapCircle(missPosition, .2f, 1 << 8) == null)
            { found = true; break; }
        }
        Require(found, "Need a clear, distant position for non-hitting projectiles.");
    }

    private static void CheckPool(AlphaHostileShots shots)
    {
        var unique = new HashSet<object>();
        foreach (string name in new[] { "active", "pooled" })
            foreach (var shot in (IEnumerable)Field(shots, name))
            {
                Require(unique.Add(shot), "A projectile must belong to exactly one active/pool slot.");
                var view = (Transform)Field(shot, "view");
                Require(view.gameObject.activeSelf == (name == "active"), "Projectile visibility must agree with pool ownership.");
            }
        int allocated = 0;
        foreach (Transform child in shots.transform)
            if (child.name == "Hostile shot") allocated++;
        Require(unique.Count == allocated, "Every allocated projectile must remain accounted for.");
    }

    private static object Field(object value, string name) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(value);
    private static void CountDeath() => deaths++;
    private static void Advance(double seconds) { phase++; next = EditorApplication.timeSinceStartup + seconds; }
    private static void Require(bool condition, string message)
    { assertions++; if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(int code)
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= OnLog;
        BatchCheckGuard.Finish(Key, code);
    }
}

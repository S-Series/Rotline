using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Repeatable, stationary first-minute combat sample. It measures attack throughput,
// not player skill or the balance of movement and pickup collection.
[InitializeOnLoad]
public static class AlphaPacingProbe
{
    private const string Key = "Rotline.AlphaPacingProbe";
    private static int phase;
    private static int character;
    private static float firstKill;
    private static int maxEnemies;
    private static double next;
    private static bool errors;

    static AlphaPacingProbe()
    {
        if (!SessionState.GetBool(Key, false)) return;
        AlphaSession.UseProfileForChecks(SessionState.GetString(Key + "Profile", ""));
        next = EditorApplication.timeSinceStartup + 3;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }

    public static void RunBatch()
    {
        string path = Path.GetFullPath("Logs/Pacing/" + Guid.NewGuid().ToString("N") + ".json");
        SessionState.SetString(Key + "Profile", path);
        SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (phase == 0)
            {
                Launch();
                phase = 1;
                next = EditorApplication.timeSinceStartup + 3;
                return;
            }
            var run = Object.FindFirstObjectByType<SurvivalRunController>();
            if (run == null || run.Health == null) return;
            if (run.State == SurvivalRunState.ChoosingReward) run.ChooseReward(0);
            if (run.State != SurvivalRunState.Running) throw new InvalidOperationException("Pacing sample stopped early: " + run.State);
            run.Health.GrantInvulnerability(5f);
            Time.timeScale = 3f;
            var spawner = Object.FindFirstObjectByType<SpawnDirector>();
            maxEnemies = Mathf.Max(maxEnemies, spawner.ActiveCount);
            if (firstKill < 0f && run.Kills > 0) firstKill = run.Elapsed;
            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsSortMode.None);
            var target = enemies.Where(e => e != null && e.isActiveAndEnabled && !e.IsDead)
                .OrderBy(e => ((Vector2)e.transform.position - (Vector2)run.Player.transform.position).sqrMagnitude)
                .FirstOrDefault();
            if (target != null)
            {
                var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(mouse, new MouseState
                {
                    position = Camera.main.WorldToScreenPoint(target.transform.position)
                });
            }
            if (run.Elapsed < 60f) { next = EditorApplication.timeSinceStartup + 0.08; return; }
            Debug.Log($"[AlphaPacing] {run.Player.Definition.CharacterId}: first kill {firstKill:0.0}s, " +
                $"kills {run.Kills}, peak active {maxEnemies}, level {run.Experience.Level}, health {run.Health.Current:0} at 60s.");
            if (errors) throw new InvalidOperationException("Pacing sample logged a runtime error.");
            if (character == 0)
            {
                character = 1;
                Launch();
                next = EditorApplication.timeSinceStartup + 3;
            }
            else Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); Exit(1); }
    }

    private static void Launch()
    {
        firstKill = -1f;
        maxEnemies = 0;
        Time.timeScale = 1f;
        AlphaSession.Begin(character, false, 1234);
        SceneManager.LoadScene("Gameplay");
    }

    private static void Exit(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        Time.timeScale = 1f;
        EditorApplication.Exit(code);
    }
}

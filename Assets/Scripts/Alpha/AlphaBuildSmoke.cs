using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in standalone smoke check; never runs during an ordinary player launch.
public sealed class AlphaBuildSmoke : MonoBehaviour
{
    private bool failed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "-alpha-smoke") < 0) return;
        AlphaSession.UseProfileForChecks(Path.Combine(Application.temporaryCachePath, "alpha-smoke-" + Guid.NewGuid().ToString("N") + ".json"));
        var obj = new GameObject("Standalone smoke check"); DontDestroyOnLoad(obj); obj.AddComponent<AlphaBuildSmoke>();
    }
    private void OnEnable() => Application.logMessageReceived += OnLog;
    private void OnDisable() => Application.logMessageReceived -= OnLog;
    private void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true; }
    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        if (FindFirstObjectByType<AlphaMainMenu>() == null) failed = true;
        AlphaSession.Begin(1, true, 1234); SceneManager.LoadScene("Gameplay");
        yield return new WaitForSecondsRealtime(3);
        var run = FindFirstObjectByType<SurvivalRunController>(); var arena = FindFirstObjectByType<AlphaArena>();
        if (run == null || !run.IsRunning || arena == null || arena.RoomCount != 5) failed = true;
        if (run != null) run.GetComponent<AlphaRunController>().ReturnToMenu();
        yield return new WaitForSecondsRealtime(2);
        if (FindFirstObjectByType<AlphaMainMenu>() == null) failed = true;
        Debug.Log(failed ? "[AlphaSmoke] FAILED" : "[AlphaSmoke] Menu -> expedition -> menu passed in standalone player.");
        Application.Quit(failed ? 1 : 0);
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MenuHierarchyChecks
{
    const string Key = "Rotline.MenuHierarchyChecks";
    static double next;
    static int stage;
    static int count;
    static bool errors;
    static string field;
    static Vector3 spawn;
    static MenuHierarchyChecks()
    {
        if (!SessionState.GetBool(Key, false)) return;
        AlphaSession.UseProfileForChecks(SessionState.GetString(Key + "Profile", ""));
        next = EditorApplication.timeSinceStartup + 3;
        Application.logMessageReceived += (m, t, k) =>
        {
            // Unity's search index can fail in a fresh batch project, independently of game code.
            if (t.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (k == LogType.Error || k == LogType.Exception || k == LogType.Assert) errors = true;
        };
        EditorApplication.update += Tick;
    }
    public static void RunBatch()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity");
        SessionState.SetString(Key + "Field", Field());
        SessionState.SetVector3(Key + "Spawn", Object.FindFirstObjectByType<PlayerCharacter>().transform.position);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var menu = Object.FindFirstObjectByType<AlphaMainMenu>();
        Require(menu.GetComponentsInChildren<Button>(true).Length == 18, "All eighteen menu buttons must be authored in the scene.");
        foreach(var button in menu.GetComponentsInChildren<Button>(true)) Require(button.onClick.GetPersistentEventCount() == 1, "Each button needs a saved Inspector event: " + button.name);
        Require(menu.GetComponent<AlphaSystemView>() == null, "Menu must use its own authored hierarchy.");
        foreach(var text in menu.GetComponentsInChildren<Text>(true)) Require(text.font != null, "Text needs a saved font: " + text.name);
        string path = Path.GetFullPath("Logs/MenuChecks/" + Guid.NewGuid().ToString("N") + ".json");
        SessionState.SetString(Key + "Profile", path); SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if(stage == 0)
            {
                field = SessionState.GetString(Key + "Field", ""); spawn = SessionState.GetVector3(Key + "Spawn", Vector3.zero);
                var menu = Object.FindFirstObjectByType<AlphaMainMenu>();
                count = menu.GetComponentsInChildren<Transform>(true).Length;
                var title = menu.GetComponentsInChildren<Text>(true).First(t => t.name == "Title");
                title.text = "Inspector customization"; var position = title.rectTransform.anchoredPosition; title.rectTransform.anchoredPosition += Vector2.right * 7;
                foreach(string page in new[]{"select","upgrades","collection","settings","main","select"}) menu.Show(page);
                Require(count == menu.GetComponentsInChildren<Transform>(true).Length, "Changing pages must retain the hierarchy.");
                Require(title.text == "Inspector customization" && title.rectTransform.anchoredPosition == position + Vector2.right * 7, "Page changes must preserve authored text and layout.");
                title.text = "ROTLINE"; title.rectTransform.anchoredPosition = position;
                var ranger = menu.GetComponentsInChildren<Button>(true).First(b=>b.name == "Ranger");ranger.onClick.Invoke();
                Require(ranger.GetComponentInChildren<Text>().text.StartsWith("✓"), "Saved character event must work.");
                var mode = menu.GetComponentsInChildren<Button>(true).First(b=>b.name == "Mode");
                Require(mode.GetComponentInChildren<Text>().text.Contains("Gameplay 필드"), "Default must use authored field.");
                mode.onClick.Invoke(); Require(mode.GetComponentInChildren<Text>().text.Contains("무작위"), "Mode button must work.");mode.onClick.Invoke();
                AlphaSession.Profile.Data.shards = 100;menu.Show("upgrades");
                menu.GetComponentsInChildren<Button>(true).First(b=>b.name == "Upgrade 1").onClick.Invoke();
                Require(AlphaSession.Profile.Data.power == 1 && AlphaSession.Profile.Data.shards == 85, "Saved upgrade event must purchase once.");
                menu.Show("settings");float volume = AlphaSession.Profile.Data.volume;
                menu.GetComponentsInChildren<Button>(true).First(b=>b.name == "Volume").onClick.Invoke();
                Require(AlphaSession.Profile.Data.volume != volume, "Saved settings event must work.");
                menu.Show("main");Capture();menu.Show("select");
                menu.GetComponentsInChildren<Button>(true).First(b=>b.name == "Launch").onClick.Invoke();
                stage++;next = EditorApplication.timeSinceStartup + 3;
            }
            else
            {
                var run=Object.FindFirstObjectByType<SurvivalRunController>();
                Require(run != null && run.IsRunning && !AlphaSession.Expedition, "Menu must launch authored-field survival.");
                Require(run.Player.Definition.CharacterId == "Ranger", "Selected character must carry into gameplay.");
                Require(Field() == field, "Gameplay tilemaps must remain unchanged.");
                Require(Vector3.Distance(run.Player.transform.position,spawn)<0.1f, "Authored player start must remain unchanged.");
                Require(!errors,"No runtime errors are allowed.");
                Debug.Log("[MenuHierarchyChecks] Serialized references, all 18 button bindings, stable hierarchy, editable layout, selection, upgrade, settings and authored-field launch passed.");Exit(0);
            }
        }
        catch(Exception e){Debug.LogException(e);Exit(1);}
    }
    static string Field()
    {
        var result = new System.Text.StringBuilder();
        var navigation = new SerializedObject(Object.FindFirstObjectByType<NavigationService>());
        var tilemaps = new[] { (Tilemap)navigation.FindProperty("groundTilemap").objectReferenceValue, (Tilemap)navigation.FindProperty("obstacleTilemap").objectReferenceValue };
        foreach (var tilemap in tilemaps.Where(t => t != null))
        {
            result.Append(tilemap.name).Append(':');
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
                if (tilemap.HasTile(cell)) result.Append(cell).Append('=').Append(tilemap.GetTile(cell).name).Append(';');
        }
        return result.ToString();
    }
    static void Capture()
    {
        Canvas.ForceUpdateCanvases();var camera=Camera.main;var target=new RenderTexture(1280,720,24);var active=RenderTexture.active;var previous=camera.targetTexture;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Logs/AuthoredMenu.png",texture.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static void Exit(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
}

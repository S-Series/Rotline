using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Resolve the complete plan before adding components or writing a scene.
public static class RunUIRepair
{
    public const string GameplayPath = "Assets/Scenes/Gameplay.unity";
    public static Scene OpenGameplay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before scene authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your open scene edits before installation/authoring; no scenes were replaced.");
        var scene = SceneManager.GetSceneByPath(GameplayPath);
        if (scene.isLoaded) return scene;
        var previous = SceneManager.GetActiveScene();
        scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Additive);
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        return scene;
    }

    public static GameObject FindRunRoot()
    {
        return FindRunRoot(SceneManager.GetSceneByPath(GameplayPath));
    }

    public static GameObject FindRunRoot(Scene scene)
    {
        RequireGameplay(scene);
        if (Components<SurvivalRunController>(scene).Length > 1)
            throw new InvalidOperationException("Gameplay has multiple run controllers. No authoring changes were made.");
        var candidates = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
            .Where(x => x.GetComponent<SurvivalRunController>() != null || x.GetComponent<RunUIBindings>() != null || x.GetComponent<SurvivalRunHUD>() != null || x.name == "Survival Run")
            .Select(x => x.gameObject).Distinct().ToArray();
        if (candidates.Length != 1) throw new InvalidOperationException("Cannot identify the existing run owner safely; restore its component or binding marker. No hierarchy was generated.");
        return candidates[0];
    }

    public static T[] Components<T>(Scene scene) where T : Component
    {
        RequireGameplay(scene);
        return scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
    }

    public static T Unique<T>(Scene scene) where T : Component
    {
        var candidates = Components<T>(scene);
        if (candidates.Length != 1) throw new InvalidOperationException($"Gameplay needs exactly one {typeof(T).Name}; found {candidates.Length}. No authoring changes were made.");
        return candidates[0];
    }

    public static T OptionalUnique<T>(Scene scene) where T : Component
    {
        var candidates = Components<T>(scene);
        if (candidates.Length > 1) throw new InvalidOperationException($"Gameplay has multiple {typeof(T).Name} owners. No authoring changes were made.");
        return candidates.Length == 1 ? candidates[0] : null;
    }

    public static Camera MainCamera(Scene scene)
    {
        var cameras = Components<Camera>(scene).Where(c => c.CompareTag("MainCamera")).ToArray();
        if (cameras.Length != 1) throw new InvalidOperationException("Gameplay needs exactly one authored MainCamera before creating UI.");
        return cameras[0];
    }

    public static void RequireObjectInScene(Object value, Scene scene, string label)
    {
        if (value == null) return;
        if (value is GameObject obj && obj.scene != scene || value is Component component && component.gameObject.scene != scene)
            throw new InvalidOperationException($"{label} belongs to another scene. Restore a Gameplay reference; no authoring changes were made.");
    }

    private static void RequireGameplay(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.path != GameplayPath)
            throw new InvalidOperationException("The saved Gameplay scene must be loaded before resolving its authoring targets.");
    }

    public static IDisposable ActivateForAuthoring(Scene scene) => new ActiveSceneScope(scene);
    private sealed class ActiveSceneScope : IDisposable
    {
        private readonly Scene previous;
        public ActiveSceneScope(Scene scene)
        {
            RequireGameplay(scene);
            previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
        }
        public void Dispose()
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    public sealed class Plan
    {
        public GameObject root;
        public Canvas hudCanvas, pagesCanvas;
        public readonly List<RunUIBindings.Binding> bindings = new();
    }

    public static Plan Inspect(GameObject root)
    {
        RequireGameplay(root.scene);
        var plan = new Plan { root = root };
        var hud = root.GetComponent<SurvivalRunHUD>();
        var alpha = root.GetComponent<AlphaRunController>();
        var view = Read(alpha, "ui") as AlphaSystemView;
        RequireObjectInScene(view, root.scene, "AlphaRunController.ui");
        var marker = root.GetComponent<RunUIBindings>();
        plan.hudCanvas = Read(hud, "canvas") as Canvas ?? Saved(marker, "HUD", "canvas", 0) as Canvas;
        plan.pagesCanvas = Read(view, "canvas") as Canvas ?? Saved(marker, "Pages", "canvas", 0) as Canvas;
        var canvases = root.GetComponentsInChildren<Canvas>(true);
        if (plan.hudCanvas == null) plan.hudCanvas = UniqueCanvas(canvases, "Status/Status Text", "Choice Overlay/Reward 0");
        if (plan.pagesCanvas == null) plan.pagesCanvas = UniqueCanvas(canvases, "Shop/Coins", "Settings/Volume", "Equipment/Stats");
        if (plan.hudCanvas == null || plan.pagesCanvas == null || plan.hudCanvas == plan.pagesCanvas)
            throw new InvalidOperationException("Run UI is partial or ambiguous. Restore its saved references/binding marker; existing UI was left untouched and no replacement was generated.");
        view ??= plan.pagesCanvas.GetComponent<AlphaSystemView>();
        Add(plan, marker, hud, "HUD", "canvas", typeof(Canvas), plan.hudCanvas, "");
        foreach (var field in new[] { "statusPanel|Status", "hints|Control Hints", "overlay|Choice Overlay" })
            Field(plan, marker, hud, "HUD", typeof(GameObject), plan.hudCanvas, field);
        foreach (var field in new[] { "status|Status/Status Text", "inventory|Recent Rewards", "objective|Objective", "heading|Choice Overlay/Heading", "details|Choice Overlay/Details" })
            Field(plan, marker, hud, "HUD", typeof(Text), plan.hudCanvas, field);
        foreach (var field in new[] { "healthFill|Status/Health Bar/Fill", "experienceFill|Status/Experience Bar/Fill", "resourceFill|Status/Resource Bar/Fill" })
            Field(plan, marker, hud, "HUD", typeof(Image), plan.hudCanvas, field);
        foreach (var field in new[] { "resume|Choice Overlay/Resume", "restart|Choice Overlay/Restart", "mainMenu|Choice Overlay/Main Menu", "equipment|Choice Overlay/Equipment", "settings|Choice Overlay/Settings" })
            Field(plan, marker, hud, "HUD", typeof(Button), plan.hudCanvas, field);
        Add(plan, marker, hud, "HUD", "options", typeof(Button), plan.hudCanvas, Enumerable.Range(0, 3).Select(i => "Choice Overlay/Reward " + i).ToArray());
        var legacy = Read(hud, "legacyHUD") ?? Saved(marker, "HUD", "legacyHUD", 0);
        if (legacy != null) plan.bindings.Add(new RunUIBindings.Binding { role = "HUD", field = "legacyHUD", references = new[] { legacy } });
        Add(plan, marker, view, "Pages", "canvas", typeof(Canvas), plan.pagesCanvas, "");
        foreach (var field in new[] { "shopPage|Shop", "settingsPage|Settings", "inventoryPage|Equipment" })
            Field(plan, marker, view, "Pages", typeof(GameObject), plan.pagesCanvas, field);
        foreach (var field in new[] { "heading|Heading", "coins|Shop/Coins", "volumeLabel|Settings/Volume/Label", "statsLabel|Equipment/Stats" })
            Field(plan, marker, view, "Pages", typeof(Text), plan.pagesCanvas, field);
        foreach (var field in new[] { "volumeButton|Settings/Volume", "backButton|Back" })
            Field(plan, marker, view, "Pages", typeof(Button), plan.pagesCanvas, field);
        Add(plan, marker, view, "Pages", "shopOffers", typeof(Button), plan.pagesCanvas, "Shop/Heal", "Shop/Power", "Shop/Speed");
        Add(plan, marker, view, "Pages", "itemRows", typeof(Text), plan.pagesCanvas, Enumerable.Range(0, 5).Select(i => "Equipment/Item Row " + i).ToArray());
        Add(plan, marker, view, "Pages", "attackRows", typeof(Text), plan.pagesCanvas, Enumerable.Range(0, 4).Select(i => "Equipment/Attack Row " + i).ToArray());
        // Added by the explicit service authoring migration; older scenes need not invent it during repair.
        var error = Read(view, "errorLabel") ?? Saved(marker, "Pages", "errorLabel", 0);
        if (error != null) plan.bindings.Add(new RunUIBindings.Binding { role = "Pages", field = "errorLabel", references = new[] { error } });
        Validate(plan);
        if (marker != null) RequireObjectInScene(marker.audioRoot, root.scene, "RunUIBindings.audioRoot");
        return plan;
    }

    public static SurvivalRunController Apply(Plan plan)
    {
        Validate(plan);
        var run = Ensure<SurvivalRunController>(plan.root);
        var hud = Ensure<SurvivalRunHUD>(plan.root);
        var alpha = Ensure<AlphaRunController>(plan.root);
        var view = Ensure<AlphaSystemView>(plan.pagesCanvas.gameObject);
        foreach (var binding in plan.bindings)
        {
            var data = new SerializedObject(binding.role == "HUD" ? (Object)hud : view);
            var property = data.FindProperty(binding.field);
            if (property.isArray)
            {
                property.arraySize = binding.references.Length;
                for (int i = 0; i < binding.references.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = binding.references[i];
            }
            else property.objectReferenceValue = binding.references[0];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var alphaData = new SerializedObject(alpha);
        alphaData.FindProperty("ui").objectReferenceValue = view;
        alphaData.ApplyModifiedPropertiesWithoutUndo();
        var marker = Ensure<RunUIBindings>(plan.root);
        // Persist recovery references through SerializedObject so they survive controller removal.
        var markerData = new SerializedObject(marker);
        var entries = markerData.FindProperty("bindings"); entries.arraySize = plan.bindings.Count;
        for (int i = 0; i < plan.bindings.Count; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i); var binding = plan.bindings[i];
            entry.FindPropertyRelative("role").stringValue = binding.role;
            entry.FindPropertyRelative("field").stringValue = binding.field;
            var refs = entry.FindPropertyRelative("references"); refs.arraySize = binding.references.Length;
            for (int j = 0; j < binding.references.Length; j++) refs.GetArrayElementAtIndex(j).objectReferenceValue = binding.references[j];
        }
        markerData.ApplyModifiedPropertiesWithoutUndo();
        return run;
    }

    private static void Validate(Plan plan)
    {
        RequireGameplay(plan.root.scene);
        RequireObjectInScene(plan.hudCanvas, plan.root.scene, "HUD canvas");
        RequireObjectInScene(plan.pagesCanvas, plan.root.scene, "System pages canvas");
        foreach (var binding in plan.bindings)
            foreach (var reference in binding.references) RequireObjectInScene(reference, plan.root.scene, binding.role + "." + binding.field);
    }

    private static T Ensure<T>(GameObject owner) where T : Component => owner.GetComponent<T>() ?? Undo.AddComponent<T>(owner);
    private static Object Read(Object owner, string field, int index = 0)
    {
        if (owner == null) return null;
        var property = new SerializedObject(owner).FindProperty(field);
        if (property == null) return null;
        if (property.isArray) return index < property.arraySize ? property.GetArrayElementAtIndex(index).objectReferenceValue : null;
        return property.objectReferenceValue;
    }
    private static Object Saved(RunUIBindings marker, string role, string field, int index)
    {
        var entry = marker != null ? marker.bindings.FirstOrDefault(x => x.role == role && x.field == field) : null;
        return entry?.references != null && index < entry.references.Length ? entry.references[index] : null;
    }
    private static Canvas UniqueCanvas(Canvas[] canvases, params string[] paths)
    {
        var candidates = canvases.Where(c => paths.All(p => c.transform.Find(p) != null)).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
    private static void Field(Plan plan, RunUIBindings marker, Object owner, string role, Type type, Canvas canvas, string pair)
    { var parts = pair.Split('|'); Add(plan, marker, owner, role, parts[0], type, canvas, parts[1]); }
    private static void Add(Plan plan, RunUIBindings marker, Object owner, string role, string field, Type type, Canvas canvas, params string[] paths)
    {
        var property = owner != null ? new SerializedObject(owner).FindProperty(field) : null;
        bool array = field == "options" || field == "shopOffers" || field == "itemRows" || field == "attackRows";
        var saved = marker != null ? marker.bindings.FirstOrDefault(x => x.role == role && x.field == field) : null;
        int count = array && property != null && property.arraySize > 0 ? property.arraySize : array && saved?.references?.Length > 0 ? saved.references.Length : paths.Length;
        var refs = new Object[count];
        for (int i = 0; i < count; i++)
        {
            var value = Read(owner, field, i) ?? Saved(marker, role, field, i);
            if (value == null && i < paths.Length)
            {
                var transform = paths[i] == "" ? canvas.transform : canvas.transform.Find(paths[i]);
                if (transform != null) value = type == typeof(GameObject) ? transform.gameObject : transform.GetComponent(type);
            }
            if (value == null || !type.IsInstanceOfType(value)) throw new InvalidOperationException($"Cannot safely restore {role}.{field}[{i}]. Restore its existing authored object/reference; the scene was not saved.");
            refs[i] = value;
        }
        plan.bindings.Add(new RunUIBindings.Binding { role = role, field = field, references = refs });
    }
}

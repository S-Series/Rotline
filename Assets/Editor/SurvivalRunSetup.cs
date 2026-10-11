using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SurvivalRunSetup
{
    [MenuItem("ROTLINE/Run/Install Survival Systems")]
    public static void Install()
    {
        var scene = RunUIRepair.OpenGameplay();
        var root = RunUIRepair.FindRunRoot(scene);
        var repair = RunUIRepair.Inspect(root);
        var player = RunUIRepair.Unique<PlayerCharacter>(scene);
        var spawner = RunUIRepair.Unique<SpawnDirector>(scene);
        var currentRun = root.GetComponent<SurvivalRunController>();
        if (currentRun != null)
        {
            var existing = new SerializedObject(currentRun);
            RunUIRepair.RequireObjectInScene(existing.FindProperty("player").objectReferenceValue, scene, "Run.player");
            RunUIRepair.RequireObjectInScene(existing.FindProperty("spawner").objectReferenceValue, scene, "Run.spawner");
        }
        TemporaryItemAssets.Create(); CombatExampleAssets.Create();
        if (!AssetDatabase.IsValidFolder("Assets/Data/Runs")) AssetDatabase.CreateFolder("Assets/Data", "Runs");
        const string path = "Assets/Data/Runs/SurvivalRun.asset";
        var definition = AssetDatabase.LoadAssetAtPath<SurvivalRunDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<SurvivalRunDefinition>();
            string[] names = { "Experience50", "AttackSpeed20", "AttackPower2", "Luck3", "ProjectileSpeed01" };
            definition.levelRewards = new ItemDefinition[names.Length];
            for (int i = 0; i < names.Length; i++) definition.levelRewards[i] = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Item_" + names[i] + ".asset");
            string[] attacks = { "ChargedBeam", "ChargedRing", "Multishot", "Orbit" };
            string[] titles = { "응축된 광선", "파동의 핵", "갈라지는 힘", "궤도의 씨앗" };
            string[] descriptions = { "공격을 모았다가 놓으면 빔을 발사합니다.", "공격을 모았다가 놓으면 고리 형태의 공격을 펼칩니다.", "공격의 발사 수와 퍼짐을 늘립니다.", "공격에 공전 후 전진하는 궤도를 부여합니다." };
            definition.treasures = new RunReward[attacks.Length];
            for (int i = 0; i < attacks.Length; i++) definition.treasures[i] = new RunReward { title = titles[i], description = descriptions[i], requiresModularAttack = i >= 2, attack = AssetDatabase.LoadAssetAtPath<AttackRecipeModifier>("Assets/Data/CombatExamples/Item_" + attacks[i] + ".asset") };
            AssetDatabase.CreateAsset(definition, path);
        }
        AssetDatabase.SaveAssets();
        definition = AssetDatabase.LoadAssetAtPath<SurvivalRunDefinition>(path);
        Ensure<PlayerHealth>(player.gameObject); Ensure<PlayerExperience>(player.gameObject); Ensure<PlayerItemInventory>(player.gameObject);
        var run = RunUIRepair.Apply(repair);
        var hudSettings = new SerializedObject(run.GetComponent<SurvivalRunHUD>());
        foreach (var canvas in RunUIRepair.Components<Canvas>(scene))
            if (canvas.name == "UI Field" && hudSettings.FindProperty("legacyHUD").objectReferenceValue == null) hudSettings.FindProperty("legacyHUD").objectReferenceValue = canvas;
        hudSettings.ApplyModifiedPropertiesWithoutUndo();
        var serialized = new SerializedObject(run);
        if (serialized.FindProperty("definition").objectReferenceValue == null) serialized.FindProperty("definition").objectReferenceValue = definition;
        if (serialized.FindProperty("player").objectReferenceValue == null) serialized.FindProperty("player").objectReferenceValue = player;
        if (serialized.FindProperty("spawner").objectReferenceValue == null) serialized.FindProperty("spawner").objectReferenceValue = spawner;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[SurvivalSetup] Run definition, progression, health and HUD installed.");
    }
    private static void Ensure<T>(GameObject owner) where T : Component { if (owner.GetComponent<T>() == null) owner.AddComponent<T>(); }
}

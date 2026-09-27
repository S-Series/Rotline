using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SurvivalRunSetup
{
    [MenuItem("ROTLINE/Run/Install Survival Systems")]
    public static void Install()
    {
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
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity");
        definition = AssetDatabase.LoadAssetAtPath<SurvivalRunDefinition>(path);
        var player = Object.FindFirstObjectByType<PlayerCharacter>();
        var spawner = Object.FindFirstObjectByType<SpawnDirector>();
        if (player == null || spawner == null) throw new System.InvalidOperationException("Gameplay player or spawner missing.");
        Ensure<PlayerHealth>(player.gameObject); Ensure<PlayerExperience>(player.gameObject); Ensure<PlayerItemInventory>(player.gameObject);
        var run = Object.FindFirstObjectByType<SurvivalRunController>();
        if (run == null) run = new GameObject("Survival Run").AddComponent<SurvivalRunController>();
        Ensure<SurvivalRunHUD>(run.gameObject);
        var hudSettings = new SerializedObject(run.GetComponent<SurvivalRunHUD>());
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.name == "UI Field") hudSettings.FindProperty("legacyHUD").objectReferenceValue = canvas;
        hudSettings.ApplyModifiedPropertiesWithoutUndo();
        var serialized = new SerializedObject(run);
        serialized.FindProperty("definition").objectReferenceValue = definition;
        serialized.FindProperty("player").objectReferenceValue = player;
        serialized.FindProperty("spawner").objectReferenceValue = spawner;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[SurvivalSetup] Run definition, progression, health and HUD installed.");
    }
    private static void Ensure<T>(GameObject owner) where T : Component { if (owner.GetComponent<T>() == null) owner.AddComponent<T>(); }
}

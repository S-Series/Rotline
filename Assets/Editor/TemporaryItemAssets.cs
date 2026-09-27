using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TemporaryItemAssets
{
    public const string Folder = "Assets/Data/Items";
    [MenuItem("ROTLINE/Items/Create Temporary Stat Items")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Items");
        CreateItem("Item_Experience50", "experience-50", "학습 노트", "획득 경험치 +50%",
            StatKey.ExperienceGainRate, ModifierType.BasePercent, 50f);
        CreateItem("Item_AttackSpeed20", "attack-speed-20", "가벼운 장갑", "공격속도 +20%",
            StatKey.AttackSpeed, ModifierType.BasePercent, 20f);
        CreateItem("Item_AttackPower2", "attack-power-2", "숫돌", "공격력 +2",
            StatKey.AttackPower, ModifierType.BaseFlat, 2f);
        CreateItem("Item_Luck3", "luck-3", "행운의 동전", "행운 +3",
            StatKey.Luck, ModifierType.BaseFlat, 3f);
        CreateItem("Item_ProjectileSpeed01", "projectile-speed-01", "가속 깃털", "탄속 +0.1",
            StatKey.ProjectileSpeedMultiplier, ModifierType.BaseFlat, 0.1f);
        AssetDatabase.SaveAssets();
    }
    private static void CreateItem(string filename, string id, string title, string description,
        StatKey stat, ModifierType type, float amount)
    {
        string path = Folder + "/" + filename + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) != null) return;
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        var serialized = new SerializedObject(item);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("displayName").stringValue = title;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("icon").objectReferenceValue = null;
        var modifiers = serialized.FindProperty("modifiers");
        modifiers.arraySize = 1;
        var entry = modifiers.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("stat").enumValueIndex = (int)stat;
        entry.FindPropertyRelative("type").enumValueIndex = (int)type;
        entry.FindPropertyRelative("amount").floatValue = amount;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(item, path);
    }
    public static void CreateAndValidate()
    {
        Create();
        ItemRegressionChecks.Run();
        // Install the testable inventory on the existing player without granting items by default.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity");
        var player = Object.FindFirstObjectByType<PlayerCharacter>();
        if (player == null) throw new System.InvalidOperationException("Gameplay player is missing.");
        if (player.GetComponent<PlayerItemInventory>() == null)
        {
            player.gameObject.AddComponent<PlayerItemInventory>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}

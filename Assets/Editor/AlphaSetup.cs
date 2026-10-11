using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AlphaSetup
{
    [MenuItem("ROTLINE/Alpha/Install Alpha")]
    public static void Install()
    {
        var scene = RunUIRepair.OpenGameplay();
        var navigation = RunUIRepair.Unique<NavigationService>(scene);
        var arena = RunUIRepair.Unique<AlphaArena>(scene);
        SurvivalRunSetup.Install();
        if (!AssetDatabase.IsValidFolder("Assets/Data/Alpha")) AssetDatabase.CreateFolder("Assets/Data", "Alpha");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var berserk = Character("Berserker", "광전사", false);
        var ranger = Character("Ranger", "유랑 사수", true);
        var chaser = Enemy("Chaser", 12, 2.4f, 3, 0, new Color(0.3f, 0.8f, 0.35f), 0.8f, "MoveChase_Default");
        var hopper = Enemy("Hopper", 20, 1.7f, 5, 0, new Color(0.75f, 0.5f, 0.9f), 1.1f, "MoveHop_Slime");
        var shooter = Enemy("Shooter", 16, 1.6f, 2, 1, new Color(0.9f, 0.65f, 0.2f), 0.8f, "MoveKeepDistance_Default");
        var boss = Enemy("Boss", 500, 0.9f, 12, 2, new Color(0.85f, 0.2f, 0.35f), 2.5f, "MoveChase_Default");
        const string catalogPath = "Assets/Resources/AlphaCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<AlphaCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<AlphaCatalog>();
            catalog.characters = new[] { berserk, ranger };
            catalog.weapons = new[] { AssetDatabase.LoadAssetAtPath<WeaponAttackDefinition>("Assets/Data/Attack_Sword_Default..asset"), AssetDatabase.LoadAssetAtPath<WeaponAttackDefinition>("Assets/Data/CombatExamples/Weapon_Projectile.asset") };
            catalog.enemies = new[] { chaser, hopper, shooter }; catalog.boss = boss;
            catalog.run = AssetDatabase.LoadAssetAtPath<SurvivalRunDefinition>("Assets/Data/Runs/SurvivalRun.asset");
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }
        var run = RunUIRepair.FindRunRoot(scene).GetComponent<SurvivalRunController>();
        if (run.GetComponent<AlphaRunController>() == null) run.gameObject.AddComponent<AlphaRunController>();
        var navData = new SerializedObject(navigation); var arenaData = new SerializedObject(arena);
        if (arenaData.FindProperty("ground").objectReferenceValue == null) arenaData.FindProperty("ground").objectReferenceValue = navData.FindProperty("groundTilemap").objectReferenceValue;
        if (arenaData.FindProperty("obstacles").objectReferenceValue == null) arenaData.FindProperty("obstacles").objectReferenceValue = navData.FindProperty("obstacleTilemap").objectReferenceValue;
        arenaData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(run.gameObject.scene); EditorSceneManager.SaveScene(run.gameObject.scene);
        AssetDatabase.SaveAssets();
        // Preserve the authored menu hierarchy and all Inspector edits.
        // Keep other open scenes intact; installation does not change the user's active scene.
        var scenes = EditorBuildSettings.scenes.ToList();
        EditorBuildSettings.scenes = scenes.ToArray();
        PlayerSettings.productName = "Rotline Alpha";
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        Debug.Log("[AlphaSetup] Menu, characters, enemies, expedition and build scenes installed.");
    }
    private static CharacterDefinition Character(string id, string title, bool ranged)
    {
        string path = "Assets/Data/Alpha/" + id + ".asset";
        var value = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path); if (value != null) return value;
        value = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/Data/Characters/Berserk.asset"));
        var data = new SerializedObject(value); data.FindProperty("characterId").stringValue = id; data.FindProperty("displayName").stringValue = title;
        if (ranged)
        {
            data.FindProperty("abilityDefinition").objectReferenceValue = null;
            data.FindProperty("baseStats.maxHealth").floatValue = 70;
            data.FindProperty("baseStats.maxResource").floatValue = 0;
            data.FindProperty("baseStats.moveSpeed").floatValue = 8;
            data.FindProperty("baseStats.attackPower").floatValue = 8;
        }
        data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(value, path); return value;
    }
    private static EnemyDefinition Enemy(string id, float health, float speed, float damage, int style, Color color, float size, string movement)
    {
        string path = "Assets/Data/Alpha/" + id + ".asset";
        var value = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path); if (value != null) return value;
        value = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Def/EnemySlime.asset"));
        var data = new SerializedObject(value); data.FindProperty("enemyId").stringValue = id;
        data.FindProperty("maxHealth").floatValue = health; data.FindProperty("moveSpeed").floatValue = speed; data.FindProperty("contactDamage").floatValue = damage;
        data.FindProperty("movement").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EnemyMovementDefinition>("Assets/Data/Enemies/Movement/" + movement + ".asset");
        if (style == 1)
        {
            data.FindProperty("rangeBand").enumValueIndex = (int)EnemyRangeBand.Medium;
            data.FindProperty("minimumDistance").floatValue = 4f;
            data.FindProperty("maximumDistance").floatValue = 6f;
            data.FindProperty("meleeAttackRange").floatValue = 0f;
        }
        else if (style == 2)
        {
            data.FindProperty("rangeBand").enumValueIndex = (int)EnemyRangeBand.Long;
            data.FindProperty("minimumDistance").floatValue = 6f;
            data.FindProperty("maximumDistance").floatValue = 8f;
            data.FindProperty("separationRadius").floatValue = 2f;
            data.FindProperty("meleeAttackRange").floatValue = 0f;
        }
        else if (movement == "MoveHop_Slime")
            data.FindProperty("meleeAttackRange").floatValue = 0f;
        data.ApplyModifiedPropertiesWithoutUndo(); value.rangedStyle = style; value.tint = color; value.size = size;
        AssetDatabase.CreateAsset(value, path); return value;
    }
    [MenuItem("ROTLINE/Alpha/Build Windows Alpha")]
    public static void Build()
    {
        System.IO.Directory.CreateDirectory("Builds/RotlineAlpha");
        var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            "Builds/RotlineAlpha/Rotline.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (report.summary.result != BuildResult.Succeeded) throw new System.InvalidOperationException("Alpha build failed: " + report.summary.result);
        Debug.Log("[AlphaBuild] Windows alpha built successfully.");
    }
}

using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Editor-only checks inspect authored data; installation remains an explicit user action.
[InitializeOnLoad]
public static class BatchCheckGuard
{
    private const string ActiveKey = "Rotline.BatchCheckGuard.Active";
    private const string DeadlineKey = "Rotline.BatchCheckGuard.Deadline";
    private const string ProfileKey = "Rotline.BatchCheckGuard.Profile";
    private static string active;
    private static long deadline;
    public static string ProfilePath => SessionState.GetString(ProfileKey, "");

    static BatchCheckGuard()
    {
        active = SessionState.GetString(ActiveKey, "");
        if (string.IsNullOrEmpty(active)) return;
        long.TryParse(SessionState.GetString(DeadlineKey, "0"), out deadline);
        RestoreProfile();
        EditorApplication.update += Watchdog;
    }

    public static void Run(string key, double seconds, Action prepare)
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Checks require a separate batch-mode Editor outside Play Mode.");
        if (!string.IsNullOrEmpty(active))
            throw new InvalidOperationException("Another batch check is active: " + active);
        active = key;
        deadline = DateTime.UtcNow.AddSeconds(seconds).Ticks;
        SessionState.SetString(ActiveKey, key);
        SessionState.SetString(DeadlineKey, deadline.ToString(CultureInfo.InvariantCulture));
        SessionState.SetBool(key, true);
        EditorApplication.update += Watchdog;
        try
        {
            string profile = Path.GetFullPath("Logs/BatchProfiles/" + Guid.NewGuid().ToString("N") + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(profile));
            SessionState.SetString(ProfileKey, profile);
            RestoreProfile();
            Debug.Log($"[BatchCheck] {key}; Unity {Application.unityVersion}; project {Path.GetFullPath(".")}; profile {profile}; timeout {seconds}s.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Play Mode checks require a graphics device; omit -nographics (scene rendering and captures are exercised).");
            prepare();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(key, 1);
        }
    }

    public static void RestoreProfile()
    {
        string path = SessionState.GetString(ProfileKey, "");
        if (!string.IsNullOrEmpty(path)) AlphaSession.UseProfileForChecks(path);
    }

    private static void Watchdog()
    {
        // Persist an absolute deadline across domain reloads, including failed Play entry.
        if (DateTime.UtcNow.Ticks < deadline) return;
        Debug.LogError($"[BatchCheck] {active} timed out before completion; playing={EditorApplication.isPlaying}.");
        Finish(active, 1);
    }

    public static void Finish(string key, int code)
    {
        SessionState.SetBool(key, false);
        SessionState.EraseString(ActiveKey);
        SessionState.EraseString(DeadlineKey);
        SessionState.EraseString(ProfileKey);
        EditorApplication.update -= Watchdog;
        active = null;
        Time.timeScale = 1f;
        Debug.Log($"[BatchCheck] {key} finished with exit code {code}.");
        EditorApplication.Exit(code);
    }

    public static void Gameplay()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity");
        RequireScene<AppRoot>();
        Require(Camera.main != null, "Gameplay needs an authored MainCamera.");
        References(RequireScene<NavigationService>(), "groundTilemap", "obstacleTilemap", "target");
        References(RequireScene<SpawnDirector>(), "player", "enemyDefinition", "corruptionField");
        References(RequireScene<CorruptionField>(), "groundTilemap", "obstacleTilemap", "corruptionTilemap", "corruptionTile");
        var player = RequireScene<PlayerCharacter>();
        References(player, "definition");
        foreach (Type type in new[] { typeof(PlayerStats), typeof(PlayerHealth), typeof(PlayerExperience),
            typeof(PlayerItemInventory), typeof(PlayerActionController), typeof(PlayerModeController), typeof(PlayerCombatController) })
            Require(player.GetComponent(type) != null, $"Gameplay/{player.name} needs saved {type.Name}.");
        References(player.GetComponent<PlayerCombatController>(), "equippedWeapon");
        var run = RequireScene<SurvivalRunController>();
        References(run, "definition", "player", "spawner");
        var definition = Asset<SurvivalRunDefinition>("Assets/Data/Runs/SurvivalRun.asset");
        Require(run.Definition == definition, "Gameplay run must reference Assets/Data/Runs/SurvivalRun.asset.");
        Require(definition.levelRewards != null && definition.levelRewards.Length >= 3, "Run needs at least three saved level rewards.");
        foreach (var item in definition.levelRewards) Require(item != null, "Run has a missing level reward asset.");
        Require(definition.treasures != null && definition.treasures.Length > 0, "Run needs saved treasure rewards.");
        foreach (var reward in definition.treasures)
            Require(reward != null && (reward.item != null || reward.attack != null), "Run has a treasure without an item or attack asset.");
        var hud = run.GetComponent<SurvivalRunHUD>();
        Require(hud != null && run.transform.Find("Run HUD") != null && run.transform.Find("Alpha System Pages") != null,
            "Gameplay/Survival Run needs its saved HUD and system-page hierarchy.");
        References(hud, "canvas", "statusPanel", "status", "inventory", "objective", "hints", "healthFill", "experienceFill",
            "resourceFill", "overlay", "heading", "details", "options", "resume", "restart", "mainMenu", "equipment", "settings");
        var alpha = run.GetComponent<AlphaRunController>();
        References(alpha, "ui");
        References(RequireScene<AlphaSystemView>(), "canvas", "shopPage", "settingsPage", "inventoryPage", "heading", "coins",
            "shopOffers", "volumeButton", "volumeLabel", "statsLabel", "itemRows", "attackRows", "backButton");
        References(RequireScene<AlphaArena>(), "ground", "obstacles");
        References(RequireScene<PlayerPurificationController>(), "corruptionField", "gameplayCamera", "run");
        References(RequireScene<PurificationView>(), "player", "run", "hud", "status", "rangeRing", "targetRing", "beam");
        RequireScene<AlphaCorruptionHazard>();
        Enemy(Asset<EnemyDefinition>("Assets/Data/Enemies/Def/EnemySlime.asset"));
        foreach (string name in new[] { "Experience50", "AttackSpeed20", "AttackPower2", "Luck3", "ProjectileSpeed01" })
            Asset<ItemDefinition>("Assets/Data/Items/Item_" + name + ".asset");
        foreach (string name in new[] { "ChargedBeam", "ChargedRing", "Multishot", "Orbit" })
            Asset<AttackRecipeModifier>("Assets/Data/CombatExamples/Item_" + name + ".asset");
        foreach (string name in new[] { "Projectile", "ChargedBeam", "ChargedRing" })
            Asset<WeaponAttackDefinition>("Assets/Data/CombatExamples/Weapon_" + name + ".asset");
    }

    public static void Menu()
    {
        var catalog = Asset<AlphaCatalog>("Assets/Resources/AlphaCatalog.asset");
        Require(catalog.characters != null && catalog.characters.Length >= 2, "Catalog needs two saved characters.");
        foreach (var character in catalog.characters) Require(character != null, "Catalog has a missing character.");
        Require(catalog.weapons != null && catalog.weapons.Length >= 2, "Catalog needs two saved weapons.");
        foreach (var weapon in catalog.weapons) Require(weapon != null, "Catalog has a missing weapon.");
        Require(catalog.enemies != null && catalog.enemies.Length > 0, "Catalog needs saved enemies.");
        foreach (var enemy in catalog.enemies) Enemy(enemy);
        Enemy(catalog.boss);
        Require(catalog.run != null, "Catalog needs its saved run definition.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        References(RequireScene<AlphaMainMenu>(), "mainPage", "selectPage", "upgradesPage", "collectionPage", "settingsPage",
            "profileLabel", "errorLabel", "characterLabels", "modeLabel", "upgradeButtons", "upgradeLabels", "itemRows",
            "attackRows", "volumeLabel", "fullscreenLabel");
    }

    private static void Enemy(EnemyDefinition enemy)
    {
        Require(enemy != null, "Missing saved enemy definition.");
        References(enemy, "prefab", "movement");
    }

    private static T Asset<T>(string path) where T : Object
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        Require(value != null, $"Missing required {typeof(T).Name}: {path}. Checks do not create assets.");
        return value;
    }

    private static T RequireScene<T>() where T : Component
    {
        var value = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        Require(value != null, $"Missing saved {typeof(T).Name} in {SceneManager.GetActiveScene().path}.");
        return value;
    }

    private static void References(Object owner, params string[] fields)
    {
        Require(owner != null, "Missing saved component for required references.");
        var data = new SerializedObject(owner);
        foreach (string field in fields)
        {
            var property = data.FindProperty(field);
            Require(property != null, $"{owner.name}/{owner.GetType().Name}: unknown required field {field}.");
            if (property.isArray)
            {
                Require(property.arraySize > 0, $"{owner.name}/{owner.GetType().Name}.{field} needs saved entries.");
                for (int i = 0; i < property.arraySize; i++)
                    Require(property.GetArrayElementAtIndex(i).objectReferenceValue != null,
                        $"{owner.name}/{owner.GetType().Name}.{field}[{i}] has a missing saved reference.");
            }
            else Require(property.objectReferenceValue != null, $"{owner.name}/{owner.GetType().Name}.{field} has a missing saved reference.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

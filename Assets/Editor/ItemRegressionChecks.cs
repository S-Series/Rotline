using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ItemRegressionChecks
{
    private static readonly string[] Names = { "Item_Experience50", "Item_AttackSpeed20", "Item_AttackPower2", "Item_Luck3", "Item_ProjectileSpeed01" };
    [MenuItem("ROTLINE/Tests/Temporary Items")]
    public static void Run()
    {
        var owner = new GameObject("ItemRegressionOwner");
        var scopes = new List<ModifierScope>();
        try
        {
            var stats = owner.AddComponent<PlayerStats>();
            stats.Initialize(JsonUtility.FromJson<CharacterStats>("{\"experienceGainRate\":1,\"attackSpeed\":2,\"attackPower\":10,\"luck\":0,\"projectileSpeedMultiplier\":1}"));
            foreach (var name in Names)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(TemporaryItemAssets.Folder + "/" + name + ".asset");
                Require(item != null && item.Icon == null, "Item must exist with empty icon: " + name);
                scopes.Add(item.Apply(stats));
            }
            Check(stats.ExperienceGainRate, 1.5f); Check(stats.AttackSpeed, 2.4f);
            Check(stats.AttackPower, 12f); Check(stats.Luck, 3f); Check(stats.ProjectileSpeedMultiplier, 1.1f);
            var speed = AssetDatabase.LoadAssetAtPath<ItemDefinition>(TemporaryItemAssets.Folder + "/Item_AttackSpeed20.asset");
            using (var duplicate = speed.Apply(stats)) Check(stats.AttackSpeed, 2.8f);
            Check(stats.AttackSpeed, 2.4f);
            foreach (var scope in scopes) scope.Dispose();
            Check(stats.ExperienceGainRate, 1f); Check(stats.AttackSpeed, 2f);
            Check(stats.AttackPower, 10f); Check(stats.Luck, 0f); Check(stats.ProjectileSpeedMultiplier, 1f);
            Debug.Log("[Items] Five assets, empty icons, modifiers, stacking and removal passed.");
        }
        finally
        {
            foreach (var scope in scopes) scope.Dispose();
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }
    public static void CheckInventory(PlayerItemInventory inventory, PlayerStats stats)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(TemporaryItemAssets.Folder + "/Item_AttackPower2.asset");
        float before = stats.AttackPower;
        Require(inventory != null && inventory.AddItem(item), "Player must acquire the item.");
        Check(stats.AttackPower, before + 2f);
        inventory.enabled = false; Check(stats.AttackPower, before);
        inventory.enabled = true; Check(stats.AttackPower, before + 2f);
        Require(inventory.Count(item) == 1 && inventory.RemoveItem(item), "One acquisition must survive re-enable and be removable.");
        Check(stats.AttackPower, before);
        Debug.Log("[ItemsPlayMode] Acquisition, disable/re-enable and removal passed.");
    }
    private static void Check(float actual, float expected) => Require(Mathf.Abs(actual - expected) < 0.0001f, $"Expected {expected}, got {actual}.");
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

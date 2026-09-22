using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CombatExampleAssets
{
    private const string Folder = "Assets/Data/CombatExamples";

    [MenuItem("ROTLINE/Combat/Create Example Assets")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "CombatExamples");
        var beam = Asset<BeamShapeDefinition>("Shape_Beam");
        var wrapped = Asset<BeamShapeDefinition>("Shape_WrappedBeam", value => Set(value, "wrapAroundOwner", true));
        var ring = Asset<RingShapeDefinition>("Shape_Ring");
        var disk = Asset<ProjectileShapeDefinition>("Shape_Projectile");
        var line = Asset<LinearTrajectoryDefinition>("Path_Linear");
        var orbit = Asset<OrbitTrajectoryDefinition>("Path_OrbitThenLinear");
        var attached = Asset<AttachedTrajectoryDefinition>("Path_Attached");

        Weapon("Weapon_Projectile", new AttackRecipe { shape = disk, trajectory = line,
            lifetime = 2f, speed = 8f, unlimitedTargets = false });
        Weapon("Weapon_ChargedBeam", new AttackRecipe { shape = beam, trajectory = attached,
            trigger = AttackTrigger.ChargeRelease, lifetime = 0.5f, repeatHitSeconds = 0.1f });
        Weapon("Weapon_ChargedRing", new AttackRecipe { shape = ring, trajectory = line,
            trigger = AttackTrigger.ChargeRelease, lifetime = 2f, radius = 2f,
            radiusScalesWithCharge = true, repeatHitSeconds = 0.1f, speed = 4f });
        Weapon("Weapon_WrappedBeam", new AttackRecipe { shape = wrapped, trajectory = attached,
            trigger = AttackTrigger.ChargeRelease, lifetime = 0.5f, repeatHitSeconds = 0.1f });

        Asset<AttackRecipeModifier>("Item_ChargedBeam", value =>
        {
            Set(value, "id", "charged-beam"); Set(value, "priority", 100);
            Set(value, "replaceTrigger", true); Set(value, "trigger", AttackTrigger.ChargeRelease);
            Set(value, "shape", beam); Set(value, "trajectory", attached); Set(value, "replaceTiming", true);
        });
        Asset<AttackRecipeModifier>("Item_ChargedRing", value =>
        {
            Set(value, "id", "charged-ring"); Set(value, "priority", 200);
            Set(value, "replaceTrigger", true); Set(value, "trigger", AttackTrigger.ChargeRelease);
            Set(value, "shape", ring); Set(value, "trajectory", line);
            Set(value, "scaleRadiusWithCharge", true); Set(value, "radiusMultiplier", 10f);
            Set(value, "replaceTiming", true); Set(value, "lifetime", 2f);
        });
        Asset<AttackRecipeModifier>("Item_Orbit", value =>
        {
            Set(value, "id", "orbit"); Set(value, "priority", 300); Set(value, "trajectory", orbit);
        });
        Asset<AttackRecipeModifier>("Item_Multishot", value =>
        {
            Set(value, "id", "multishot"); Set(value, "priority", 400);
            Set(value, "maximumStacks", 3); Set(value, "extraProjectiles", 2); Set(value, "extraSpread", 20f);
        });
        AssetDatabase.SaveAssets();
        Debug.Log("[Combat] Example assets ready in " + Folder);
    }

    private static void Weapon(string name, AttackRecipe recipe)
    {
        recipe.Validate();
        Asset<WeaponAttackDefinition>(name, value => Set(value, "recipe", recipe));
    }

    private static T Asset<T>(string name, System.Action<T> initialize = null) where T : ScriptableObject
    {
        string path = Folder + "/" + name + ".asset";
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        T value = ScriptableObject.CreateInstance<T>();
        value.name = name;
        initialize?.Invoke(value);
        AssetDatabase.CreateAsset(value, path);
        return value;
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}

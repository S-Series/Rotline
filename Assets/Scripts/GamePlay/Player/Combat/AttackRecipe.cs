using System;
using System.Collections.Generic;
using UnityEngine;

public enum AttackTrigger { Automatic, Press, ChargeRelease }

// Serialized configuration only. Mutable combat state belongs to each runtime.
[Serializable]
public sealed class AttackRecipe
{
    public AttackTrigger trigger;
    public AttackShapeDefinition shape;
    public AttackTrajectoryDefinition trajectory;
    [Min(0.01f)] public float chargeSeconds = 1f;
    [Range(0f, 1f)] public float minimumCharge = 0.1f;
    [Min(0.01f)] public float lifetime = 0.5f;
    [Min(0f)] public float speed = 8f;
    [Min(0.01f)] public float radius = 0.2f;
    [Min(0.01f)] public float thickness = 0.1f;
    [Min(0f)] public float repeatHitSeconds;
    [Min(0)] public int pierceCount;
    public bool unlimitedTargets = true;
    public bool radiusScalesWithCharge;
    [Range(1, 32)] public int projectileCount = 1;
    [Range(0f, 360f)] public float spreadDegrees;
    [Min(0.01f)] public float damageMultiplier = 1f;

    public AttackRecipe Copy() => (AttackRecipe)MemberwiseClone();

    public void Validate()
    {
        Positive(chargeSeconds, nameof(chargeSeconds));
        Positive(lifetime, nameof(lifetime));
        Positive(radius, nameof(radius));
        Positive(thickness, nameof(thickness));
        Positive(damageMultiplier, nameof(damageMultiplier));
        NonNegative(speed, nameof(speed));
        NonNegative(repeatHitSeconds, nameof(repeatHitSeconds));
        NonNegative(minimumCharge, nameof(minimumCharge));
        NonNegative(spreadDegrees, nameof(spreadDegrees));
        if (minimumCharge > 1f || spreadDegrees > 360f || projectileCount < 1 ||
            projectileCount > 32 || pierceCount < 0 || !Enum.IsDefined(typeof(AttackTrigger), trigger))
            throw new ArgumentException("Invalid attack recipe.");
        if (shape == null && trigger == AttackTrigger.ChargeRelease)
            throw new ArgumentException("ChargeRelease requires an attack shape.");
    }

    private static void Positive(float value, string name)
    {
        if (value <= 0f) throw new ArgumentOutOfRangeException(name);
        NonNegative(value, name);
    }

    private static void NonNegative(float value, string name)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            throw new ArgumentOutOfRangeException(name);
    }
}

public static class AttackRecipeResolver
{
    public static AttackRecipe Resolve(AttackRecipe basis,
        IReadOnlyDictionary<AttackRecipeModifier, int> inventory)
    {
        var recipe = (basis ?? new AttackRecipe()).Copy();
        if (inventory != null)
        {
            var entries = new List<AttackRecipeModifier>(inventory.Keys);
            entries.Sort((a, b) =>
            {
                int priority = a.Priority.CompareTo(b.Priority);
                return priority != 0 ? priority : StringComparer.Ordinal.Compare(a.Id, b.Id);
            });
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in entries)
            {
                if (string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id))
                    throw new ArgumentException("Attack items require unique, non-empty IDs.");
                item.Apply(recipe, inventory[item]);
            }
        }
        recipe.Validate();
        return recipe;
    }
}

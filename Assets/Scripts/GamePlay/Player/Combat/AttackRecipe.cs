using System;
using System.Collections.Generic;
using UnityEngine;

public enum AttackTrigger { Automatic, Press, ChargeRelease, Channel }

// Serialized configuration only. Mutable combat state belongs to each runtime.
[Serializable]
public sealed class AttackRecipe
{
    public AttackTrigger trigger;
    public AttackTriggerDefinition triggerModule;
    public AttackEmissionDefinition emission;
    public AttackHitPolicyDefinition hitPolicy;
    public List<CombatEffectDefinition> effects = new();
    [Range(0, 16)] public int maxChainDepth = 4;
    [Range(0, 256)] public int maxChildAttacks = 64;
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

    public AttackRecipe Copy()
    {
        var copy = (AttackRecipe)MemberwiseClone();
        copy.effects = effects != null ? new List<CombatEffectDefinition>(effects) : new List<CombatEffectDefinition>();
        return copy;
    }

    public AttackTriggerRuntime CreateTrigger(Func<AttackActivation, bool> fire, Action stop) =>
        (triggerModule != null ? triggerModule.CreateRuntime(Copy(), fire, stop)
            : new StandardAttackTriggerRuntime(trigger, this, fire, stop))
        ?? throw new InvalidOperationException("Trigger module returned no runtime.");

    public AttackHitPolicyRuntime CreateHitPolicy() =>
        (hitPolicy != null ? hitPolicy.CreateRuntime(Copy()) : new IntervalHitPolicyRuntime(repeatHitSeconds))
        ?? throw new InvalidOperationException("Hit policy returned no runtime.");

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
        if (maxChainDepth < 0 || maxChainDepth > 16 || maxChildAttacks < 0 || maxChildAttacks > 256)
            throw new ArgumentException("Invalid proc chain limits.");
        if (shape == null && (emission != null || (triggerModule == null && trigger == AttackTrigger.Channel)))
            throw new ArgumentException("Scheduled/channel melee requires a shape and a swing trajectory; the legacy collider adapter does not support it.");
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
        IReadOnlyDictionary<AttackRecipeModifier, int> inventory,
        IReadOnlyList<AttackCompositionRule> rules = null)
    {
        var recipe = (basis ?? new AttackRecipe()).Copy();
        var sources = new Dictionary<string, int>(StringComparer.Ordinal);
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
                sources[item.Id] = inventory[item];
            }
        }
        if (rules != null)
        {
            var ordered = new List<AttackCompositionRule>(rules);
            ordered.RemoveAll(rule => rule == null);
            ordered.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority)
                : StringComparer.Ordinal.Compare(a.Id, b.Id));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var readOnly = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(sources);
            foreach (var rule in ordered)
            {
                if (string.IsNullOrWhiteSpace(rule.Id) || !ids.Add(rule.Id))
                    throw new ArgumentException("Composition rules require unique IDs.");
                rule.Apply(recipe, readOnly);
            }
        }
        recipe.Validate();
        return recipe;
    }
}

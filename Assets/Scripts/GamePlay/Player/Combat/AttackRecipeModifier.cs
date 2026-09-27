using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Attack Recipe Modifier")]
public class AttackRecipeModifier : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private int priority;
    [SerializeField, Min(1)] private int maximumStacks = 1;
    [SerializeField] private bool replaceTrigger;
    [SerializeField] private AttackTrigger trigger;
    [SerializeField] private AttackShapeDefinition shape;
    [SerializeField] private AttackTrajectoryDefinition trajectory;
    [SerializeField] private bool scaleRadiusWithCharge;
    [SerializeField, Range(0, 31)] private int extraProjectiles;
    [SerializeField, Range(0f, 360f)] private float extraSpread;
    [SerializeField, Min(0.01f)] private float damageMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float radiusMultiplier = 1f;
    [SerializeField] private bool replaceTiming;
    [SerializeField, Min(0.01f)] private float chargeSeconds = 1f;
    [SerializeField, Min(0.01f)] private float lifetime = 0.5f;
    [SerializeField, Min(0f)] private float repeatHitSeconds = 0.1f;
    [SerializeField] private AttackTriggerDefinition triggerModule;
    [SerializeField] private AttackEmissionDefinition emission;
    [SerializeField] private AttackHitPolicyDefinition hitPolicy;
    [SerializeField] private CombatEffectDefinition[] effects;

    public string Id => id;
    public int Priority => priority;
    public int MaximumStacks => Mathf.Max(1, maximumStacks);

    public virtual void Apply(AttackRecipe recipe, int stacks)
    {
        stacks = Mathf.Clamp(stacks, 0, MaximumStacks);
        if (stacks == 0) return;
        if (replaceTrigger) { recipe.trigger = trigger; recipe.triggerModule = null; }
        if (triggerModule != null) recipe.triggerModule = triggerModule;
        if (emission != null) recipe.emission = emission;
        if (hitPolicy != null) recipe.hitPolicy = hitPolicy;
        if (effects != null) foreach (var effect in effects)
            if (effect != null && !recipe.effects.Contains(effect)) recipe.effects.Add(effect);
        if (shape != null) recipe.shape = shape;
        if (trajectory != null) recipe.trajectory = trajectory;
        if (scaleRadiusWithCharge) recipe.radiusScalesWithCharge = true;
        recipe.projectileCount = Mathf.Clamp(recipe.projectileCount + extraProjectiles * stacks, 1, 32);
        recipe.spreadDegrees = Mathf.Clamp(recipe.spreadDegrees + extraSpread * stacks, 0f, 360f);
        recipe.damageMultiplier *= Mathf.Pow(damageMultiplier, stacks);
        recipe.radius *= Mathf.Pow(radiusMultiplier, stacks);
        if (replaceTiming)
        {
            recipe.chargeSeconds = chargeSeconds;
            recipe.lifetime = lifetime;
            recipe.repeatHitSeconds = repeatHitSeconds;
        }
    }
}

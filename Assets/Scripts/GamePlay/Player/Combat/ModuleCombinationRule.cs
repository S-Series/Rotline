using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Composition/Required Sources Override")]
public sealed class ModuleCombinationRule : AttackCompositionRule
{
    [SerializeField] private string[] requiredSourceIds;
    [SerializeField] private AttackShapeDefinition shape;
    [SerializeField] private AttackTrajectoryDefinition trajectory;
    [SerializeField] private AttackTriggerDefinition trigger;
    [SerializeField] private AttackEmissionDefinition emission;
    [SerializeField] private AttackHitPolicyDefinition hitPolicy;
    public override void Apply(AttackRecipe recipe, IReadOnlyDictionary<string, int> sources)
    {
        if (requiredSourceIds == null || requiredSourceIds.Length == 0) return;
        foreach (var id in requiredSourceIds)
            if (string.IsNullOrEmpty(id) || !sources.TryGetValue(id, out int count) || count <= 0) return;
        if (shape != null) recipe.shape = shape;
        if (trajectory != null) recipe.trajectory = trajectory;
        if (trigger != null) recipe.triggerModule = trigger;
        if (emission != null) recipe.emission = emission;
        if (hitPolicy != null) recipe.hitPolicy = hitPolicy;
    }
}

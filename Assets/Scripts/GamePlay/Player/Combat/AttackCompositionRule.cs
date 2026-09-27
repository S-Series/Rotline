using System.Collections.Generic;
using UnityEngine;

public abstract class AttackCompositionRule : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private int priority;
    public string Id => id;
    public int Priority => priority;
    // Runs once, after all ordinary modifiers, in deterministic priority/ID order.
    public abstract void Apply(AttackRecipe recipe, IReadOnlyDictionary<string, int> sources);
}

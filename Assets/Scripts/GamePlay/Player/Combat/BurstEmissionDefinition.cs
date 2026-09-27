using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Modules/Burst Emission")]
public sealed class BurstEmissionDefinition : AttackEmissionDefinition
{
    [SerializeField, Range(1, 16)] private int waves = 3;
    [SerializeField, Min(0f)] private float interval = 0.1f;
    public override void Build(AttackRecipe recipe, AttackSnapshot attack, List<AttackEmission> output)
    {
        for (int i = 0; i < Mathf.Clamp(waves, 1, 16); i++)
            Fan(recipe, output, i * Mathf.Max(0f, interval));
    }
}

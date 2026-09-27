using System.Collections.Generic;
using UnityEngine;

public readonly struct AttackEmission
{
    public readonly float Delay;
    public readonly float Angle;
    public readonly Vector2 Offset;
    public AttackEmission(float delay, float angle, Vector2 offset = default)
    { Delay = delay; Angle = angle; Offset = offset; }
}

public abstract class AttackEmissionDefinition : ScriptableObject
{
    // Pure schedule generation. Delays are relative to activation, not frame count.
    public abstract void Build(AttackRecipe recipe, AttackSnapshot attack, List<AttackEmission> output);
    public static void Fan(AttackRecipe recipe, List<AttackEmission> output, float delay = 0f)
    {
        for (int i = 0; i < recipe.projectileCount; i++)
        {
            float denominator = recipe.spreadDegrees >= 360f ? recipe.projectileCount : Mathf.Max(1, recipe.projectileCount - 1);
            float angle = recipe.projectileCount == 1 ? 0f : -recipe.spreadDegrees * 0.5f + recipe.spreadDegrees * i / denominator;
            output.Add(new AttackEmission(delay, angle));
        }
    }
}

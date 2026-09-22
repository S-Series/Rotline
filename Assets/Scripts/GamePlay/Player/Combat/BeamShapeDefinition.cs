using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Shapes/Beam")]
public sealed class BeamShapeDefinition : AttackShapeDefinition
{
    [SerializeField] private bool wrapAroundOwner;
    [SerializeField, Min(0.1f)] private float wrapRadius = 1f;

    public override void BuildPoints(List<Vector2> points, float range, float radius)
    {
        points.Clear();
        if (wrapAroundOwner)
        {
            // A continuous loop and outgoing line, not a projectile orbit.
            for (int i = 0; i <= 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64f;
                points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * wrapRadius);
            }
            points.Add(new Vector2(wrapRadius + range, 0f));
        }
        else
        {
            points.Add(Vector2.zero);
            points.Add(new Vector2(range, 0f));
        }
    }
}

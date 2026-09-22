using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Shapes/Ring")]
public sealed class RingShapeDefinition : AttackShapeDefinition
{
    public override void BuildPoints(List<Vector2> points, float range, float radius)
    {
        points.Clear();
        for (int i = 0; i <= 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64f;
            points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}

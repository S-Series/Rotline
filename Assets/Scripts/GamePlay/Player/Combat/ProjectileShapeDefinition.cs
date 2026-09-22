using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Shapes/Projectile")]
public sealed class ProjectileShapeDefinition : AttackShapeDefinition
{
    public override void BuildPoints(List<Vector2> points, float range, float radius)
    {
        points.Clear();
        points.Add(Vector2.zero);
    }
}

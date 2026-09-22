using System.Collections.Generic;
using UnityEngine;

// The same polyline drives both rendering and collision. A single point is a disk.
public abstract class AttackShapeDefinition : ScriptableObject
{
    public abstract void BuildPoints(List<Vector2> points, float range, float radius);
}

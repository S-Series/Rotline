using UnityEngine;

public sealed class EnemyTargeting
{
    public Transform Target { get; }
    public float PreferredDistance { get; }
    public float DistanceTolerance { get; }
    public float MinimumDistance { get; }
    public float MaximumDistance { get; }

    public EnemyTargeting(Transform target, float minimumDistance, float maximumDistance)
    {
        Target = target;
        MinimumDistance = Mathf.Max(0f, minimumDistance);
        MaximumDistance = Mathf.Max(MinimumDistance, maximumDistance);
        PreferredDistance = (MinimumDistance + MaximumDistance) * 0.5f;
        DistanceTolerance = (MaximumDistance - MinimumDistance) * 0.5f;
    }

    public Vector2 Approach(Vector2 position)
    {
        if (Target == null) return Vector2.zero;
        Vector2 toTarget = (Vector2)Target.position - position;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
            return MinimumDistance > 0f ? Vector2.right : Vector2.zero;
        if (distance > MaximumDistance)
            return toTarget / distance * Mathf.Clamp01((distance - MaximumDistance) / 0.5f);
        if (distance < MinimumDistance)
            return -toTarget / distance * Mathf.Clamp01((MinimumDistance - distance) / 0.5f);
        return Vector2.zero;
    }

    public bool IsApproaching(Vector2 position)
    {
        return Target != null &&
            ((Vector2)Target.position - position).sqrMagnitude > MaximumDistance * MaximumDistance;
    }
}

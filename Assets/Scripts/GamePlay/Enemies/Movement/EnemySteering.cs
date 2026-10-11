using UnityEngine;

public sealed class EnemySteering
{
    private const int MaximumNeighbours = 24;
    private readonly Transform self;
    private readonly Collider2D selfCollider;
    private readonly NavigationService navigation;
    private readonly Collider2D[] neighbours = new Collider2D[MaximumNeighbours];
    private readonly ContactFilter2D enemyFilter;
    private readonly float separationRadius;
    private readonly float separationWeight;

    public EnemySteering(Transform self, Collider2D selfCollider, NavigationService navigation,
        float separationRadius, float separationWeight)
    {
        this.self = self;
        this.selfCollider = selfCollider;
        this.navigation = navigation;
        this.separationRadius = Mathf.Max(0f, separationRadius);
        this.separationWeight = Mathf.Clamp01(separationWeight);
        enemyFilter = new ContactFilter2D { useTriggers = false };
        enemyFilter.SetLayerMask(1 << LayerMask.NameToLayer("Enemy"));
    }

    public Vector2 Direction(Vector2 position, Vector2 approach, bool towardTarget)
    {
        Vector2 separation = Separation(position);
        Vector2 desired = approach + separation * separationWeight;
        if (desired.sqrMagnitude <= 0.0001f) return Vector2.zero;

        // Separation alone should make a small adjustment while an enemy is attacking.
        float speedFraction = approach.sqrMagnitude <= 0.0001f
            ? Mathf.Min(0.35f, desired.magnitude)
            : Mathf.Min(1f, desired.magnitude);
        Vector2 direction = desired.normalized;
        if (navigation != null && !navigation.IsWalkable(position + direction * 0.55f))
        {
            // The flow field routes an approaching enemy around solid tiles.
            if (towardTarget)
            {
                Vector2 flow = navigation.GetDirection(position);
                if (flow.sqrMagnitude > 0.0001f &&
                    navigation.IsWalkable(position + flow.normalized * 0.55f))
                    return flow.normalized * speedFraction;
            }
            Vector2 left = new Vector2(-direction.y, direction.x);
            Vector2 right = -left;
            if (navigation.IsWalkable(position + left * 0.55f)) return left * speedFraction;
            if (navigation.IsWalkable(position + right * 0.55f)) return right * speedFraction;
            return Vector2.zero;
        }
        return direction * speedFraction;
    }

    private Vector2 Separation(Vector2 position)
    {
        if (selfCollider == null || separationRadius <= 0f || separationWeight <= 0f)
            return Vector2.zero;
        int count = Physics2D.OverlapCircle(position, separationRadius, enemyFilter, neighbours);
        Vector2 result = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Collider2D other = neighbours[i];
            if (other == null || other == selfCollider ||
                (selfCollider.attachedRigidbody != null &&
                 other.attachedRigidbody == selfCollider.attachedRigidbody)) continue;
            Vector2 otherPosition = other.attachedRigidbody != null
                ? other.attachedRigidbody.position : (Vector2)other.transform.position;
            Vector2 away = position - otherPosition;
            float distance = away.magnitude;
            if (distance <= 0.0001f)
            {
                int sign = self.GetInstanceID() < other.GetInstanceID() ? -1 : 1;
                away = new Vector2(sign, 0f);
                distance = 0f;
            }
            else away /= distance;
            result += away * (1f - distance / separationRadius);
        }
        return Vector2.ClampMagnitude(result, 1f);
    }
}

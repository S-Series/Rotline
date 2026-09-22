using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Trajectories/Orbit Then Linear")]
public sealed class OrbitTrajectoryDefinition : AttackTrajectoryDefinition
{
    [SerializeField, Min(0.01f)] private float orbitSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float orbitRadius = 1f;
    public override float MaximumSpeed(float speed) =>
        Mathf.Max(speed, Mathf.PI * 2f * orbitRadius / Mathf.Max(0.01f, orbitSeconds));

    public override Vector2 Evaluate(float age, float speed)
    {
        float duration = Mathf.Max(0.01f, orbitSeconds);
        if (age >= duration)
            return new Vector2(orbitRadius + (age - duration) * speed, 0f);
        float angle = age / duration * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
    }
}

using UnityEngine;

// A beam/line shape + this trajectory is a procedural melee sweep in the common pipeline.
[CreateAssetMenu(menuName = "Game/Combat/Trajectories/Melee Swing")]
public sealed class SwingTrajectoryDefinition : AttackTrajectoryDefinition
{
    [SerializeField, Range(0f, 360f)] private float angle = 100f;
    [SerializeField, Min(0.01f)] private float duration = 0.2f;
    [SerializeField, Min(0.01f)] private float maximumRadius = 3f;
    public override AttackTrajectoryRuntime CreateRuntime(Transform owner, LayerMask layers,
        Vector2 origin, Vector2 direction, float speed) => new Runtime(owner, origin, direction,
            angle, Mathf.Max(0.01f, duration), Mathf.Max(0.01f, maximumRadius));
    private sealed class Runtime : AttackTrajectoryRuntime
    {
        private readonly Transform owner;
        private readonly Vector2 origin;
        private readonly float yaw;
        private readonly float angle;
        private readonly float duration;
        private readonly float radius;
        public Runtime(Transform owner, Vector2 origin, Vector2 direction, float angle, float duration, float radius)
        {
            this.owner = owner; this.origin = origin; this.angle = angle; this.duration = duration; this.radius = radius;
            yaw = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }
        public override float MaximumSpeed => angle * Mathf.Deg2Rad * radius / duration;
        public override AttackPose Evaluate(float age) => new AttackPose(owner != null ? (Vector2)owner.position : origin,
            Quaternion.Euler(0, 0, yaw + Mathf.Lerp(-angle * 0.5f, angle * 0.5f, Mathf.Clamp01(age / duration))));
    }
}

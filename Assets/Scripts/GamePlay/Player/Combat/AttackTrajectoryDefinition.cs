using UnityEngine;

public abstract class AttackTrajectoryDefinition : ScriptableObject
{
    // Local coordinates: +X is the captured aim direction.
    public virtual Vector2 Evaluate(float age, float speed) => Vector2.zero;
    public virtual bool FollowOwner => false;
    public virtual float MaximumSpeed(float speed) => speed;
    public virtual AttackTrajectoryRuntime CreateRuntime(Transform owner, LayerMask layers,
        Vector2 origin, Vector2 direction, float speed) => new LegacyRuntime(this, owner, origin, direction, speed);

    private sealed class LegacyRuntime : AttackTrajectoryRuntime
    {
        private readonly AttackTrajectoryDefinition definition;
        private readonly Transform owner;
        private readonly Vector2 origin;
        private readonly float speed;
        private Quaternion rotation;
        public LegacyRuntime(AttackTrajectoryDefinition definition, Transform owner, Vector2 origin, Vector2 direction, float speed)
        {
            this.definition = definition; this.owner = owner; this.origin = origin; this.speed = speed;
            rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
        public override float MaximumSpeed => definition.MaximumSpeed(speed);
        public override void SetAim(Vector2 direction) =>
            rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        public override AttackPose Evaluate(float age)
        {
            Vector2 anchor = definition.FollowOwner && owner != null ? (Vector2)owner.position : origin;
            return new AttackPose(anchor + (Vector2)(rotation * definition.Evaluate(age, speed)), rotation);
        }
    }
}

public readonly struct AttackPose
{
    public readonly Vector2 Position;
    public readonly Quaternion Rotation;
    public AttackPose(Vector2 position, Quaternion rotation) { Position = position; Rotation = rotation; }
}

public abstract class AttackTrajectoryRuntime
{
    public abstract float MaximumSpeed { get; }
    public abstract AttackPose Evaluate(float age);
    public virtual void SetAim(Vector2 direction) { }
}

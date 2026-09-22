using UnityEngine;

public abstract class AttackTrajectoryDefinition : ScriptableObject
{
    // Local coordinates: +X is the captured aim direction.
    public abstract Vector2 Evaluate(float age, float speed);
    public virtual bool FollowOwner => false;
    public virtual float MaximumSpeed(float speed) => speed;
}

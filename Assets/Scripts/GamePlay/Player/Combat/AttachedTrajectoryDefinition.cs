using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Trajectories/Attached")]
public sealed class AttachedTrajectoryDefinition : AttackTrajectoryDefinition
{
    public override bool FollowOwner => true;
    public override Vector2 Evaluate(float age, float speed) => Vector2.zero;
}

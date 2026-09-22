using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Trajectories/Linear")]
public sealed class LinearTrajectoryDefinition : AttackTrajectoryDefinition
{
    public override Vector2 Evaluate(float age, float speed) => new Vector2(age * speed, 0f);
}

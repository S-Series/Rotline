using UnityEngine;

[CreateAssetMenu(
    fileName = "KeepDistanceMovement",
    menuName = "Game/Enemies/Movement/Keep Distance"
)]
public sealed class KeepDistanceMovementDefinition
    : EnemyMovementDefinition
{
    public override IEnemyMovementRuntime CreateRuntime(
        EnemyMovementContext context)
    {
        return new DistanceBandMovementRuntime(context);
    }
}

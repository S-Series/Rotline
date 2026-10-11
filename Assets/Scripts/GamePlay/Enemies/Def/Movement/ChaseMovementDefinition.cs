using UnityEngine;

[CreateAssetMenu(
    fileName = "ChaseMovement",
    menuName = "Game/Enemies/Movement/Chase"
)]
public sealed class ChaseMovementDefinition
    : EnemyMovementDefinition
{
    public override IEnemyMovementRuntime CreateRuntime(
        EnemyMovementContext context)
    {
        return new DistanceBandMovementRuntime(context);
    }
}

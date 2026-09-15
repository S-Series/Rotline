using UnityEngine;

public abstract class EnemyMovementDefinition : ScriptableObject
{
    public abstract IEnemyMovementRuntime CreateRuntime(
        EnemyMovementContext context
    );
}
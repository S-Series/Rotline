using UnityEngine;

public sealed class EnemyMovementContext
{
    public Transform Self { get; }
    public Transform Target { get; }
    public EnemyMovementMotor Motor { get; }
    public NavigationService Navigation { get; }
    public EnemyTargeting Targeting { get; }
    public EnemySteering Steering { get; }

    public float MoveSpeed { get; }


    public EnemyMovementContext(
        Transform self,
        Transform target,
        EnemyMovementMotor motor,
        NavigationService navigation,
        EnemyDefinition definition,
        Collider2D bodyCollider)
    {
        Self = self;
        Target = target;
        Motor = motor;
        Navigation = navigation;
        MoveSpeed = definition.MoveSpeed;
        Targeting = new EnemyTargeting(target, definition.MinimumDistance, definition.MaximumDistance);
        Steering = new EnemySteering(self, bodyCollider, navigation,
            definition.SeparationRadius, definition.SeparationWeight);
    }
}

using UnityEngine;

public sealed class EnemyMovementContext
{
    public Transform Self { get; }
    public Transform Target { get; }
    public EnemyMovementMotor Motor { get; }
    public NavigationService Navigation { get; }

    public float MoveSpeed { get; }


    public EnemyMovementContext(
        Transform self,
        Transform target,
        EnemyMovementMotor motor,
        NavigationService navigation,
        float moveSpeed)
    {
        Self = self;
        Target = target;
        Motor = motor;
        Navigation = navigation;
        MoveSpeed = moveSpeed;
    }
}
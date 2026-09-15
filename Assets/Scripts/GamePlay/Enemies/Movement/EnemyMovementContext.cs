using UnityEngine;

public sealed class EnemyMovementContext
{
    public Transform Self { get; }
    public Transform Target { get; }
    public EnemyMovementMotor Motor { get; }

    public float MoveSpeed { get; }

    public EnemyMovementContext(
        Transform self,
        Transform target,
        EnemyMovementMotor motor,
        float moveSpeed)
    {
        Self = self;
        Target = target;
        Motor = motor;
        MoveSpeed = moveSpeed;
    }
}
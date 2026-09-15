using UnityEngine;

[CreateAssetMenu(
    fileName = "KeepDistanceMovement",
    menuName = "Game/Enemies/Movement/Keep Distance"
)]
public sealed class KeepDistanceMovementDefinition
    : EnemyMovementDefinition
{
    [SerializeField]
    private float minDistance = 4f;

    [SerializeField]
    private float maxDistance = 7f;

    public override IEnemyMovementRuntime CreateRuntime(
        EnemyMovementContext context)
    {
        return new Runtime(
            context,
            minDistance,
            maxDistance
        );
    }

    private sealed class Runtime : IEnemyMovementRuntime
    {
        private readonly EnemyMovementContext context;

        private readonly float minDistance;
        private readonly float maxDistance;

        public Runtime(
            EnemyMovementContext context,
            float minDistance,
            float maxDistance)
        {
            this.context = context;
            this.minDistance = minDistance;
            this.maxDistance = maxDistance;
        }

        public void Tick(float deltaTime)
        {
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (context.Target == null)
            {
                context.Motor.Stop();
                return;
            }

            Vector2 offset =
                (Vector2)context.Target.position -
                context.Motor.Position;

            float distance = offset.magnitude;

            if (distance > maxDistance)
            {
                context.Motor.SetVelocity(
                    offset.normalized * context.MoveSpeed
                );

                return;
            }

            if (distance < minDistance)
            {
                context.Motor.SetVelocity(
                    -offset.normalized * context.MoveSpeed
                );

                return;
            }

            context.Motor.Stop();
        }
    }
}
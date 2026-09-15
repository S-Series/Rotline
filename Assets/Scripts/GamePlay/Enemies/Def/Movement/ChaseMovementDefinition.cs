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
        return new Runtime(context);
    }

    private sealed class Runtime : IEnemyMovementRuntime
    {
        private readonly EnemyMovementContext context;

        public Runtime(EnemyMovementContext context)
        {
            this.context = context;
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

            Vector2 direction =
                ((Vector2)context.Target.position -
                 context.Motor.Position).normalized;

            context.Motor.SetVelocity(
                direction * context.MoveSpeed
            );
        }
    }
}
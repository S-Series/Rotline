using UnityEngine;

[CreateAssetMenu(
    fileName = "HopMovement",
    menuName = "Game/Enemies/Movement/Hop"
)]
public sealed class HopMovementDefinition
    : EnemyMovementDefinition
{
    [SerializeField]
    private float hopInterval = 1f;

    [SerializeField]
    private float hopDuration = 0.25f;

    [SerializeField]
    private float hopSpeedMultiplier = 2f;

    public override IEnemyMovementRuntime CreateRuntime(
        EnemyMovementContext context)
    {
        return new Runtime(
            context,
            hopInterval,
            hopDuration,
            hopSpeedMultiplier
        );
    }

    private sealed class Runtime : IEnemyMovementRuntime
    {
        private readonly EnemyMovementContext context;

        private readonly float hopInterval;
        private readonly float hopDuration;
        private readonly float hopSpeedMultiplier;

        private float waitTimer;
        private float hopTimer;

        private bool isHopping;

        private Vector2 hopDirection;

        public Runtime(
            EnemyMovementContext context,
            float hopInterval,
            float hopDuration,
            float hopSpeedMultiplier)
        {
            this.context = context;

            this.hopInterval = hopInterval;
            this.hopDuration = hopDuration;
            this.hopSpeedMultiplier = hopSpeedMultiplier;

            // 모든 슬라임이 정확히 동시에 뛰는 현상 방지
            waitTimer = Random.Range(0f, hopInterval);
        }

        public void Tick(float deltaTime)
        {
            if (isHopping)
            {
                hopTimer -= deltaTime;

                if (hopTimer <= 0f)
                {
                    isHopping = false;
                    waitTimer = hopInterval;
                }

                return;
            }

            waitTimer -= deltaTime;

            if (waitTimer > 0f)
                return;

            if (context.Target == null)
                return;

            hopDirection =
                ((Vector2)context.Target.position -
                 context.Motor.Position).normalized;

            hopTimer = hopDuration;
            isHopping = true;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!isHopping)
            {
                context.Motor.Stop();
                return;
            }

            context.Motor.SetVelocity(
                hopDirection *
                context.MoveSpeed *
                hopSpeedMultiplier
            );
        }
    }
}
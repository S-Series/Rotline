using UnityEngine;

public sealed class DistanceBandMovementRuntime : IEnemyMovementRuntime
{
    private readonly EnemyMovementContext context;

    public DistanceBandMovementRuntime(EnemyMovementContext context)
    {
        this.context = context;
    }

    public void Tick(float deltaTime) { }

    public void FixedTick(float fixedDeltaTime)
    {
        if (context.Targeting.Target == null)
        {
            context.Motor.Stop();
            return;
        }
        Vector2 position = context.Motor.Position;
        Vector2 approach = context.Targeting.Approach(position);
        Vector2 steering = context.Steering.Direction(position, approach,
            context.Targeting.IsApproaching(position));
        context.Motor.SetVelocity(steering * context.MoveSpeed);
    }
}

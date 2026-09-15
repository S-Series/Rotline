public interface IEnemyMovementRuntime
{
    void Tick(float deltaTime);

    void FixedTick(float fixedDeltaTime);
}
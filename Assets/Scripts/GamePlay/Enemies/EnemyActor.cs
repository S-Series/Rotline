using UnityEngine;

[RequireComponent(typeof(EnemyMovementMotor))]
public sealed class EnemyActor : MonoBehaviour
{
    private EnemyDefinition definition;

    private EnemyMovementMotor movementMotor;
    private IEnemyMovementRuntime movementRuntime;

    public EnemyDefinition Definition => definition;

    private void Awake()
    {
        movementMotor =
            GetComponent<EnemyMovementMotor>();
    }

    public void Initialize(
    EnemyDefinition definition,
    Transform target,
    NavigationService navigation)
    {
        this.definition = definition;

        if (definition == null)
        {
            Debug.LogError(
                $"{name}: EnemyDefinition이 없습니다.",
                this
            );

            return;
        }

        if (definition.Movement == null)
        {
            Debug.LogError(
                $"{name}: MovementDefinition이 없습니다.",
                this
            );

            return;
        }

        EnemyMovementContext context =
            new EnemyMovementContext(
                transform,
                target,
                movementMotor,
                navigation,
                definition.MoveSpeed
            );

        movementRuntime =
            definition.Movement.CreateRuntime(context);
    }
    private void Update()
    {
        movementRuntime?.Tick(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        movementRuntime?.FixedTick(
            Time.fixedDeltaTime
        );
    }

    public void ResetActor()
    {
        movementRuntime = null;
        definition = null;

        movementMotor.Stop();
    }
}
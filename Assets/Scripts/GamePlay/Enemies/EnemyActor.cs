using UnityEngine;

[RequireComponent(typeof(EnemyMovementMotor))]
public sealed class EnemyActor : MonoBehaviour
{
    [SerializeField]
    private EnemyDefinition definition;

    private EnemyMovementMotor movementMotor;
    private IEnemyMovementRuntime movementRuntime;

    public EnemyDefinition Definition => definition;

    private void Awake()
    {
        movementMotor =
            GetComponent<EnemyMovementMotor>();
    }

    public void Initialize(Transform target)
    {
        if (definition == null)
        {
            Debug.LogError(
                $"{name}: EnemyDefinition이 설정되지 않았습니다.",
                this
            );

            return;
        }

        if (definition.Movement == null)
        {
            Debug.LogError(
                $"{name}: MovementDefinition이 설정되지 않았습니다.",
                this
            );

            return;
        }

        EnemyMovementContext context =
            new EnemyMovementContext(
                transform,
                target,
                movementMotor,
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
}
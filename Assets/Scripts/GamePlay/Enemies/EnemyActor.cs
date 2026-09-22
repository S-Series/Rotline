using System;
using UnityEngine;

[RequireComponent(typeof(EnemyMovementMotor))]
public sealed class EnemyActor : MonoBehaviour
{
    private EnemyDefinition definition;

    private EnemyMovementMotor movementMotor;
    private IEnemyMovementRuntime movementRuntime;

    private float currentHealth;
    private bool isDead;

    public EnemyDefinition Definition => definition;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    // 사망한 적과 사망 위치를 전달한다.
    public event Action<EnemyActor, Vector2> Died;
    public event Action<EnemyActor, DamageContext, DamageResult> Damaged;
    public uint SpawnVersion { get; private set; }


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
        if (movementMotor == null) movementMotor = GetComponent<EnemyMovementMotor>();

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

        // 풀에서 재사용할 때마다 체력과 사망 상태 초기화.
        currentHealth = Mathf.Max(0.01f, definition.MaxHealth);
        isDead = false;
        SpawnVersion++;

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
        if (isDead)
            return;

        movementRuntime?.Tick(Time.deltaTime);
    }


    private void FixedUpdate()
    {
        if (isDead)
            return;

        movementRuntime?.FixedTick(Time.fixedDeltaTime);
    }


    public void TakeDamage(float damage)
    {
        TakeDamage(new DamageContext(null, damage, Vector2.zero));
    }

    public DamageResult TakeDamage(DamageContext context)
    {
        float damage = context.Amount;
        if (isDead || definition == null)
            return default;

        if (float.IsNaN(damage) ||
            float.IsInfinity(damage) ||
            damage <= 0f)
        {
            return default;
        }

        float applied = Mathf.Min(currentHealth, damage);
        currentHealth =
            Mathf.Max(0f, currentHealth - damage);
        bool killed = currentHealth <= 0f;
        var result = new DamageResult(applied, killed);
        // Lock death before callbacks; a listener may deal damage recursively.
        if (killed) isDead = true;
        try
        {
            Damaged?.Invoke(this, context, result);
        }
        finally
        {
            if (killed) Die();
        }
        return result;
    }


    private void Die()
    {
        isDead = true;

        Vector2 deathPosition = transform.position;

        movementMotor.Stop();

        // SpawnDirector가 이 이벤트를 받아 풀에 반환한다.
        Died?.Invoke(this, deathPosition);
    }


    public void ResetActor()
    {
        movementRuntime = null;
        definition = null;

        currentHealth = 0f;
        isDead = false;

        // 이전 생명주기의 구독자가 다음 스폰에 남지 않도록 정리.
        Died = null;
        Damaged = null;

        if (movementMotor != null) movementMotor.Stop();
    }


    // 공격 시스템 연결 전 임시 테스트용.
    [ContextMenu("Debug/Kill Enemy (Play Mode)")]
    private void DebugKill()
    {
        if (!Application.isPlaying ||
            definition == null ||
            isDead)
        {
            return;
        }

        TakeDamage(currentHealth);
    }
}

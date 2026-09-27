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
    private PlayerHealth targetHealth;
    private float contactMultiplier = 1f;
    private Collider2D bodyCollider;
    private Collider2D targetCollider;
    private float knockbackUntil;
    private Vector2 knockbackVelocity;

    public EnemyDefinition Definition => definition;
    public float CurrentHealth => currentHealth;
    public float MaximumHealth { get; private set; }
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
        targetHealth = target != null ? target.GetComponent<PlayerHealth>() : null;
        bodyCollider = GetComponent<Collider2D>();
        targetCollider = target != null ? target.GetComponent<Collider2D>() : null;
        contactMultiplier = 1f;
        knockbackUntil = 0f;
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
        currentHealth = MaximumHealth = Mathf.Max(0.01f, definition.MaxHealth);
        isDead = false;
        SpawnVersion++;
        if (AlphaSession.Active)
        {
            transform.localScale = Vector3.one * definition.size;
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>()) { renderer.color = definition.tint; renderer.sprite = AlphaVisuals.Square; }
            var offense = GetComponent<AlphaEnemyOffense>();
            if (offense == null) offense = gameObject.AddComponent<AlphaEnemyOffense>();
            offense.Initialize(this, targetHealth);
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
        if (isDead)
            return;

        movementRuntime?.Tick(Time.deltaTime);
    }


    private void FixedUpdate()
    {
        if (isDead)
            return;

        movementRuntime?.FixedTick(Time.fixedDeltaTime);
        if (Time.time < knockbackUntil) movementMotor.SetVelocity(knockbackVelocity);
        // Enemy/player collision is disabled to allow crowds; query actual shapes for contact.
        if (targetHealth != null && bodyCollider != null && targetCollider != null &&
            ((Vector2)targetHealth.transform.position - (Vector2)transform.position).sqrMagnitude < 9f &&
            bodyCollider.Distance(targetCollider).distance <= 0.04f)
            targetHealth.TakeDamage(definition.ContactDamage * contactMultiplier);
    }

    public void ScaleDifficulty(float healthMultiplier, float damageMultiplier)
    {
        currentHealth *= Mathf.Max(1f, healthMultiplier);
        MaximumHealth *= Mathf.Max(1f, healthMultiplier);
        contactMultiplier = Mathf.Max(1f, damageMultiplier);
    }

    private void OnCollisionStay2D(Collision2D collision) => TryContact(collision.collider);
    private void OnCollisionEnter2D(Collision2D collision) => TryContact(collision.collider);
    private void OnTriggerStay2D(Collider2D collider) => TryContact(collider);
    private void TryContact(Collider2D collider)
    {
        if (isDead || definition == null || targetHealth == null) return;
        if (collider.GetComponentInParent<PlayerHealth>() == targetHealth)
            targetHealth.TakeDamage(definition.ContactDamage * contactMultiplier);
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

        PlayerStats sourceStats = context.Source != null ? context.Source.GetComponent<PlayerStats>() : null;
        if (sourceStats != null && !context.IsCritical && UnityEngine.Random.value < sourceStats.CriticalChance)
        {
            damage *= sourceStats.CriticalDamage > 0f ? Mathf.Max(1f, sourceStats.CriticalDamage) : 1.5f;
            context = new DamageContext(context.Source, damage, context.Direction, true, context.ChainDepth, context.AttackId, context.RootAttackId);
        }
        float applied = Mathf.Min(currentHealth, damage);
        currentHealth =
            Mathf.Max(0f, currentHealth - damage);
        bool killed = currentHealth <= 0f;
        if (!killed && sourceStats != null && sourceStats.KnockbackPower > 0f)
        {
            Vector2 direction = context.Direction.sqrMagnitude > 0f ? context.Direction.normalized : ((Vector2)transform.position - (Vector2)context.Source.position).normalized;
            knockbackVelocity = direction * sourceStats.KnockbackPower;
            knockbackUntil = Time.time + 0.15f;
        }
        if (context.Source != null && context.Source.TryGetComponent<PlayerCharacter>(out var source) &&
            source.AbilityRuntime is BerserkAbilityRuntime berserk && berserk.ActiveLifeStealFraction > 0f)
            source.GetComponent<PlayerHealth>()?.Heal(applied * berserk.ActiveLifeStealFraction);
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
        targetHealth = null;

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

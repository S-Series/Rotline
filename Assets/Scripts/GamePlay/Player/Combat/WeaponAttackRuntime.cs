using System;
using UnityEngine;

public readonly struct AttackSnapshot
{
    public readonly Vector2 Direction;
    public readonly float Damage;
    public readonly float Range;
    public readonly float Duration;
    public readonly float Charge;
    public readonly float ProjectileSpeedMultiplier;

    public AttackSnapshot(
        Vector2 direction,
        float damage,
        float range,
        float duration,
        float charge = 1f,
        float projectileSpeedMultiplier = 1f)
    {
        Direction = direction;
        Damage = damage;
        Range = range;
        Duration = duration;
        Charge = charge;
        ProjectileSpeedMultiplier = projectileSpeedMultiplier;
    }
}


public abstract class WeaponAttackRuntime : IDisposable
{
    protected readonly PlayerStats Stats;
    protected readonly Transform Owner;
    protected readonly LayerMask EnemyLayers;

    private readonly ModifierScope weaponModifiers;

    private float nextAttackTime;

    private bool attacking;
    private bool disposed;

    public bool IsAttacking => attacking && !disposed;
    public bool IsDisposed => disposed;
    public float NextAttackTime { get => nextAttackTime; set => nextAttackTime = Mathf.Max(nextAttackTime, value); }
    public virtual bool IsCharging => false;
    protected bool CanStart => !disposed && !attacking && Time.time >= nextAttackTime;
    public virtual void PrimaryStarted(Vector2 direction) => RequestAttack(direction);
    public virtual void PrimaryHeld(Vector2 direction) => RequestAttack(direction);
    public virtual void PrimaryReleased(Vector2 direction) { }

    // 실제 공격 판정이 활성화될 때 발생.
    public event Action<AttackSnapshot> AttackActiveStarted;


    protected WeaponAttackRuntime(
        WeaponAttackDefinition definition,
        PlayerStats stats,
        Transform owner,
        LayerMask enemyLayers)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(
                nameof(definition)
            );
        }

        Stats = stats != null
            ? stats
            : throw new ArgumentNullException(nameof(stats));

        Owner = owner != null
            ? owner
            : throw new ArgumentNullException(nameof(owner));

        EnemyLayers = enemyLayers;

        weaponModifiers = new ModifierScope(stats);

        // 현재 무기의 기본 사거리를 등록한다.
        weaponModifiers.AddModifier(
            StatKey.AttackRange,
            ModifierType.BaseFlat,
            definition.BaseRange
        );
    }


    public bool RequestAttack(Vector2 direction)
    {
        if (disposed || attacking)
            return false;

        if (Time.time < nextAttackTime)
            return false;

        if (float.IsNaN(direction.x) || float.IsNaN(direction.y) ||
            float.IsInfinity(direction.x) || float.IsInfinity(direction.y) ||
            direction.sqrMagnitude < 0.0001f)
            return false;

        float attackSpeed = Stats.AttackSpeed;
        float range = Stats.AttackRange;
        float damage = Stats.AttackPower;

        if (!IsPositiveFinite(attackSpeed) ||
            !IsPositiveFinite(range) ||
            !IsPositiveFinite(damage))
        {
            return false;
        }

        float duration = 1f / attackSpeed;

        if (!IsPositiveFinite(duration))
            return false;

        var attack = new AttackSnapshot(
            direction.normalized,
            damage,
            range,
            duration,
            1f,
            Stats.ProjectileSpeedMultiplier
        );

        // 공격 준비 과정에서 재진입해도
        // 중복 공격이 시작되지 않도록 먼저 상태를 변경.
        attacking = true;
        nextAttackTime = Time.time + duration;

        try
        {
            StartAttack(attack);
        }
        catch
        {
            // 공격 초기화에 실패했다면
            // 부분적으로 생성된 공격 상태를 정리한다.
            CancelAttack();
            throw;
        }

        return IsAttacking;
    }


    public void Tick(float deltaTime)
    {
        if (disposed)
            return;

        if (!IsPositiveFinite(deltaTime))
            return;

        bool stillAttacking;

        try
        {
            TickPersistent(deltaTime);
            if (disposed || !attacking) return;
            stillAttacking = UpdateAttack(deltaTime);
        }
        catch
        {
            // 공격 효과 실행 중 예외가 발생해도
            // Collider가 계속 켜진 채 남지 않도록 한다.
            CancelAttack();
            throw;
        }

        // UpdateAttack 내부의 이벤트 콜백이
        // CancelAttack 또는 Dispose를 실행했을 수 있다.
        if (disposed || !attacking)
            return;

        if (!stillAttacking)
        {
            attacking = false;
        }
    }


    public void FixedTick()
    {
        if (disposed || !attacking)
            return;

        UpdatePhysics();
    }


    // 현재 공격만 취소한다.
    // 무기 Modifier 및 공격 오브젝트는 유지.
    public void CancelAttack()
    {
        if (disposed) return;

        // 취소 처리에서 다른 이벤트가 발생하더라도
        // 같은 공격이 재진입하지 않게 상태를 먼저 종료.
        attacking = false;

        CancelCurrentAttack();
    }


    public void Dispose()
    {
        if (disposed)
            return;

        // 외부 호출을 즉시 차단한다.
        disposed = true;

        attacking = false;

        try
        {
            CancelCurrentAttack();
        }
        finally
        {
            try
            {
                ReleaseAttack();
            }
            finally
            {
                // 공격 오브젝트 정리 중 문제가 생겨도
                // 무기 Modifier 제거는 시도한다.
                try
                {
                    weaponModifiers.Dispose();
                }
                finally
                {
                    AttackActiveStarted = null;
                }
            }
        }
    }


    protected void NotifyAttackActiveStarted(
        AttackSnapshot attack)
    {
        if (disposed || !attacking)
            return;

        AttackActiveStarted?.Invoke(attack);
    }


    protected abstract void StartAttack(
        AttackSnapshot attack
    );


    // true: 계속 진행
    // false: 이번 공격 종료
    protected abstract bool UpdateAttack(
        float deltaTime
    );


    protected abstract void UpdatePhysics();
    protected virtual void TickPersistent(float deltaTime) { }


    // 진행 중인 공격 상태만 해제.
    protected abstract void CancelCurrentAttack();


    // 무기 자체의 GameObject 등 영구 정리.
    protected abstract void ReleaseAttack();


    private static bool IsPositiveFinite(float value)
    {
        return value > 0f &&
               !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }
}

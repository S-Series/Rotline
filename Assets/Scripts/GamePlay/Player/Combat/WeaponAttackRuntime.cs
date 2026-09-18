using System;
using UnityEngine;

public readonly struct AttackSnapshot
{
    public readonly Vector2 Direction;
    public readonly float Damage;
    public readonly float Range;
    public readonly float Duration;

    public AttackSnapshot(
        Vector2 direction,
        float damage,
        float range,
        float duration)
    {
        Direction = direction;
        Damage = damage;
        Range = range;
        Duration = duration;
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

    public bool IsAttacking => attacking;


    protected WeaponAttackRuntime(
        WeaponAttackDefinition definition,
        PlayerStats stats,
        Transform owner,
        LayerMask enemyLayers)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        Stats = stats != null
            ? stats
            : throw new ArgumentNullException(nameof(stats));

        Owner = owner != null
            ? owner
            : throw new ArgumentNullException(nameof(owner));

        EnemyLayers = enemyLayers;

        weaponModifiers = new ModifierScope(stats);

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

        float attackSpeed = Stats.AttackSpeed;
        float range = Stats.AttackRange;
        float damage = Stats.AttackPower;

        if (attackSpeed <= 0f ||
            range <= 0f ||
            damage <= 0f ||
            direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        float duration = 1f / attackSpeed;

        var attack = new AttackSnapshot(
            direction.normalized,
            damage,
            range,
            duration
        );

        StartAttack(attack);

        attacking = true;
        nextAttackTime = Time.time + duration;

        return true;
    }


    public void Tick(float deltaTime)
    {
        if (disposed || !attacking)
            return;

        if (!UpdateAttack(deltaTime))
            attacking = false;
    }


    public void FixedTick()
    {
        if (disposed || !attacking)
            return;

        UpdatePhysics();
    }


    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        attacking = false;

        try
        {
            ReleaseAttack();
        }
        finally
        {
            weaponModifiers.Dispose();
        }
    }


    protected abstract void StartAttack(AttackSnapshot attack);

    // 공격이 끝나면 false.
    protected abstract bool UpdateAttack(float deltaTime);

    protected abstract void UpdatePhysics();

    protected abstract void ReleaseAttack();


    // 실제 공격 판정이 활성화되는 순간 발생.
    public event Action<AttackSnapshot> AttackActiveStarted;

    protected void NotifyAttackActiveStarted(
        AttackSnapshot attack)
    {
        AttackActiveStarted?.Invoke(attack);
    }
    
}
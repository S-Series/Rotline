using UnityEngine;

public readonly struct AttackEffectContext
{
    public readonly AttackSnapshot Attack;
    public readonly Transform Owner;
    public readonly LayerMask EnemyLayers;

    public readonly float ProjectileSpeedMultiplier;
    public readonly System.Action<AttackProjectile> RegisterProjectile;

    public AttackEffectContext(
        AttackSnapshot attack,
        Transform owner,
        LayerMask enemyLayers,
        float projectileSpeedMultiplier,
        System.Action<AttackProjectile> registerProjectile = null)
    {
        Attack = attack;
        Owner = owner;
        EnemyLayers = enemyLayers;

        ProjectileSpeedMultiplier =
            projectileSpeedMultiplier;
        RegisterProjectile = registerProjectile;
    }
}


public abstract class AttackEffectDefinition : ScriptableObject
{
    public abstract void OnAttack(
        in AttackEffectContext context
    );
}

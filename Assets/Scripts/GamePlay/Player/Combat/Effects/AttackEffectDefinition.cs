using UnityEngine;

public readonly struct AttackEffectContext
{
    public readonly AttackSnapshot Attack;
    public readonly Transform Owner;
    public readonly LayerMask EnemyLayers;

    public readonly float ProjectileSpeedMultiplier;

    public AttackEffectContext(
        AttackSnapshot attack,
        Transform owner,
        LayerMask enemyLayers,
        float projectileSpeedMultiplier)
    {
        Attack = attack;
        Owner = owner;
        EnemyLayers = enemyLayers;

        ProjectileSpeedMultiplier =
            projectileSpeedMultiplier;
    }
}


public abstract class AttackEffectDefinition : ScriptableObject
{
    public abstract void OnAttack(
        in AttackEffectContext context
    );
}
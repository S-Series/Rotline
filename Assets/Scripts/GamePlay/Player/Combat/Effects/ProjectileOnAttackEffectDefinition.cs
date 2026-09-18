using UnityEngine;

[CreateAssetMenu(
    fileName = "ProjectileOnAttack",
    menuName = "Game/Combat/Effects/Projectile On Attack"
)]
public sealed class ProjectileOnAttackEffectDefinition
    : AttackEffectDefinition
{
    [Header("Projectile")]
    [SerializeField]
    private AttackProjectile projectilePrefab;

    [Header("Stats")]
    [SerializeField, Min(0.01f)]
    private float baseSpeed = 8f;

    [SerializeField, Min(0.01f)]
    private float damageMultiplier = 0.5f;

    [SerializeField, Min(0.01f)]
    private float rangeMultiplier = 1.5f;

    [SerializeField, Min(0f)]
    private float spawnOffset = 0.5f;

    [SerializeField, Min(0.01f)]
    private float maxLifetime = 3f;


    public override void OnAttack(
        in AttackEffectContext context)
    {
        if (projectilePrefab == null ||
            context.Owner == null)
        {
            return;
        }

        Vector2 direction =
            context.Attack.Direction.normalized;

        Vector2 origin =
            (Vector2)context.Owner.position +
            direction * spawnOffset;

        float damage =
            context.Attack.Damage * damageMultiplier;

        float speed =
            baseSpeed *
            context.ProjectileSpeedMultiplier;

        float range =
            context.Attack.Range * rangeMultiplier;

        AttackProjectile projectile =
            Instantiate(
                projectilePrefab,
                origin,
                Quaternion.identity
            );

        projectile.Launch(
            direction,
            damage,
            speed,
            range,
            maxLifetime,
            context.EnemyLayers
        );
    }
}
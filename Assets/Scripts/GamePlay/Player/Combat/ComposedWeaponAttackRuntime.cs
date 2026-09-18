using UnityEngine;

public sealed class ComposedWeaponAttackRuntime
    : WeaponAttackRuntime
{
    private readonly WeaponAttackDefinition settings;

    private readonly GameObject attackObject;
    private readonly AttackHitbox hitbox;

    private AttackSnapshot currentAttack;
    private float elapsed;

    private bool reverseNextAttack;
    private bool reverseCurrentAttack;


    public ComposedWeaponAttackRuntime(
        WeaponAttackDefinition definition,
        PlayerStats stats,
        Transform owner,
        LayerMask enemyLayers)
        : base(definition, stats, owner, enemyLayers)
    {
        settings = definition;

        attackObject = Object.Instantiate(
            definition.HitboxPrefab,
            owner
        );

        attackObject.name = "AttackInstance";

        attackObject.transform.localPosition = Vector3.zero;
        attackObject.transform.localRotation = Quaternion.identity;
        attackObject.transform.localScale = Vector3.one;

        Collider2D collider =
            attackObject.GetComponent<Collider2D>();

        hitbox = attackObject.GetComponent<AttackHitbox>();

        hitbox.Initialize(collider, enemyLayers);
    }


    protected override void StartAttack(
        AttackSnapshot attack)
    {
        currentAttack = attack;
        elapsed = 0f;

        if (settings.Motion.AlternateDirection)
        {
            reverseCurrentAttack = reverseNextAttack;
            reverseNextAttack = !reverseNextAttack;
        }
        else
        {
            reverseCurrentAttack = false;
        }

        hitbox.End();

        // 프리팹은 반지름 1을 기준으로 제작한다.
        // 공격범위 Modifier가 반영된 최종 사거리로 확대.
        attackObject.transform.localScale =
            Vector3.one * attack.Range;

        settings.Motion.ApplyPose(
            attackObject.transform,
            attack,
            reverseCurrentAttack ? 1f : 0f
        );
    }


    protected override bool UpdateAttack(float deltaTime)
    {
        elapsed += deltaTime;

        float duration = currentAttack.Duration;

        if (elapsed >= duration)
        {
            hitbox.End();
            return false;
        }

        float windupEnd =
            duration * settings.WindupRatio;

        float activeEnd =
            duration * (
                settings.WindupRatio +
                settings.ActiveRatio
            );

        // 준비 구간: 판정 OFF
        if (elapsed < windupEnd)
        {
            hitbox.End();
            return true;
        }

        // 공격 활성 구간
        if (elapsed < activeEnd)
        {
            float progress = Mathf.InverseLerp(
                windupEnd,
                activeEnd,
                elapsed
            );

            settings.Motion.ApplyPose(
                attackObject.transform,
                currentAttack,
                reverseCurrentAttack
                    ? 1f - progress
                    : progress
            );

            if (!hitbox.IsActive)
            {
                hitbox.Begin(currentAttack.Damage);

                // 한 번의 휘두르기에서 딱 한 번 발생.
                NotifyAttackActiveStarted(currentAttack);
            }

            // 이동한 Collider를 현재 위치에서 검사.
            hitbox.Scan();

            return true;
        }

        // 후딜레이: 판정 OFF
        hitbox.End();

        return true;
    }


    protected override void UpdatePhysics()
    {
        // 현재 근접 공격은 UpdateAttack에서 판정을 검사한다.
        // 향후 물리 기반 공격은 이 경계를 사용할 수 있다.
    }


    protected override void ReleaseAttack()
    {
        hitbox.End();

        if (attackObject != null)
            Object.Destroy(attackObject);
    }
}
using UnityEngine;

public sealed class ComposedWeaponAttackRuntime
    : WeaponAttackRuntime
{
    // 현재 검의 회전 판정용 임시 샘플링 해상도.
    // 추후 Motion별 충돌 검사 정책으로 분리할 예정.
    private const int FullSweepSampleCount = 24;

    private readonly WeaponAttackDefinition settings;
    private readonly AttackRecipe recipe;
    private readonly AttackTriggerRuntime input;
    private readonly AttackExecutionRuntime execution;
    private AttackExecutionRuntime.AttackContext attackContext;
    private float charge = 1f;
    public override bool IsCharging => !IsDisposed && input != null && input.IsCharging;
    public override bool RequiresChargeInput => input != null && input.RequiresChargeInput;

    private readonly GameObject attackObject;
    private readonly AttackHitbox hitbox;

    private AttackSnapshot currentAttack;

    private float elapsed;

    private bool reverseNextAttack;
    private bool reverseCurrentAttack;

    private bool activationNotified;


    public ComposedWeaponAttackRuntime(
        WeaponAttackDefinition definition,
        PlayerStats stats,
        Transform owner,
        LayerMask enemyLayers,
        AttackRecipe recipe = null,
        CombatEffectStateStore effects = null)
        : base(definition, stats, owner, enemyLayers, effects)
    {
        settings = definition;
        this.recipe = (recipe ?? new AttackRecipe()).Copy();
        execution = new AttackExecutionRuntime(owner, enemyLayers, EffectStates);

        try
        {
            input = this.recipe.CreateTrigger(request =>
            {
                charge = request.Charge;
                return RequestAttack(request.Direction);
            }, CancelAttack);
            attackObject = Object.Instantiate(
                definition.HitboxPrefab,
                owner
            );

            attackObject.name = "AttackInstance";

            attackObject.transform.localPosition = Vector3.zero;
            attackObject.transform.localRotation =
                Quaternion.identity;
            attackObject.transform.localScale =
                Vector3.one;

            Collider2D collider =
                attackObject.GetComponent<Collider2D>();

            hitbox =
                attackObject.GetComponent<AttackHitbox>();

            hitbox.Initialize(
                collider,
                enemyLayers
            );
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public override void PrimaryHeld(Vector2 direction)
    {
        if (!IsDisposed) input.Held(direction);
    }
    public override void PrimaryStarted(Vector2 direction)
    { if (CanStart && !IsCharging) input.Started(direction); }
    public override void PrimaryReleased(Vector2 direction)
    { if (!IsDisposed) input.Released(direction); }
    protected override void TickPersistent(float deltaTime)
    {
        input.Tick(deltaTime);
        execution.Tick(deltaTime);
    }


    protected override void StartAttack(
        AttackSnapshot attack)
    {
        currentAttack = new AttackSnapshot(attack.Direction, attack.Damage * recipe.damageMultiplier,
            attack.Range, attack.Duration, charge, attack.ProjectileSpeedMultiplier);
        attackContext = execution.CreateAttack(recipe, currentAttack);

        elapsed = 0f;
        activationNotified = false;

        // 현재 공격의 방향을 확정한다.
        // 다음 공격의 방향은 미리 반전해 둔다.
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

        // 프리팹은 반지름 1 기준.
        // 최종 AttackRange를 반영한다.
        attackObject.transform.localScale =
            Vector3.one * attack.Range;

        ApplyProgress(0f);
    }


    protected override bool UpdateAttack(
        float deltaTime)
    {
        float duration = currentAttack.Duration;

        if (duration <= 0f)
        {
            hitbox.End();
            return false;
        }

        float previousElapsed = elapsed;

        elapsed = Mathf.Min(
            duration,
            elapsed + Mathf.Max(0f, deltaTime)
        );

        float windupEnd =
            duration * settings.WindupRatio;

        float activeEnd =
            duration * (
                settings.WindupRatio +
                settings.ActiveRatio
            );

        // 이번 프레임에 활성 구간 일부라도
        // 통과했다면 판정을 실행한다.
        bool crossesActiveWindow =
            previousElapsed < activeEnd &&
            elapsed >= windupEnd &&
            elapsed > previousElapsed &&
            activeEnd > windupEnd;

        if (crossesActiveWindow)
        {
            float fromTime = Mathf.Clamp(
                previousElapsed,
                windupEnd,
                activeEnd
            );

            float toTime = Mathf.Clamp(
                elapsed,
                windupEnd,
                activeEnd
            );

            float fromProgress = Mathf.InverseLerp(
                windupEnd,
                activeEnd,
                fromTime
            );

            float toProgress = Mathf.InverseLerp(
                windupEnd,
                activeEnd,
                toTime
            );

            bool newlyActivated = !activationNotified;

            if (newlyActivated)
            {
                // 공격 시작 자세를 먼저 적용한다.
                ApplyProgress(fromProgress);

                hitbox.Begin(new DamageContext(Owner, currentAttack.Damage, currentAttack.Direction),
                    recipe.CreateHitPolicy(), target => execution.Hit(attackContext, target));

                activationNotified = true;
                execution.Activate(attackContext, Owner.position);

                // 검기는 공격당 정확히 한 번 발사한다.
                NotifyAttackActiveStarted(currentAttack);

                // 유물 효과 실행 도중 공격이 취소되거나
                // 소유자가 비활성화됐을 수 있다.
                if (!IsAttacking || IsDisposed)
                {
                    return false;
                }
            }

            // 활성 구간을 지나가는 비율만큼
            // 회전 경로를 나눠 검사한다.
            int steps = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    (toProgress - fromProgress) *
                    FullSweepSampleCount
                )
            );

            // 첫 활성화 시에는 시작 지점도 검사.
            // 계속 진행 중이었다면 지난 프레임의
            // 마지막 지점이 이미 검사됐으므로 생략.
            int firstStep = newlyActivated ? 0 : 1;

            for (int i = firstStep; i <= steps; i++)
            {
                if (!IsAttacking || IsDisposed)
                {
                    return false;
                }

                float interpolation =
                    i / (float)steps;

                float progress = Mathf.Lerp(
                    fromProgress,
                    toProgress,
                    interpolation
                );

                ApplyProgress(progress);

                hitbox.Scan(Mathf.Lerp(fromTime, toTime, interpolation));
            }
        }

        // 활성 구간을 통과한 프레임에서도
        // 위의 경로 검사를 끝낸 뒤 Collider 종료.
        if (elapsed >= activeEnd)
        {
            hitbox.End();
        }

        // 공격 전체 시간이 끝났는지 검사.
        if (elapsed >= duration) execution.Complete(attackContext, Owner.position);
        return elapsed < duration;
    }


    protected override void UpdatePhysics()
    {
        // 현재 근접 공격은 UpdateAttack에서
        // 위치 갱신과 명중 판정을 수행한다.
    }


    protected override void CancelCurrentAttack()
    {
        activationNotified = false;
        input?.Cancel();
        execution?.Cancel();

        hitbox?.End();
    }


    protected override void ReleaseAttack()
    {
        execution?.Dispose();
        hitbox?.End();

        if (attackObject != null)
        {
            if (Application.isPlaying) Object.Destroy(attackObject);
            else Object.DestroyImmediate(attackObject);
        }
    }


    private void ApplyProgress(float progress)
    {
        // 역방향 공격에서는 진행도만 반전한다.
        float motionProgress = reverseCurrentAttack
            ? 1f - progress
            : progress;

        settings.Motion.ApplyPose(
            attackObject.transform,
            currentAttack,
            motionProgress
        );
    }
}

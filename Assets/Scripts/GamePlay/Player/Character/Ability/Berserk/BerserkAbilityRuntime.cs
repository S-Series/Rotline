using System;

public sealed class BerserkAbilityRuntime : CharacterAbilityRuntime
{
    private readonly BerserkAbilityDefinition settings;

    private SpecialGaugeRuntime gauge;
    private ModifierScope modifierScope;
    private float toggleCooldownRemaining;

    public SpecialGaugeRuntime Gauge => gauge;
    public float ToggleCooldownRemaining => toggleCooldownRemaining;

    // 실제 적용하려면 체력 / 전투 / 상태이상 시스템의 연결이 필요하다.
    // PlayerStats.HealthRegen 값을 읽는 것만으로 재생이 차단되지는 않는다.
    public bool BlocksHealthRegeneration => IsActive;

    public float ActiveLifeStealFraction =>
        IsActive ? settings.LifeStealFraction : 0f;

    public float ActiveSlowResistanceBonus =>
        IsActive ? settings.SlowResistanceBonus : 0f;


    public BerserkAbilityRuntime(
        BerserkAbilityDefinition definition,
        PlayerStats stats)
        : base(definition, stats)
    {
        settings = definition;
        ValidateSettings();
    }


    // 특수 게이지는 어빌리티가 아닌 캐릭터가 소유한다.
    public void BindGauge(SpecialGaugeRuntime characterGauge)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(
                nameof(BerserkAbilityRuntime)
            );
        }

        if (IsActive || gauge != null)
        {
            throw new InvalidOperationException(
                "Berserk gauge is already bound or ability is active."
            );
        }

        gauge = characterGauge
            ?? throw new ArgumentNullException(nameof(characterGauge));
    }


    // 플레이어가 R키를 누를 때마다 1회 호출한다.
    // 실제 On/Off 전환에 성공했을 때만 true를 반환한다.
    public bool RequestToggle()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(
                nameof(BerserkAbilityRuntime)
            );
        }

        EnsureGauge();

        if (toggleCooldownRemaining > 0f)
            return false;

        if (IsActive)
        {
            Deactivate();
            return true;
        }

        if (modifierScope != null)
        {
            throw new InvalidOperationException(
                "Previous Berserk modifiers are not cleaned up."
            );
        }

        // 게이지가 최대치의 10% 미만이면 활성화 불가능.
        if (gauge.Current <
            gauge.Maximum * settings.ActivationThresholdRatio)
        {
            return false;
        }

        Activate();
        return true;
    }


    // On/Off 상태와 관계없이 매 프레임 호출해야 한다.
    // 부모의 Tick()은 활성 상태에서만 실행되기 때문에
    // Off 상태의 게이지 회복은 Advance()에서 처리한다.
    public void Advance(float deltaTime)
    {
        if (IsDisposed)
            return;

        if (float.IsNaN(deltaTime) ||
            float.IsInfinity(deltaTime) ||
            deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }

        EnsureGauge();

        toggleCooldownRemaining = Math.Max(
            0f,
            toggleCooldownRemaining - deltaTime
        );

        // Off 상태: 초당 2 회복.
        if (!IsActive)
        {
            gauge.Restore(
                CalculateAmount(settings.RestorePerSecond, deltaTime)
            );

            return;
        }

        // On 상태: 초당 5 소모.
        // On 상태에서는 자연 회복하지 않는다.
        gauge.Drain(
            CalculateAmount(settings.DrainPerSecond, deltaTime)
        );

        // 게이지 고갈 시 전환 쿨타임을 기다리지 않고 즉시 Off.
        if (gauge.Current <= 0f)
        {
            Deactivate();
            return;
        }

        Tick(deltaTime);
    }


    protected override void OnActivate()
    {
        EnsureGauge();

        // 부모 Activate()를 직접 호출했을 때도 조건을 검사한다.
        if (toggleCooldownRemaining > 0f ||
            gauge.Current <
            gauge.Maximum * settings.ActivationThresholdRatio)
        {
            throw new InvalidOperationException(
                "Berserk cannot be activated yet."
            );
        }

        if (modifierScope != null)
        {
            throw new InvalidOperationException(
                "Previous Berserk modifiers are not cleaned up."
            );
        }

        var newScope = new ModifierScope(Stats);

        try
        {
            AddPercent(
                newScope,
                StatKey.MoveSpeed,
                settings.MoveSpeedPercent
            );

            AddPercent(
                newScope,
                StatKey.AttackSpeed,
                settings.AttackSpeedPercent
            );

            AddPercent(
                newScope,
                StatKey.AttackRange,
                settings.AttackRangePercent
            );

            AddPercent(
                newScope,
                StatKey.ProjectileSpeedMultiplier,
                settings.ProjectileSpeedPercent
            );

            if (settings.DamageReductionBonus != 0f)
            {
                newScope.AddModifier(
                    StatKey.DamageReduction,
                    ModifierType.BaseFlat,
                    settings.DamageReductionBonus
                );
            }
        }
        catch
        {
            newScope.Dispose();
            throw;
        }

        modifierScope = newScope;

        toggleCooldownRemaining = settings.ToggleCooldownSeconds;
    }


    protected override void OnDeactivate()
    {
        toggleCooldownRemaining = settings.ToggleCooldownSeconds;

        ReleaseModifiers();
    }


    protected override void OnDispose()
    {
        // 비활성화 중 정리가 실패했다면 다시 시도한다.
        ReleaseModifiers();
    }


    private void ReleaseModifiers()
    {
        if (modifierScope == null)
            return;

        modifierScope.Dispose();

        // 정리에 성공했을 때만 참조를 해제한다.
        modifierScope = null;
    }


    private static void AddPercent(
        ModifierScope scope,
        StatKey stat,
        float percent)
    {
        if (percent != 0f)
        {
            scope.AddModifier(
                stat,
                ModifierType.BasePercent,
                percent
            );
        }
    }


    private void EnsureGauge()
    {
        if (gauge == null)
        {
            throw new InvalidOperationException(
                "Bind the character's SpecialGaugeRuntime first."
            );
        }
    }


    private static float CalculateAmount(
        float perSecond,
        float deltaTime)
    {
        return (float)Math.Min(
            (double)perSecond * deltaTime,
            float.MaxValue
        );
    }


    private void ValidateSettings()
    {
        ValidateRange(
            settings.ActivationThresholdRatio,
            0f,
            1f,
            "ActivationThresholdRatio"
        );

        ValidatePositive(
            settings.ToggleCooldownSeconds,
            "ToggleCooldownSeconds"
        );

        ValidatePositive(
            settings.DrainPerSecond,
            "DrainPerSecond"
        );

        ValidatePositive(
            settings.RestorePerSecond,
            "RestorePerSecond"
        );

        ValidateFinite(
            settings.MoveSpeedPercent,
            "MoveSpeedPercent"
        );

        ValidateFinite(
            settings.AttackSpeedPercent,
            "AttackSpeedPercent"
        );

        ValidateFinite(
            settings.AttackRangePercent,
            "AttackRangePercent"
        );

        ValidateFinite(
            settings.ProjectileSpeedPercent,
            "ProjectileSpeedPercent"
        );

        ValidateRange(
            settings.DamageReductionBonus,
            0f,
            1f,
            "DamageReductionBonus"
        );

        ValidateRange(
            settings.SlowResistanceBonus,
            0f,
            1f,
            "SlowResistanceBonus"
        );

        ValidateRange(
            settings.LifeStealFraction,
            0f,
            1f,
            "LifeStealFraction"
        );
    }


    private static void ValidatePositive(float value, string name)
    {
        ValidateFinite(value, name);

        if (value < 0f)
            throw new ArgumentOutOfRangeException(name);
    }


    private static void ValidateRange(
        float value,
        float min,
        float max,
        string name)
    {
        ValidateFinite(value, name);

        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(name);
    }


    private static void ValidateFinite(float value, string name)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name);
    }

    // 공용 능력 입력 → 버서커 On/Off
    public override bool RequestAbility()
    {
        return RequestToggle();
    }

    // 공용 프레임 갱신 → 버서커 게이지 소모·회복
    public override void UpdateAbility(float deltaTime)
    {
        Advance(deltaTime);
    }
    
}
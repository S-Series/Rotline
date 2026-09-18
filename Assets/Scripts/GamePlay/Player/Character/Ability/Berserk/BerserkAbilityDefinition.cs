using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BerserkAbility",
    menuName = "Game/Abilities/Berserk"
)]
public sealed class BerserkAbilityDefinition : CharacterAbilityDefinition
{
    [Header("Gauge / Toggle")]
    [SerializeField, Range(0f, 1f)]
    private float activationThresholdRatio = 0.1f;

    [SerializeField, Min(0f)]
    private float toggleCooldownSeconds = 1f;

    [SerializeField, Min(0f)]
    private float drainPerSecond = 5f;

    [SerializeField, Min(0f)]
    private float restorePerSecond = 2f;

    [Header("Stat bonuses - percentages")]
    [SerializeField] private float moveSpeedPercent;
    [SerializeField] private float attackSpeedPercent;
    [SerializeField] private float attackRangePercent;
    [SerializeField] private float projectileSpeedPercent;

    [Header("Defense")]
    [SerializeField, Range(0f, 1f)]
    private float damageReductionBonus;

    [Header("Additional Effects")]
    [SerializeField, Range(0f, 1f)]
    private float slowResistanceBonus;

    [SerializeField, Range(0f, 1f)]
    private float lifeStealFraction = 0.05f;


    public float ActivationThresholdRatio => activationThresholdRatio;
    public float ToggleCooldownSeconds => toggleCooldownSeconds;
    public float DrainPerSecond => drainPerSecond;
    public float RestorePerSecond => restorePerSecond;

    public float MoveSpeedPercent => moveSpeedPercent;
    public float AttackSpeedPercent => attackSpeedPercent;
    public float AttackRangePercent => attackRangePercent;
    public float ProjectileSpeedPercent => projectileSpeedPercent;

    public float DamageReductionBonus => damageReductionBonus;
    public float SlowResistanceBonus => slowResistanceBonus;
    public float LifeStealFraction => lifeStealFraction;


    // 새로운 공용 생성 경로.
    public override CharacterAbilityRuntime CreateRuntime(
        AbilityContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!context.HasSpecialGauge)
        {
            throw new InvalidOperationException(
                "Berserk requires a SpecialGaugeRuntime."
            );
        }

        return CreateRuntime(
            context.Stats,
            context.SpecialGauge
        );
    }


    // 기존 생성 경로와의 호환성을 위해 유지.
    // 이 경로에서는 게이지가 연결되지 않는다.
    public override CharacterAbilityRuntime CreateRuntime(
        PlayerStats stats)
    {
        return new BerserkAbilityRuntime(this, stats);
    }


    // 실제 버서커 생성 및 게이지 연결.
    public BerserkAbilityRuntime CreateRuntime(
        PlayerStats stats,
        SpecialGaugeRuntime characterGauge)
    {
        if (characterGauge == null)
        {
            throw new ArgumentNullException(
                nameof(characterGauge)
            );
        }

        var runtime = new BerserkAbilityRuntime(this, stats);

        runtime.BindGauge(characterGauge);

        return runtime;
    }
}
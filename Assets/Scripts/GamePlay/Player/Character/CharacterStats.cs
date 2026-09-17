using System;
using UnityEngine;

[Serializable]
public struct CharacterStats
{
    // =========================
    // Survival
    // =========================

    [Header("Survival")]
    [SerializeField]
    private float maxHealth;

    [SerializeField]
    private float healthRegen;

    // 0~1: 받는 피해 감소율
    [SerializeField]
    private float damageReduction;


    // =========================
    // Resource
    // =========================

    [Header("Resource")]
    [SerializeField]
    private float maxResource;

    [SerializeField]
    private float resourceRegen;


    // =========================
    // Movement
    // =========================

    [Header("Movement")]
    [SerializeField]
    private float moveSpeed;

    [SerializeField]
    private float acceleration;

    [SerializeField]
    private float deceleration;


    // =========================
    // Combat
    // =========================

    [Header("Combat")]
    [SerializeField]
    private float attackPower;

    // 초당 공격 횟수
    [SerializeField]
    private float attackSpeed;

    [SerializeField]
    private float attackRange;

    // 배율: 기본값 1
    [SerializeField]
    private float projectileSpeedMultiplier;

    [SerializeField]
    private float knockbackPower;

    // 0~1
    [SerializeField]
    private float criticalChance;

    // 피해 배율. 기본값은 아직 미확정.
    [SerializeField]
    private float criticalDamage;


    // =========================
    // Purification
    // =========================

    [Header("Purification")]
    // 초당 오염 제거량
    [SerializeField]
    private float purificationPower;

    [SerializeField]
    private float purificationRange;

    // 세부 계산 규칙 미확정
    [SerializeField]
    private float purificationEfficiency;


    // =========================
    // Progression
    // =========================

    [Header("Progression")]
    // 배율: 기본값 1
    [SerializeField]
    private float experienceGainRate;

    [SerializeField]
    private float pickupRange;


    // =========================
    // Special
    // =========================

    [Header("Special")]
    // 음수 허용
    [SerializeField]
    private float luck;


    // =========================
    // Public Properties
    // =========================

    // Survival
    public float MaxHealth => maxHealth;
    public float HealthRegen => healthRegen;
    public float DamageReduction => damageReduction;

    // Resource
    public float MaxResource => maxResource;
    public float ResourceRegen => resourceRegen;

    // Movement
    public float MoveSpeed => moveSpeed;
    public float Acceleration => acceleration;
    public float Deceleration => deceleration;

    // Combat
    public float AttackPower => attackPower;
    public float AttackSpeed => attackSpeed;
    public float AttackRange => attackRange;
    public float ProjectileSpeedMultiplier =>
        projectileSpeedMultiplier;
    public float KnockbackPower => knockbackPower;
    public float CriticalChance => criticalChance;
    public float CriticalDamage => criticalDamage;

    // Purification
    public float PurificationPower => purificationPower;
    public float PurificationRange => purificationRange;
    public float PurificationEfficiency =>
        purificationEfficiency;

    // Progression
    public float ExperienceGainRate => experienceGainRate;
    public float PickupRange => pickupRange;

    // Special
    public float Luck => luck;
}
using System;
using UnityEngine;

[Serializable]
public struct CharacterStats
{
    [Header("Survival")]
    [SerializeField]
    private float maxHealth;

    [Header("Movement")]
    [SerializeField]
    private float moveSpeed;

    [SerializeField]
    private float acceleration;

    [SerializeField]
    private float deceleration;

    [Header("Combat")]
    [SerializeField]
    private float attackPower;

    [SerializeField]
    private float attackSpeed;

    [Header("Purification")]
    [SerializeField]
    private float purificationPower;

    [SerializeField]
    private float purificationRange;


    public float MaxHealth => maxHealth;

    public float MoveSpeed => moveSpeed;
    public float Acceleration => acceleration;
    public float Deceleration => deceleration;

    public float AttackPower => attackPower;
    public float AttackSpeed => attackSpeed;

    public float PurificationPower => purificationPower;
    public float PurificationRange => purificationRange;
}
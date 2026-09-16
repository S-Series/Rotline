using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerStats : MonoBehaviour
{
    public float MaxHealth { get; private set; }

    // Movement
    public float BaseMoveSpeed { get; private set; }
    public float MoveSpeed { get; private set; }

    public float Acceleration { get; private set; }
    public float Deceleration { get; private set; }

    // Combat
    public float AttackPower { get; private set; }
    public float AttackSpeed { get; private set; }

    // Purification
    public float PurificationPower { get; private set; }
    public float PurificationRange { get; private set; }


    // Runtime modifiers
    private readonly Dictionary<int, float> moveSpeedModifiers =
        new Dictionary<int, float>();

    private int nextModifierId = 1;


    public void Initialize(CharacterStats baseStats)
    {
        MaxHealth = baseStats.MaxHealth;

        BaseMoveSpeed = baseStats.MoveSpeed;
        Acceleration = baseStats.Acceleration;
        Deceleration = baseStats.Deceleration;

        AttackPower = baseStats.AttackPower;
        AttackSpeed = baseStats.AttackSpeed;

        PurificationPower = baseStats.PurificationPower;
        PurificationRange = baseStats.PurificationRange;

        // 캐릭터 초기화 시 기존 효과 제거
        moveSpeedModifiers.Clear();

        RecalculateMoveSpeed();
    }


    // 이동속도 배율 추가
    // 반환된 ID는 효과가 종료될 때 제거하는 데 사용한다.
    public int AddMoveSpeedModifier(float multiplier)
    {
        if (multiplier < 0f ||
            float.IsNaN(multiplier) ||
            float.IsInfinity(multiplier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier),
                "Modifier 배율은 0 이상의 유한한 값이어야 합니다."
            );
        }

        int modifierId = nextModifierId++;

        moveSpeedModifiers.Add(
            modifierId,
            multiplier
        );

        RecalculateMoveSpeed();

        return modifierId;
    }


    // 지정한 이동속도 Modifier 제거
    public bool RemoveMoveSpeedModifier(int modifierId)
    {
        if (!moveSpeedModifiers.Remove(modifierId))
            return false;

        RecalculateMoveSpeed();

        return true;
    }


    private void RecalculateMoveSpeed()
    {
        float multiplier = 1f;

        foreach (float value in moveSpeedModifiers.Values)
        {
            multiplier *= value;
        }

        MoveSpeed = BaseMoveSpeed * multiplier;
    }

    [ContextMenu("Test Move Speed Modifiers")]
private void TestMoveSpeedModifiers()
{
    float original = MoveSpeed;

    int buff = AddMoveSpeedModifier(1.3f);
    int debuff = AddMoveSpeedModifier(0.85f);

    Debug.Log($"Original: {original}");
    Debug.Log($"Combined: {MoveSpeed}");

    RemoveMoveSpeedModifier(buff);
    Debug.Log($"Debuff Only: {MoveSpeed}");

    RemoveMoveSpeedModifier(debuff);
    Debug.Log($"Restored: {MoveSpeed}");
}
}
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerStats : MonoBehaviour
{
    private readonly Dictionary<StatKey, ModifierCollection> stats = new();
    private readonly Dictionary<int, (StatKey Stat, int LocalId)>
        modifierHandles = new();

    private int nextModifierHandleId = 1;

    // Survival
    public float MaxHealth => GetValue(StatKey.MaxHealth);
    public float HealthRegen => GetValue(StatKey.HealthRegen);
    public float DamageReduction => GetValue(StatKey.DamageReduction);

    // Resource
    public float MaxResource => GetValue(StatKey.MaxResource);
    public float ResourceRegen => GetValue(StatKey.ResourceRegen);

    // Movement
    public float BaseMoveSpeed =>
        GetCollection(StatKey.MoveSpeed).BaseValue;

    public float MoveSpeed => GetValue(StatKey.MoveSpeed);
    public float Acceleration => GetValue(StatKey.Acceleration);
    public float Deceleration => GetValue(StatKey.Deceleration);

    // Combat
    public float AttackPower => GetValue(StatKey.AttackPower);
    public float AttackSpeed => GetValue(StatKey.AttackSpeed);
    public float AttackRange => GetValue(StatKey.AttackRange);

    public float ProjectileSpeedMultiplier =>
        GetValue(StatKey.ProjectileSpeedMultiplier);

    public float KnockbackPower => GetValue(StatKey.KnockbackPower);
    public float CriticalChance => GetValue(StatKey.CriticalChance);
    public float CriticalDamage => GetValue(StatKey.CriticalDamage);

    // Purification
    public float PurificationPower => GetValue(StatKey.PurificationPower);
    public float PurificationRange => GetValue(StatKey.PurificationRange);

    public float PurificationEfficiency =>
        GetValue(StatKey.PurificationEfficiency);

    // Progression
    public float ExperienceGainRate =>
        GetValue(StatKey.ExperienceGainRate);

    public float PickupRange => GetValue(StatKey.PickupRange);

    // Special
    public float Luck => GetValue(StatKey.Luck);


    public void Initialize(CharacterStats baseStats)
    {
        // 모든 Collection 생성이 성공한 뒤 한꺼번에 교체한다.
        var newStats = new Dictionary<StatKey, ModifierCollection>();

        // Survival
        AddStat(newStats, StatKey.MaxHealth, baseStats.MaxHealth);
        AddStat(newStats, StatKey.HealthRegen, baseStats.HealthRegen);

        AddStat(
            newStats,
            StatKey.DamageReduction,
            baseStats.DamageReduction,
            0f,
            1f
        );

        // Resource
        AddStat(newStats, StatKey.MaxResource, baseStats.MaxResource);
        AddStat(newStats, StatKey.ResourceRegen, baseStats.ResourceRegen);

        // Movement
        AddStat(newStats, StatKey.MoveSpeed, baseStats.MoveSpeed);
        AddStat(newStats, StatKey.Acceleration, baseStats.Acceleration);
        AddStat(newStats, StatKey.Deceleration, baseStats.Deceleration);

        // Combat
        AddStat(newStats, StatKey.AttackPower, baseStats.AttackPower);
        AddStat(newStats, StatKey.AttackSpeed, baseStats.AttackSpeed);
        AddStat(newStats, StatKey.AttackRange, baseStats.AttackRange);

        AddStat(
            newStats,
            StatKey.ProjectileSpeedMultiplier,
            baseStats.ProjectileSpeedMultiplier
        );

        AddStat(newStats, StatKey.KnockbackPower, baseStats.KnockbackPower);

        AddStat(
            newStats,
            StatKey.CriticalChance,
            baseStats.CriticalChance,
            0f,
            1f
        );

        AddStat(newStats, StatKey.CriticalDamage, baseStats.CriticalDamage);

        // Purification
        AddStat(
            newStats,
            StatKey.PurificationPower,
            baseStats.PurificationPower
        );

        AddStat(
            newStats,
            StatKey.PurificationRange,
            baseStats.PurificationRange
        );

        AddStat(
            newStats,
            StatKey.PurificationEfficiency,
            baseStats.PurificationEfficiency
        );

        // Progression
        AddStat(
            newStats,
            StatKey.ExperienceGainRate,
            baseStats.ExperienceGainRate
        );

        AddStat(newStats, StatKey.PickupRange, baseStats.PickupRange);

        // 행운은 음수를 허용한다.
        AddStat(
            newStats,
            StatKey.Luck,
            baseStats.Luck,
            float.NegativeInfinity,
            float.PositiveInfinity
        );

        modifierHandles.Clear();

        stats.Clear();

        foreach (var entry in newStats)
        {
            stats.Add(entry.Key, entry.Value);
        }
    }


    public float GetValue(StatKey stat)
    {
        return GetCollection(stat).Value;
    }


    public int AddModifier(
        StatKey stat,
        ModifierType type,
        float amount)
    {
        return RegisterModifier(
            stat,
            () => GetCollection(stat).AddModifier(type, amount)
        );
    }


    public bool RemoveModifier(StatKey stat, int modifierId)
    {
        if (!modifierHandles.TryGetValue(modifierId, out var handle))
            return false;

        // 다른 스탯의 ID를 전달하면 제거하지 않는다.
        if (handle.Stat != stat)
            return false;

        if (!GetCollection(stat).Remove(handle.LocalId))
            return false;

        modifierHandles.Remove(modifierId);

        return true;
    }


    public int AddMoveSpeedModifier(float multiplier)
    {
        return RegisterModifier(
            StatKey.MoveSpeed,
            () => GetCollection(StatKey.MoveSpeed)
                .AddMultiplier(multiplier)
        );
    }


    public bool RemoveMoveSpeedModifier(int modifierId)
    {
        return RemoveModifier(StatKey.MoveSpeed, modifierId);
    }

    private int RegisterModifier(
        StatKey stat,
        Func<int> register)
    {
        if (nextModifierHandleId == int.MaxValue)
        {
            throw new InvalidOperationException(
                "Modifier handle ID limit reached."
            );
        }

        // 실제 Collection에 효과 등록
        int localId = register();

        // 외부에 반환할 고유 ID 발급
        int handleId = nextModifierHandleId++;

        modifierHandles.Add(
            handleId,
            (stat, localId)
        );

        return handleId;
    }

    private ModifierCollection GetCollection(StatKey stat)
    {
        if (!stats.TryGetValue(stat, out var collection))
        {
            throw new InvalidOperationException(
                $"PlayerStats is not initialized or stat is missing: {stat}"
            );
        }

        return collection;
    }


    private static void AddStat(
        Dictionary<StatKey, ModifierCollection> target,
        StatKey stat,
        float baseValue,
        float minValue = 0f,
        float maxValue = float.PositiveInfinity)
    {
        target.Add(
            stat,
            new ModifierCollection(baseValue, minValue, maxValue)
        );
    }

    //! Debugging Test Codes ==========================

    [ContextMenu("Test Move Speed Modifiers")]
    private void TestMoveSpeedModifiers()
    {
        float original = MoveSpeed;

        int buff = AddMoveSpeedModifier(1.3f);
        int debuff = AddMoveSpeedModifier(0.85f);

        Debug.Log($"Original: {original}", this);
        Debug.Log($"Combined: {MoveSpeed}", this);

        RemoveMoveSpeedModifier(buff);
        Debug.Log($"Debuff Only: {MoveSpeed}", this);

        RemoveMoveSpeedModifier(debuff);
        Debug.Log($"Restored: {MoveSpeed}", this);
    }

    [ContextMenu("Test Four Stage Modifiers")]
    private void TestFourStageModifiers()
    {
        // 실제 플레이어 스탯과 독립된 테스트용 Collection
        var speed = new ModifierCollection(7.5f);

        int baseFlat = speed.AddModifier(
            ModifierType.BaseFlat, 0.1f
        );

        int basePercent = speed.AddModifier(
            ModifierType.BasePercent, 10f
        );

        int finalFlat = speed.AddModifier(
            ModifierType.FinalFlat, 0.2f
        );

        int finalPercent = speed.AddModifier(
            ModifierType.FinalPercent, 5f
        );

        Debug.Log($"All Effects: {speed.Value}", this);

        speed.Remove(finalPercent);
        Debug.Log($"Without Final Percent: {speed.Value}", this);

        speed.Remove(finalFlat);
        Debug.Log($"Without Final Flat: {speed.Value}", this);

        speed.Clear();
        Debug.Log($"Restored: {speed.Value}", this);

        // 동일 단계 비율 합연산 검증
        speed.AddModifier(ModifierType.BasePercent, 10f);
        speed.AddModifier(ModifierType.BasePercent, 20f);

        Debug.Log($"Additive Percent: {speed.Value}", this);
    }

    [ContextMenu("Test Modifier Value Ranges")]
    private void TestModifierValueRanges()
    {
        // 1. 치명타 확률: 0~1
        var critical = new ModifierCollection(0.1f, 0f, 1f);

        int first = critical.AddModifier(
            ModifierType.BaseFlat, 0.8f
        );

        int second = critical.AddModifier(
            ModifierType.BaseFlat, 0.5f
        );

        Debug.Log($"Critical Capped: {critical.Value}", this);
        Debug.Assert(Mathf.Approximately(critical.Value, 1f));

        // 상한을 넘긴 상태에서 효과 하나를 제거한다.
        // 잘린 값 1.0에서 빼는 게 아니라 원본부터 재계산해야 한다.
        critical.Remove(second);

        Debug.Log($"Critical Recalculated: {critical.Value}", this);
        Debug.Assert(Mathf.Approximately(critical.Value, 0.9f));

        critical.Remove(first);

        Debug.Log($"Critical Restored: {critical.Value}", this);
        Debug.Assert(Mathf.Approximately(critical.Value, 0.1f));


        // 2. 행운: 음수 허용
        var luck = new ModifierCollection(
            0f,
            float.NegativeInfinity,
            float.PositiveInfinity
        );

        int badLuck = luck.AddModifier(
            ModifierType.BaseFlat, -3f
        );

        Debug.Log($"Negative Luck: {luck.Value}", this);
        Debug.Assert(Mathf.Approximately(luck.Value, -3f));

        int goodLuck = luck.AddModifier(
            ModifierType.BaseFlat, 1f
        );

        Debug.Log($"Combined Luck: {luck.Value}", this);
        Debug.Assert(Mathf.Approximately(luck.Value, -2f));

        luck.Remove(badLuck);
        luck.Remove(goodLuck);

        Debug.Log($"Luck Restored: {luck.Value}", this);
        Debug.Assert(Mathf.Approximately(luck.Value, 0f));
    }

    [ContextMenu("Test Independent Stat Modifiers")]
    private void TestIndependentStatModifiers()
    {
        float originalMoveSpeed = MoveSpeed;
        float originalAttackSpeed = AttackSpeed;

        int moveId = -1;
        int attackId = -1;

        try
        {
            // 1. 이동속도에만 +0.5
            moveId = AddModifier(
                StatKey.MoveSpeed,
                ModifierType.BaseFlat,
                0.5f
            );

            Debug.Log(
                $"Move Buff: Move={MoveSpeed}, Attack={AttackSpeed}",
                this
            );

            Debug.Assert(
                Mathf.Approximately(AttackSpeed, originalAttackSpeed),
                "이동속도 효과가 공격속도에 영향을 줬습니다."
            );

            // 2. 공격속도에만 +0.25
            float moveSpeedAfterBuff = MoveSpeed;

            attackId = AddModifier(
                StatKey.AttackSpeed,
                ModifierType.BaseFlat,
                0.25f
            );

            Debug.Log(
                $"Both Buffs: Move={MoveSpeed}, Attack={AttackSpeed}",
                this
            );

            Debug.Assert(
                Mathf.Approximately(MoveSpeed, moveSpeedAfterBuff),
                "공격속도 효과가 이동속도에 영향을 줬습니다."
            );

            // 3. 이동속도 효과만 제거
            float attackSpeedAfterBuff = AttackSpeed;

            RemoveModifier(StatKey.MoveSpeed, moveId);
            moveId = -1;

            Debug.Log(
                $"Attack Only: Move={MoveSpeed}, Attack={AttackSpeed}",
                this
            );

            Debug.Assert(
                Mathf.Approximately(MoveSpeed, originalMoveSpeed),
                "이동속도가 복원되지 않았습니다."
            );

            Debug.Assert(
                Mathf.Approximately(AttackSpeed, attackSpeedAfterBuff),
                "이동속도 효과 제거가 공격속도에 영향을 줬습니다."
            );
        }
        finally
        {
            // 테스트 도중 오류가 나도 등록한 효과를 정리
            if (moveId != -1)
                RemoveModifier(StatKey.MoveSpeed, moveId);

            if (attackId != -1)
                RemoveModifier(StatKey.AttackSpeed, attackId);
        }

        Debug.Log(
            $"Restored: Move={MoveSpeed}, Attack={AttackSpeed}",
            this
        );

        Debug.Assert(
            Mathf.Approximately(MoveSpeed, originalMoveSpeed) &&
            Mathf.Approximately(AttackSpeed, originalAttackSpeed),
            "스탯이 원래 값으로 복원되지 않았습니다."
        );
    }

    [ContextMenu("Test Modifier Handle Safety")]
    private void TestModifierHandleSafety()
    {
        float originalMove = MoveSpeed;
        float originalAttack = AttackSpeed;

        int moveId = -1;
        int attackId = -1;

        try
        {
            // 서로 다른 스탯에 효과 등록
            moveId = AddModifier(
                StatKey.MoveSpeed,
                ModifierType.BaseFlat,
                0.5f
            );

            attackId = AddModifier(
                StatKey.AttackSpeed,
                ModifierType.BaseFlat,
                0.25f
            );

            Debug.Log(
                $"IDs: Move={moveId}, Attack={attackId}",
                this
            );

            Debug.Assert(
                moveId != attackId,
                "서로 다른 효과의 외부 ID가 중복됐습니다."
            );

            // 이동속도 ID를 공격속도 제거에 잘못 전달
            bool wrongRemove = RemoveModifier(
                StatKey.AttackSpeed,
                moveId
            );

            Debug.Log($"Wrong Remove: {wrongRemove}", this);

            Debug.Assert(
                !wrongRemove,
                "잘못된 StatKey로 효과가 제거됐습니다."
            );

            Debug.Assert(
                Mathf.Approximately(MoveSpeed, originalMove + 0.5f) &&
                Mathf.Approximately(AttackSpeed, originalAttack + 0.25f),
                "잘못된 제거 요청으로 스탯이 변경됐습니다."
            );

            // 올바른 ID로 이동속도 효과 제거
            bool correctRemove = RemoveModifier(
                StatKey.MoveSpeed,
                moveId
            );

            Debug.Log($"Correct Remove: {correctRemove}", this);

            Debug.Assert(
                correctRemove &&
                Mathf.Approximately(MoveSpeed, originalMove),
                "올바른 효과 제거에 실패했습니다."
            );

            // 이미 제거한 ID를 다시 사용
            bool repeatedRemove = RemoveModifier(
                StatKey.MoveSpeed,
                moveId
            );

            Debug.Log($"Repeated Remove: {repeatedRemove}", this);

            Debug.Assert(
                !repeatedRemove,
                "이미 제거한 ID가 다시 사용됐습니다."
            );
        }
        finally
        {
            // 테스트 효과만 정리
            if (moveId != -1)
                RemoveModifier(StatKey.MoveSpeed, moveId);

            if (attackId != -1)
                RemoveModifier(StatKey.AttackSpeed, attackId);
        }

        Debug.Log(
            $"Restored: Move={MoveSpeed}, Attack={AttackSpeed}",
            this
        );

        Debug.Assert(
            Mathf.Approximately(MoveSpeed, originalMove) &&
            Mathf.Approximately(AttackSpeed, originalAttack),
            "테스트 후 스탯이 복원되지 않았습니다."
        );
    }

    [ContextMenu("Test Modifier Reinitialization")]
    private void TestModifierReinitialization()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Play Mode에서 테스트해 주세요.",
                this
            );
            return;
        }

        // 실제 플레이어와 독립된 임시 객체
        GameObject testObject =
            new GameObject("ModifierReinitializationTest");

        try
        {
            PlayerStats testStats =
                testObject.AddComponent<PlayerStats>();

            // ID 동작만 검사하므로 기본값이 모두 0인 테스트 데이터 사용
            CharacterStats testBaseStats = default;

            // 첫 번째 초기화
            testStats.Initialize(testBaseStats);

            int oldId = testStats.AddModifier(
                StatKey.MoveSpeed,
                ModifierType.BaseFlat,
                0.5f
            );

            Debug.Log(
                $"Before Reinitialize: ID={oldId}, Speed={testStats.MoveSpeed}",
                this
            );

            // 두 번째 초기화: 기존 효과와 ID 무효화
            testStats.Initialize(testBaseStats);

            int newId = testStats.AddModifier(
                StatKey.MoveSpeed,
                ModifierType.BaseFlat,
                0.25f
            );

            Debug.Log(
                $"After Reinitialize: ID={newId}, Speed={testStats.MoveSpeed}",
                this
            );

            // 오래된 ID가 새 효과를 제거할 수 없어야 한다.
            bool removedOld = testStats.RemoveModifier(
                StatKey.MoveSpeed,
                oldId
            );

            Debug.Log(
                $"Remove Old: {removedOld}, Speed={testStats.MoveSpeed}",
                this
            );

            Debug.Assert(oldId != newId);
            Debug.Assert(!removedOld);
            Debug.Assert(
                Mathf.Approximately(testStats.MoveSpeed, 0.25f)
            );

            // 새로운 ID는 정상적으로 제거되어야 한다.
            bool removedNew = testStats.RemoveModifier(
                StatKey.MoveSpeed,
                newId
            );

            Debug.Log(
                $"Remove New: {removedNew}, Speed={testStats.MoveSpeed}",
                this
            );

            Debug.Assert(removedNew);
            Debug.Assert(
                Mathf.Approximately(testStats.MoveSpeed, 0f)
            );
        }
        finally
        {
            Destroy(testObject);
        }
    }

}
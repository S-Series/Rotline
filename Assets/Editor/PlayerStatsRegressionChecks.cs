using System;
using UnityEditor;
using UnityEngine;

public static class PlayerStatsRegressionChecks
{
    [MenuItem("ROTLINE/Tests/Player Stats")]
    private static void Run()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning(
                "[PlayerStats] Run this test outside Play Mode."
            );
            return;
        }

        GameObject testObject =
            new GameObject("PlayerStatsRegressionTest");

        try
        {
            PlayerStats stats =
                testObject.AddComponent<PlayerStats>();

            // 실제 캐릭터 에셋을 변경하지 않는 독립 테스트 데이터.
            CharacterStats baseStats =
                JsonUtility.FromJson<CharacterStats>(
                    "{\"moveSpeed\":7.5," +
                    "\"attackSpeed\":2," +
                    "\"experienceGainRate\":1," +
                    "\"projectileSpeedMultiplier\":1}"
                );

            stats.Initialize(baseStats);

            TestMoveSpeed(stats);
            TestStatIndependence(stats);
            TestHandleSafety(stats);
            TestReinitialization(stats, baseStats);

            Debug.Log(
                "[PlayerStats] All regression checks passed."
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static void TestMoveSpeed(PlayerStats stats)
    {
        Check("Original speed", stats.MoveSpeed, 7.5f);

        int buff = stats.AddMoveSpeedModifier(1.3f);
        int debuff = stats.AddMoveSpeedModifier(0.85f);

        Check("Combined speed", stats.MoveSpeed, 8.625f);

        Require(
            stats.RemoveMoveSpeedModifier(buff),
            "Failed to remove speed buff."
        );

        Check("Debuff only", stats.MoveSpeed, 6.375f);

        Require(
            stats.RemoveMoveSpeedModifier(debuff),
            "Failed to remove speed debuff."
        );

        Check("Speed restored", stats.MoveSpeed, 7.5f);
    }

    private static void TestStatIndependence(PlayerStats stats)
    {
        int moveId = stats.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            0.5f
        );

        Check("Move buff", stats.MoveSpeed, 8f);
        Check("Attack unchanged", stats.AttackSpeed, 2f);

        int attackId = stats.AddModifier(
            StatKey.AttackSpeed,
            ModifierType.BaseFlat,
            0.25f
        );

        Check("Move unchanged", stats.MoveSpeed, 8f);
        Check("Attack buff", stats.AttackSpeed, 2.25f);

        Require(
            stats.RemoveModifier(StatKey.MoveSpeed, moveId),
            "Failed to remove move modifier."
        );

        Check("Move restored", stats.MoveSpeed, 7.5f);
        Check("Attack retained", stats.AttackSpeed, 2.25f);

        Require(
            stats.RemoveModifier(StatKey.AttackSpeed, attackId),
            "Failed to remove attack modifier."
        );

        Check("Attack restored", stats.AttackSpeed, 2f);
    }

    private static void TestHandleSafety(PlayerStats stats)
    {
        int moveId = stats.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            0.5f
        );

        int attackId = stats.AddModifier(
            StatKey.AttackSpeed,
            ModifierType.BaseFlat,
            0.25f
        );

        Require(
            moveId != attackId,
            "Modifier handles must be unique."
        );

        bool wrongRemove = stats.RemoveModifier(
            StatKey.AttackSpeed,
            moveId
        );

        Require(
            !wrongRemove,
            "A handle removed a modifier from the wrong stat."
        );

        Check("Move after wrong removal", stats.MoveSpeed, 8f);
        Check("Attack after wrong removal", stats.AttackSpeed, 2.25f);

        Require(
            stats.RemoveModifier(StatKey.MoveSpeed, moveId),
            "Correct removal failed."
        );

        Require(
            !stats.RemoveModifier(StatKey.MoveSpeed, moveId),
            "A removed handle was accepted again."
        );

        Require(
            stats.RemoveModifier(StatKey.AttackSpeed, attackId),
            "Attack modifier removal failed."
        );

        Check("Handle test move restored", stats.MoveSpeed, 7.5f);
        Check("Handle test attack restored", stats.AttackSpeed, 2f);
    }

    private static void TestReinitialization(
        PlayerStats stats,
        CharacterStats baseStats)
    {
        int oldId = stats.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            0.5f
        );

        Check("Before reinitialization", stats.MoveSpeed, 8f);

        stats.Initialize(baseStats);

        int newId = stats.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            0.25f
        );

        Require(
            oldId != newId,
            "Handle ID was reused after initialization."
        );

        bool removedOld = stats.RemoveModifier(
            StatKey.MoveSpeed,
            oldId
        );

        Require(
            !removedOld,
            "An old handle removed a new modifier."
        );

        Check("New modifier retained", stats.MoveSpeed, 7.75f);

        Require(
            stats.RemoveModifier(StatKey.MoveSpeed, newId),
            "Failed to remove new modifier."
        );

        Check("Final speed restored", stats.MoveSpeed, 7.5f);
    }

    private static void Check(
        string name,
        float actual,
        float expected)
    {
        if (float.IsNaN(actual) ||
            Mathf.Abs(actual - expected) > 0.0001f)
        {
            throw new InvalidOperationException(
                $"[PlayerStats] {name} failed. " +
                $"Expected={expected}, Actual={actual}"
            );
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                $"[PlayerStats] {message}"
            );
        }
    }
}
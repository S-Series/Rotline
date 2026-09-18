using System;
using UnityEditor;
using UnityEngine;

public static class ModifierScopeRegressionChecks
{
    [MenuItem("ROTLINE/Tests/Modifier Scope")]
    private static void Run()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning(
                "[ModifierScope] Run this test outside Play Mode."
            );
            return;
        }

        GameObject testObject =
            new GameObject("ModifierScopeRegressionTest");

        try
        {
            PlayerStats stats =
                testObject.AddComponent<PlayerStats>();

            CharacterStats baseStats =
                JsonUtility.FromJson<CharacterStats>(
                    "{\"moveSpeed\":7.5," +
                    "\"attackSpeed\":2," +
                    "\"experienceGainRate\":1," +
                    "\"projectileSpeedMultiplier\":1}"
                );

            stats.Initialize(baseStats);

            TestMultipleModifiers(stats);
            TestScopeIsolation(stats);
            TestExternalModifier(stats);
            TestDisposeSafety(stats);
            TestDisposeFailureAndRetry(stats);

            Debug.Log(
                "[ModifierScope] All regression checks passed."
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static void TestMultipleModifiers(PlayerStats stats)
    {
        var scope = new ModifierScope(stats);

        scope.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            0.5f
        );

        scope.AddModifier(
            StatKey.AttackSpeed,
            ModifierType.BaseFlat,
            0.25f
        );

        Require(scope.Count == 2, "Incorrect handle count.");

        Check("Move buff", stats.MoveSpeed, 8f);
        Check("Attack buff", stats.AttackSpeed, 2.25f);

        scope.Dispose();

        Require(scope.IsDisposed, "Scope was not disposed.");
        Require(scope.Count == 0, "Handles were not cleared.");

        Check("Move restored", stats.MoveSpeed, 7.5f);
        Check("Attack restored", stats.AttackSpeed, 2f);
    }

    private static void TestScopeIsolation(PlayerStats stats)
    {
        var scopeA = new ModifierScope(stats);
        var scopeB = new ModifierScope(stats);

        scopeA.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            0.5f
        );

        scopeA.AddModifier(
            StatKey.AttackSpeed,
            ModifierType.BaseFlat,
            0.25f
        );

        scopeB.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            1f
        );

        Check("Both scopes move", stats.MoveSpeed, 9f);
        Check("Both scopes attack", stats.AttackSpeed, 2.25f);

        scopeA.Dispose();

        Check("Scope B move retained", stats.MoveSpeed, 8.5f);
        Check("Scope A attack removed", stats.AttackSpeed, 2f);

        Require(!scopeB.IsDisposed, "Scope B was disposed.");

        scopeB.Dispose();

        Check("All scopes removed", stats.MoveSpeed, 7.5f);
    }

    private static void TestExternalModifier(PlayerStats stats)
    {
        int externalId = stats.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            2f
        );

        var scope = new ModifierScope(stats);

        scope.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            0.5f
        );

        Check("External and scope", stats.MoveSpeed, 10f);

        scope.Dispose();

        Check("External retained", stats.MoveSpeed, 9.5f);

        Require(
            stats.RemoveModifier(StatKey.MoveSpeed, externalId),
            "Failed to remove external modifier."
        );

        Check("External removed", stats.MoveSpeed, 7.5f);
    }

    private static void TestDisposeSafety(PlayerStats stats)
    {
        var scope = new ModifierScope(stats);

        scope.Dispose();
        scope.Dispose();

        Require(scope.IsDisposed, "Scope should be disposed.");
        Require(scope.Count == 0, "Disposed scope contains handles.");

        bool threw = false;

        try
        {
            scope.AddModifier(
                StatKey.MoveSpeed,
                ModifierType.BaseFlat,
                1f
            );
        }
        catch (ObjectDisposedException)
        {
            threw = true;
        }

        Require(threw, "Disposed scope accepted a modifier.");

        Check("Speed unchanged", stats.MoveSpeed, 7.5f);
    }

    private static void TestDisposeFailureAndRetry(PlayerStats stats)
    {
        var scope = new ModifierScope(stats);

        // 음수 효과는 단독으로도 등록 가능하다.
        scope.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            -3e38f
        );

        scope.AddModifier(
            StatKey.AttackSpeed,
            ModifierType.BaseFlat,
            0.25f
        );

        // 외부 효과 두 개를 더한다.
        // 현재 합계는 유효하지만 음수 효과를 제거하면
        // float 범위를 초과하도록 구성한다.
        int externalA = stats.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            2e38f
        );

        int externalB = stats.AddModifier(
            StatKey.MoveSpeed,
            ModifierType.BaseFlat,
            2e38f
        );

        float speedBeforeDispose = stats.MoveSpeed;

        bool caughtExpectedException = false;

        try
        {
            scope.Dispose();
        }
        catch (AggregateException ex)
        {
            caughtExpectedException =
                ex.InnerExceptions.Count == 1 &&
                ex.InnerExceptions[0] is InvalidOperationException;
        }

        Require(
            caughtExpectedException,
            "Expected an AggregateException from Dispose."
        );

        // Dispose가 실패했더라도 종료는 이미 시작된 상태.
        // 새로운 Modifier 등록은 허용되면 안 된다.
        int remainingCount = scope.Count;
        float speedAfterFailure = stats.MoveSpeed;

        bool addRejected = false;

        try
        {
            scope.AddModifier(
                StatKey.MoveSpeed,
                ModifierType.BaseFlat,
                1f
            );
        }
        catch (ObjectDisposedException)
        {
            addRejected = true;
        }

        Require(
            addRejected,
            "Scope accepted a modifier after disposal started."
        );

        Require(
            scope.Count == remainingCount,
            "Rejected registration changed the handle count."
        );

        Require(
            stats.MoveSpeed == speedAfterFailure,
            "Rejected registration changed move speed."
        );

        // 공격속도 효과는 제거됐고,
        // 제거에 실패한 이동속도 효과만 남아야 한다.
        Require(
            !scope.IsDisposed,
            "Failed scope must allow disposal retry."
        );

        Require(
            scope.Count == 1,
            "Only the failed modifier should remain."
        );

        Check("Attack cleaned up", stats.AttackSpeed, 2f);

        Require(
            stats.MoveSpeed == speedBeforeDispose,
            "Failed removal changed move speed."
        );

        // 외부 효과 하나를 제거해 범위를 정상화한다.
        Require(
            stats.RemoveModifier(StatKey.MoveSpeed, externalB),
            "Failed to remove external modifier B."
        );

        // 남아 있는 Scope 효과 제거를 재시도한다.
        scope.Dispose();

        Require(scope.IsDisposed, "Scope retry did not finish.");
        Require(scope.Count == 0, "Scope still contains handles.");

        // 마지막 외부 효과도 제거한다.
        Require(
            stats.RemoveModifier(StatKey.MoveSpeed, externalA),
            "Failed to remove external modifier A."
        );

        Check("Speed restored after retry", stats.MoveSpeed, 7.5f);
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
                $"[ModifierScope] {name} failed. " +
                $"Expected={expected}, Actual={actual}"
            );
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                $"[ModifierScope] {message}"
            );
        }
    }
}
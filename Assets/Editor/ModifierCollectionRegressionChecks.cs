using System;
using UnityEditor;
using UnityEngine;

public static class ModifierCollectionRegressionChecks
{
    [MenuItem("ROTLINE/Tests/Modifier Collection")]
    private static void Run()
    {
        TestFourStageCalculation();
        TestValueRanges();

        Debug.Log(
            "[ModifierCollection] All regression checks passed."
        );
    }

    private static void TestFourStageCalculation()
    {
        var speed = new ModifierCollection(7.5f);

        speed.AddModifier(ModifierType.BaseFlat, 0.1f);
        speed.AddModifier(ModifierType.BasePercent, 10f);

        int finalFlat = speed.AddModifier(
            ModifierType.FinalFlat, 0.2f
        );

        int finalPercent = speed.AddModifier(
            ModifierType.FinalPercent, 5f
        );

        Check("Four stages", speed.Value, 8.9675f);

        speed.Remove(finalPercent);
        Check("Remove final percent", speed.Value, 8.55f);

        speed.Remove(finalFlat);
        Check("Remove final flat", speed.Value, 8.35f);

        speed.Clear();
        Check("Restore base", speed.Value, 7.5f);

        // 동일 단계의 비율은 합연산
        speed.AddModifier(ModifierType.BasePercent, 10f);
        speed.AddModifier(ModifierType.BasePercent, 20f);

        Check("Additive percent", speed.Value, 9.75f);
    }

    private static void TestValueRanges()
    {
        // 치명타 확률: 0~1
        var critical = new ModifierCollection(0.1f, 0f, 1f);

        int first = critical.AddModifier(
            ModifierType.BaseFlat, 0.8f
        );

        int second = critical.AddModifier(
            ModifierType.BaseFlat, 0.5f
        );

        Check("Critical cap", critical.Value, 1f);

        // 상한에 도달한 상태에서도 원본 Modifier로 재계산
        critical.Remove(second);
        Check("Critical recalculation", critical.Value, 0.9f);

        critical.Remove(first);
        Check("Critical restore", critical.Value, 0.1f);

        // 행운: 음수 허용
        var luck = new ModifierCollection(
            0f,
            float.NegativeInfinity,
            float.PositiveInfinity
        );

        int badLuck = luck.AddModifier(
            ModifierType.BaseFlat, -3f
        );

        Check("Negative luck", luck.Value, -3f);

        int goodLuck = luck.AddModifier(
            ModifierType.BaseFlat, 1f
        );

        Check("Combined luck", luck.Value, -2f);

        luck.Remove(badLuck);
        luck.Remove(goodLuck);

        Check("Luck restore", luck.Value, 0f);
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
                $"[ModifierCollection] {name} failed. " +
                $"Expected={expected}, Actual={actual}"
            );
        }
    }
}
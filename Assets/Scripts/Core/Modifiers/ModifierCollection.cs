using System;
using System.Collections.Generic;

public enum ModifierType
{
    BaseFlat,
    BasePercent,
    FinalFlat,
    FinalPercent
}

public sealed class ModifierCollection
{
    private readonly struct Modifier
    {
        public readonly ModifierType Type;
        public readonly float Amount;

        public Modifier(ModifierType type, float amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    private readonly Dictionary<int, Modifier> modifiers = new();

    private int nextId = 1;

    public float BaseValue { get; }

    public float MinValue { get; }
    public float MaxValue { get; }

    public float Value { get; private set; }

    public int Count => modifiers.Count;

    public event Action<float> ValueChanged;

    public ModifierCollection(
        float baseValue,
        float minValue = 0f,
        float maxValue = float.PositiveInfinity
        )
    {
        ValidateFinite(baseValue);

        // 하한은 음의 무한대까지,
        // 상한은 양의 무한대까지 지정할 수 있다.
        if (float.IsNaN(minValue) ||
            float.IsPositiveInfinity(minValue))
        {
            throw new ArgumentOutOfRangeException(nameof(minValue));
        }

        if (float.IsNaN(maxValue) ||
            float.IsNegativeInfinity(maxValue))
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue));
        }

        if (minValue > maxValue)
        {
            throw new ArgumentException(
                "Minimum value cannot exceed maximum value."
            );
        }

        if (baseValue < minValue || baseValue > maxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseValue),
                "Base value must be within the configured range."
            );
        }

        BaseValue = baseValue;
        MinValue = minValue;
        MaxValue = maxValue;

        Value = baseValue;
    }
    // 새로운 4단계 Modifier API
    public int AddModifier(ModifierType type, float amount)
    {
        ValidateFinite(amount);

        if (!Enum.IsDefined(typeof(ModifierType), type))
            throw new ArgumentOutOfRangeException(nameof(type));

        if (nextId == int.MaxValue)
            throw new InvalidOperationException("Modifier ID limit reached.");

        Modifier modifier = new Modifier(type, amount);

        // 등록 전에 결과를 검증한다.
        float nextValue = CalculateValue(
            additionalModifier: modifier
        );

        int id = nextId++;

        modifiers.Add(id, modifier);

        SetValue(nextValue);

        return id;
    }

    // 기존 PlayerStats 및 AbilityRuntime 호환용.
    // 1.3f -> 기본 비율 +30%
    // 0.85f -> 기본 비율 -15%
    public int AddMultiplier(float multiplier)
    {
        ValidateFinite(multiplier);

        if (multiplier < 0f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));

        double percent = ((double)multiplier - 1d) * 100d;

        if (percent < -float.MaxValue ||
            percent > float.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }

        return AddModifier(
            ModifierType.BasePercent,
            (float)percent
        );
    }

    public bool Remove(int id)
    {
        if (!modifiers.ContainsKey(id))
            return false;

        // 제거 후 결과도 먼저 검증한다.
        float nextValue = CalculateValue(excludedId: id);

        modifiers.Remove(id);

        SetValue(nextValue);

        return true;
    }

    public void Clear()
    {
        if (modifiers.Count == 0)
            return;

        modifiers.Clear();

        SetValue(BaseValue);
    }

    private float CalculateValue(
        int? excludedId = null,
        Modifier? additionalModifier = null)
    {
        double baseFlat = 0d;
        double basePercent = 0d;
        double finalFlat = 0d;
        double finalPercent = 0d;

        foreach (var entry in modifiers)
        {
            if (excludedId.HasValue &&
                entry.Key == excludedId.Value)
            {
                continue;
            }

            Accumulate(
                entry.Value,
                ref baseFlat,
                ref basePercent,
                ref finalFlat,
                ref finalPercent
            );
        }

        if (additionalModifier.HasValue)
        {
            Accumulate(
                additionalModifier.Value,
                ref baseFlat,
                ref basePercent,
                ref finalFlat,
                ref finalPercent
            );
        }

        double result =
            (
                BaseValue * (1d + basePercent / 100d)
                + baseFlat
            )
            * (1d + finalPercent / 100d)
            + finalFlat;

        if (double.IsNaN(result) ||
            double.IsInfinity(result) ||
            result < -float.MaxValue ||
            result > float.MaxValue)
        {
            throw new InvalidOperationException(
                "Calculated modifier value is invalid."
            );
        }

        // 최종 결과에만 범위 적용.
        // Modifier 원본과 단계별 합산값은 변경하지 않는다.
        double clampedValue = Math.Max(
            MinValue,
            Math.Min(MaxValue, result)
        );

        return (float)clampedValue;
    }

    private static void Accumulate(
        Modifier modifier,
        ref double baseFlat,
        ref double basePercent,
        ref double finalFlat,
        ref double finalPercent)
    {
        switch (modifier.Type)
        {
            case ModifierType.BaseFlat:
                baseFlat += modifier.Amount;
                break;

            case ModifierType.BasePercent:
                basePercent += modifier.Amount;
                break;

            case ModifierType.FinalFlat:
                finalFlat += modifier.Amount;
                break;

            case ModifierType.FinalPercent:
                finalPercent += modifier.Amount;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(modifier)
                );
        }
    }

    private void SetValue(float nextValue)
    {
        if (Value == nextValue)
            return;

        Value = nextValue;

        ValueChanged?.Invoke(Value);
    }

    private static void ValidateFinite(float value)
    {
        if (float.IsNaN(value) ||
            float.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Modifier value must be finite."
            );
        }
    }
}
using System;
using System.Collections.Generic;

public sealed class ModifierCollection
{
    private readonly Dictionary<int, float> multipliers = new();

    private int nextId = 1;

    public float BaseValue { get; }

    public float Value { get; private set; }

    public int Count => multipliers.Count;

    public event Action<float> ValueChanged;


    public ModifierCollection(float baseValue)
    {
        Validate(baseValue);

        BaseValue = baseValue;
        Value = baseValue;
    }


    // 배율 효과를 등록하고, 제거에 사용할 ID를 반환한다.
    public int AddMultiplier(float multiplier)
    {
        Validate(multiplier);

        if (nextId == int.MaxValue)
            throw new InvalidOperationException(
                "Modifier ID limit reached."
            );

        int id = nextId++;

        multipliers.Add(id, multiplier);

        Recalculate();

        return id;
    }


    // 해당 ID의 효과만 제거한다.
    public bool Remove(int id)
    {
        if (!multipliers.Remove(id))
            return false;

        Recalculate();

        return true;
    }


    // 이 Collection에 등록된 효과를 모두 제거한다.
    public void Clear()
    {
        if (multipliers.Count == 0)
            return;

        multipliers.Clear();

        Recalculate();
    }


    private void Recalculate()
    {
        float multiplier = 1f;

        foreach (float value in multipliers.Values)
        {
            multiplier *= value;
        }

        float nextValue = BaseValue * multiplier;

        if (float.IsNaN(nextValue) ||
            float.IsInfinity(nextValue))
        {
            throw new InvalidOperationException(
                "Calculated modifier value is invalid."
            );
        }

        if (Value == nextValue)
            return;

        Value = nextValue;

        ValueChanged?.Invoke(Value);
    }


    private static void Validate(float value)
    {
        if (value < 0f ||
            float.IsNaN(value) ||
            float.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Value must be finite and non-negative."
            );
        }
    }
}
using System;

public sealed class SpecialGaugeRuntime
{
    public const float DefaultMaximum = 100f;

    public float Maximum { get; private set; }

    public float Current { get; private set; }

    public float Ratio => Current / Maximum;


    public SpecialGaugeRuntime(
        float maximum = DefaultMaximum,
        float? initialValue = null)
    {
        ValidateMaximum(maximum);

        float initial = initialValue ?? maximum;
        ValidateAmount(initial, nameof(initialValue));

        Maximum = maximum;
        Current = Math.Min(initial, maximum);
    }


    // 부족하면 아무것도 소모하지 않는다.
    // 발동 비용 등 전액 지불이 필요한 상황에 사용한다.
    public bool TrySpend(float amount)
    {
        ValidateAmount(amount, nameof(amount));

        if (Current < amount)
            return false;

        Current -= amount;
        return true;
    }


    // 현재량이 부족하면 0까지만 소모한다.
    // 버서커의 초당 게이지 소모 등에 사용한다.
    // 반환값은 실제로 소모된 양.
    public float Drain(float amount)
    {
        ValidateAmount(amount, nameof(amount));

        float actual = Math.Min(Current, amount);

        Current -= actual;

        return actual;
    }


    // 최대치를 초과하지 않도록 회복한다.
    // 반환값은 실제로 회복된 양.
    public float Restore(float amount)
    {
        ValidateAmount(amount, nameof(amount));

        float actual = Math.Min(
            Maximum - Current,
            amount
        );

        Current += actual;

        return actual;
    }


    // 최대치가 줄어들면 현재량도 새 최대치로 제한한다.
    // 현재량의 비율을 자동으로 보존하지는 않는다.
    public void SetMaximum(float maximum)
    {
        ValidateMaximum(maximum);

        Maximum = maximum;
        Current = Math.Min(Current, Maximum);
    }


    private static void ValidateMaximum(float value)
    {
        if (float.IsNaN(value) ||
            float.IsInfinity(value) ||
            value <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Maximum must be finite and greater than zero."
            );
        }
    }


    private static void ValidateAmount(
        float value,
        string parameterName)
    {
        if (float.IsNaN(value) ||
            float.IsInfinity(value) ||
            value < 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Amount must be finite and non-negative."
            );
        }
    }
}
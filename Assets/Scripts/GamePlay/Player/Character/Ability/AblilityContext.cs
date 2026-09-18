using System;

public sealed class AbilityContext
{
    public PlayerStats Stats { get; }

    // 특수 게이지가 없는 캐릭터는 null.
    public SpecialGaugeRuntime SpecialGauge { get; }

    public bool HasSpecialGauge => SpecialGauge != null;


    public AbilityContext(
        PlayerStats stats,
        SpecialGaugeRuntime specialGauge = null)
    {
        Stats = stats != null
            ? stats
            : throw new ArgumentNullException(nameof(stats));

        SpecialGauge = specialGauge;
    }
}
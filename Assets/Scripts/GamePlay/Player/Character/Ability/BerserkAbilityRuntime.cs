using System;

public sealed class BerserkAbilityRuntime : CharacterAbilityRuntime
{
    private readonly float moveSpeedMultiplier;

    private int? moveSpeedModifierId;


    public BerserkAbilityRuntime(
        CharacterAbilityDefinition definition,
        PlayerStats stats,
        float moveSpeedMultiplier
    ) : base(definition, stats)
    {
        if (moveSpeedMultiplier < 0f ||
            float.IsNaN(moveSpeedMultiplier) ||
            float.IsInfinity(moveSpeedMultiplier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(moveSpeedMultiplier)
            );
        }

        this.moveSpeedMultiplier = moveSpeedMultiplier;
    }


    protected override void OnActivate()
    {
        moveSpeedModifierId =
            Stats.AddMoveSpeedModifier(moveSpeedMultiplier);
    }


    protected override void OnDeactivate()
    {
        if (!moveSpeedModifierId.HasValue)
            return;

        Stats.RemoveMoveSpeedModifier(
            moveSpeedModifierId.Value
        );

        moveSpeedModifierId = null;
    }
}
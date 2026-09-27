using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Modules/Trigger")]
public sealed class StandardTriggerDefinition : AttackTriggerDefinition
{
    [SerializeField] private AttackTrigger mode;
    public override AttackTriggerRuntime CreateRuntime(AttackRecipe recipe,
        Func<AttackActivation, bool> fire, Action stop) =>
        new StandardAttackTriggerRuntime(mode, recipe, fire, stop);
}

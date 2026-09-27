using System;
using UnityEngine;

public readonly struct AttackActivation
{
    public readonly Vector2 Direction;
    public readonly float Charge;
    public readonly bool Channel;
    public AttackActivation(Vector2 direction, float charge = 1f, bool channel = false)
    { Direction = direction; Charge = charge; Channel = channel; }
}

public abstract class AttackTriggerDefinition : ScriptableObject
{
    public abstract AttackTriggerRuntime CreateRuntime(AttackRecipe recipe,
        Func<AttackActivation, bool> fire, Action stop);
}

public abstract class AttackTriggerRuntime
{
    public virtual bool RequiresChargeInput => false;
    public virtual bool IsCharging => false;
    public virtual void Started(Vector2 aim) { }
    public virtual void Held(Vector2 aim) { }
    public virtual void Released(Vector2 aim) { }
    public virtual void Tick(float deltaTime) { }
    public abstract void Cancel();
}

// Compatibility for existing serialized weapons; new triggers can supply their own runtime.
public sealed class StandardAttackTriggerRuntime : AttackTriggerRuntime
{
    private readonly AttackTrigger mode;
    private readonly float chargeSeconds;
    private readonly float minimumCharge;
    private readonly Func<AttackActivation, bool> fire;
    private readonly Action stop;
    private bool charging;
    private float elapsed;
    public override bool IsCharging => charging;
    public override bool RequiresChargeInput => mode == AttackTrigger.ChargeRelease;

    public StandardAttackTriggerRuntime(AttackTrigger mode, AttackRecipe recipe,
        Func<AttackActivation, bool> fire, Action stop)
    {
        this.mode = mode; this.fire = fire; this.stop = stop;
        chargeSeconds = recipe.chargeSeconds; minimumCharge = recipe.minimumCharge;
    }
    public override void Started(Vector2 aim)
    {
        if (mode == AttackTrigger.ChargeRelease) { charging = true; elapsed = 0f; }
        else fire(new AttackActivation(aim, 1f, mode == AttackTrigger.Channel));
    }
    public override void Held(Vector2 aim)
    {
        if (mode == AttackTrigger.Automatic || mode == AttackTrigger.Channel)
            fire(new AttackActivation(aim, 1f, mode == AttackTrigger.Channel));
    }
    public override void Released(Vector2 aim)
    {
        if (mode == AttackTrigger.Channel) { stop(); return; }
        if (!charging) return;
        charging = false;
        float charge = Mathf.Clamp01(elapsed / chargeSeconds);
        if (charge >= minimumCharge) fire(new AttackActivation(aim, charge));
    }
    public override void Tick(float deltaTime)
    { if (charging) elapsed = Mathf.Min(chargeSeconds, elapsed + deltaTime); }
    public override void Cancel() { charging = false; elapsed = 0f; }
}

using UnityEngine;

public sealed class ModularWeaponAttackRuntime : WeaponAttackRuntime
{
    private readonly AttackRecipe recipe;
    private readonly AttackTriggerRuntime input;
    private readonly AttackExecutionRuntime execution;
    private AttackActivation activation;
    private bool channeling;
    private float recovery;
    public override bool IsCharging => !IsDisposed && input != null && input.IsCharging;
    public override bool RequiresChargeInput => input != null && input.RequiresChargeInput;
    public int ActiveBodyCount => execution.ActiveBodyCount;
    public int PendingEmissionCount => execution.PendingCount;

    public ModularWeaponAttackRuntime(WeaponAttackDefinition definition, PlayerStats stats,
        Transform owner, LayerMask layers, AttackRecipe recipe, CombatEffectStateStore effects = null)
        : base(definition, stats, owner, layers, effects)
    {
        this.recipe = recipe.Copy();
        execution = new AttackExecutionRuntime(owner, layers, EffectStates);
        try { input = this.recipe.CreateTrigger(Fire, CancelAttack); }
        catch { Dispose(); throw; }
    }
    public override void PrimaryStarted(Vector2 direction)
    { if (CanStart && !IsCharging) input.Started(direction); }
    public override void PrimaryHeld(Vector2 direction)
    { if (!IsDisposed) input.Held(direction); }
    public override void PrimaryReleased(Vector2 direction)
    { if (!IsDisposed) input.Released(direction); }
    private bool Fire(AttackActivation request)
    {
        if (channeling && request.Channel && IsAttacking)
        {
            execution.SetChannelAim(request.Direction);
            return true;
        }
        activation = request;
        return RequestAttack(request.Direction);
    }
    protected override void StartAttack(AttackSnapshot attack)
    {
        recovery = attack.Duration;
        channeling = activation.Channel;
        float charge = activation.Direction.sqrMagnitude > 0f ? activation.Charge : 1f;
        var shot = new AttackSnapshot(attack.Direction, attack.Damage * recipe.damageMultiplier,
            attack.Range, recipe.lifetime, charge, attack.ProjectileSpeedMultiplier);
        var context = execution.CreateAttack(recipe, shot);
        execution.Activate(context, Owner.position);
        if (!IsAttacking) return;
        execution.Emit(context, Owner.position, channeling);
        NotifyAttackActiveStarted(shot);
    }
    protected override void TickPersistent(float deltaTime)
    {
        input.Tick(deltaTime);
        execution.Tick(deltaTime);
    }
    protected override bool UpdateAttack(float deltaTime)
    {
        recovery -= deltaTime;
        return channeling || recovery > 0f;
    }
    protected override void UpdatePhysics() { }
    protected override void CancelCurrentAttack()
    {
        channeling = false;
        input?.Cancel();
        execution?.Cancel();
    }
    protected override void ReleaseAttack() => execution?.Dispose();
}

using System.Collections.Generic;
using UnityEngine;

public sealed class ModularWeaponAttackRuntime : WeaponAttackRuntime
{
    private readonly AttackRecipe recipe;
    private readonly List<AttackBodyRuntime> bodies = new();
    private bool charging;
    private float chargeElapsed;
    private float releaseCharge = 1f;
    private float recovery;
    public override bool IsCharging => charging && !IsDisposed;
    public int ActiveBodyCount => bodies.Count;

    public ModularWeaponAttackRuntime(WeaponAttackDefinition definition, PlayerStats stats,
        Transform owner, LayerMask layers, AttackRecipe recipe) : base(definition, stats, owner, layers)
    {
        this.recipe = recipe.Copy();
    }

    public override void PrimaryStarted(Vector2 direction)
    {
        if (recipe.trigger != AttackTrigger.ChargeRelease)
        {
            RequestAttack(direction);
            return;
        }
        if (!CanStart || charging) return;
        charging = true;
        chargeElapsed = 0f;
    }

    public override void PrimaryHeld(Vector2 direction)
    {
        if (recipe.trigger == AttackTrigger.Automatic) RequestAttack(direction);
    }

    public override void PrimaryReleased(Vector2 direction)
    {
        if (!IsCharging) return;
        charging = false;
        releaseCharge = Mathf.Clamp01(chargeElapsed / recipe.chargeSeconds);
        if (releaseCharge < recipe.minimumCharge) return;
        RequestAttack(direction);
    }

    protected override void StartAttack(AttackSnapshot attack)
    {
        float charge = recipe.trigger == AttackTrigger.ChargeRelease ? releaseCharge : 1f;
        recovery = attack.Duration;
        var shot = new AttackSnapshot(attack.Direction, attack.Damage * recipe.damageMultiplier,
            attack.Range, recipe.lifetime, charge, attack.ProjectileSpeedMultiplier);
        for (int i = 0; i < recipe.projectileCount; i++)
        {
            // A full circle excludes its duplicate endpoint.
            float denominator = recipe.spreadDegrees >= 360f
                ? recipe.projectileCount : Mathf.Max(1, recipe.projectileCount - 1);
            float angle = recipe.projectileCount == 1 ? 0f
                : -recipe.spreadDegrees * 0.5f + recipe.spreadDegrees * i / denominator;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * shot.Direction;
            bodies.Add(new AttackBodyRuntime(recipe, shot, direction, Owner, EnemyLayers));
        }
        NotifyAttackActiveStarted(shot);
    }

    protected override void TickPersistent(float deltaTime)
    {
        if (charging) chargeElapsed = Mathf.Min(recipe.chargeSeconds, chargeElapsed + deltaTime);
        // Damage callbacks may cancel/dispose this runtime and clear the list.
        for (int i = bodies.Count - 1; i >= 0; i--)
        {
            var body = bodies[i];
            bool alive = body.Tick(deltaTime);
            if (IsDisposed || i >= bodies.Count || bodies[i] != body) return;
            if (!alive)
            {
                body.Dispose();
                bodies.RemoveAt(i);
            }
        }
    }

    protected override bool UpdateAttack(float deltaTime)
    {
        recovery -= deltaTime;
        return recovery > 0f;
    }

    protected override void UpdatePhysics() { }
    protected override void CancelCurrentAttack()
    {
        charging = false;
        chargeElapsed = 0f;
        foreach (var body in bodies) body.Dispose();
        bodies.Clear();
    }
    protected override void ReleaseAttack() => CancelCurrentAttack();
}

using UnityEngine;

public enum CombatEffectEvent { Attack, Hit, Kill, Expire }

// A reusable proc building block, not an item, loot definition or inventory entry.
[CreateAssetMenu(menuName = "Game/Combat/Effects/Spawn On Combat Event")]
public sealed class SpawnOnEventEffectDefinition : CombatEffectDefinition
{
    [SerializeField] private CombatEffectEvent onEvent = CombatEffectEvent.Hit;
    [SerializeField, Min(1)] private int everyNth = 1;
    [SerializeField, Min(0f)] private float cooldown;
    [SerializeField, Min(0.01f)] private float damageScale = 0.5f;
    [SerializeField] private AttackRecipe child = new();
    public override CombatEffectRuntime CreateRuntime() =>
        new Runtime(onEvent, Mathf.Max(1, everyNth), Mathf.Max(0f, cooldown), damageScale, child.Copy());
    private sealed class Runtime : CombatEffectRuntime
    {
        private readonly CombatEffectEvent trigger;
        private readonly int every;
        private readonly float cooldown;
        private readonly float scale;
        private readonly AttackRecipe child;
        private int count;
        private double readyAt;
        public Runtime(CombatEffectEvent trigger, int every, float cooldown, float scale, AttackRecipe child)
        { this.trigger = trigger; this.every = every; this.cooldown = cooldown; this.scale = scale; this.child = child; }
        private void Handle(CombatEffectEvent kind, CombatEvent context)
        {
            if (trigger != kind || ++count % every != 0 || context.Time < readyAt) return;
            if (context.SpawnChild(child, context.Attack.Direction, scale)) readyAt = context.Time + cooldown;
        }
        public override void OnAttack(CombatEvent context) => Handle(CombatEffectEvent.Attack, context);
        public override void OnHit(CombatEvent context) => Handle(CombatEffectEvent.Hit, context);
        public override void OnKill(CombatEvent context) => Handle(CombatEffectEvent.Kill, context);
        public override void OnExpire(CombatEvent context) => Handle(CombatEffectEvent.Expire, context);
    }
}

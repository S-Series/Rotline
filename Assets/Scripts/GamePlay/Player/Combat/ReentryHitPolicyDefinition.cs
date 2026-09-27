using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Combat/Modules/Hit On Reentry")]
public sealed class ReentryHitPolicyDefinition : AttackHitPolicyDefinition
{
    public override AttackHitPolicyRuntime CreateRuntime(AttackRecipe recipe) => new Runtime();
    private sealed class Runtime : AttackHitPolicyRuntime
    {
        private HashSet<(EnemyActor, uint)> previous = new();
        private HashSet<(EnemyActor, uint)> current = new();
        public override void BeginSample(float time) => current.Clear();
        public override bool TryHit(EnemyActor target, float time)
        {
            var key = (target, target.SpawnVersion);
            return current.Add(key) && !previous.Contains(key);
        }
        public override void EndSample() { var swap = previous; previous = current; current = swap; }
    }
}

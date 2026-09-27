using System.Collections.Generic;
using UnityEngine;

public abstract class AttackHitPolicyDefinition : ScriptableObject
{
    public abstract AttackHitPolicyRuntime CreateRuntime(AttackRecipe recipe);
}

public abstract class AttackHitPolicyRuntime
{
    public virtual void BeginSample(float time) { }
    // Reserve before damage callbacks to prevent duplicate/reentrant hits.
    public abstract bool TryHit(EnemyActor target, float time);
    public virtual void EndSample() { }
}

public sealed class IntervalHitPolicyRuntime : AttackHitPolicyRuntime
{
    private readonly float interval;
    private readonly Dictionary<(EnemyActor, uint), float> nextHits = new();
    public IntervalHitPolicyRuntime(float interval) { this.interval = interval; }
    public override bool TryHit(EnemyActor target, float time)
    {
        var key = (target, target.SpawnVersion);
        bool previous = nextHits.TryGetValue(key, out float next);
        if (previous && time + 0.00001f < next) return false;
        int intervals = previous && interval > 0f
            ? Mathf.Max(1, Mathf.FloorToInt((time + 0.00001f - next) / interval) + 1) : 1;
        nextHits[key] = interval > 0f ? (previous ? next : time) + intervals * interval : float.PositiveInfinity;
        return true;
    }
}

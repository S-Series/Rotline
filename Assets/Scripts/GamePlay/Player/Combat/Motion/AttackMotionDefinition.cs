using UnityEngine;

public abstract class AttackMotionDefinition : ScriptableObject
{
    // 해당 Motion이 공격마다 진행 방향을 반전하는가?
    public virtual bool AlternateDirection => false;

    public abstract void ApplyPose(
        Transform attackTransform,
        AttackSnapshot attack,
        float progress
    );
}
using UnityEngine;

public readonly struct DamageContext
{
    public readonly Transform Source;
    public readonly float Amount;
    public readonly Vector2 Direction;
    public readonly bool IsCritical;
    public readonly int ChainDepth;
    public readonly long AttackId;
    public readonly long RootAttackId;

    public DamageContext(Transform source, float amount, Vector2 direction,
        bool isCritical = false, int chainDepth = 0, long attackId = 0, long rootAttackId = 0)
    {
        Source = source;
        Amount = amount;
        Direction = direction;
        IsCritical = isCritical;
        ChainDepth = chainDepth;
        AttackId = attackId;
        RootAttackId = rootAttackId;
    }
}

public readonly struct DamageResult
{
    public readonly float AppliedDamage;
    public readonly bool Killed;
    public DamageResult(float appliedDamage, bool killed)
    {
        AppliedDamage = appliedDamage;
        Killed = killed;
    }
}

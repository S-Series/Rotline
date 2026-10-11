using UnityEngine;

public enum EnemyRangeBand
{
    Melee = 0,
    Close = 1,
    Medium = 2,
    Long = 3,
    Sniper = 4
}

public static class EnemyRangeBands
{
    public static Vector2 DefaultDistances(EnemyRangeBand band)
    {
        switch (band)
        {
            case EnemyRangeBand.Melee: return new Vector2(1f, 1.5f);
            case EnemyRangeBand.Close: return new Vector2(2f, 3f);
            case EnemyRangeBand.Medium: return new Vector2(4f, 6f);
            case EnemyRangeBand.Long: return new Vector2(6f, 8f);
            case EnemyRangeBand.Sniper: return new Vector2(9f, 12f);
            default: return new Vector2(1f, 1.5f);
        }
    }
}

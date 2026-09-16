using UnityEngine;

public sealed class PlayerStats : MonoBehaviour
{
    public float MaxHealth { get; private set; }

    public float MoveSpeed { get; private set; }
    public float Acceleration { get; private set; }
    public float Deceleration { get; private set; }

    public float AttackPower { get; private set; }
    public float AttackSpeed { get; private set; }

    public float PurificationPower { get; private set; }
    public float PurificationRange { get; private set; }


    public void Initialize(
        CharacterStats baseStats)
    {
        MaxHealth =
            baseStats.MaxHealth;

        MoveSpeed =
            baseStats.MoveSpeed;

        Acceleration =
            baseStats.Acceleration;

        Deceleration =
            baseStats.Deceleration;

        AttackPower =
            baseStats.AttackPower;

        AttackSpeed =
            baseStats.AttackSpeed;

        PurificationPower =
            baseStats.PurificationPower;

        PurificationRange =
            baseStats.PurificationRange;
    }
}
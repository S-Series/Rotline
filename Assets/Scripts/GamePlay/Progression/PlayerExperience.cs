using System;
using UnityEngine;

[RequireComponent(typeof(PlayerCharacter))]
public sealed class PlayerExperience : MonoBehaviour
{
    [SerializeField, Min(1f)] private float firstLevelExperience = 5f;
    [SerializeField, Min(1f)] private float levelGrowth = 1.25f;
    public int Level { get; private set; } = 1;
    public double Current { get; private set; }
    public double Required => Math.Min(1000000000d, firstLevelExperience * Math.Pow(levelGrowth, Level - 1));
    public event Action<int> LeveledUp;
    public event Action Changed;
    public void AddExperience(float baseAmount)
    {
        if (!float.IsFinite(baseAmount) || baseAmount <= 0f) return;
        double amount = baseAmount * (double)GetComponent<PlayerStats>().ExperienceGainRate;
        if (double.IsInfinity(amount) || double.IsNaN(amount)) return;
        Current += amount;
        // Process overflow in Update so very large grants cannot lock the main thread.
        ProcessLevels();
        Changed?.Invoke();
    }
    private void Update() { if (Time.deltaTime > 0f) ProcessLevels(); }
    private void ProcessLevels()
    {
        for (int i = 0; i < 64 && Current >= Required; i++)
        {
            Current -= Required;
            Level++;
            LeveledUp?.Invoke(Level);
            Changed?.Invoke();
        }
    }
}

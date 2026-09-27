using System;
using UnityEngine;

[RequireComponent(typeof(PlayerCharacter))]
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(0f)] private float invulnerabilitySeconds = 0.7f;
    private PlayerStats stats;
    private float immuneUntil;
    public float Current { get; private set; }
    public float Maximum => stats == null ? 0f : stats.MaxHealth;
    public bool IsDead => Current <= 0f;
    public event Action Died;
    public event Action Changed;

    private void Start()
    {
        stats = GetComponent<PlayerStats>();
        Current = Maximum;
        stats.StatChanged += OnStatChanged;
        Changed?.Invoke();
    }
    private void Update()
    {
        if (stats != null && !IsDead && Time.deltaTime > 0f &&
            !(GetComponent<PlayerCharacter>().AbilityRuntime is BerserkAbilityRuntime berserk && berserk.BlocksHealthRegeneration))
            Heal(stats.HealthRegen * Time.deltaTime);
    }
    public void GrantInvulnerability(float seconds) => immuneUntil = Mathf.Max(immuneUntil, Time.time + Mathf.Max(0f, seconds));
    public bool TakeDamage(float amount)
    {
        if (stats == null || IsDead || Time.timeScale == 0f || Time.time < immuneUntil ||
            !float.IsFinite(amount) || amount <= 0f) return false;
        float damage = amount * (1f - Mathf.Clamp01(stats.DamageReduction));
        if (damage <= 0f) return false;
        Current = Mathf.Max(0f, Current - damage);
        immuneUntil = Time.time + invulnerabilitySeconds;
        Changed?.Invoke();
        if (IsDead) Died?.Invoke();
        return true;
    }
    public void Heal(float amount)
    {
        if (stats == null || IsDead || !float.IsFinite(amount) || amount <= 0f) return;
        float next = Mathf.Min(Maximum, Current + amount);
        if (next == Current) return;
        Current = next;
        Changed?.Invoke();
    }
    private void OnStatChanged(StatKey key)
    {
        if (key != StatKey.MaxHealth) return;
        bool alive = !IsDead;
        Current = Mathf.Min(Current, Maximum);
        Changed?.Invoke();
        if (alive && IsDead) Died?.Invoke();
    }
    private void OnDestroy() { if (stats != null) stats.StatChanged -= OnStatChanged; }
}

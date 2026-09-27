using UnityEngine;

public sealed class AlphaCorruptionHazard : MonoBehaviour
{
    [SerializeField, Range(0, 100)] private float slowPercent = 25f;
    [SerializeField, Min(0)] private float damage = 2f;
    [SerializeField, Min(0.1f)] private float damageInterval = 2f;
    private PlayerCharacter player;
    private PlayerHealth health;
    private CorruptionField field;
    private int slowHandle = -1;
    private float appliedSlow;
    private float nextDamage;
    private void Start() { player = GetComponent<PlayerCharacter>(); health = GetComponent<PlayerHealth>(); field = FindFirstObjectByType<CorruptionField>(); }
    private void Update()
    {
        if (Time.deltaTime <= 0f || field == null) return;
        bool infected = field.IsCorrupted(transform.position);
        float resistance = player.AbilityRuntime is BerserkAbilityRuntime berserk ? berserk.ActiveSlowResistanceBonus : 0f;
        float slow = infected ? -slowPercent * (1f - Mathf.Clamp01(resistance)) : 0f;
        if (!Mathf.Approximately(slow, appliedSlow))
        {
            Release(); appliedSlow = slow;
            if (slow != 0f) slowHandle = player.Stats.AddModifier(StatKey.MoveSpeed, ModifierType.FinalPercent, slow);
        }
        if (infected && Time.time >= nextDamage) { health.TakeDamage(damage); nextDamage = Time.time + Mathf.Max(0.1f, damageInterval); }
    }
    private void Release() { if (slowHandle >= 0 && player != null) player.Stats.RemoveModifier(StatKey.MoveSpeed, slowHandle); slowHandle = -1; appliedSlow = 0f; }
    private void OnDisable() => Release();
}

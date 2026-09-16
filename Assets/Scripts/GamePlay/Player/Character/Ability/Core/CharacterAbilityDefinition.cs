using UnityEngine;

public abstract class CharacterAbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string abilityId;

    [SerializeField]
    private string displayName;

    [Header("Settings")]
    [Min(0f)]
    [SerializeField]
    private float cooldown = 10f;

    public string AbilityId => abilityId;

    public string DisplayName => displayName;

    public float Cooldown => cooldown;

    // Definition은 설정만 보관한다.
    // 실제 실행 상태는 매번 새 Runtime에 생성한다.
    public abstract CharacterAbilityRuntime CreateRuntime(
        PlayerStats stats
    );
}
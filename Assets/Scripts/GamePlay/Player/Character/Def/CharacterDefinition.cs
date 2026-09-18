using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterDefinition",
    menuName = "Game/Characters/Character Definition"
)]
public sealed class CharacterDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string characterId;

    [SerializeField]
    private string displayName;

    [Header("Base Stats")]
    [SerializeField]
    private CharacterStats baseStats;

    [Header("Ability")]
    [SerializeField]
    private CharacterAbilityDefinition abilityDefinition;


    public string CharacterId => characterId;
    public string DisplayName => displayName;

    public CharacterStats BaseStats => baseStats;

    // 능력이 없는 캐릭터라면 null일 수 있다.
    public CharacterAbilityDefinition AbilityDefinition =>
        abilityDefinition;
}
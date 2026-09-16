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


    public string CharacterId => characterId;
    public string DisplayName => displayName;

    public CharacterStats BaseStats => baseStats;
}
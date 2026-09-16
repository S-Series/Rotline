using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
public sealed class PlayerCharacter : MonoBehaviour
{
    [SerializeField]
    private CharacterDefinition definition;

    private PlayerStats stats;


    public CharacterDefinition Definition =>
        definition;

    public PlayerStats Stats =>
        stats;


    private void Awake()
    {
        stats =
            GetComponent<PlayerStats>();

        if (definition == null)
        {
            Debug.LogError(
                $"{name}: CharacterDefinition이 설정되지 않았습니다.",
                this
            );

            return;
        }

        stats.Initialize(
            definition.BaseStats
        );
    }
}
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyDefinition",
    menuName = "Game/Enemies/Enemy Definition"
)]
public sealed class EnemyDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string enemyId;

    [SerializeField]
    private EnemyActor prefab;

    [Header("Stats")]
    [SerializeField]
    private float maxHealth = 10f;

    [SerializeField]
    private float moveSpeed = 2f;

    [SerializeField]
    private float contactDamage = 1f;

    [Header("Behaviors")]
    [SerializeField]
    private EnemyMovementDefinition movement;

    public string EnemyId => enemyId;
    public EnemyActor Prefab => prefab;

    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float ContactDamage => contactDamage;

    public EnemyMovementDefinition Movement => movement;
}
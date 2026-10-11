using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyDefinition",
    menuName = "Game/Enemies/Enemy Definition"
)]
public sealed class EnemyDefinition : ScriptableObject
{
    [Header("Alpha presentation and attacks")]
    public Color tint = Color.white;
    [Min(0.2f)] public float size = 1f;
    [Range(0, 2)] public int rangedStyle;
    [Min(0.2f)] public float shotInterval = 2.5f;
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

    [Header("Targeting and steering")]
    [SerializeField] private EnemyRangeBand rangeBand = EnemyRangeBand.Melee;
    [SerializeField, Min(0f)] private float minimumDistance = 1f;
    [SerializeField, Min(0f)] private float maximumDistance = 1.5f;
    [SerializeField, Min(0f)] private float separationRadius = 0.9f;
    [SerializeField, Range(0f, 1f)] private float separationWeight = 0.45f;
    [SerializeField, Min(0f)] private float meleeAttackRange = 1.5f;

    [Header("Behaviors")]
    [SerializeField]
    private EnemyMovementDefinition movement;

    public string EnemyId => enemyId;
    public EnemyActor Prefab => prefab;

    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float ContactDamage => contactDamage;
    public EnemyRangeBand RangeBand => rangeBand;
    public float MinimumDistance => minimumDistance;
    public float MaximumDistance => Mathf.Max(minimumDistance, maximumDistance);
    public float SeparationRadius => separationRadius;
    public float SeparationWeight => separationWeight;
    public float MeleeAttackRange => meleeAttackRange;

    public EnemyMovementDefinition Movement => movement;

    [ContextMenu("Apply Range Band Distance Preset")]
    private void ApplyRangeBandDistancePreset()
    {
        Vector2 distances = EnemyRangeBands.DefaultDistances(rangeBand);
        minimumDistance = distances.x;
        maximumDistance = distances.y;
    }
}

using UnityEngine;

[CreateAssetMenu(
    fileName = "AttackDefinition",
    menuName = "Game/Combat/Attack Definition"
)]
public sealed class WeaponAttackDefinition : ScriptableObject
{
    [Header("Attack Components")]
    [SerializeField]
    private GameObject hitboxPrefab;

    [SerializeField]
    private AttackMotionDefinition motion;

    [Header("Stats")]
    [SerializeField, Min(0.01f)]
    private float baseRange = 3f;

    [Header("Timing")]
    [SerializeField, Range(0f, 0.8f)]
    private float windupRatio = 0.2f;

    [SerializeField, Range(0.1f, 0.8f)]
    private float activeRatio = 0.5f;

    public GameObject HitboxPrefab => hitboxPrefab;
    public AttackMotionDefinition Motion => motion;

    public float BaseRange => baseRange;
    public float WindupRatio => windupRatio;

    public float ActiveRatio =>
        Mathf.Min(activeRatio, 1f - windupRatio);


    public WeaponAttackRuntime CreateRuntime(
        PlayerStats stats,
        Transform owner,
        LayerMask enemyLayers)
    {
        if (motion == null || hitboxPrefab == null)
        {
            Debug.LogError(
                $"{name}: 공격 구성 요소가 누락되었습니다.",
                this
            );

            return null;
        }

        if (hitboxPrefab.GetComponent<AttackHitbox>() == null ||
            hitboxPrefab.GetComponent<Collider2D>() == null)
        {
            Debug.LogError(
                $"{name}: Hitbox Prefab 루트에 " +
                "AttackHitbox와 Collider2D가 필요합니다.",
                this
            );

            return null;
        }

        return new ComposedWeaponAttackRuntime(
            this,
            stats,
            owner,
            enemyLayers
        );
    }
}
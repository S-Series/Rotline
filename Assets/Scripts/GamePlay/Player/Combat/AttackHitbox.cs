using System.Collections.Generic;
using UnityEngine;

public sealed class AttackHitbox : MonoBehaviour
{
    private readonly HashSet<EnemyActor> hitEnemies = new();
    private readonly List<Collider2D> overlaps = new(32);

    private Collider2D hitCollider;
    private ContactFilter2D filter;

    private float damage;
    private bool active;

    public bool IsActive => active;


    public void Initialize(
        Collider2D collider,
        LayerMask enemyLayers)
    {
        hitCollider = collider;

        filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayers);
        filter.useTriggers = true;

        End();
    }


    public void Begin(float attackDamage)
    {
        hitEnemies.Clear();

        damage = attackDamage;
        active = true;

        hitCollider.enabled = true;
    }


    public void Scan()
    {
        if (!active || hitCollider == null)
            return;

        // 회전시킨 Collider의 최신 Transform을
        // 물리 쿼리에 반영한다.
        Physics2D.SyncTransforms();

        overlaps.Clear();

        hitCollider.Overlap(filter, overlaps);

        foreach (Collider2D other in overlaps)
        {
            if (other == null || other == hitCollider)
                continue;

            EnemyActor enemy =
                other.GetComponentInParent<EnemyActor>();

            if (enemy == null ||
                enemy.IsDead ||
                !enemy.isActiveAndEnabled)
            {
                continue;
            }

            // 같은 공격에서 한 적에게 1회만 피해.
            if (!hitEnemies.Add(enemy))
                continue;

            enemy.TakeDamage(damage);
        }
    }


    public void End()
    {
        active = false;

        if (hitCollider != null)
            hitCollider.enabled = false;
    }


    private void OnDisable()
    {
        End();
    }
}
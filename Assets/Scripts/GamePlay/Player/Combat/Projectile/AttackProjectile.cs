using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class AttackProjectile : MonoBehaviour
{
    private readonly List<RaycastHit2D> hits = new(8);

    private CircleCollider2D hitCircle;
    private ContactFilter2D hitFilter;

    private Vector2 direction;

    private float damage;
    private float speed;

    private float remainingDistance;
    private float remainingLifetime;

    private bool launched;


    private void Awake()
    {
        hitCircle = GetComponent<CircleCollider2D>();

        hitCircle.isTrigger = true;
    }


    public void Launch(
        Vector2 launchDirection,
        float attackDamage,
        float projectileSpeed,
        float maxDistance,
        float maxLifetime,
        LayerMask enemyLayers)
    {
        direction = launchDirection.normalized;

        damage = Mathf.Max(0f, attackDamage);
        speed = Mathf.Max(0f, projectileSpeed);

        remainingDistance = Mathf.Max(0f, maxDistance);
        remainingLifetime = Mathf.Max(0f, maxLifetime);

        hitFilter = new ContactFilter2D();
        hitFilter.SetLayerMask(enemyLayers);
        hitFilter.useTriggers = true;

        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(direction.y, direction.x)
                * Mathf.Rad2Deg
        );

        launched =
            direction.sqrMagnitude > 0f &&
            damage > 0f &&
            speed > 0f &&
            remainingDistance > 0f &&
            remainingLifetime > 0f;

        hitCircle.enabled = launched;

        if (!launched)
            Destroy(gameObject);
    }


    private void Update()
    {
        if (!launched)
            return;

        float dt = Time.deltaTime;

        if (dt <= 0f)
            return;

        remainingLifetime -= dt;

        if (remainingLifetime <= 0f ||
            remainingDistance <= 0f)
        {
            Finish();
            return;
        }

        float movementDistance = Mathf.Min(
            speed * dt,
            remainingDistance
        );

        Vector2 origin = transform.position;

        // CircleCollider의 크기를 사용해
        // 이동 경로 전체에 대한 충돌을 검사한다.
        float radius =
            hitCircle.radius *
            Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y)
            );

        hits.Clear();

        Physics2D.CircleCast(
            origin,
            radius,
            direction,
            hitFilter,
            hits,
            movementDistance
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null ||
                hit.collider == hitCircle)
            {
                continue;
            }

            EnemyActor enemy =
                hit.collider.GetComponentInParent<EnemyActor>();

            if (enemy == null ||
                enemy.IsDead ||
                !enemy.isActiveAndEnabled)
            {
                continue;
            }

            transform.position =
                origin + direction * hit.distance;

            enemy.TakeDamage(damage);

            Finish();
            return;
        }

        transform.position =
            origin + direction * movementDistance;

        remainingDistance -= movementDistance;

        if (remainingDistance <= 0f)
            Finish();
    }


    private void Finish()
    {
        if (!launched)
            return;

        launched = false;
        hitCircle.enabled = false;

        Destroy(gameObject);
    }
}
using UnityEngine;

[RequireComponent(typeof(NavigationService))]
public sealed class SpawnDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform player;

    [SerializeField]
    private EnemyDefinition enemyDefinition;

    [SerializeField]
    private CorruptionField corruptionField;

    [Header("Spawn")]
    [SerializeField]
    private float spawnInterval = 2f;

    [SerializeField]
    private float spawnDistance = 12f;

    private EnemyPool enemyPool;
    private NavigationService navigation;

    private float spawnTimer;


    private void Awake()
    {
        navigation =
            GetComponent<NavigationService>();

        enemyPool =
            new EnemyPool(
                enemyDefinition,
                transform
            );
    }


    private void Update()
    {
        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
            return;

        SpawnEnemy();

        spawnTimer = spawnInterval;
    }


    private void SpawnEnemy()
    {
        Vector2 direction =
            Random.insideUnitCircle.normalized;

        Vector2 spawnPosition =
            (Vector2)player.position +
            direction * spawnDistance;

        EnemyActor enemy =
            enemyPool.Get();

        enemy.transform.position =
            spawnPosition;

        enemy.Died += OnEnemyDied;

        enemy.Initialize(
            enemyDefinition,
            player,
            navigation
        );
    }


    private void OnEnemyDied(
        EnemyActor enemy,
        Vector2 deathPosition)
    {
        enemy.Died -= OnEnemyDied;

        int added = 0;

        if (corruptionField != null)
        {
            added = corruptionField.Seed(deathPosition);
        }

        Debug.Log(
            $"[Enemy] Died at {deathPosition}, " +
            $"New corruption cells={added}",
            this
        );

        // 기존 풀링 시스템으로 반환.
        enemyPool.Release(enemy);
    }
}
using UnityEngine;

public sealed class SpawnDirector : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Transform player;

    [SerializeField]
    private EnemyDefinition enemyDefinition;


    [Header("Spawn")]

    [SerializeField]
    private float spawnInterval = 2f;

    [SerializeField]
    private float spawnDistance = 12f;


    private EnemyPool enemyPool;

    private float spawnTimer;


    private void Awake()
    {
        enemyPool = new EnemyPool(
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

        enemy.Initialize(
            enemyDefinition,
            player
        );
    }
}
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
    private readonly System.Collections.Generic.HashSet<EnemyActor> activeEnemies = new();
    public event System.Action<Vector2> EnemyKilled;


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
        if (player == null || enemyDefinition == null || enemyDefinition.Prefab == null ||
            enemyDefinition.Movement == null) return;
        Vector2 spawnPosition = default;
        bool found = false;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            spawnPosition = (Vector2)player.position + direction * spawnDistance;
            if (navigation.IsWalkable(spawnPosition)) { found = true; break; }
        }
        if (!found) return;

        EnemyActor enemy =
            enemyPool.Get();

        enemy.transform.position =
            spawnPosition;

        enemy.Died += OnEnemyDied;

        try
        {
            enemy.Initialize(enemyDefinition, player, navigation);
        }
        catch
        {
            enemyPool.Release(enemy);
            throw;
        }
        activeEnemies.Add(enemy);
        enemy.gameObject.SetActive(true);
    }


    private void OnEnemyDied(
        EnemyActor enemy,
        Vector2 deathPosition)
    {
        enemy.Died -= OnEnemyDied;
        activeEnemies.Remove(enemy);

        int added = 0;

        try
        {
            if (corruptionField != null) added = corruptionField.Seed(deathPosition);
        }
        finally { enemyPool.Release(enemy); }

        Debug.Log(
            $"[Enemy] Died at {deathPosition}, " +
            $"New corruption cells={added}",
            this
        );

        EnemyKilled?.Invoke(deathPosition);
    }

    private void OnDisable()
    {
        foreach (var enemy in activeEnemies)
            if (enemy != null) enemyPool.Release(enemy);
        activeEnemies.Clear();
        spawnTimer = 0f;
    }

    private void OnDestroy() => enemyPool?.Dispose();
}

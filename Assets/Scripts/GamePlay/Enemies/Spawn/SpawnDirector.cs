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
    private readonly System.Collections.Generic.Dictionary<EnemyDefinition, EnemyPool> pools = new();
    public bool SuspendSpawning { get; set; }
    public bool RestrictToRoom { get; set; }
    public Rect SpawnBounds { get; set; }
    private NavigationService navigation;

    private float spawnTimer;
    private float spawnRate = 1f;
    private float healthMultiplier = 1f;
    private float damageMultiplier = 1f;
    private int maximumEnemies = 160;
    public int ActiveCount => activeEnemies.Count;
    public void ConfigureDifficulty(float rate, float health, float damage, int limit)
    {
        spawnRate = Mathf.Max(0.1f, rate);
        healthMultiplier = Mathf.Max(1f, health);
        damageMultiplier = Mathf.Max(1f, damage);
        maximumEnemies = Mathf.Clamp(limit, 1, 512);
    }
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
        pools[enemyDefinition] = enemyPool;
    }


    private void Update()
    {
        if (Time.deltaTime <= 0f || SuspendSpawning) return;
        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
            return;

        SpawnEnemy();

        spawnTimer = Mathf.Max(0.05f, spawnInterval / spawnRate);
    }


    private void SpawnEnemy()
    {
        if (activeEnemies.Count >= maximumEnemies) return;
        if (player == null || enemyDefinition == null || enemyDefinition.Prefab == null ||
            enemyDefinition.Movement == null) return;
        Vector2 spawnPosition = default;
        bool found = false;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            spawnPosition = RestrictToRoom
                ? new Vector2(Random.Range(SpawnBounds.xMin, SpawnBounds.xMax), Random.Range(SpawnBounds.yMin, SpawnBounds.yMax))
                : (Vector2)player.position + direction * spawnDistance;
            if (RestrictToRoom && Vector2.Distance(spawnPosition, player.position) < 5f) continue;
            if (navigation.IsWalkable(spawnPosition)) { found = true; break; }
        }
        if (!found) return;

        var selected = enemyDefinition;
        if (AlphaSession.Active && AlphaSession.Catalog.enemies.Length > 0)
            selected = AlphaSession.Catalog.enemies[Random.Range(0, AlphaSession.Catalog.enemies.Length)];
        SpawnSpecific(selected, spawnPosition);
    }

    public EnemyActor SpawnSpecific(EnemyDefinition selected, Vector2 spawnPosition)
    {
        if (selected == null || selected.Prefab == null) return null;
        if (!pools.TryGetValue(selected, out var pool)) pools[selected] = pool = new EnemyPool(selected, transform);
        EnemyActor enemy = pool.Get();

        enemy.transform.position =
            spawnPosition;

        enemy.Died += OnEnemyDied;

        try
        {
            enemy.Initialize(selected, player, navigation);
            enemy.ScaleDifficulty(healthMultiplier, damageMultiplier);
        }
        catch
        {
            pool.Release(enemy);
            throw;
        }
        activeEnemies.Add(enemy);
        enemy.gameObject.SetActive(true);
        return enemy;
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
        finally { pools[enemy.Definition].Release(enemy); }

        Debug.Log(
            $"[Enemy] Died at {deathPosition}, " +
            $"New corruption cells={added}",
            this
        );

        EnemyKilled?.Invoke(deathPosition);
    }

    public void ClearEnemies()
    {
        foreach (var enemy in activeEnemies)
            if (enemy != null && enemy.Definition != null) pools[enemy.Definition].Release(enemy);
        activeEnemies.Clear();
        spawnTimer = 0f;
    }
    private void OnDisable() => ClearEnemies();

    private void OnDestroy() { foreach (var pool in pools.Values) pool.Dispose(); }
}

using UnityEngine;
using UnityEngine.Pool;

public sealed class EnemyPool
{
    private readonly EnemyDefinition definition;
    private readonly Transform container;

    private readonly ObjectPool<EnemyActor> pool;

    public EnemyPool(
        EnemyDefinition definition,
        Transform container,
        int defaultCapacity = 32,
        int maxSize = 256)
    {
        this.definition = definition;
        this.container = container;

        pool = new ObjectPool<EnemyActor>(
            CreateEnemy,
            OnGetEnemy,
            OnReleaseEnemy,
            OnDestroyEnemy,
            collectionCheck: false,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    public EnemyActor Get()
    {
        return pool.Get();
    }

    public void Release(EnemyActor enemy)
    {
        pool.Release(enemy);
    }

    private EnemyActor CreateEnemy()
    {
        EnemyActor enemy =
            Object.Instantiate(
                definition.Prefab,
                container
            );

        enemy.gameObject.SetActive(false);

        return enemy;
    }

    private void OnGetEnemy(EnemyActor enemy)
    {
        enemy.gameObject.SetActive(true);
    }

    private void OnReleaseEnemy(EnemyActor enemy)
    {
        enemy.ResetActor();

        enemy.gameObject.SetActive(false);
    }

    private void OnDestroyEnemy(EnemyActor enemy)
    {
        Object.Destroy(enemy.gameObject);
    }
}
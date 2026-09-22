using UnityEngine;
using UnityEngine.Pool;

public sealed class EnemyPool : System.IDisposable
{
    private readonly EnemyDefinition definition;
    private readonly Transform container;

    private readonly ObjectPool<EnemyActor> pool;
    private readonly GameObject inactiveRoot;

    public EnemyPool(
        EnemyDefinition definition,
        Transform container,
        int defaultCapacity = 32,
        int maxSize = 256)
    {
        this.definition = definition;
        this.container = container;
        inactiveRoot = new GameObject("Enemy Pool (Inactive)");
        inactiveRoot.transform.SetParent(container, false);
        inactiveRoot.SetActive(false);

        pool = new ObjectPool<EnemyActor>(
            CreateEnemy,
            OnGetEnemy,
            OnReleaseEnemy,
            OnDestroyEnemy,
            collectionCheck: true,
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
                inactiveRoot.transform
            );

        enemy.gameObject.SetActive(false);
        enemy.transform.SetParent(container, false);

        return enemy;
    }

    private void OnGetEnemy(EnemyActor enemy)
    {
        // SpawnDirector activates only after position, subscriptions and data are ready.
    }

    private void OnReleaseEnemy(EnemyActor enemy)
    {
        enemy.gameObject.SetActive(false);
        enemy.ResetActor();
    }

    private void OnDestroyEnemy(EnemyActor enemy)
    {
        Object.Destroy(enemy.gameObject);
    }

    public void Dispose()
    {
        pool.Dispose();
        Object.Destroy(inactiveRoot);
    }
}

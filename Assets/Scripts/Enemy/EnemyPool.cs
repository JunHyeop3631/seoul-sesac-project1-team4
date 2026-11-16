using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class EnemyPool : MonoBehaviour
{
    [Serializable]
    public class EnemyPrefabEntry
    {
        public EnemyId enemyId;
        public Enemy prefab;
    }

    [SerializeField] private EnemyPrefabEntry[] enemyPrefabs = Array.Empty<EnemyPrefabEntry>();
    [SerializeField] private HitEffectPool hitEffectPool;

    private readonly Dictionary<EnemyId, ObjectPool<Enemy>> pools = new();
    private readonly Dictionary<Enemy, ObjectPool<Enemy>> owners = new();

    public bool HasEnemy(EnemyId enemyId)
    {
        return FindPrefab(enemyId) != null;
    }

    public Enemy RequestEnemy(EnemyId enemyId, Vector3 position, Quaternion rotation)
    {
        Enemy prefab = FindPrefab(enemyId);
        if (prefab == null)
        {
            Debug.LogWarning($"Enemy prefab is not registered: {enemyId}.", this);
            return null;
        }

        if (!pools.TryGetValue(enemyId, out ObjectPool<Enemy> pool))
        {
            pool = new ObjectPool<Enemy>(
                () =>
                {
                    Enemy instance = Instantiate(prefab, transform);
                    instance.gameObject.SetActive(false);
                    return instance;
                },
                actionOnRelease: instance => instance.gameObject.SetActive(false),
                actionOnDestroy: instance =>
                {
                    owners.Remove(instance);
                    Destroy(instance.gameObject);
                });
            pools.Add(enemyId, pool);
        }

        Enemy enemy = pool.Get();
        owners[enemy] = pool;
        enemy.SetHitEffectPool(hitEffectPool);
        enemy.transform.SetPositionAndRotation(position, rotation);
        enemy.gameObject.SetActive(true);
        return enemy;
    }

    public void ReturnEnemy(Enemy enemy)
    {
        if (enemy != null && enemy.gameObject.activeSelf && owners.TryGetValue(enemy, out var pool))
        {
            pool.Release(enemy);
        }
    }

    private Enemy FindPrefab(EnemyId enemyId)
    {
        foreach (EnemyPrefabEntry entry in enemyPrefabs)
        {
            if (entry != null && entry.enemyId == enemyId)
            {
                return entry.prefab;
            }
        }

        return null;
    }

    private void OnDestroy()
    {
        foreach (var pool in pools.Values)
        {
            pool.Dispose();
        }

        pools.Clear();
        owners.Clear();
    }
}

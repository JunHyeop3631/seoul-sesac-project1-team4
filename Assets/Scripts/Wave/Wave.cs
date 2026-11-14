using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wave : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
    [SerializeField] private LayerMask enemyTargetLayers;

    private readonly HashSet<Enemy> spawnedEnemies = new();
    private readonly List<Collider2D> overlapResults = new();
    private int lastPhysicsSyncFrame = -1;
    private bool spawningComplete;

    public bool IsRunning { get; private set; }
    public int AliveEnemyCount => spawnedEnemies.Count;
    public int DefeatedEnemyCount { get; private set; }
    public event Action Completed;
    public event Action EnemyArrived;

    public void StartWave(WaveData data, EnemyPool enemyPool, float spawnInterval)
    {
        if (IsRunning || !isActiveAndEnabled || data == null || enemyPool == null)
        {
            return;
        }

        if (waypoints == null || waypoints.Length < 2 || Array.Exists(waypoints, point => point == null))
        {
            Debug.LogWarning("Assign valid enemy waypoints before starting a wave.", this);
            return;
        }

        foreach (WaveData.EnemyGroup group in data.Enemies)
        {
            if (group == null || group.count < 1 || !enemyPool.HasEnemy(group.enemyId))
            {
                Debug.LogWarning("Assign valid enemy IDs, counts, and registered prefabs before starting a wave.", this);
                return;
            }
        }

        spawnedEnemies.Clear();
        DefeatedEnemyCount = 0;
        spawningComplete = false;
        IsRunning = true;
        StartCoroutine(SpawnEnemies(data, enemyPool, Mathf.Max(0f, spawnInterval)));
    }

    private IEnumerator SpawnEnemies(WaveData data, EnemyPool enemyPool, float spawnInterval)
    {
        Action<Enemy> releaseEnemy = ReleaseEnemy;
        bool firstEnemy = true;
        foreach (WaveData.EnemyGroup group in data.Enemies)
        {
            for (int index = 0; index < group.count; index++)
            {
                if (!firstEnemy)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }

                firstEnemy = false;
                Enemy enemy = enemyPool.RequestEnemy(group.enemyId, waypoints[0].position, waypoints[0].rotation);
                if (enemy != null)
                {
                    spawnedEnemies.Add(enemy);
                    enemy.Defeated += releaseEnemy;
                    enemy.StartMoving(waypoints, releaseEnemy);
                }
            }
        }

        spawningComplete = true;
        TryCompleteWave();

        void ReleaseEnemy(Enemy enemy)
        {
            if (spawnedEnemies.Remove(enemy))
            {
                if (enemy.CurrentHealth == 0)
                {
                    DefeatedEnemyCount++;
                }
                else
                {
                    EnemyArrived?.Invoke();
                }

                enemy.Defeated -= ReleaseEnemy;
                enemyPool.ReturnEnemy(enemy);
                TryCompleteWave();
            }
        }
    }

    internal Enemy FindAttackTarget(Vector2 origin, float range)
    {
        if (!IsRunning || !isActiveAndEnabled || range <= 0f)
        {
            return null;
        }

        Enemy closest = null;
        float closestDistanceSquared = range * range;

        // Enemy movement writes transforms in Update. Synchronize once before the
        // first overlap query in LateUpdate so it sees the latest positions.
        if (lastPhysicsSyncFrame != Time.frameCount)
        {
            Physics2D.SyncTransforms();
            lastPhysicsSyncFrame = Time.frameCount;
        }

        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(enemyTargetLayers);
        int hitCount = Physics2D.OverlapCircle(origin, range, filter, overlapResults);
        for (int index = 0; index < hitCount; index++)
        {
            if (overlapResults[index].TryGetComponent(out Enemy enemy) && spawnedEnemies.Contains(enemy))
            {
                ConsiderTarget(enemy, origin, ref closest, ref closestDistanceSquared);
            }
        }

        return closest;
    }

    private static void ConsiderTarget(
        Enemy enemy, Vector2 origin, ref Enemy closest, ref float closestDistanceSquared)
    {
        if (enemy == null || !enemy.isActiveAndEnabled)
        {
            return;
        }

        float distanceSquared = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
        if (distanceSquared < closestDistanceSquared ||
            (distanceSquared == closestDistanceSquared &&
             (closest == null || enemy.GetInstanceID() < closest.GetInstanceID())))
        {
            closest = enemy;
            closestDistanceSquared = distanceSquared;
        }
    }

    private void TryCompleteWave()
    {
        if (!IsRunning || !spawningComplete || spawnedEnemies.Count != 0)
        {
            return;
        }

        IsRunning = false;
        Completed?.Invoke();
    }
}

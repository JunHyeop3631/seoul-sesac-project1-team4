using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wave : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
    [SerializeField] private LayerMask enemyTargetLayers;

    private readonly HashSet<Enemy> spawnedEnemies = new();
    private readonly HashSet<Enemy> dyingEnemies = new();
    private readonly List<Collider2D> overlapResults = new();
    private int lastPhysicsSyncFrame = -1;
    private bool spawningComplete;
    private EnemyPool activePool;
    private bool stopping;

    public bool IsRunning { get; private set; }
    public int AliveEnemyCount => spawnedEnemies.Count;
    public int DefeatedEnemyCount { get; private set; }
    public Enemy Boss { get; private set; }
    public event Action Completed;
    public event Action EnemyArrived;
    public event Action<Enemy> EnemyEscaped;
    public event Action<Enemy> EnemyDefeated;
    public event Action Ended;

    public void Prepare()
    {
        if (!IsRunning)
        {
            DefeatedEnemyCount = 0;
            Boss = null;
        }
    }

    public void StartWave(WaveData data, EnemyPool enemyPool, float spawnInterval)
    {
        if (IsRunning || stopping || !isActiveAndEnabled || data == null || enemyPool == null)
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
        dyingEnemies.Clear();
        DefeatedEnemyCount = 0;
        Boss = null;
        spawningComplete = false;
        IsRunning = true;
        activePool = enemyPool;
        StartCoroutine(SpawnEnemies(data, enemyPool, Mathf.Max(0f, spawnInterval)));
    }

    private IEnumerator SpawnEnemies(WaveData data, EnemyPool enemyPool, float spawnInterval)
    {
        bool firstEnemy = true;
        foreach (WaveData.EnemyGroup group in data.Enemies)
        {
            for (int index = 0; index < group.count; index++)
            {
                if (!firstEnemy)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }

                if (!IsRunning) yield break;
                firstEnemy = false;
                Enemy enemy = enemyPool.RequestEnemy(group.enemyId, waypoints[0].position, waypoints[0].rotation);
                if (enemy != null)
                {
                    enemy.ConfigureHealth(group.health);
                    if (enemy is BossEnemy) Boss = enemy;
                    spawnedEnemies.Add(enemy);
                    enemy.Defeated += DefeatEnemy;
                    enemy.DeathCompleted += ReleaseEnemy;
                    enemy.StartMoving(waypoints, ReleaseEnemy);
                }
            }
        }

        spawningComplete = true;
        TryCompleteWave();

    }

    private void DefeatEnemy(Enemy enemy)
    {
        if (IsRunning && spawnedEnemies.Remove(enemy))
        {
            DefeatedEnemyCount++;
            dyingEnemies.Add(enemy);
            EnemyDefeated?.Invoke(enemy);
        }
    }

    private void ReleaseEnemy(Enemy enemy)
    {
        if (!IsRunning) return;
        bool arrived = spawnedEnemies.Remove(enemy);
        if (!arrived && !dyingEnemies.Remove(enemy)) return;
        ReturnEnemy(enemy);
        // Return this enemy before notifying: a lethal arrival can stop and
        // clean the rest of the wave synchronously inside this callback.
        if (arrived)
        {
            EnemyEscaped?.Invoke(enemy);
            if (IsRunning) EnemyArrived?.Invoke();
        }
        TryCompleteWave();
    }

    private void ReturnEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.Defeated -= DefeatEnemy;
        enemy.DeathCompleted -= ReleaseEnemy;
        if (activePool != null) activePool.ReturnEnemy(enemy);
        else enemy.gameObject.SetActive(false);
    }

    public void StopWave()
    {
        if (!IsRunning || stopping) return;
        stopping = true;
        IsRunning = false;
        StopAllCoroutines();
        var remaining = new HashSet<Enemy>(spawnedEnemies);
        remaining.UnionWith(dyingEnemies);
        spawnedEnemies.Clear(); dyingEnemies.Clear(); spawningComplete = false;
        foreach (var enemy in remaining) ReturnEnemy(enemy);
        activePool = null;
        try { Ended?.Invoke(); }
        finally { stopping = false; }
    }

    private void OnDisable() => StopWave();

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

    // Snapshot membership once at the strike, independently of collider count.
    internal void CollectSectorTargets(Vector2 origin, Vector2 direction, float radius, List<Enemy> results)
    {
        results.Clear();
        if (!IsRunning || !isActiveAndEnabled || radius <= 0f) return;
        direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        foreach (Enemy enemy in spawnedEnemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.CurrentHealth <= 0f) continue;
            Vector2 offset = (Vector2)enemy.transform.position - origin;
            if (offset.sqrMagnitude <= radius * radius + 0.00001f &&
                (offset.sqrMagnitude == 0f || Vector2.Dot(direction, offset.normalized) >= 0.5f - 0.000001f))
                results.Add(enemy);
        }
        results.Sort((left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
    }

    private static void ConsiderTarget(
        Enemy enemy, Vector2 origin, ref Enemy closest, ref float closestDistanceSquared)
    {
        if (enemy == null || !enemy.isActiveAndEnabled || enemy.CurrentHealth <= 0)
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
        if (!IsRunning || !spawningComplete || spawnedEnemies.Count != 0 || dyingEnemies.Count != 0)
        {
            return;
        }

        IsRunning = false;
        activePool = null;
        Ended?.Invoke();
        Completed?.Invoke();
    }
}

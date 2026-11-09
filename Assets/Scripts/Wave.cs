using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class Wave : MonoBehaviour
{
    private enum TargetSearchMode
    {
        WaveList,
        OverlapCircle
    }

    private static readonly ProfilerMarker WaveListMarker = new("Ally.TargetSearch.WaveList");
    private static readonly ProfilerMarker OverlapCircleMarker = new("Ally.TargetSearch.OverlapCircle");
    private static readonly ProfilerMarker SyncTransformsMarker = new("Ally.TargetSearch.SyncTransforms");

    [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();
    [SerializeField] private TargetSearchMode targetSearchMode;
    [SerializeField] private LayerMask enemyTargetLayers;

    private readonly HashSet<Enemy> spawnedEnemies = new();
    private readonly List<Collider2D> overlapResults = new();
    private int lastPhysicsSyncFrame = -1;

    public bool IsRunning { get; private set; }
    public event Action Completed;

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
        IsRunning = true;
        StartCoroutine(SpawnEnemies(data, enemyPool, Mathf.Max(0f, spawnInterval)));
    }

    private IEnumerator SpawnEnemies(WaveData data, EnemyPool enemyPool, float spawnInterval)
    {
        Action<Enemy> returnOnArrival = enemyPool.ReturnEnemy;
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
                    enemy.StartMoving(waypoints, returnOnArrival);
                    spawnedEnemies.Add(enemy);
                }
            }
        }

        // 생성 종료만으로 웨이브 완료를 알리지 않는다.
    }

    internal Enemy FindAttackTarget(Vector2 origin, float range)
    {
        if (!IsRunning || !isActiveAndEnabled || range <= 0f)
        {
            return null;
        }

        Enemy closest = null;
        float closestDistanceSquared = range * range;

        if (targetSearchMode == TargetSearchMode.WaveList)
        {
            using (WaveListMarker.Auto())
            {
                foreach (Enemy enemy in spawnedEnemies)
                {
                    ConsiderTarget(enemy, origin, ref closest, ref closestDistanceSquared);
                }
            }

            return closest;
        }

        // Enemy movement writes transforms in Update. Synchronize once before the
        // first overlap query in LateUpdate so both modes read the same positions.
        if (lastPhysicsSyncFrame != Time.frameCount)
        {
            using (SyncTransformsMarker.Auto())
            {
                Physics2D.SyncTransforms();
            }

            lastPhysicsSyncFrame = Time.frameCount;
        }

        using (OverlapCircleMarker.Auto())
        {
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

    private void CompleteWave()
    {
        // 실제 완료 조건 판정이 구현되면 이 알림을 호출한다.
        IsRunning = false;
        Completed?.Invoke();
    }
}

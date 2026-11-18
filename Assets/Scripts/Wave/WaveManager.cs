using System;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [SerializeField] private WaveData[] waves = Array.Empty<WaveData>();
    [SerializeField] private Wave wave;
    [SerializeField] private EnemyPool enemyPool;
    [SerializeField, Min(0f)] private float spawnInterval;

    public event Action WaveCompleted;
    public event Action EnemyArrived;
    public event Action<Enemy> EnemyDefeated;
    public event Action BossDefeated;
    public event Action BossEscaped;

    public int AliveEnemyCount => wave != null ? wave.AliveEnemyCount : 0;
    public bool IsRunning => wave != null && wave.IsRunning;
    public int WaveCount => waves != null ? waves.Length : 0;
    public int DefeatedEnemyCount => wave != null ? wave.DefeatedEnemyCount : 0;
    public Enemy Boss => wave != null ? wave.Boss : null;

    public int GetBossMaxHealth(int waveIndex)
    {
        if (waves == null || waveIndex < 0 || waveIndex >= waves.Length || waves[waveIndex] == null) return 0;
        foreach (var group in waves[waveIndex].Enemies)
            if (group != null && group.enemyId == EnemyId.BossEnemy) return Mathf.Max(0, group.health);
        return 0;
    }

    public int GetCompletionReward(int waveIndex)
    {
        return waves != null && waveIndex >= 0 && waveIndex < waves.Length && waves[waveIndex] != null
            ? waves[waveIndex].CompletionReward : 0;
    }

    public int GetTotalEnemyCount(int waveNumber)
    {
        if (waves == null || waveNumber < 0 || waveNumber >= waves.Length || waves[waveNumber] == null)
        {
            return 0;
        }

        int total = 0;
        foreach (WaveData.EnemyGroup group in waves[waveNumber].Enemies)
        {
            if (group != null)
            {
                total += Mathf.Max(0, group.count);
            }
        }

        return total;
    }

    private void OnEnable()
    {
        if (wave != null)
        {
            wave.Completed += HandleWaveCompleted;
            wave.EnemyArrived += HandleEnemyArrived;
            wave.EnemyDefeated += HandleEnemyDefeated;
            wave.EnemyEscaped += HandleEnemyEscaped;
        }
    }

    private void OnDisable()
    {
        if (wave != null)
        {
            wave.Completed -= HandleWaveCompleted;
            wave.EnemyArrived -= HandleEnemyArrived;
            wave.EnemyDefeated -= HandleEnemyDefeated;
            wave.EnemyEscaped -= HandleEnemyEscaped;
        }
    }

    public void StartWave(int waveNumber)
    {
        int index = waveNumber;
        if (wave == null || enemyPool == null || waves == null ||
            index < 0 || index >= waves.Length || waves[index] == null)
        {
            return;
        }

        wave.StartWave(waves[index], enemyPool, spawnInterval);
    }

    public void PrepareWave()
    {
        wave?.Prepare();
    }

    public void StopWave() => wave?.StopWave();

    private void HandleEnemyArrived()
    {
        EnemyArrived?.Invoke();
    }

    private void HandleEnemyDefeated(Enemy enemy)
    {
        // Finish synchronous kill rewards before announcing the final result.
        EnemyDefeated?.Invoke(enemy);
        if (enemy is BossEnemy && IsRunning) BossDefeated?.Invoke();
    }

    private void HandleEnemyEscaped(Enemy enemy)
    {
        if (enemy is BossEnemy && IsRunning) BossEscaped?.Invoke();
    }

    private void HandleWaveCompleted()
    {
        WaveCompleted?.Invoke();
    }
}

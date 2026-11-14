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

    public int AliveEnemyCount => wave != null ? wave.AliveEnemyCount : 0;
    public bool IsRunning => wave != null && wave.IsRunning;
    public int DefeatedEnemyCount => wave != null ? wave.DefeatedEnemyCount : 0;

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
        }
    }

    private void OnDisable()
    {
        if (wave != null)
        {
            wave.Completed -= HandleWaveCompleted;
            wave.EnemyArrived -= HandleEnemyArrived;
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

    private void HandleEnemyArrived()
    {
        EnemyArrived?.Invoke();
    }

    private void HandleWaveCompleted()
    {
        WaveCompleted?.Invoke();
    }
}

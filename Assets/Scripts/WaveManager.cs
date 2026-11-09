using System;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [SerializeField] private WaveData[] waves = Array.Empty<WaveData>();
    [SerializeField] private Wave wave;
    [SerializeField] private EnemyPool enemyPool;
    [SerializeField, Min(0f)] private float spawnInterval;

    public event Action WaveCompleted;

    private void OnEnable()
    {
        if (wave != null)
        {
            wave.Completed += HandleWaveCompleted;
        }
    }

    private void OnDisable()
    {
        if (wave != null)
        {
            wave.Completed -= HandleWaveCompleted;
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

    private void HandleWaveCompleted()
    {
        WaveCompleted?.Invoke();
    }
}

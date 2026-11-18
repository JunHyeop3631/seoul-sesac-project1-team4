using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveData", menuName = "Yut Defense/Wave Data")]
public class WaveData : ScriptableObject
{
    [Serializable]
    public class EnemyGroup
    {
        public EnemyId enemyId;
        [Min(1)] public int count;
        [Tooltip("Per-enemy health for this wave. Zero keeps the prefab default.")]
        [Min(0)] public int health;
    }

    [SerializeField] private EnemyGroup[] enemies = Array.Empty<EnemyGroup>();
    [SerializeField, Min(0)] private int completionReward = 60;

    public EnemyGroup[] Enemies => enemies;
    public int CompletionReward => Mathf.Max(0, completionReward);
}

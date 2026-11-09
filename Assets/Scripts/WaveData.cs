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
    }

    [SerializeField] private EnemyGroup[] enemies = Array.Empty<EnemyGroup>();

    public EnemyGroup[] Enemies => enemies;
}

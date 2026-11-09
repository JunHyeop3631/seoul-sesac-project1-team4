using UnityEngine;

public class Ally : MonoBehaviour
{
    [SerializeField, Min(0f)] private float attackRange = 3f;

    private Wave wave;

    public Enemy CurrentTarget { get; private set; }

    internal void SetTargetingWave(Wave targetingWave)
    {
        wave = targetingWave;
    }

    private void LateUpdate()
    {
        CurrentTarget = wave != null ? wave.FindAttackTarget(transform.position, attackRange) : null;
    }

    private void OnDisable()
    {
        CurrentTarget = null;
    }
}

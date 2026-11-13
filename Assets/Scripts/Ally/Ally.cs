using UnityEngine;

public class Ally : MonoBehaviour
{
    [SerializeField, Min(0f)] private float attackRange = 3f;
    [SerializeField, Min(1)] private int attackDamage = 2;
    [SerializeField, Min(0.01f)] private float attackInterval = 1f;

    private Wave wave;
    private int targetActivationVersion;
    private double nextAttackTime;

    public Enemy CurrentTarget { get; private set; }

    internal void SetTargetingWave(Wave targetingWave)
    {
        wave = targetingWave;
        CurrentTarget = null;
    }

    private void LateUpdate()
    {
        if (wave == null || !wave.isActiveAndEnabled || !wave.IsRunning || attackRange <= 0f)
        {
            CurrentTarget = null;
            return;
        }

        Vector2 origin = transform.position;
        if (!HasTargetInRange(origin))
        {
            CurrentTarget = wave.FindAttackTarget(origin, attackRange);
            if (CurrentTarget != null)
            {
                targetActivationVersion = CurrentTarget.ActivationVersion;
            }
        }

        double now = Time.timeAsDouble;
        if (CurrentTarget == null || CurrentTarget.CurrentHealth <= 0 || now < nextAttackTime)
        {
            return;
        }

        CurrentTarget.TakeDamage(attackDamage);
        nextAttackTime = now + attackInterval;
        if (CurrentTarget.CurrentHealth == 0)
        {
            CurrentTarget = null;
        }
    }

    private bool HasTargetInRange(Vector2 origin)
    {
        return CurrentTarget != null && CurrentTarget.isActiveAndEnabled &&
               CurrentTarget.ActivationVersion == targetActivationVersion &&
               ((Vector2)CurrentTarget.transform.position - origin).sqrMagnitude <= attackRange * attackRange;
    }

    private void OnDisable()
    {
        CurrentTarget = null;
    }
}

using System;
using UnityEngine;

public class HomingProjectile : MonoBehaviour
{
    private static readonly int FlightState = Animator.StringToHash("Flight");

    private Animator animator;
    private Enemy target;
    private int targetActivationVersion;
    private int damage;
    private HitEffect hitEffect;
    private Vector3 startPosition;
    private double launchTime;
    private float flightDuration;
    private bool flying;
    internal Action<HomingProjectile> ReturnToPool { private get; set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Launch(Enemy enemy, int attackDamage, float duration, HitEffect attackHitEffect = null)
    {
        target = enemy;
        targetActivationVersion = enemy != null ? enemy.ActivationVersion : -1;
        damage = attackDamage;
        hitEffect = attackHitEffect;
        startPosition = transform.position;
        launchTime = Time.timeAsDouble;
        flightDuration = Mathf.Max(0.01f, duration);
        flying = true;
        if (animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && animator.HasState(0, FlightState))
        {
            animator.speed = 1f;
            animator.Play(FlightState, 0, 0f);
            // Evaluate the selected state so its actual clip length is available
            // on the first launch and after returning from the pool.
            animator.Update(0f);
            float animationDuration = animator.GetCurrentAnimatorStateInfo(0).length;
            if (animationDuration > 0f)
                animator.speed = animationDuration / flightDuration;
        }
        if (target != null)
            FaceTarget(target.transform.position);
    }

    private void LateUpdate()
    {
        if (!flying)
            return;

        if (target == null || !target.isActiveAndEnabled || target.CurrentHealth <= 0 ||
            target.ActivationVersion != targetActivationVersion)
        {
            Finish();
            return;
        }

        if (Time.timeScale <= 0f)
            return;

        Vector3 targetPosition = target.transform.position;
        FaceTarget(targetPosition);
        float progress = Mathf.Clamp01((float)((Time.timeAsDouble - launchTime) / flightDuration));
        transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
        if (progress < 1f)
            return;

        // Clear first so this projectile can never apply damage twice.
        flying = false;
        target.TakeDamage(damage, hitEffect);
        Finish();
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector2 direction = targetPosition - transform.position;
        if (direction.sqrMagnitude <= 0.000001f)
            return;

        // Projectile sprites face local +X. Keep them facing the current target.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Finish()
    {
        flying = false;
        target = null;
        if (ReturnToPool != null)
        {
            ReturnToPool(this);
        }
        else
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        flying = false;
        target = null;
        damage = 0;
        hitEffect = null;
    }
}

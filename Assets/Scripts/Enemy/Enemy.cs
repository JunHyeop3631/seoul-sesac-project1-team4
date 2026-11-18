using System;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    private static readonly int WalkLeftState = Animator.StringToHash("WalkLeft");
    private static readonly int WalkRightState = Animator.StringToHash("WalkRight");
    private static readonly int DeathLeftState = Animator.StringToHash("DeathLeft");
    private static readonly int DeathRightState = Animator.StringToHash("DeathRight");

    [SerializeField, Min(0.01f)] private float moveSpeed = 2f;
    [Tooltip("Seconds per closed lap at 1x. Zero uses Move Speed directly.")]
    [SerializeField, Min(0f)] private float lapDurationSeconds;
    [SerializeField, Min(1)] private int maxHealth = 10;
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private HitEffect hitEffectPrefab;
    [SerializeField] private HitEffectPool hitEffectPool;

    [Header("Death Audio")]
    [Tooltip("Sound played once when this enemy is defeated. Optional.")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 1f;

    internal void SetHitEffectPool(HitEffectPool pool)
    {
        hitEffectPool = pool;
    }

    private Vector3[] pathPoints;
    private int targetIndex;
    private Action<Enemy> onArrived;
    private bool facingLeft = true;
    private bool awaitingDeathAnimation;
    private int spawnedMaxHealth;

    public bool IsMoving { get; private set; }
    public float CurrentHealth { get; private set; }
    internal int ActivationVersion { get; private set; }
    internal event Action<Enemy> Defeated;
    internal event Action<Enemy> DeathCompleted;

    protected virtual void OnEnable()
    {
        awaitingDeathAnimation = false;
        ConfigureHealth(0);
        facingLeft = true;
        if (enemyAnimator != null)
        {
            enemyAnimator.Rebind();
            enemyAnimator.speed = 1f;
            enemyAnimator.Play(WalkLeftState, 0, 0f);
        }
    }

    // Called after pool activation, before the enemy can move or be targeted.
    // Keep the prefab default intact so pooled reuse never inherits a prior wave's HP.
    internal void ConfigureHealth(int health)
    {
        spawnedMaxHealth = health > 0 ? health : Mathf.Max(1, maxHealth);
        CurrentHealth = spawnedMaxHealth;
        UpdateHealthBar();
    }

    public void TakeDamage(float damage, HitEffect attackHitEffect = null)
    {
        if (!isActiveAndEnabled || damage <= 0 || CurrentHealth <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        UpdateHealthBar();
        HitEffect effect = attackHitEffect != null ? attackHitEffect : hitEffectPrefab;
        if (effect != null && hitEffectPool != null)
            hitEffectPool.Play(effect, transform.position);

        if (CurrentHealth == 0)
        {
            StopMoving();
            PlayDeathSound();
            Defeated?.Invoke(this);
            if (isActiveAndEnabled)
            {
                PlayDeathAnimation();
            }
        }
    }

    private void PlayDeathSound()
    {
        if (deathSound == null || deathVolume <= 0f)
            return;

        // Keep playback independent of the enemy's death animation and pool return.
        var soundObject = new GameObject("Enemy Death Sound");
        soundObject.transform.position = transform.position;
        var source = soundObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(deathVolume);
        source.clip = deathSound;
        source.Play();
        Destroy(soundObject, deathSound.length);
    }

    private void PlayDeathAnimation()
    {
        awaitingDeathAnimation = true;
        int state = facingLeft ? DeathLeftState : DeathRightState;
        if (enemyAnimator != null && enemyAnimator.isActiveAndEnabled &&
            enemyAnimator.runtimeAnimatorController != null && enemyAnimator.HasState(0, state))
        {
            enemyAnimator.Play(state, 0, 0f);
            return;
        }

        CompleteDeathAnimation();
    }

    // Called by EnemyAnimationEvents on the Animator's child object.
    public void CompleteDeathAnimation()
    {
        if (!awaitingDeathAnimation || !isActiveAndEnabled || CurrentHealth > 0)
            return;

        awaitingDeathAnimation = false;
        if (DeathCompleted != null)
        {
            DeathCompleted(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = CurrentHealth / Mathf.Max(1, spawnedMaxHealth);
        }
    }

    public void StartMoving(Transform[] waypoints, Action<Enemy> onArrived = null)
    {
        StopMoving();
        if (CurrentHealth <= 0 || waypoints == null || waypoints.Length < 2)
        {
            Debug.LogWarning("Assign at least two waypoints before moving an enemy.", this);
            return;
        }

        var points = new Vector3[waypoints.Length];
        for (int index = 0; index < waypoints.Length; index++)
        {
            if (waypoints[index] == null)
            {
                Debug.LogWarning("Enemy waypoints must not contain missing references.", this);
                return;
            }

            points[index] = waypoints[index].position;
        }

        pathPoints = points;
        if (lapDurationSeconds > 0f)
        {
            // Update traverses the closing segment from the last point to point zero too.
            float length = 0f;
            for (int index = 0; index < points.Length; index++)
                length += Vector3.Distance(points[index], points[(index + 1) % points.Length]);
            if (length > 0f) moveSpeed = length / lapDurationSeconds;
        }
        this.onArrived = onArrived;
        transform.position = pathPoints[0];
        targetIndex = 1;
        IsMoving = true;
        if (enemyAnimator != null)
        {
            enemyAnimator.speed = 1f;
            enemyAnimator.Play(facingLeft ? WalkLeftState : WalkRightState, 0, 0f);
        }
    }

    public void StopMoving()
    {
        IsMoving = false;
        pathPoints = null;
        onArrived = null;
        targetIndex = 0;
    }

    protected virtual void Update()
    {
        if (!IsMoving)
        {
            return;
        }

        float remainingDistance = Mathf.Max(0f, moveSpeed) * Time.deltaTime;
        // 모서리를 지났다면 이번 프레임의 남은 거리를 다음 구간에서 이동한다.
        while (IsMoving)
        {
            Vector3 target = pathPoints[targetIndex % pathPoints.Length];
            float horizontalDirection = target.x - transform.position.x;
            if (horizontalDirection != 0f && facingLeft != (horizontalDirection < 0f))
            {
                facingLeft = horizontalDirection < 0f;
                if (enemyAnimator != null)
                {
                    enemyAnimator.Play(facingLeft ? WalkLeftState : WalkRightState, 0, 0f);
                }
            }

            float distance = Vector3.Distance(transform.position, target);
            if (distance > remainingDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, remainingDistance);
                return;
            }

            transform.position = target;
            remainingDistance -= distance;
            if (targetIndex == pathPoints.Length)
            {
                if (onArrived != null)
                {
                    onArrived(this);
                }
                else
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            targetIndex++;
        }
    }

    protected virtual void OnDisable()
    {
        awaitingDeathAnimation = false;

        // A pooled object can be returned and reused before the next target check.
        ActivationVersion++;
        StopMoving();
    }
}

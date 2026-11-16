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
    [SerializeField, Min(1)] private int maxHealth = 10;
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private HitEffect hitEffectPrefab;

    private Vector3[] pathPoints;
    private int targetIndex;
    private Action<Enemy> onArrived;
    private bool facingLeft = true;
    private bool awaitingDeathAnimation;

    public bool IsMoving { get; private set; }
    public int CurrentHealth { get; private set; }
    internal int ActivationVersion { get; private set; }
    internal event Action<Enemy> Defeated;
    internal event Action<Enemy> DeathCompleted;

    protected virtual void OnEnable()
    {
        awaitingDeathAnimation = false;
        CurrentHealth = maxHealth;
        UpdateHealthBar();
        facingLeft = true;
        if (enemyAnimator != null)
        {
            enemyAnimator.Rebind();
            enemyAnimator.speed = 1f;
            enemyAnimator.Play(WalkLeftState, 0, 0f);
        }
    }

    public void TakeDamage(int damage)
    {
        if (!isActiveAndEnabled || damage <= 0 || CurrentHealth <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        UpdateHealthBar();
        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);

        if (CurrentHealth == 0)
        {
            StopMoving();
            Defeated?.Invoke(this);
            if (isActiveAndEnabled)
            {
                PlayDeathAnimation();
            }
        }
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
            healthBarFill.fillAmount = (float)CurrentHealth / Mathf.Max(1, maxHealth);
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

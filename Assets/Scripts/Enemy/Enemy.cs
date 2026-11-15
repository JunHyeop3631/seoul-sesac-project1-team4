using System;
using System.Collections;
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
    [SerializeField] private AnimationClip deathLeftClip;
    [SerializeField] private AnimationClip deathRightClip;

    private Vector3[] pathPoints;
    private int targetIndex;
    private Action<Enemy> onArrived;
    private bool facingLeft = true;
    private Coroutine deathRoutine;

    public bool IsMoving { get; private set; }
    public int CurrentHealth { get; private set; }
    internal int ActivationVersion { get; private set; }
    internal event Action<Enemy> Defeated;
    internal event Action<Enemy> DeathCompleted;

    protected virtual void OnEnable()
    {
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
        if (CurrentHealth == 0)
        {
            StopMoving();
            Defeated?.Invoke(this);
            if (isActiveAndEnabled)
            {
                deathRoutine = StartCoroutine(PlayDeathAnimation());
            }
        }
    }

    private IEnumerator PlayDeathAnimation()
    {
        AnimationClip clip = facingLeft ? deathLeftClip : deathRightClip;
        if (enemyAnimator != null && clip != null)
        {
            enemyAnimator.speed = 1f;
            enemyAnimator.Play(facingLeft ? DeathLeftState : DeathRightState, 0, 0f);
            yield return new WaitForSeconds(clip.length);
        }

        deathRoutine = null;
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
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        // A pooled object can be returned and reused before the next target check.
        ActivationVersion++;
        StopMoving();
    }
}

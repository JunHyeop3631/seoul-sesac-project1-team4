using UnityEngine;

public class Ally : MonoBehaviour
{
    protected static readonly int AttackLeftState = Animator.StringToHash("AttackLeft");
    protected static readonly int AttackRightState = Animator.StringToHash("AttackRight");

    [SerializeField, Min(0f)] private float attackRange = 3f;
    [SerializeField, Min(1)] private int attackDamage = 2;
    [SerializeField, Min(0.01f)] private float attackInterval = 1f;
    [SerializeField] private Animator allyAnimator;
    [SerializeField] private SpriteRenderer visualRenderer;

    private static readonly int IdleLeftState = Animator.StringToHash("IdleLeft");
    private static readonly int IdleRightState = Animator.StringToHash("IdleRight");
    private bool attacking;
    private bool released;
    private bool facingRight;
    private int attackStartFrame;

    private Wave wave;
    private int targetActivationVersion;
    protected double nextAttackTime;
    protected Animator animator;
    private ProjectileAttack projectileAttack;

    protected int AttackDamage => attackDamage;
    protected float AttackInterval => attackInterval;

    public Enemy CurrentTarget { get; private set; }
    public float AttackRange => attackRange;
    public SpriteRenderer VisualRenderer => visualRenderer;

    protected virtual void Awake()
    {
        animator = allyAnimator != null ? allyAnimator : GetComponentInChildren<Animator>();
        if (visualRenderer == null)
            visualRenderer = GetComponentInChildren<SpriteRenderer>();
        projectileAttack = GetComponent<ProjectileAttack>();
    }

    internal void SetTargetingWave(Wave targetingWave)
    {
        wave = targetingWave;
        CurrentTarget = null;
    }

    private void LateUpdate()
    {
        if (!RefreshTarget())
        {
            OnTargetUnavailable();
            return;
        }

        UpdateAttack();
    }

    // Also called by animation events: their target must be valid at release time.
    protected bool RefreshTarget()
    {
        if (wave == null || !wave.isActiveAndEnabled || !wave.IsRunning || attackRange <= 0f)
        {
            CurrentTarget = null;
            return false;
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

        return CurrentTarget != null;
    }

    protected virtual void UpdateAttack()
    {
        if (animator == null || !animator.isActiveAndEnabled || !CanAttack)
        {
            OnTargetUnavailable();
            return;
        }

        if (attacking)
        {
            // Play is evaluated by the Animator after the frame in which we start.
            if (Time.frameCount == attackStartFrame)
                return;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool inAttackState = state.shortNameHash == AttackLeftState ||
                                 state.shortNameHash == AttackRightState;
            if (inAttackState && state.normalizedTime < 1f)
            {
                FaceTarget(state.normalizedTime);
                return;
            }

            attacking = false;
        }

        if (Time.timeAsDouble < nextAttackTime || Time.timeScale <= 0f)
            return;

        attacking = true;
        released = false;
        attackStartFrame = Time.frameCount;
        facingRight = CurrentTarget.transform.position.x > transform.position.x;
        animator.Play(facingRight ? AttackRightState : AttackLeftState, 0, 0f);
    }

    private void FaceTarget(float normalizedTime)
    {
        bool right = CurrentTarget.transform.position.x > transform.position.x;
        if (right == facingRight)
            return;

        facingRight = right;
        // Left and right clips have matching lengths and release events. Preserve progress;
        // the release guard belongs to the attack, not to either Animator state.
        animator.Play(facingRight ? AttackRightState : AttackLeftState, 0, normalizedTime);
    }

    protected virtual bool CanAttack => projectileAttack == null || projectileAttack.CanFire;

    protected void ReleaseProjectile()
    {
        if (projectileAttack == null || !projectileAttack.CanFire || !TryBeginAttackEvent())
            return;

        projectileAttack.Fire(CurrentTarget, AttackDamage);
    }

    // Animation Event on both left/right melee attack clips.
    public void ApplyAttack()
    {
        if (!TryBeginAttackEvent())
            return;

        CurrentTarget.TakeDamage(AttackDamage);
        if (!RefreshTarget())
            OnTargetUnavailable();
    }

    protected bool TryBeginAttackEvent()
    {
        if (!isActiveAndEnabled || !attacking || released || Time.timeScale <= 0f)
            return false;

        if (!RefreshTarget())
        {
            OnTargetUnavailable();
            return false;
        }

        released = true;
        nextAttackTime = Time.timeAsDouble + AttackInterval;
        return true;
    }

    private bool HasTargetInRange(Vector2 origin)
    {
        return CurrentTarget != null && CurrentTarget.isActiveAndEnabled &&
               CurrentTarget.CurrentHealth > 0 &&
               CurrentTarget.ActivationVersion == targetActivationVersion &&
               ((Vector2)CurrentTarget.transform.position - origin).sqrMagnitude <= attackRange * attackRange;
    }

    protected virtual void OnTargetUnavailable()
    {
        if (!attacking)
            return;

        attacking = false;
        if (animator != null && animator.isActiveAndEnabled)
            animator.Play(facingRight ? IdleRightState : IdleLeftState, 0, 0f);
    }

    protected virtual void OnDisable()
    {
        OnTargetUnavailable();
        CurrentTarget = null;
    }
}

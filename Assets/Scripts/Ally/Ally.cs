using UnityEngine;

public class Ally : MonoBehaviour
{
    protected static readonly int AttackLeftState = Animator.StringToHash("AttackLeft");
    protected static readonly int AttackRightState = Animator.StringToHash("AttackRight");

    [SerializeField, Min(0f)] private float attackRange = 3f;
    [SerializeField, Min(0f)] private float attackDamage = 2f;
    [SerializeField, Min(0.01f)] private float attackInterval = 1f;
    [SerializeField] private Animator allyAnimator;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private HitEffect attackHitEffect;
    [SerializeField, Min(1)] private int evolutionStage = 1;

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

    public float AttackDamage => attackDamage;
    public float AttacksPerSecond => 1f / Mathf.Max(0.01f, attackInterval);
    protected float AttackInterval => attackInterval;

    public Enemy CurrentTarget { get; private set; }
    public float AttackRange => attackRange;
    public SpriteRenderer VisualRenderer => visualRenderer;
    public int EvolutionStage => evolutionStage;

    internal void ResetForReuse()
    {
        CurrentTarget = null;
        attacking = false;
        released = false;
        facingRight = false;
        nextAttackTime = 0;
        if (animator != null)
        {
            animator.Rebind();
            animator.speed = 1f;
            animator.Play(IdleLeftState, 0, 0f);
        }
    }

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
            // Do not complete a newly started attack in the same frame.
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
            animator.speed = 1f;
        }

        if (Time.timeAsDouble < nextAttackTime || Time.timeScale <= 0f)
            return;

        attacking = true;
        released = false;
        attackStartFrame = Time.frameCount;
        facingRight = CurrentTarget.transform.position.x > transform.position.x;
        animator.speed = 1f;
        animator.Play(facingRight ? AttackRightState : AttackLeftState, 0, 0f);
        // Evaluate the chosen state before reading its actual clip duration.
        animator.Update(0f);
        float duration = animator.GetCurrentAnimatorStateInfo(0).length;
        animator.speed = duration / Mathf.Max(0.01f, AttackInterval);
        nextAttackTime = Time.timeAsDouble + AttackInterval;
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

        projectileAttack.Fire(CurrentTarget, AttackDamage, attackHitEffect);
    }

    // Animation Event on both left/right melee attack clips.
    public void ApplyAttack()
    {
        if (!TryBeginAttackEvent())
            return;

        CurrentTarget.TakeDamage(AttackDamage, attackHitEffect);
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
        {
            animator.speed = 1f;
            animator.Play(facingRight ? IdleRightState : IdleLeftState, 0, 0f);
        }
    }

    protected virtual void OnDisable()
    {
        OnTargetUnavailable();
        CurrentTarget = null;
    }
}

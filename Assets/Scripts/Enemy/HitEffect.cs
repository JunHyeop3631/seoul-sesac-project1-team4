using UnityEngine;

public class HitEffect : MonoBehaviour
{
    private static readonly int HitState = Animator.StringToHash("Hit");

    private void Start()
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && animator.HasState(0, HitState))
        {
            animator.Play(HitState, 0, 0f);
            return;
        }

        OnHitAnimationFinished();
    }

    // Animation Event at the end of Enemy_Hit.anim.
    public void OnHitAnimationFinished()
    {
        Destroy(gameObject);
    }
}

using System;
using UnityEngine;

public class HitEffect : MonoBehaviour
{
    private static readonly int HitState = Animator.StringToHash("Hit");

    private Animator animator;
    private bool playing;
    internal Action<HitEffect> ReturnToPool { private get; set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (!playing)
            Play();
    }

    public void Play()
    {
        playing = true;
        if (animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null && animator.HasState(0, HitState))
        {
            animator.speed = 1f;
            animator.Play(HitState, 0, 0f);
            return;
        }

        OnHitAnimationFinished();
    }

    // Animation Event at the end of Common_Hit.anim.
    public void OnHitAnimationFinished()
    {
        if (!playing)
            return;

        playing = false;
        if (ReturnToPool != null)
            ReturnToPool(this);
        else
            Destroy(gameObject);
    }

    private void OnDisable()
    {
        playing = false;
    }
}

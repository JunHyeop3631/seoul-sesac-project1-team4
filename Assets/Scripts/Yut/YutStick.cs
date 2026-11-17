using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public enum YutFace
{
    Back,
    Belly
}

public class YutStick : MonoBehaviour
{
    [SerializeField] private SpriteRenderer faceRenderer;
    [SerializeField] private Sprite bellySprite;
    [SerializeField] private Sprite backSprite;
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip backToBack;
    [SerializeField] private AnimationClip backToBelly;
    [SerializeField] private AnimationClip bellyToBack;
    [SerializeField] private AnimationClip bellyToBelly;
    [SerializeField] private Sprite markedBellySprite;
    [SerializeField] private Sprite markedBackSprite;
    [SerializeField] private AnimationClip markedBackToBack;
    [SerializeField] private AnimationClip markedBackToBelly;
    [SerializeField] private AnimationClip markedBellyToBack;
    [SerializeField] private AnimationClip markedBellyToBelly;

    private YutFace currentFace;
    private YutFace targetFace;
    private PlayableGraph animationGraph;
    private bool isMarked;

    public bool IsAnimating { get; private set; }

    internal bool CanThrow => isActiveAndEnabled && !IsAnimating &&
        faceRenderer != null && bellySprite != null && backSprite != null &&
        animator != null && animator.isActiveAndEnabled &&
        backToBack != null && backToBelly != null && bellyToBack != null && bellyToBelly != null &&
        markedBellySprite != null && markedBackSprite != null &&
        markedBackToBack != null && markedBackToBelly != null &&
        markedBellyToBack != null && markedBellyToBelly != null;

    private void Awake()
    {
        currentFace = faceRenderer != null && faceRenderer.sprite == bellySprite
            ? YutFace.Belly : YutFace.Back;
    }

    public YutFace Throw()
    {
        return Throw(false);
    }

    public YutFace Throw(bool marked)
    {
        if (!CanThrow)
        {
            throw new InvalidOperationException("The yut stick is busy or its display and animation settings are unavailable.");
        }

        YutFace face = UnityEngine.Random.Range(0, 2) == 0 ? YutFace.Back : YutFace.Belly;
        isMarked = marked;
        targetFace = face;
        AnimationClip clip = (currentFace, face) switch
        {
            (YutFace.Back, YutFace.Back) => marked ? markedBackToBack : backToBack,
            (YutFace.Back, YutFace.Belly) => marked ? markedBackToBelly : backToBelly,
            (YutFace.Belly, YutFace.Back) => marked ? markedBellyToBack : bellyToBack,
            _ => marked ? markedBellyToBelly : bellyToBelly
        };

        IsAnimating = true;
        StartCoroutine(PlayThrow(clip));
        return face;
    }

    private IEnumerator PlayThrow(AnimationClip clip)
    {
        animationGraph = PlayableGraph.Create(name);
        animationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(animationGraph, clip);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(animationGraph, "Yut Throw", animator);
        output.SetSourcePlayable(playable);
        animationGraph.Play();
        animationGraph.Evaluate(0f);

        while (playable.GetTime() < clip.length)
        {
            yield return null;
            animationGraph.Evaluate(Time.deltaTime);
        }

        FinishThrow();
    }

    private void OnDisable()
    {
        if (IsAnimating)
        {
            StopAllCoroutines();
            FinishThrow();
        }
    }

    private void FinishThrow()
    {
        if (animationGraph.IsValid())
        {
            animationGraph.Destroy();
        }

        if (faceRenderer != null)
        {
            faceRenderer.sprite = targetFace == YutFace.Belly
                ? (isMarked ? markedBellySprite : bellySprite)
                : (isMarked ? markedBackSprite : backSprite);
        }

        currentFace = targetFace;
        IsAnimating = false;
    }
}

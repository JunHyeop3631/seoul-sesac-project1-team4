using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum YutThrowResult
{
    Do,
    Gae,
    Geol,
    Yut,
    Mo
}

public class YutThrowController : MonoBehaviour
{
    private const int StickCount = 4;

    [SerializeField] private YutStick[] sticks = new YutStick[StickCount];
    [SerializeField] private Image resultImage;
    [SerializeField] private Sprite doSprite;
    [SerializeField] private Sprite gaeSprite;
    [SerializeField] private Sprite geolSprite;
    [SerializeField] private Sprite yutSprite;
    [SerializeField] private Sprite moSprite;

    public YutThrowResult? LastResult { get; private set; }
    public bool IsThrowing { get; private set; }
    public event Action<YutThrowResult> ThrowCompleted;

    private void Awake()
    {
        if (resultImage != null)
        {
            resultImage.enabled = false;
        }
    }

    public void Throw()
    {
        if (!isActiveAndEnabled || IsThrowing)
        {
            return;
        }

        if (!HasReadySticks())
        {
            Debug.LogWarning("Assign four distinct, ready yut sticks with their face sprites and animation clips.", this);
            return;
        }

        IsThrowing = true;
        if (resultImage != null)
        {
            resultImage.enabled = false;
        }

        int bellyCount = 0;
        foreach (YutStick stick in sticks)
        {
            if (stick.Throw() == YutFace.Belly)
            {
                bellyCount++;
            }
        }

        YutThrowResult result = bellyCount switch
        {
            0 => YutThrowResult.Mo,
            1 => YutThrowResult.Do,
            2 => YutThrowResult.Gae,
            3 => YutThrowResult.Geol,
            4 => YutThrowResult.Yut,
            _ => throw new ArgumentOutOfRangeException(nameof(bellyCount))
        };

        LastResult = result;
        StartCoroutine(CompleteThrow(result));
    }

    private IEnumerator CompleteThrow(YutThrowResult result)
    {
        foreach (YutStick stick in sticks)
        {
            while (stick != null && stick.IsAnimating)
            {
                yield return null;
            }
        }

        IsThrowing = false;
        ShowResult(result);
        ThrowCompleted?.Invoke(result);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        IsThrowing = false;
    }

    private void ShowResult(YutThrowResult result)
    {
        if (resultImage == null)
        {
            return;
        }

        resultImage.sprite = result switch
        {
            YutThrowResult.Do => doSprite,
            YutThrowResult.Gae => gaeSprite,
            YutThrowResult.Geol => geolSprite,
            YutThrowResult.Yut => yutSprite,
            YutThrowResult.Mo => moSprite,
            _ => null
        };
        resultImage.enabled = resultImage.sprite != null;
    }

    private bool HasReadySticks()
    {
        if (sticks == null || sticks.Length != StickCount)
        {
            return false;
        }

        var uniqueSticks = new HashSet<YutStick>();
        foreach (YutStick stick in sticks)
        {
            if (stick == null || !stick.CanThrow || !uniqueSticks.Add(stick))
            {
                return false;
            }
        }

        return true;
    }
}

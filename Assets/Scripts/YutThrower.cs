using System;
using System.Collections.Generic;
using UnityEngine;

public enum YutThrowResult
{
    Do,
    Gae,
    Geol,
    Yut,
    Mo
}

public class YutThrower : MonoBehaviour
{
    private const int StickCount = 4;

    [SerializeField] private YutStick[] sticks = new YutStick[StickCount];

    public YutThrowResult? LastResult { get; private set; }
    public event Action<YutThrowResult> ThrowCompleted;

    public void Throw()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (!HasReadySticks())
        {
            Debug.LogWarning("Assign four distinct, active yut sticks with both face sprites.", this);
            return;
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
        ThrowCompleted?.Invoke(result);
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

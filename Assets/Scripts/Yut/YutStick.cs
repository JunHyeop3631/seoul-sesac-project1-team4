using System;
using UnityEngine;

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

    internal bool CanThrow => isActiveAndEnabled &&
        faceRenderer != null && bellySprite != null && backSprite != null;

    public YutFace Throw()
    {
        if (!CanThrow)
        {
            throw new InvalidOperationException("The yut stick's display is unavailable.");
        }

        YutFace face = UnityEngine.Random.Range(0, 2) == 0 ? YutFace.Back : YutFace.Belly;
        faceRenderer.sprite = face == YutFace.Belly ? bellySprite : backSprite;
        return face;
    }
}

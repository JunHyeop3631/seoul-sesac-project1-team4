using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class SpecialCurrencyDigits : MonoBehaviour
{
    [SerializeField] private Sprite[] sprites = new Sprite[10];
    [SerializeField] private Vector2 digitSize = new Vector2(26, 32);
    [SerializeField, Min(0)] private int value;
    private readonly List<Image> images = new List<Image>();
    public int Value => value;
    private void Awake() { SetValue(value); }

    public void SetValue(int amount)
    {
        value = Mathf.Max(0, amount);
        string text = value.ToString(CultureInfo.InvariantCulture);
        float width = Mathf.Min(digitSize.x, ((RectTransform)transform).rect.width / text.Length);
        while (images.Count < text.Length)
        {
            var child = new GameObject("Digit", typeof(RectTransform), typeof(Image));
            child.layer = gameObject.layer; child.transform.SetParent(transform, false);
            var image = child.GetComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
            images.Add(image);
        }
        for (int i = 0; i < images.Count; i++)
        {
            images[i].gameObject.SetActive(i < text.Length);
            if (i >= text.Length) continue;
            images[i].sprite = sprites[text[i] - '0'];
            images[i].rectTransform.sizeDelta = new Vector2(width, digitSize.y);
            images[i].rectTransform.anchoredPosition = new Vector2((i - (text.Length - 1) * .5f) * width, 0);
        }
    }
}

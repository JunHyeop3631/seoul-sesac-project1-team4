using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class GoldNumberDisplay : MonoBehaviour
{
    [SerializeField, Min(1f)] private float digitWidth = 26f;
    [SerializeField, Min(1f)] private float digitHeight = 34f;
    [SerializeField, Min(1f)] private float displayWidth = 148f;
    [SerializeField] private float rightPadding = 18f;

    [SerializeField, Min(0)] private int value;
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];

    private readonly List<Image> digits = new();

    private void Awake()
    {
        SetValue(value);
    }

    public void SetValue(int amount)
    {
        value = Mathf.Max(0, amount);
        string number = value.ToString(CultureInfo.InvariantCulture);
        float width = Mathf.Min(digitWidth, displayWidth / number.Length);

        while (digits.Count < number.Length)
        {
            var digitObject = new GameObject("Digit", typeof(RectTransform), typeof(Image));
            digitObject.transform.SetParent(transform, false);

            Image digit = digitObject.GetComponent<Image>();
            digit.preserveAspect = true;
            digit.raycastTarget = false;
            digit.rectTransform.anchorMin = digit.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            digit.rectTransform.pivot = new Vector2(1f, 0.5f);
            digits.Add(digit);
        }

        for (int index = 0; index < number.Length; index++)
        {
            Image digit = digits[index];
            digit.sprite = digitSprites[number[index] - '0'];
            digit.rectTransform.sizeDelta = new Vector2(width, digitHeight);
            digit.rectTransform.anchoredPosition =
                new Vector2(-rightPadding - (number.Length - index - 1) * width, 0f);
            digit.gameObject.SetActive(true);
        }

        for (int index = number.Length; index < digits.Count; index++)
        {
            digits[index].gameObject.SetActive(false);
        }
    }
}

using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Presentation only. TurnController owns completion, rewards and preparation.
public sealed class WaveCompleteBanner : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Image artwork;
    [SerializeField] private CanvasGroup whole;
    [SerializeField] private CanvasGroup labels;
    [SerializeField] private Image waveWord;
    [SerializeField] private Text waveLabel;
    [SerializeField] private Image divider;
    [SerializeField] private Image coin;
    [SerializeField] private Text rewardLabel;
    [SerializeField, Min(.01f)] private float unfoldSeconds = .4f;
    [SerializeField, Min(0f)] private float holdSeconds = 1.5f;
    [SerializeField, Min(.01f)] private float fadeSeconds = .3f;
    private float elapsed;

    public bool IsPlaying { get; private set; }
    public int DisplayedWave { get; private set; }
    public int DisplayedReward { get; private set; }

    private void Awake()
    {
        foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        foreach (var group in GetComponentsInChildren<CanvasGroup>(true))
        {
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        Hide();
    }

    public void Play(int completedWave, int grantedReward)
    {
        if (completedWave < 1 || frames == null || frames.Length == 0) return;
        DisplayedWave = completedWave;
        DisplayedReward = Mathf.Max(0, grantedReward);
        waveLabel.text = completedWave.ToString(CultureInfo.InvariantCulture) + " 방어 성공";
        rewardLabel.text = "+" + DisplayedReward.ToString(CultureInfo.InvariantCulture);
        LayoutLabels();
        elapsed = 0f;
        IsPlaying = true;
        whole.alpha = 1f;
        labels.alpha = 0f;
        artwork.sprite = frames[0];
    }

    private void Update()
    {
        if (!IsPlaying) return;
        elapsed += Time.unscaledDeltaTime;
        float open = Mathf.Max(.01f, unfoldSeconds);
        float fade = Mathf.Max(.01f, fadeSeconds);
        artwork.sprite = frames[Mathf.Clamp(Mathf.FloorToInt(elapsed / open * (frames.Length - 1)), 0, frames.Length - 1)];
        labels.alpha = elapsed >= open ? 1f : 0f;
        whole.alpha = 1f - Mathf.Clamp01((elapsed - open - holdSeconds) / fade);
        if (elapsed >= open + holdSeconds + fade) Hide();
    }

    public void Hide()
    {
        IsPlaying = false;
        if (whole != null) whole.alpha = 0f;
    }

    private void OnDisable() { Hide(); }

    private void LayoutLabels()
    {
        float labelWidth = waveLabel.preferredWidth;
        float rewardWidth = rewardLabel.preferredWidth;
        float total = 99f + 18f + labelWidth + 24f + 1f + 24f + 30f + 12f + rewardWidth;
        float left = -total * .5f;
        Place(waveWord.rectTransform, ref left, 99f, 34f, 18f);
        Place(waveLabel.rectTransform, ref left, labelWidth, 56f, 24f);
        Place(divider.rectTransform, ref left, 1f, 28f, 24f);
        Place(coin.rectTransform, ref left, 30f, 30f, 12f);
        Place(rewardLabel.rectTransform, ref left, rewardWidth, 56f, 0f);
        // Keep long rewards/wave numbers inside the parchment without clipping.
        float scale = Mathf.Min(1f, 780f / total);
        labels.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static void Place(RectTransform rect, ref float left, float width, float height, float gap)
    {
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(left + width * .5f, 0f);
        left += width + gap;
    }
}

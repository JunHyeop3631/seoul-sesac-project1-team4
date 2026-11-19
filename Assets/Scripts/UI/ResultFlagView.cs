using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Presentation and navigation only. TurnController owns the outcome.</summary>
public sealed class ResultFlagView : MonoBehaviour
{
    [Serializable]
    private sealed class Theme
    {
        public Sprite banner, title, waveLabel, slash;
        public Sprite homeNormal, homePressed, retryNormal, retryPressed;
        public Sprite[] numbers;
        public Vector2 bannerSize, bannerOffset, titleSize, titleOffset;
    }
    [SerializeField] private CanvasGroup whole;
    [SerializeField] private Image dimmer;
    [SerializeField] private RectTransform flag;
    [SerializeField] private Image bannerImage, titleImage, waveLabelImage;
    [SerializeField] private RectTransform waveValue;
    [SerializeField] private CanvasGroup buttonOpacity;
    [SerializeField] private Button homeButton, retryButton;
    [SerializeField] private Theme victory, defeat;
    [SerializeField] private string homeScene = "Title";
    [Header("Result Audio")]
    [SerializeField] private AudioClip victorySound;
    [SerializeField, Range(0f, 1f)] private float victoryVolume = 1f;
    [SerializeField] private AudioClip defeatSound;
    [SerializeField, Range(0f, 1f)] private float defeatVolume = 1f;
    private AudioSource resultAudioSource;
    private Coroutine activeRoutine;
    private Vector2 restPosition;
    private float previousTimeScale;
    private bool ownsPause, navigating;
    private string retryScene;
    public bool IsVisible { get; private set; }
    public bool ButtonsReady { get; private set; }
    public int ReachedWave { get; private set; }
    public int TotalWaves { get; private set; }

    private void Awake()
    {
        restPosition = flag.anchoredPosition;
        homeButton.onClick.AddListener(GoHome); retryButton.onClick.AddListener(Retry);
        Hide();
    }
    public void Show(bool won, int reachedWave, int totalWaves, string gameplayScene)
    {
        if (IsVisible) return;
        IsVisible = true; ReachedWave = Mathf.Max(1, reachedWave); TotalWaves = Mathf.Max(1, totalWaves);
        retryScene = gameplayScene;
        ApplyTheme(won ? victory : defeat);
        transform.SetAsLastSibling();
        whole.alpha = 1f; whole.blocksRaycasts = true; whole.interactable = true;
        previousTimeScale = Time.timeScale; ownsPause = true; Time.timeScale = 0f;
        EventSystem.current?.SetSelectedGameObject(null);
        SetButtonsReady(false);
        activeRoutine = StartCoroutine(Enter(won ? victorySound : defeatSound, won ? victoryVolume : defeatVolume));
    }
    private void ApplyTheme(Theme theme)
    {
        bannerImage.sprite = theme.banner; bannerImage.rectTransform.sizeDelta = theme.bannerSize;
        bannerImage.rectTransform.anchoredPosition = theme.bannerOffset;
        titleImage.sprite = theme.title; titleImage.rectTransform.sizeDelta = theme.titleSize;
        titleImage.rectTransform.anchoredPosition = theme.titleOffset;
        waveLabelImage.sprite = theme.waveLabel;
        SetButtonTheme(homeButton, theme.homeNormal, theme.homePressed);
        SetButtonTheme(retryButton, theme.retryNormal, theme.retryPressed);
        foreach (Transform child in waveValue) Destroy(child.gameObject);
        var sprites = new List<Sprite>();
        AddNumber(sprites, theme, ReachedWave); sprites.Add(theme.slash); AddNumber(sprites, theme, TotalWaves);
        float width = 0f;
        foreach (var sprite in sprites) width += sprite.rect.width * .5f + 8f;
        width -= 8f; waveValue.localScale = Vector3.one * Mathf.Min(1f, 510f / width);
        float x = -width / 2f;
        foreach (var sprite in sprites)
        {
            var obj = new GameObject(sprite.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.layer = gameObject.layer;
            var image = obj.GetComponent<Image>(); image.sprite = sprite; image.raycastTarget = false;
            var rect = image.rectTransform; rect.SetParent(waveValue, false);
            rect.sizeDelta = sprite.rect.size * .5f;
            rect.anchoredPosition = new Vector2(x + rect.sizeDelta.x / 2f, 0f);
            x += rect.sizeDelta.x + 8f;
        }
    }
    private static void AddNumber(List<Sprite> sprites, Theme theme, int value)
    {
        if (value <= 10) { sprites.Add(theme.numbers[value]); return; }
        foreach (char digit in value.ToString(System.Globalization.CultureInfo.InvariantCulture)) sprites.Add(theme.numbers[digit - '0']);
    }
    private static void SetButtonTheme(Button button, Sprite normal, Sprite pressed)
    {
        button.image.sprite = normal; button.image.overrideSprite = null;
        button.transition = Selectable.Transition.SpriteSwap;
        button.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
        button.spriteState = new SpriteState { highlightedSprite = normal, selectedSprite = normal,
            pressedSprite = pressed, disabledSprite = normal };
    }
    private void PlayResultSound(AudioClip sound, float volume)
    {
        if (sound == null || volume <= 0f) return;
        if (resultAudioSource == null)
        {
            resultAudioSource = gameObject.AddComponent<AudioSource>();
            resultAudioSource.playOnAwake = false;
            resultAudioSource.loop = false;
            resultAudioSource.spatialBlend = 0f;
            resultAudioSource.ignoreListenerPause = true;
            resultAudioSource.priority = 32;
        }
        resultAudioSource.Stop();
        resultAudioSource.clip = sound;
        resultAudioSource.volume = Mathf.Clamp01(volume);
        resultAudioSource.Play();
    }
    private IEnumerator Enter(AudioClip sound, float volume)
    {
        float elapsed = 0f;
        bool soundStarted = false;
        while (elapsed < .98f)
        {
            // The entrance has a short lead-in before the flag begins to fall.
            if (!soundStarted && elapsed >= .12f)
            {
                soundStarted = true;
                PlayResultSound(sound, volume);
            }
            float drop = Mathf.Clamp01((elapsed - .12f) / .56f);
            float y = 1130f * Mathf.Pow(1f - drop, 3f), angle = 0f;
            if (elapsed >= .68f)
            {
                float settle = Mathf.Clamp01((elapsed - .68f) / .26f);
                y = -8f * Mathf.Sin(settle * Mathf.PI * 2f) * (1f - settle);
                angle = .35f * Mathf.Sin(settle * Mathf.PI * 2f) * (1f - settle);
            }
            flag.anchoredPosition = restPosition + new Vector2(0f, y);
            flag.localRotation = Quaternion.Euler(0f, 0f, angle);
            dimmer.color = new Color(12f / 255f, 17f / 255f, 33f / 255f, .64f * Mathf.Clamp01(elapsed / .2f));
            buttonOpacity.alpha = Mathf.Clamp01((elapsed - .76f) / .2f);
            yield return null; elapsed += Time.unscaledDeltaTime;
        }
        flag.anchoredPosition = restPosition; flag.localRotation = Quaternion.identity;
        buttonOpacity.alpha = 1f; SetButtonsReady(true); activeRoutine = null;
        EventSystem.current?.SetSelectedGameObject(retryButton.gameObject);
    }
    private void SetButtonsReady(bool ready)
    {
        ButtonsReady = ready; homeButton.interactable = ready; retryButton.interactable = ready; buttonOpacity.interactable = ready;
    }
    private void GoHome() => Navigate(homeScene, homeButton);
    private void Retry() => Navigate(retryScene, retryButton);
    private void Navigate(string scene, Button pressed)
    {
        if (!IsVisible || !ButtonsReady || navigating) return;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        { Debug.LogError("Result screen destination is not registered: " + scene, this); return; }
        navigating = true; SetButtonsReady(false); pressed.image.overrideSprite = pressed.spriteState.pressedSprite;
        activeRoutine = StartCoroutine(LoadScene(scene));
    }
    private IEnumerator LoadScene(string scene)
    {
        yield return new WaitForSecondsRealtime(.08f);
        ownsPause = false; Time.timeScale = 1f; SceneManager.LoadScene(scene);
    }
    public void Hide()
    {
        if (resultAudioSource != null) resultAudioSource.Stop();
        if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
        if (ownsPause) { Time.timeScale = previousTimeScale; ownsPause = false; }
        IsVisible = false; navigating = false; SetButtonsReady(false);
        whole.alpha = 0f; whole.blocksRaycasts = false; whole.interactable = false;
        flag.anchoredPosition = restPosition; flag.localRotation = Quaternion.identity;
    }
    private void OnDisable() { Hide(); }
}

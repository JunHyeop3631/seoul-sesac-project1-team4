using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterInfoPanel : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    private enum UnitKind
    {
        DoTaoist,
        GaeWarrior,
        GeolSpearman,
        YutMaceman,
        MoArcher,
        WhiteTiger
    }

    [Serializable]
    private sealed class Profile
    {
        public UnitKind unitKind = UnitKind.DoTaoist;
        public Sprite nameSprite = null;
        public Sprite idleLeft = null;
        public Sprite evolvedIdleLeft = null;
        [TextArea(3, 3)] public string story = string.Empty;
    }

    [SerializeField] private Image characterName;
    [SerializeField] private Image characterPortrait;
    [SerializeField] private GameObject firstStar;
    [SerializeField] private GameObject secondStar;
    [SerializeField] private Text attackValue;
    [SerializeField] private Text attackSpeedValue;
    [SerializeField] private Text[] storyLines = Array.Empty<Text>();
    [SerializeField] private Profile[] profiles = Array.Empty<Profile>();
    [SerializeField] private CanvasGroup[] defaultContent = Array.Empty<CanvasGroup>();
    [SerializeField] private SpriteRenderer[] defaultWorldContent = Array.Empty<SpriteRenderer>();
    [SerializeField] private GameObject[] storyDecorations = Array.Empty<GameObject>();
    [SerializeField] private CanvasGroup infoContent;
    [SerializeField, Min(0.01f)] private float transitionDuration = .24f;
    private enum Phase { ToInfo, SwapOut, ToDefault, Info }
    private Phase phase;
    private float blend = -1f;
    private Ally displayedAlly;
    private Color[] previousColors;
    public bool IsTransitioning => gameObject.activeSelf && phase != Phase.Info;
    private float[] previousAlpha;
    private bool[] previousInteractable, previousRaycasts, previousRenderers;
    private float displayedDamage, displayedSpeed;
    private int displayedStage;
    public Ally CurrentAlly { get; private set; }

    public void Show(Ally ally)
    {
        if (turnController == null || !turnController.CanUsePreparationInput ||
            ally == null || !ally.isActiveAndEnabled || !TryGetKind(ally, out UnitKind kind))
        {
            gameObject.SetActive(false);
            return;
        }

        Profile profile = Array.Find(profiles, entry => entry != null && entry.unitKind == kind);
        if (profile == null)
        {
            gameObject.SetActive(false);
            return;
        }
        bool opening = !gameObject.activeSelf;
        CurrentAlly = ally;
        turnController.CloseAuxiliaryPanelsForInfo();
        if (opening)
        {
            Populate(ally); phase = Phase.ToInfo; gameObject.SetActive(true);
        }
        else if (displayedAlly != ally && blend > 0f)
            phase = Phase.SwapOut;
        else
        {
            Populate(ally); phase = Phase.ToInfo;
        }
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        CurrentAlly = null; phase = Phase.ToDefault;
    }

    private void Populate(Ally ally)
    {
        TryGetKind(ally, out UnitKind kind);
        Profile profile = Array.Find(profiles, entry => entry != null && entry.unitKind == kind);
        displayedAlly = ally;
        displayedDamage = ally.AttackDamage; displayedSpeed = ally.AttacksPerSecond; displayedStage = ally.EvolutionStage;

        string[] lines = profile.story.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < storyLines.Length; i++)
        {
            if (storyLines[i] != null)
                storyLines[i].text = i < lines.Length ? lines[i] : string.Empty;
        }
        bool hasStory = !string.IsNullOrWhiteSpace(profile.story);
        foreach (var decoration in storyDecorations)
            if (decoration != null) decoration.SetActive(hasStory);

        bool evolved = ally.EvolutionStage >= 2;
        if (characterPortrait != null)
        {
            characterPortrait.sprite = evolved ? profile.evolvedIdleLeft : profile.idleLeft;
            characterPortrait.enabled = characterPortrait.sprite != null;
            characterPortrait.preserveAspect = true;
        }
        if (characterName != null)
        {
            characterName.sprite = profile.nameSprite;
            characterName.enabled = profile.nameSprite != null;
        }
        if (firstStar != null)
            firstStar.SetActive(true);
        if (secondStar != null)
            secondStar.SetActive(evolved);
        if (attackValue != null)
        {
            attackValue.text = ally.AttackDamage.ToString("0.#", CultureInfo.InvariantCulture);
        }
        if (attackSpeedValue != null)
        {
            attackSpeedValue.text = ally.AttacksPerSecond.ToString("0.0", CultureInfo.InvariantCulture) + "회/초";
        }

    }

    private void OnEnable()
    {
        if (turnController == null || !turnController.CanUsePreparationInput || CurrentAlly == null)
        {
            gameObject.SetActive(false);
            return;
        }
        previousAlpha = new float[defaultContent.Length];
        previousInteractable = new bool[defaultContent.Length]; previousRaycasts = new bool[defaultContent.Length];
        for (int i = 0; i < defaultContent.Length; i++)
        {
            var group = defaultContent[i]; if (group == null) continue;
            previousAlpha[i] = group.alpha; previousInteractable[i] = group.interactable; previousRaycasts[i] = group.blocksRaycasts;
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
        }
        previousRenderers = new bool[defaultWorldContent.Length];
        previousColors = new Color[defaultWorldContent.Length];
        for (int i = 0; i < defaultWorldContent.Length; i++)
        {
            if (defaultWorldContent[i] == null) continue;
            previousRenderers[i] = defaultWorldContent[i].enabled; previousColors[i] = defaultWorldContent[i].color;
        }
        blend = -1f; ApplyVisibility();
    }

    private void LateUpdate()
    {
        if (turnController == null || !turnController.CanUsePreparationInput ||
            (phase != Phase.ToDefault && CurrentAlly == null) ||
            (CurrentAlly != null && !CurrentAlly.isActiveAndEnabled))
        {
            gameObject.SetActive(false); return;
        }
        float goal = phase == Phase.ToDefault ? -1f : phase == Phase.SwapOut ? 0f : 1f;
        blend = Mathf.MoveTowards(blend, goal, Time.unscaledDeltaTime * 2f / Mathf.Max(.01f, transitionDuration));
        if (phase == Phase.SwapOut && blend == 0f)
        {
            if (CurrentAlly == null) { Hide(); }
            else { Populate(CurrentAlly); phase = Phase.ToInfo; }
        }
        ApplyVisibility();
        if (phase == Phase.ToDefault && blend == -1f) { gameObject.SetActive(false); return; }
        if (phase == Phase.ToInfo && blend == 1f) phase = Phase.Info;
        if (phase == Phase.Info && CurrentAlly != null &&
            (CurrentAlly.AttackDamage != displayedDamage || CurrentAlly.AttacksPerSecond != displayedSpeed || CurrentAlly.EvolutionStage != displayedStage))
            Populate(CurrentAlly);
    }

    private void ApplyVisibility()
    {
        float defaults = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-blend));
        if (infoContent != null) infoContent.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));
        for (int i = 0; i < defaultContent.Length; i++)
        {
            var group = defaultContent[i]; if (group == null) continue;
            group.alpha = previousAlpha[i] * defaults; group.interactable = false; group.blocksRaycasts = false;
        }
        for (int i = 0; i < defaultWorldContent.Length; i++)
        {
            var renderer = defaultWorldContent[i]; if (renderer == null) continue;
            Color color = previousColors[i]; color.a *= defaults; renderer.color = color;
            renderer.enabled = previousRenderers[i] && defaults > 0f;
        }
    }

    private void OnDisable()
    {
        CurrentAlly = null; displayedAlly = null;
        if (infoContent != null) infoContent.alpha = 0f;
        if (previousAlpha != null)
            for (int i = 0; i < defaultContent.Length; i++)
            {
                var group = defaultContent[i]; if (group == null) continue;
                group.alpha = previousAlpha[i]; group.interactable = previousInteractable[i]; group.blocksRaycasts = previousRaycasts[i];
            }
        if (previousRenderers != null)
            for (int i = 0; i < defaultWorldContent.Length; i++)
                if (defaultWorldContent[i] != null)
                {
                    defaultWorldContent[i].enabled = previousRenderers[i]; defaultWorldContent[i].color = previousColors[i];
                }
        previousAlpha = null; previousRenderers = null;
    }

    private static bool TryGetKind(Ally ally, out UnitKind kind)
    {
        switch (ally)
        {
            case Do_Taoist _: kind = UnitKind.DoTaoist; return true;
            case Gae_Warrior _: kind = UnitKind.GaeWarrior; return true;
            case Geol_Spearman _: kind = UnitKind.GeolSpearman; return true;
            case Yut_Maceman _: kind = UnitKind.YutMaceman; return true;
            case Mo_Archer _: kind = UnitKind.MoArcher; return true;
            case WhiteTiger _: kind = UnitKind.WhiteTiger; return true;
            default: kind = default; return false;
        }
    }
}

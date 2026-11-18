using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SpecialSummonUI : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private SpecialCurrency currency;
    [SerializeField] private SpecialSummonController executor;
    [SerializeField] private Text availabilityText;
    [SerializeField] private GameObject startCellHint;
    [SerializeField] private AudioSource clickSound, hoverSound;
    [SerializeField] private Button hudButton, closeButton, randomButton;
    [SerializeField] private GameObject overlay;
    [SerializeField] private SpecialCurrencyDigits hudDigits, ownedDigits;
    public bool IsOpen => overlay != null && overlay.activeSelf;
    private bool CanOpen => isActiveAndEnabled && turnController != null &&
        turnController.CanOpenPreparationPanel && waveManager != null && !waveManager.IsRunning;

    private void Awake()
    {
        // Match the HUD hit area to its visible silhouette, including rounded corners.
        hudButton.image.alphaHitTestMinimumThreshold = 0.1f;
        hudButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
        randomButton.onClick.AddListener(Summon);
        randomButton.interactable = false;
        AddHover(hudButton); AddHover(closeButton); AddHover(randomButton);
        overlay.SetActive(false);
    }
    private void OnEnable()
    {
        if (currency != null) currency.Changed += RefreshBalance;
        if (executor != null) executor.Summoned += HandleSummoned;
        if (turnController != null)
        {
            turnController.SetSpecialSummonUI(this);
            turnController.BattleStarted += Dismiss;
            turnController.BattleWon += Dismiss;
            turnController.BattleLost += Dismiss;
        }
    }
    private void Start() { RefreshBalance(currency != null ? currency.Balance : 0); RefreshAvailability(); }
    private void OnDisable()
    {
        if (currency != null) currency.Changed -= RefreshBalance;
        if (executor != null) executor.Summoned -= HandleSummoned;
        if (turnController != null)
        {
            turnController.SetSpecialSummonUI(null);
            turnController.BattleStarted -= Dismiss;
            turnController.BattleWon -= Dismiss;
            turnController.BattleLost -= Dismiss;
        }
        Dismiss();
    }
    private void Update()
    {
        RefreshAvailability();
    }
    private void RefreshAvailability()
    {
        hudButton.interactable = CanOpen;
        randomButton.interactable = IsOpen && CanOpen && executor != null && executor.CanSummon;
        if (!CanOpen && IsOpen && (executor == null || !executor.IsSummoning)) Dismiss();
        string reason = executor != null && !executor.HasSpace
            ? "시작 칸 공간이 부족합니다. (필요 인구 3)"
            : currency != null && currency.Balance < SpecialSummonController.Cost
                ? "특별재화가 부족합니다. (필요 30)" : "";
        if (availabilityText != null && availabilityText.text != reason) availabilityText.text = reason;
        if (startCellHint != null) startCellHint.SetActive(reason.Length == 0);
        closeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = randomButton.interactable ? randomButton : closeButton,
            selectOnDown = randomButton.interactable ? randomButton : closeButton,
            selectOnLeft = closeButton, selectOnRight = closeButton };
        if (!randomButton.interactable && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == randomButton.gameObject)
            EventSystem.current.SetSelectedGameObject(IsOpen ? closeButton.gameObject : null);
    }
    public void Summon()
    {
        if (!IsOpen || !CanOpen || executor == null) return;
        if (executor.TrySummon() && clickSound != null) clickSound.Play();
        RefreshAvailability();
    }
    private void HandleSummoned(Ally ally) => Dismiss();
    public void Open()
    {
        if (!CanOpen || IsOpen) return;
        RefreshBalance(currency != null ? currency.Balance : 0);
        transform.SetAsLastSibling(); overlay.SetActive(true); RefreshAvailability();
        if (clickSound != null) clickSound.Play();
        EventSystem.current?.SetSelectedGameObject(closeButton.gameObject);
    }
    public void Close()
    {
        if (!IsOpen) return;
        Dismiss();
        if (clickSound != null) clickSound.Play();
        if (CanOpen) EventSystem.current?.SetSelectedGameObject(hudButton.gameObject);
    }
    private void Dismiss()
    {
        if (overlay == null) return;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(overlay.transform)) EventSystem.current.SetSelectedGameObject(null);
        overlay.SetActive(false);
    }
    private void RefreshBalance(int amount)
    {
        hudDigits.SetValue(amount); ownedDigits.SetValue(amount); RefreshAvailability();
    }
    private void AddHover(Button button)
    {
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => { if (button.IsInteractable() && hoverSound != null) hoverSound.Play(); });
        trigger.triggers.Add(entry);
    }
}

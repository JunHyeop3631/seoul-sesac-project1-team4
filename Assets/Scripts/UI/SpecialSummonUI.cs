using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// This increment opens the approved window; it does not spend or summon.
public sealed class SpecialSummonUI : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private SpecialCurrency currency;
    [SerializeField] private AudioSource clickSound, hoverSound;
    [SerializeField] private Button hudButton, closeButton, randomButton;
    [SerializeField] private GameObject overlay;
    [SerializeField] private SpecialCurrencyDigits hudDigits, ownedDigits;
    public bool IsOpen => overlay != null && overlay.activeSelf;
    private bool CanOpen => turnController != null && waveManager != null &&
        turnController.RemainingLives > 0 && !turnController.IsBattleWon &&
        !waveManager.IsRunning && Time.timeScale > 0;

    private void Awake()
    {
        hudButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
        // Kept visibly disabled until the actual summon transaction is implemented.
        randomButton.interactable = false;
        AddHover(hudButton); AddHover(closeButton);
        overlay.SetActive(false);
    }
    private void OnEnable()
    {
        if (currency != null) currency.Changed += RefreshBalance;
        if (turnController != null)
        {
            turnController.BattleStarted += Dismiss;
            turnController.BattleWon += Dismiss;
            turnController.BattleLost += Dismiss;
        }
    }
    private void Start() { RefreshBalance(currency != null ? currency.Balance : 0); RefreshAvailability(); }
    private void OnDisable()
    {
        if (currency != null) currency.Changed -= RefreshBalance;
        if (turnController != null)
        {
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
        if (!CanOpen && IsOpen) Dismiss();
    }
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
    private void RefreshBalance(int amount) { hudDigits.SetValue(amount); ownedDigits.SetValue(amount); }
    private void AddHover(Button button)
    {
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => { if (button.IsInteractable() && hoverSound != null) hoverSound.Play(); });
        trigger.triggers.Add(entry);
    }
}

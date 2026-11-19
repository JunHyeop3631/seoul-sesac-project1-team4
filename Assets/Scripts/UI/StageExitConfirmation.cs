using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StageExitConfirmation : MonoBehaviour
{
    [SerializeField] private TurnController turn;
    [SerializeField] private YutActionController actions;
    [SerializeField] private StageItemUseController itemUse;
    [SerializeField] private ShopPurchaseController purchases;
    [SerializeField] private StageCurrency currency;
    [SerializeField] private GameObject navigation, modal;
    [SerializeField] private Button homeButton, quitButton, cancelButton, confirmButton;
    [SerializeField] private Text title, message, confirmLabel;
    [SerializeField] private string homeScene = "Title";
    private bool quitting, ownsPause;
    private float previousTimeScale;
    private GameObject previousSelection;
    public bool IsOpen { get; private set; }
    public bool IsCommitting { get; private set; }
    private bool CanRequest => !IsCommitting && !IsOpen && this != null && isActiveAndEnabled && turn != null && turn.isActiveAndEnabled &&
        !turn.IsBattleWon && !turn.IsBattleLost && (currency == null || !currency.IsNotifying) &&
        (actions == null || (!actions.IsResolvingMove && !actions.IsResolvingSummon)) &&
        (itemUse == null || !itemUse.IsUsing) && (purchases == null || !purchases.IsPurchasing);

    private void Awake()
    {
        modal.SetActive(false);
        homeButton.onClick.AddListener(RequestHome); quitButton.onClick.AddListener(RequestQuit);
        cancelButton.onClick.AddListener(Cancel); confirmButton.onClick.AddListener(Confirm);
    }
    private void OnEnable()
    {
        if (turn != null) { turn.BattleWon += DismissForResult; turn.BattleLost += DismissForResult; }
    }
    private void OnDisable()
    {
        if (turn != null) { turn.BattleWon -= DismissForResult; turn.BattleLost -= DismissForResult; }
        if (ownsPause) { Time.timeScale = previousTimeScale; ownsPause = false; }
        IsOpen = false;
        if (modal != null) modal.SetActive(false);
    }
    private void Update()
    {
        navigation.SetActive(turn != null && !turn.IsBattleWon && !turn.IsBattleLost);
        homeButton.interactable = quitButton.interactable = CanRequest;
        if (IsOpen && !IsCommitting && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
    }
    public void RequestHome() => Open(false);
    public void RequestQuit() => Open(true);
    private void Open(bool quit)
    {
        if (!CanRequest) return;
        quitting = quit; previousTimeScale = Time.timeScale;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        IsOpen = true; ownsPause = true; Time.timeScale = 0f;
        title.text = quit ? "게임을 종료할까요?" : "메인 화면으로 이동할까요?";
        message.text = "현재 진행이 사라집니다.\n취소하면 현재 게임으로 돌아갑니다.";
        confirmLabel.text = quit ? "종료" : "이동";
        modal.SetActive(true); transform.SetAsLastSibling();
        EventSystem.current?.SetSelectedGameObject(cancelButton.gameObject);
    }
    public void Cancel()
    {
        if (!IsOpen || IsCommitting) return;
        if (ownsPause) { Time.timeScale = previousTimeScale; ownsPause = false; }
        IsOpen = false; modal.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
    }
    private void DismissForResult()
    {
        if (!IsOpen) return;
        // A late terminal callback owns the pause now, independently of subscriber ordering.
        ownsPause = false; IsOpen = false; modal.SetActive(false); Time.timeScale = 0f;
    }
    public void Confirm()
    {
        if (!IsOpen || IsCommitting) return;
        if (!quitting && !Application.CanStreamedLevelBeLoaded(homeScene))
        { Debug.LogError("Exit destination is not registered: " + homeScene, this); return; }
        IsCommitting = true; ownsPause = false; IsOpen = false; Time.timeScale = 1f;
        modal.SetActive(false); cancelButton.interactable = confirmButton.interactable = false;
        if (!quitting) { SceneManager.LoadScene(homeScene, LoadSceneMode.Single); return; }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

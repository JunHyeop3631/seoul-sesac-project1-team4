using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class TurnController : MonoBehaviour
{
    public const int YutThrowCost = 20;
    [SerializeField] private HeavenlyLuckTalismanEffect heavenlyLuck;
    [SerializeField] private GoldNumberDisplay throwCostDisplay;
    public int CurrentYutThrowCost => heavenlyLuck != null && heavenlyLuck.IsApplied
        ? HeavenlyLuckTalismanEffect.DiscountedThrowCost : YutThrowCost;
    [SerializeField] private HeavenlyMandateEffect heavenlyMandate;
    public int BattleRewardMultiplier => heavenlyMandate != null && heavenlyMandate.IsActive ? 2 : 1;
    [SerializeField] private StageExitConfirmation exitConfirmation;
    public bool IsExitConfirmationOpen => exitConfirmation != null && exitConfirmation.IsOpen;
    private enum YutAction
    {
        None,
        Summon,
        Move
    }

    [SerializeField] private YutThrowController throwController;
    [SerializeField] private YutActionController actionController;
    [SerializeField] private AllyRangeDisplay allyRangeDisplay;
    [SerializeField] private GameObject actionButtons;
    [SerializeField] private GameObject throwButton;
    [SerializeField] private Button summonButton;
    [SerializeField] private Button moveButton;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Button shopButton;
    [SerializeField] private Toggle inventoryToggle;
    [SerializeField] private CharacterInfoPanel characterInfoPanel;
    [SerializeField] private StageCurrency currency;
    [SerializeField] private AudioSource yutResultSound;
    [SerializeField] private AudioClip doSound;
    [SerializeField] private AudioClip gaeSound;
    [SerializeField] private AudioClip geolSound;
    [SerializeField] private AudioClip yutSound;
    [SerializeField] private AudioClip moSound;
    [SerializeField] private AudioClip backdoSound;
    [SerializeField, Min(0)] private int currentTurnNumber = 0;
    [SerializeField, Min(1)] private int startingLives = 10;

    public int CurrentTurnNumber => currentTurnNumber;
    public int RemainingLives { get; private set; }
    public int MaxLives => Mathf.Max(1, startingLives);
    public YutThrowResult? CurrentYutResult { get; private set; }
    public event Action BattleStarted;
    public event Action BattleWon;
    public event Action BattleLost;
    public event Action PreparationStarted;
    // Completed (one-based) wave and the amount actually credited to the wallet.
    public event Action<int, int> WaveCleared;
    public bool IsBattleWon { get; private set; }
    public bool IsBattleLost { get; private set; }
    internal StageCurrency Currency => currency;
    internal Canvas PreparationCanvas => actionButtons != null ? actionButtons.GetComponentInParent<Canvas>(true) : null;
    private bool preparing;
    private bool endingBattle;
    private SpecialSummonUI specialSummonUI;
    private ShopPurchaseController shopPurchases;
    private StageItemUseController itemUse;
    internal void SetItemUseController(StageItemUseController controller) => itemUse = controller;
    internal void SetShopPurchaseController(ShopPurchaseController controller) => shopPurchases = controller;
    internal void SetSpecialSummonUI(SpecialSummonUI view) => specialSummonUI = view;
    public bool CanUsePreparationInput => isActiveAndEnabled && preparing && !endingBattle &&
        RemainingLives > 0 && !IsBattleWon && !IsBattleLost && Time.timeScale > 0f &&
        (shopPurchases == null || !shopPurchases.IsPurchasing) &&
        (itemUse == null || !itemUse.IsUsing) &&
        (actionController == null || (!actionController.IsResolvingMove && !actionController.IsResolvingSummon)) &&
        (waveManager == null || !waveManager.IsRunning);
    public bool IsThrowing => throwController != null && throwController.IsThrowing;
    public bool CanEvolve => CanUsePreparationInput && !IsThrowing && !CurrentYutResult.HasValue;
    public bool CanOpenPreparationPanel => CanUsePreparationInput && !IsThrowing;
    public bool CanCancelYutAction => CanUsePreparationInput && !IsThrowing &&
        CurrentYutResult.HasValue && chosenAction == YutAction.Move;
    public bool CanThrowYut => CanUsePreparationInput && !IsThrowing && !CurrentYutResult.HasValue &&
        throwController != null && throwController.isActiveAndEnabled &&
        currency != null && currency.CanAfford(CurrentYutThrowCost) &&
        (shopPanel == null || !shopPanel.activeInHierarchy) &&
        (inventoryPanel == null || !inventoryPanel.activeInHierarchy);
    private YutAction chosenAction;
    private CanvasGroup summonButtonGroup;
    private readonly List<Graphic> throwGraphics = new();
    private readonly Dictionary<Graphic, Color> throwGraphicColors = new();
    private PreparationNotice preparationNotice;

    private void Awake()
    {
        RemainingLives = Mathf.Max(1, startingLives);
        if (summonButton != null)
        {
            summonButtonGroup = summonButton.GetComponent<CanvasGroup>();
            if (summonButtonGroup == null)
                summonButtonGroup = summonButton.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void LoseLife()
    {
        if (RemainingLives == 0 || IsBattleWon || IsBattleLost)
        {
            return;
        }

        RemainingLives = Mathf.Max(0, RemainingLives - 1);
        if (RemainingLives == 0)
            LoseBattle();
    }

    private void LoseBattle()
    {
        if (IsBattleWon || IsBattleLost) return;
        IsBattleLost = true;
        heavenlyMandate?.FinishBattle();
        preparing = false;
        waveManager?.StopWave();
        ClosePreparationUI();
        RefreshYutButtons();
        BattleLost?.Invoke();
    }

    private void HandleBossEscaped()
    {
        if (IsFinalBattleRunning()) LoseBattle();
    }

    private void HandleBossDefeated()
    {
        if (!IsFinalBattleRunning()) return;
        IsBattleWon = true;
        heavenlyMandate?.FinishBattle();
        preparing = false;
        waveManager.StopWave();
        ClosePreparationUI();
        RefreshYutButtons();
        BattleWon?.Invoke();
    }

    private bool IsFinalBattleRunning()
    {
        return isActiveAndEnabled && !preparing && !IsBattleWon && !IsBattleLost &&
            RemainingLives > 0 && waveManager != null && waveManager.IsRunning &&
            currentTurnNumber == waveManager.WaveCount - 1;
    }

    private void OnEnable()
    {
        if (currency != null)
            currency.Changed += HandleCurrencyChanged;
        if (waveManager != null)
        {
            waveManager.WaveCompleted += EndBattle;
            waveManager.EnemyArrived += LoseLife;
            waveManager.BossDefeated += HandleBossDefeated;
            waveManager.BossEscaped += HandleBossEscaped;
        }

        if (throwController != null)
        {
            throwController.ThrowCompleted += ReceiveYutResult;
        }
    }

    private void OnDisable()
    {
        preparing = false;
        if (currency != null)
            currency.Changed -= HandleCurrencyChanged;
        if (waveManager != null)
        {
            waveManager.WaveCompleted -= EndBattle;
            waveManager.EnemyArrived -= LoseLife;
            waveManager.BossDefeated -= HandleBossDefeated;
            waveManager.BossEscaped -= HandleBossEscaped;
        }

        if (throwController != null)
        {
            throwController.ThrowCompleted -= ReceiveYutResult;
        }

        ClosePreparationUI();
    }

    private void LateUpdate()
    {
        if (specialSummonUI != null && specialSummonUI.IsOpen) return;
        if (!CanUsePreparationInput || actionController == null)
            return;

        if (TryConsumeUnavailableResult()) return;
        if (CurrentYutResult.HasValue) RefreshYutButtons();

        Mouse mouse = Mouse.current;
        // Cancellation is independent of left-click hit testing and UI hover.
        if (CanCancelYutAction && mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            CancelYutAction();
            return;
        }

        if (mouse == null || !mouse.leftButton.wasPressedThisFrame ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            return;
        }

        Camera boardCamera = Camera.main;
        if (boardCamera == null)
        {
            return;
        }

        Vector2 worldPoint = boardCamera.ScreenToWorldPoint(mouse.position.ReadValue());
        HandleBoardClick(worldPoint);
    }

    private void HandleBoardClick(Vector2 worldPoint)
    {
        if (!CanUsePreparationInput || actionController == null ||
            (specialSummonUI != null && specialSummonUI.IsOpen)) return;
        if (chosenAction == YutAction.Move)
        {
            if (actionController.TryGetDestinationAt(worldPoint, out int cellId))
            {
                SelectDestination(cellId);
                return;
            }

            Ally ally = actionController.GetAllyAt(worldPoint);
            SelectAlly(ally);
            if (ally == null) allyRangeDisplay?.Hide();
            return;
        }

        Ally clickedAlly = actionController.GetAllyAt(worldPoint);
        if (clickedAlly != null)
        {
            allyRangeDisplay?.Show(clickedAlly);
        }
        else
        {
            allyRangeDisplay?.Hide();
        }
    }

    private void ReceiveYutResult(YutThrowResult result)
    {
        if (!CanUsePreparationInput)
            return;
        AudioClip resultClip = null;

        switch (result)
        {
            case YutThrowResult.Do:
                resultClip = doSound;
                break;
            case YutThrowResult.Gae:
                resultClip = gaeSound;
                break;
            case YutThrowResult.Geol:
                resultClip = geolSound;
                break;
            case YutThrowResult.Yut:
                resultClip = yutSound;
                break;
            case YutThrowResult.Mo:
                resultClip = moSound;
                break;
            case YutThrowResult.Backdo:
                resultClip = backdoSound;
                break;
        }

        if (yutResultSound != null && resultClip != null)
            yutResultSound.PlayOneShot(resultClip);

        chosenAction = YutAction.None;
        if (preparationNotice != null) preparationNotice.Hide();
        actionController?.ClearSelection();
        CurrentYutResult = result;

        if (TryConsumeUnavailableResult()) return;

        RefreshYutButtons();
    }

    private bool TryConsumeUnavailableResult()
    {
        if (!CanUsePreparationInput || IsThrowing || !CurrentYutResult.HasValue || actionController == null)
            return false;
        var result = CurrentYutResult.Value;
        if (actionController.CanDeploy(result) || actionController.CanMove(result)) return false;

        actionController.ClearSelection();
        allyRangeDisplay?.Hide();
        if (characterInfoPanel != null) characterInfoPanel.gameObject.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(null);
        CompleteYutAction();
        ShowPreparationNotice(result == YutThrowResult.Backdo
            ? "빽도로 이동할 수 있는 아군이 없어 결과를 소모했습니다."
            : "소환하거나 이동할 수 있는 곳이 없어 결과를 소모했습니다.");
        return true;
    }

    private void ShowPreparationNotice(string message)
    {
        if (PreparationCanvas == null || actionController == null || actionController.PreparationFont == null)
            return;
        if (preparationNotice == null)
            preparationNotice = PreparationNotice.Create(PreparationCanvas, actionController.PreparationFont);
        preparationNotice.Show(message);
    }

    private void OnDestroy()
    {
        if (preparationNotice != null) Destroy(preparationNotice.gameObject);
    }

    public void ThrowYut()
    {
        if (!CanThrowYut)
            return;

        throwController.Throw();
        if (IsThrowing && preparationNotice != null) preparationNotice.Hide();
        RefreshYutButtons();
    }

    // Called only after the thrower validates all four sticks and locks IsThrowing.
    // The lock is already set when wallet notifications refresh UI or invoke callbacks.
    internal bool TryPayForYutThrow()
    {
        return CanUsePreparationInput && IsThrowing && !CurrentYutResult.HasValue &&
            (shopPanel == null || !shopPanel.activeInHierarchy) &&
            currency != null && currency.TrySpend(CurrentYutThrowCost);
    }

    private void HandleCurrencyChanged(int balance)
    {
        RefreshYutButtons();
    }

    private void RefreshYutButtons()
    {
        if (throwCostDisplay != null) throwCostDisplay.SetValue(CurrentYutThrowCost);
        bool preparing = CanUsePreparationInput;
        bool throwing = throwController != null && throwController.IsThrowing;
        bool choosing = preparing && !throwing && CurrentYutResult.HasValue;
        if (!choosing)
            throwController?.HideResult();
        if (throwButton != null)
            throwButton.SetActive(preparing && !throwing && !CurrentYutResult.HasValue);
        if (actionButtons != null)
            actionButtons.SetActive(choosing);

        bool canSummon = choosing && chosenAction == YutAction.None &&
            actionController != null && actionController.CanDeploy(CurrentYutResult.Value);
        if (summonButton != null)
            summonButton.interactable = canSummon;
        if (summonButtonGroup != null)
            summonButtonGroup.alpha = choosing && !canSummon ? 0.35f : 1f;
        if (moveButton != null)
            moveButton.interactable = choosing && chosenAction == YutAction.None &&
                actionController != null && actionController.CanMove(CurrentYutResult.Value);
        if (shopButton != null)
            shopButton.interactable = CanOpenPreparationPanel;
        if (inventoryToggle != null)
            inventoryToggle.interactable = CanOpenPreparationPanel;
        if (throwButton != null && throwButton.TryGetComponent(out Button button))
        {
            button.interactable = CanThrowYut;
            if (button.targetGraphic != null)
            {
                float brightness = currency != null && currency.CanAfford(CurrentYutThrowCost) ? 1f : 0.45f;
                button.targetGraphic.GetComponentsInChildren(true, throwGraphics);
                foreach (Graphic graphic in throwGraphics)
                {
                    if (!throwGraphicColors.TryGetValue(graphic, out Color original))
                    {
                        original = graphic.color;
                        throwGraphicColors.Add(graphic, original);
                    }
                    graphic.color = new Color(original.r * brightness, original.g * brightness,
                        original.b * brightness, original.a);
                }
            }
        }
    }

    public void OpenShop()
    {
        if (CanOpenPreparationPanel && shopPanel != null)
        {
            SetInventoryOpen(false);
            allyRangeDisplay?.Hide();
            if (characterInfoPanel != null) characterInfoPanel.gameObject.SetActive(false);
            shopPanel.SetActive(true);
        }
        RefreshYutButtons();
    }

    public void CloseShop()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);
        RefreshYutButtons();
    }

    public void SetInventoryOpen(bool open)
    {
        bool allowed = open && CanOpenPreparationPanel;
        if (allowed)
        {
            CloseShop();
            allyRangeDisplay?.Hide();
            if (characterInfoPanel != null) characterInfoPanel.gameObject.SetActive(false);
        }
        if (inventoryPanel != null)
            inventoryPanel.SetActive(allowed);
        if (inventoryToggle != null)
            inventoryToggle.SetIsOnWithoutNotify(allowed);
        RefreshYutButtons();
    }

    private void ClosePreparationUI()
    {
        if (preparationNotice != null) preparationNotice.Hide();
        chosenAction = YutAction.None;
        actionController?.ClearSelection();
        allyRangeDisplay?.Hide();
        if (characterInfoPanel != null)
            characterInfoPanel.gameObject.SetActive(false);
        if (shopPanel != null)
            shopPanel.SetActive(false);
        SetInventoryOpen(false);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    internal void CloseAuxiliaryPanelsForInfo()
    {
        CloseShop();
        SetInventoryOpen(false);
    }

    public void ChooseSummon()
    {
        if (!TryChooseAction(YutAction.Summon))
        {
            return;
        }

        if (actionController.DeployAlly(CurrentYutResult.Value))
        {
            CompleteYutAction();
            return;
        }

        chosenAction = YutAction.None;
        RefreshYutButtons();
    }

    public void ChooseMove()
    {
        TryChooseAction(YutAction.Move);
    }

    public void CancelYutAction()
    {
        if (!CanCancelYutAction)
            return;

        chosenAction = YutAction.None;
        actionController?.ClearSelection();
        allyRangeDisplay?.Hide();
        if (characterInfoPanel != null)
            characterInfoPanel.gameObject.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(null);
        // Keep the prepaid result, units, currency, and unrelated panels intact.
        RefreshYutButtons();
    }

    public void StartBattle()
    {
        // Check the preparation lifetime first; a processing lock also makes
        // CanUsePreparationInput false, but still needs feedback while preparing.
        if (!isActiveAndEnabled || endingBattle || !preparing || RemainingLives == 0 || IsBattleWon || IsBattleLost ||
            Time.timeScale <= 0f || waveManager == null || waveManager.IsRunning)
            return;
        if (actionController != null && actionController.IsResolvingMove)
        {
            ShowPreparationNotice("이동 처리가 끝난 뒤 전투를 시작할 수 있습니다."); return;
        }
        if (actionController != null && actionController.IsResolvingSummon)
        {
            ShowPreparationNotice("소환 처리가 끝난 뒤 전투를 시작할 수 있습니다."); return;
        }
        if (shopPurchases != null && shopPurchases.IsPurchasing)
        {
            ShowPreparationNotice("구매 처리가 끝난 뒤 전투를 시작할 수 있습니다."); return;
        }
        if (itemUse != null && itemUse.IsUsing)
        {
            ShowPreparationNotice("아이템 사용이 끝난 뒤 전투를 시작할 수 있습니다."); return;
        }
        if (IsThrowing)
        {
            ShowPreparationNotice("윷 던지기가 끝난 뒤 결과를 먼저 사용해 주세요."); return;
        }
        if (CurrentYutResult.HasValue)
        {
            ShowPreparationNotice("남은 윷 결과를 소환 또는 이동에 먼저 사용해 주세요."); return;
        }
        if (shopPanel != null && shopPanel.activeInHierarchy)
        {
            ShowPreparationNotice("상점을 닫은 뒤 전투를 시작해 주세요."); return;
        }

        preparing = false;
        heavenlyMandate?.BeginBattle();
        waveManager.StartWave(currentTurnNumber);
        if (waveManager.IsRunning)
        {
            ClosePreparationUI();
            RefreshYutButtons();
            BattleStarted?.Invoke();
        }
        else
        {
            // Invalid wave data must leave preparation usable.
            heavenlyMandate?.CancelBattleStart();
            preparing = !IsBattleWon && !IsBattleLost && RemainingLives > 0;
            RefreshYutButtons();
        }
    }

    private void Start()
    {
        StartTurn();
    }

    private void StartTurn()
    {
        BeginPreparation();
    }

    private void BeginPreparation()
    {
        waveManager?.PrepareWave();
        preparing = true;
        PreparationStarted?.Invoke();
        RefreshYutButtons();
    }

    private void EndBattle()
    {
        if (endingBattle || preparing || RemainingLives == 0 || IsBattleWon || IsBattleLost || waveManager == null || waveManager.IsRunning)
        {
            return;
        }

        endingBattle = true;
        try
        {
            if (currentTurnNumber == waveManager.WaveCount - 1)
            {
                // Final results come from the boss event, never generic completion.
                heavenlyMandate?.FinishBattle();
                return;
            }

            int completedWave = currentTurnNumber + 1;
            int grantedReward = currency != null
                ? currency.Grant(waveManager.GetCompletionReward(currentTurnNumber) * BattleRewardMultiplier) : 0;
            heavenlyMandate?.FinishBattle();
            EndTurn();
            WaveCleared?.Invoke(completedWave, grantedReward);
        }
        finally
        {
            endingBattle = false;
            RefreshYutButtons();
        }
    }

    private void EndTurn()
    {
        currentTurnNumber++;
        StartTurn();
    }

    public void SelectAlly(Ally ally)
    {
        if (CanUsePreparationInput && !IsThrowing && chosenAction == YutAction.Move &&
            CurrentYutResult.HasValue && actionController != null)
        {
            actionController.SelectAlly(ally, CurrentYutResult.Value);
            if (ally != null && actionController.IsRegisteredAlly(ally)) allyRangeDisplay?.Show(ally);
        }
    }

    public void SelectDestination(int cellId)
    {
        if (CanUsePreparationInput && !IsThrowing && CurrentYutResult.HasValue &&
            chosenAction == YutAction.Move && actionController != null &&
            actionController.MoveSelectedAlly(cellId))
        {
            CompleteYutAction();
        }
    }

    private bool TryChooseAction(YutAction action)
    {
        if (!CanUsePreparationInput || IsThrowing || !CurrentYutResult.HasValue ||
            chosenAction != YutAction.None || actionController == null ||
            (action == YutAction.Summon && !actionController.CanDeploy(CurrentYutResult.Value)) ||
            (action == YutAction.Move && !actionController.CanMove(CurrentYutResult.Value)))
        {
            return false;
        }

        chosenAction = action;
        if (action == YutAction.Move)
        {
            allyRangeDisplay?.Hide();
        }

        RefreshYutButtons();

        return true;
    }

    private void CompleteYutAction()
    {
        if (preparationNotice != null) preparationNotice.Hide();
        CurrentYutResult = null;
        chosenAction = YutAction.None;
        RefreshYutButtons();
    }
}

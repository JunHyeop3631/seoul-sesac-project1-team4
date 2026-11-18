using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class TurnController : MonoBehaviour
{
    public const int YutThrowCost = 20;
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
    public YutThrowResult? CurrentYutResult { get; private set; }
    public event Action BattleStarted;
    public event Action BattleWon;
    public event Action BattleLost;
    public event Action PreparationStarted;
    // Completed (one-based) wave and the amount actually credited to the wallet.
    public event Action<int, int> WaveCleared;
    public bool IsBattleWon { get; private set; }
    private bool preparing;
    public bool CanUsePreparationInput => isActiveAndEnabled && preparing &&
        RemainingLives > 0 && !IsBattleWon && Time.timeScale > 0f &&
        (waveManager == null || !waveManager.IsRunning);
    public bool IsThrowing => throwController != null && throwController.IsThrowing;
    public bool CanEvolve => CanUsePreparationInput && !IsThrowing && !CurrentYutResult.HasValue;
    public bool CanOpenPreparationPanel => CanUsePreparationInput && !IsThrowing;
    public bool CanThrowYut => CanUsePreparationInput && !IsThrowing && !CurrentYutResult.HasValue &&
        throwController != null && throwController.isActiveAndEnabled &&
        currency != null && currency.CanAfford(YutThrowCost) &&
        (shopPanel == null || !shopPanel.activeInHierarchy);
    private YutAction chosenAction;
    private CanvasGroup summonButtonGroup;

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
        if (RemainingLives == 0)
        {
            return;
        }

        RemainingLives = Mathf.Max(0, RemainingLives - 1);
        if (RemainingLives == 0)
        {
            preparing = false;
            ClosePreparationUI();
            RefreshYutButtons();
            BattleLost?.Invoke();
        }
    }

    private void OnEnable()
    {
        if (currency != null)
            currency.Changed += HandleCurrencyChanged;
        if (waveManager != null)
        {
            waveManager.WaveCompleted += EndBattle;
            waveManager.EnemyArrived += LoseLife;
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
        }

        if (throwController != null)
        {
            throwController.ThrowCompleted -= ReceiveYutResult;
        }

        ClosePreparationUI();
    }

    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;
        if (!CanUsePreparationInput || actionController == null ||
            mouse == null || !mouse.leftButton.wasPressedThisFrame ||
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
        if (chosenAction == YutAction.Move)
        {
            if (actionController.TryGetDestinationAt(worldPoint, out int cellId))
            {
                SelectDestination(cellId);
                return;
            }

            SelectAlly(actionController.GetAllyAt(worldPoint));
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
        actionController?.ClearSelection();
        CurrentYutResult = result;

        if (result == YutThrowResult.Backdo && actionController != null && !actionController.CanMove(result))
        {
            CompleteYutAction();
            return;
        }

        RefreshYutButtons();
    }

    public void ThrowYut()
    {
        if (!CanThrowYut)
            return;

        throwController.Throw();
        RefreshYutButtons();
    }

    // Called only after the thrower validates all four sticks and locks IsThrowing.
    // The lock is already set when wallet notifications refresh UI or invoke callbacks.
    internal bool TryPayForYutThrow()
    {
        return CanUsePreparationInput && IsThrowing && !CurrentYutResult.HasValue &&
            (shopPanel == null || !shopPanel.activeInHierarchy) &&
            currency != null && currency.TrySpend(YutThrowCost);
    }

    private void HandleCurrencyChanged(int balance)
    {
        RefreshYutButtons();
    }

    private void RefreshYutButtons()
    {
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
            CurrentYutResult != YutThrowResult.Backdo;
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
            button.interactable = CanThrowYut;
    }

    public void OpenShop()
    {
        if (CanOpenPreparationPanel && shopPanel != null)
            shopPanel.SetActive(true);
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
        if (inventoryPanel != null)
            inventoryPanel.SetActive(allowed);
        if (inventoryToggle != null)
            inventoryToggle.SetIsOnWithoutNotify(allowed);
    }

    private void ClosePreparationUI()
    {
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

    public void StartBattle()
    {
        if (!CanUsePreparationInput || waveManager == null ||
            CurrentYutResult.HasValue || IsThrowing || (shopPanel != null && shopPanel.activeInHierarchy))
        {
            return;
        }

        preparing = false;
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
            preparing = !IsBattleWon && RemainingLives > 0;
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
        if (RemainingLives == 0 || IsBattleWon || waveManager == null)
        {
            return;
        }

        if (currentTurnNumber == waveManager.WaveCount - 1)
        {
            IsBattleWon = true;
            preparing = false;
            ClosePreparationUI();
            RefreshYutButtons();
            BattleWon?.Invoke();
            return;
        }

        int completedWave = currentTurnNumber + 1;
        int grantedReward = currency != null
            ? currency.Grant(waveManager.GetCompletionReward(currentTurnNumber)) : 0;
        EndTurn();
        WaveCleared?.Invoke(completedWave, grantedReward);
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
            (action == YutAction.Summon && CurrentYutResult == YutThrowResult.Backdo) ||
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
        CurrentYutResult = null;
        chosenAction = YutAction.None;
        RefreshYutButtons();
    }
}

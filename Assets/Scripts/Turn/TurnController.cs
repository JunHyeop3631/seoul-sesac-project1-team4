using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class TurnController : MonoBehaviour
{
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
            RefreshYutButtons();
            BattleLost?.Invoke();
        }
    }

    private void OnEnable()
    {
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
        if (waveManager != null)
        {
            waveManager.WaveCompleted -= EndBattle;
            waveManager.EnemyArrived -= LoseLife;
        }

        if (throwController != null)
        {
            throwController.ThrowCompleted -= ReceiveYutResult;
        }

        if (actionController != null)
        {
            actionController.ClearSelection();
        }

        allyRangeDisplay?.Hide();
    }

    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;
        if (actionController == null ||
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
        if (RemainingLives == 0 || IsBattleWon || CurrentYutResult.HasValue ||
            (waveManager != null && waveManager.IsRunning) || throwController == null || throwController.IsThrowing)
            return;

        throwController.Throw();
        RefreshYutButtons();
    }

    private void RefreshYutButtons()
    {
        bool preparing = RemainingLives > 0 && !IsBattleWon &&
            (waveManager == null || !waveManager.IsRunning);
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
        if (RemainingLives == 0 || IsBattleWon || waveManager == null || waveManager.IsRunning ||
            CurrentYutResult.HasValue || (throwController != null && throwController.IsThrowing))
        {
            return;
        }

        // 전투 시작 조건과 단계 전환은 이후 구현한다.
        waveManager.StartWave(currentTurnNumber);
        if (waveManager.IsRunning)
        {
            RefreshYutButtons();
            BattleStarted?.Invoke();
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
        if (chosenAction == YutAction.Move && CurrentYutResult.HasValue && actionController != null)
        {
            actionController.SelectAlly(ally, CurrentYutResult.Value);
        }
    }

    public void SelectDestination(int cellId)
    {
        if (chosenAction == YutAction.Move && actionController != null &&
            actionController.MoveSelectedAlly(cellId))
        {
            CompleteYutAction();
        }
    }

    private bool TryChooseAction(YutAction action)
    {
        if (!CurrentYutResult.HasValue || chosenAction != YutAction.None || actionController == null ||
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

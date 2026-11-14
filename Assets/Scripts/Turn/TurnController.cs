using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
    [SerializeField] private GameObject actionButtons;
    [SerializeField] private WaveManager waveManager;
    [SerializeField, Min(0)] private int currentTurnNumber = 0;
    [SerializeField, Min(1)] private int startingLives = 10;

    public int CurrentTurnNumber => currentTurnNumber;
    public int RemainingLives { get; private set; }
    public YutThrowResult? CurrentYutResult { get; private set; }
    public event Action BattleStarted;
    public event Action BattleWon;
    public event Action BattleLost;
    public event Action PreparationStarted;
    public bool IsAwaitingNextWave { get; private set; }
    private YutAction chosenAction;

    private void Awake()
    {
        RemainingLives = Mathf.Max(1, startingLives);
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
    }

    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;
        if (chosenAction != YutAction.Move || actionController == null ||
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
        if (actionController.TryGetDestinationAt(worldPoint, out int cellId))
        {
            SelectDestination(cellId);
            return;
        }

        SelectAlly(actionController.GetAllyAt(worldPoint));
    }

    private void ReceiveYutResult(YutThrowResult result)
    {
        chosenAction = YutAction.None;
        actionController?.ClearSelection();
        CurrentYutResult = result;

        if (actionButtons != null)
        {
            actionButtons.SetActive(true);
        }
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
        if (actionButtons != null)
        {
            actionButtons.SetActive(true);
        }
    }

    public void ChooseMove()
    {
        TryChooseAction(YutAction.Move);
    }

    public void StartBattle()
    {
        if (RemainingLives == 0 || IsAwaitingNextWave || waveManager == null || waveManager.IsRunning)
        {
            return;
        }

        // 전투 시작 조건과 단계 전환은 이후 구현한다.
        waveManager.StartWave(currentTurnNumber);
        if (waveManager.IsRunning)
        {
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
    }

    private void EndBattle()
    {
        if (RemainingLives == 0)
        {
            return;
        }

        int totalEnemies = waveManager != null ? waveManager.GetTotalEnemyCount(currentTurnNumber) : 0;
        if (totalEnemies > 0 && waveManager.DefeatedEnemyCount == totalEnemies)
        {
            IsAwaitingNextWave = true;
            BattleWon?.Invoke();
            return;
        }

        EndTurn();
    }

    private void EndTurn()
    {
        currentTurnNumber++;
        StartTurn();
    }

    public void NextWave()
    {
        if (!IsAwaitingNextWave || RemainingLives == 0 || waveManager == null || waveManager.IsRunning)
        {
            return;
        }

        IsAwaitingNextWave = false;
        EndTurn();
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
        if (!CurrentYutResult.HasValue || chosenAction != YutAction.None || actionController == null)
        {
            return false;
        }

        chosenAction = action;

        if (actionButtons != null)
        {
            actionButtons.SetActive(false);
        }

        return true;
    }

    private void CompleteYutAction()
    {
        CurrentYutResult = null;
        chosenAction = YutAction.None;
    }
}

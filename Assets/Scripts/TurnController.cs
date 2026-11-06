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

    public YutThrowResult? CurrentYutResult { get; private set; }
    private YutAction chosenAction;

    private void OnEnable()
    {
        if (throwController != null)
        {
            throwController.ThrowCompleted += ReceiveYutResult;
        }
    }

    private void OnDisable()
    {
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
        if (TryChooseAction(YutAction.Summon) && actionController.DeployAlly(CurrentYutResult.Value))
        {
            CompleteYutAction();
        }
    }

    public void ChooseMove()
    {
        TryChooseAction(YutAction.Move);
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

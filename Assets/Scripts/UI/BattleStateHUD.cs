using UnityEngine;

public sealed class BattleStateHUD : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;

    private void Awake()
    {
        HidePanels();
    }

    private void OnEnable()
    {
        if (turnController == null)
        {
            return;
        }

        turnController.BattleStarted += HidePanels;
        turnController.PreparationStarted += HidePanels;
        turnController.BattleWon += ShowVictory;
        turnController.BattleLost += ShowDefeat;
    }

    private void OnDisable()
    {
        HidePanels();

        if (turnController == null)
        {
            return;
        }

        turnController.BattleStarted -= HidePanels;
        turnController.PreparationStarted -= HidePanels;
        turnController.BattleWon -= ShowVictory;
        turnController.BattleLost -= ShowDefeat;
    }

    private void HidePanels()
    {
        SetPanels(false, false);
    }

    private void ShowVictory()
    {
        if (turnController != null && turnController.IsBattleWon && turnController.RemainingLives > 0)
        {
            HidePanels();
            SetPanels(true, false);
        }
    }

    private void ShowDefeat()
    {
        HidePanels();
        SetPanels(false, true);
    }

    private void SetPanels(bool victory, bool defeat)
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(victory);
        }

        if (defeatPanel != null)
        {
            defeatPanel.SetActive(defeat);
        }
    }
}

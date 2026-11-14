using UnityEngine;

public sealed class BattleResultHUD : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private GameObject victoryFlag;
    [SerializeField] private GameObject defeatFlag;

    private void Awake()
    {
        HideFlags();
    }

    private void OnEnable()
    {
        if (turnController == null)
        {
            return;
        }

        turnController.BattleStarted += HideFlags;
        turnController.BattleWon += ShowVictory;
        turnController.BattleLost += ShowDefeat;
    }

    private void OnDisable()
    {
        if (turnController == null)
        {
            return;
        }

        turnController.BattleStarted -= HideFlags;
        turnController.BattleWon -= ShowVictory;
        turnController.BattleLost -= ShowDefeat;
    }

    private void HideFlags()
    {
        SetFlags(false, false);
    }

    private void ShowVictory()
    {
        if (turnController != null && turnController.RemainingLives > 0)
        {
            SetFlags(true, false);
        }
    }

    private void ShowDefeat()
    {
        SetFlags(false, true);
    }

    private void SetFlags(bool victory, bool defeat)
    {
        if (victoryFlag != null)
        {
            victoryFlag.SetActive(victory);
        }

        if (defeatFlag != null)
        {
            defeatFlag.SetActive(defeat);
        }
    }
}

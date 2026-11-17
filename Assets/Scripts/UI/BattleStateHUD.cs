using UnityEngine;

public sealed class BattleStateHUD : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private ResultFlagView resultPrefab;
    private ResultFlagView result;

    private void Awake()
    {
        if (resultPrefab != null) result = Instantiate(resultPrefab, transform);
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
        if (result != null) result.Hide();
    }

    private void ShowVictory()
    {
        if (turnController != null && turnController.IsBattleWon && turnController.RemainingLives > 0)
        {
            ShowResult(true);
        }
    }

    private void ShowDefeat()
    {
        ShowResult(false);
    }

    private void ShowResult(bool victory)
    {
        if (result == null || turnController == null || waveManager == null) return;
        result.Show(victory, turnController.CurrentTurnNumber + 1, waveManager.WaveCount, gameObject.scene.name);
    }
}

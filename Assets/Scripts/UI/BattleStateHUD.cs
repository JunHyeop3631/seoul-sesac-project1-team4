using System.Collections;
using UnityEngine;

public sealed class BattleStateHUD : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private GameObject startWavePanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;

    private Coroutine startWaveHideRoutine;

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

        turnController.BattleStarted += ShowStartWave;
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

        turnController.BattleStarted -= ShowStartWave;
        turnController.PreparationStarted -= HidePanels;
        turnController.BattleWon -= ShowVictory;
        turnController.BattleLost -= ShowDefeat;
    }

    private void HidePanels()
    {
        if (startWaveHideRoutine != null)
        {
            StopCoroutine(startWaveHideRoutine);
            startWaveHideRoutine = null;
        }

        if (startWavePanel != null)
        {
            startWavePanel.SetActive(false);
        }

        SetPanels(false, false);
    }

    private void ShowStartWave()
    {
        HidePanels();
        if (startWavePanel != null)
        {
            startWavePanel.SetActive(true);
            startWaveHideRoutine = StartCoroutine(HideStartWaveAfterDelay());
        }
    }

    private IEnumerator HideStartWaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1f);

        if (startWavePanel != null)
        {
            startWavePanel.SetActive(false);
        }

        startWaveHideRoutine = null;
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

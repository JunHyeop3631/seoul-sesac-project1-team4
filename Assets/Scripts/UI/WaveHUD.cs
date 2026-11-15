using UnityEngine;
using UnityEngine.UI;

public sealed class WaveHUD : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Text waveNumberText;
    [SerializeField] private Text enemyCountText;

    private int displayedWaveNumber = -1;
    private int displayedDefeatedCount = -1;
    private int displayedTotalCount = -1;

    private void LateUpdate()
    {
        if (turnController == null || waveManager == null)
        {
            return;
        }

        int waveIndex = turnController.CurrentTurnNumber;
        int waveNumber = waveIndex + 1;
        int defeatedCount = waveManager.DefeatedEnemyCount;
        int totalCount = waveManager.GetTotalEnemyCount(waveIndex);

        if (waveNumberText != null && displayedWaveNumber != waveNumber)
        {
            waveNumberText.text = waveNumber.ToString();
            displayedWaveNumber = waveNumber;
        }

        if (enemyCountText != null &&
            (displayedDefeatedCount != defeatedCount || displayedTotalCount != totalCount))
        {
            enemyCountText.text = $"{totalCount - defeatedCount} / {totalCount}";
            displayedDefeatedCount = defeatedCount;
            displayedTotalCount = totalCount;
        }
    }
}

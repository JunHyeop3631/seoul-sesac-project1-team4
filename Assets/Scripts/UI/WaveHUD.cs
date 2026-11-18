using UnityEngine;
using UnityEngine.UI;

public sealed class WaveHUD : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Text waveNumberText;
    [SerializeField] private Text enemyCountText;

    private string displayedWave;
    private string displayedEnemies;

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

        string waveLabel = $"{waveNumber} / {waveManager.WaveCount}";
        if (waveNumberText != null && displayedWave != waveLabel)
        {
            waveNumberText.text = waveLabel;
            displayedWave = waveLabel;
        }

        // User-confirmed: pending + alive + escaped, equivalent to total - defeated.
        int remaining = Mathf.Max(0, totalCount - defeatedCount);
        string enemyLabel = $"남은 적 {remaining}";
        int bossMaxHealth = waveManager.GetBossMaxHealth(waveIndex);
        if (bossMaxHealth > 0)
        {
            float health = waveManager.Boss != null ? waveManager.Boss.CurrentHealth : bossMaxHealth;
            enemyLabel += $"\n보스 {health:0.#} / {bossMaxHealth}";
        }
        if (enemyCountText != null && displayedEnemies != enemyLabel)
        {
            enemyCountText.text = enemyLabel;
            displayedEnemies = enemyLabel;
        }
    }
}

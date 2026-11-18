using UnityEngine;

public sealed class WaveCompleteHUD : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private WaveCompleteBanner bannerPrefab;
    private WaveCompleteBanner banner;

    private void Awake()
    {
        if (bannerPrefab != null) banner = Instantiate(bannerPrefab, transform, false);
    }

    private void OnEnable()
    {
        if (turnController == null) return;
        turnController.WaveCleared += Show;
        turnController.BattleStarted += Hide;
        turnController.BattleWon += Hide;
        turnController.BattleLost += Hide;
    }

    private void OnDisable()
    {
        Hide();
        if (turnController == null) return;
        turnController.WaveCleared -= Show;
        turnController.BattleStarted -= Hide;
        turnController.BattleWon -= Hide;
        turnController.BattleLost -= Hide;
    }

    private void Show(int waveNumber, int grantedReward)
    {
        if (banner != null) banner.Play(waveNumber, grantedReward);
    }

    private void Hide()
    {
        if (banner != null) banner.Hide();
    }
}

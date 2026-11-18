using UnityEngine;
using UnityEngine.UI;

public sealed class HeartHUD : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private Text livesText;

    private int displayedLives = -1;
    private int displayedMaximum = -1;

    private void LateUpdate()
    {
        if (turnController == null || livesText == null)
        {
            return;
        }

        int lives = turnController.RemainingLives;
        int maximum = turnController.MaxLives;
        if (displayedLives != lives || displayedMaximum != maximum)
        {
            livesText.text = $"{lives} / {maximum}";
            displayedLives = lives;
            displayedMaximum = maximum;
        }
    }
}

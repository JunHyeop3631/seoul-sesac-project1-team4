using UnityEngine;
using UnityEngine.UI;

public sealed class HeartHUD : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private Text livesText;

    private int displayedLives = -1;

    private void LateUpdate()
    {
        if (turnController == null || livesText == null)
        {
            return;
        }

        int lives = turnController.RemainingLives;
        if (displayedLives != lives)
        {
            livesText.text = lives.ToString();
            displayedLives = lives;
        }
    }
}

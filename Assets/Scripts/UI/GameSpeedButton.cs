using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(GoldNumberDisplay))]
public sealed class GameSpeedButton : MonoBehaviour
{
    private Button button;
    private GoldNumberDisplay speedDisplay;

    public int CurrentSpeed { get; private set; } = 1;

    private void Awake()
    {
        button = GetComponent<Button>();
        speedDisplay = GetComponent<GoldNumberDisplay>();
    }

    private void OnEnable()
    {
        SetSpeed(1);
        button.onClick.AddListener(CycleSpeed);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(CycleSpeed);
        SetSpeed(1);
    }

    private void CycleSpeed()
    {
        SetSpeed(CurrentSpeed == 4 ? 1 : CurrentSpeed * 2);
    }

    private void SetSpeed(int speed)
    {
        CurrentSpeed = speed;
        Time.timeScale = speed;
        speedDisplay.SetValue(speed);
    }
}

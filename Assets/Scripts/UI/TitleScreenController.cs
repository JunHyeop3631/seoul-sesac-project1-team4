using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TitleScreenController : MonoBehaviour
{
    [SerializeField] private GameObject startPrompt;
    [SerializeField] private CanvasGroup promptOpacity;
    [SerializeField] private GameObject menu;
    [SerializeField] private Button[] menuButtons;
    [SerializeField] private Image[] menuLabels;
    [SerializeField] private RectTransform selection;
    [SerializeField] private Color normalColor = new Color(1.025f, 0.97f, 0.88f, 1f);
    [SerializeField] private Color selectedColor = new Color(1.02f, 0.83f, 0.387f, 1f);

    public bool IsMenuOpen => menu.activeSelf;
    public int SelectedIndex { get; private set; }
    private bool startingGame;

    private void Awake()
    {
        for (int i = 0; i < menuButtons.Length; i++)
        {
            int index = i;
            EventTrigger trigger = menuButtons[i].gameObject.AddComponent<EventTrigger>();
            var select = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            select.callback.AddListener(_ => UpdateSelection(index));
            trigger.triggers.Add(select);
            var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hover.callback.AddListener(_ => EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject));
            trigger.triggers.Add(hover);
        }

        menuButtons[0].onClick.AddListener(StartGame);
        // Settings remains unassigned until its behavior is defined.
        menuButtons[2].onClick.AddListener(QuitGame);
        ShowPrompt();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!IsMenuOpen)
        {
            promptOpacity.alpha = 0.75f + 0.25f * Mathf.Cos(Time.unscaledTime * 2.2f);
            bool submit = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
            if (submit || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                ShowMenu();
        }
        else if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            ShowPrompt();
        }
    }

    public void ShowMenu()
    {
        startPrompt.SetActive(false);
        menu.SetActive(true);
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(menuButtons[0].gameObject);
        UpdateSelection(0);
    }

    public void ShowPrompt()
    {
        EventSystem.current.SetSelectedGameObject(null);
        menu.SetActive(false);
        startPrompt.SetActive(true);
        promptOpacity.alpha = 1f;
    }

    private void UpdateSelection(int index)
    {
        SelectedIndex = index;
        for (int i = 0; i < menuLabels.Length; i++)
            menuLabels[i].color = i == index ? selectedColor : normalColor;

        selection.anchoredPosition = ((RectTransform)menuButtons[index].transform).anchoredPosition;
    }

    private void StartGame()
    {
        if (startingGame || !isActiveAndEnabled) return;
        startingGame = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene("ThrowYut");
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

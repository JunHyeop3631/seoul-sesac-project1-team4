using UnityEngine;
using UnityEngine.UI;

// Only changes artwork. TurnController continues to own result use and availability.
public sealed class YutActionButtonVisuals : MonoBehaviour
{
    [SerializeField] private TurnController turn;
    [SerializeField] private Button summonButton, moveButton;
    [SerializeField] private Sprite[] summonCards, moveCards, summonPressed, movePressed;
    private YutThrowResult? displayed;
    private void OnEnable() { displayed = null; Refresh(); }
    private void LateUpdate() => Refresh();
    private void Refresh()
    {
        var result = turn != null ? turn.CurrentYutResult : null;
        if (result == displayed) return;
        displayed = result;
        if (!result.HasValue) return;
        int index = (int)result.Value;
        SetCard(summonButton, summonCards, summonPressed, index);
        SetCard(moveButton, moveCards, movePressed, index);
    }
    private static void SetCard(Button button, Sprite[] normal, Sprite[] pressed, int index)
    {
        if (button == null || normal == null || pressed == null || index < 0 ||
            index >= normal.Length || index >= pressed.Length || normal[index] == null || pressed[index] == null) return;
        button.image.sprite = normal[index];
        var state = button.spriteState;
        state.highlightedSprite = state.selectedSprite = state.disabledSprite = normal[index];
        state.pressedSprite = pressed[index];
        button.spriteState = state;
    }
}

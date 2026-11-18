using UnityEngine;
using UnityEngine.UI;

// Display only: does not intercept board clicks or modify game state.
public sealed class MovePreviewLabel : MonoBehaviour
{
    private Canvas canvas;
    private Text label;
    private Vector3 worldPosition;

    public static MovePreviewLabel Create(Canvas canvas, Font font)
    {
        var root = new GameObject("Move Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(210, 34);
        var background = root.GetComponent<Image>();
        background.color = new Color32(65, 39, 39, 240); background.raycastTarget = false;
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = root.layer; textObject.transform.SetParent(root.transform, false);
        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var text = textObject.GetComponent<Text>();
        text.font = font; text.fontSize = 22; text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(255, 225, 171, 255); text.raycastTarget = false;
        var view = root.AddComponent<MovePreviewLabel>(); view.canvas = canvas; view.label = text;
        return view;
    }

    public void Show(Vector3 position, string text)
    {
        worldPosition = position; label.text = text; gameObject.SetActive(true); UpdatePosition();
    }
    public void Hide() => gameObject.SetActive(false);
    private void LateUpdate() => UpdatePosition();
    private void UpdatePosition()
    {
        var camera = Camera.main;
        if (camera == null || canvas == null) return;
        var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,
            camera.WorldToScreenPoint(worldPosition), uiCamera, out Vector2 local))
            ((RectTransform)transform).anchoredPosition = local;
    }
}

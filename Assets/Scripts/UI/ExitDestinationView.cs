using System;
using UnityEngine;
using UnityEngine.UI;

// The off-board choice is a real UI button; the board's 29 cells stay unchanged.
public sealed class ExitDestinationView : MonoBehaviour
{
    private Canvas canvas;
    private Vector3 worldPosition;
    private Text rewardLabel;

    public static ExitDestinationView Create(Canvas canvas, Font font, Action confirm)
    {
        var root = new GameObject("Exit Destination", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        root.SetActive(false);
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(210, 76);
        root.GetComponent<Image>().color = new Color32(65, 39, 39, 255);
        var border = root.AddComponent<Outline>();
        border.effectColor = new Color32(218, 165, 75, 255); border.effectDistance = new Vector2(2, -2);
        var button = root.GetComponent<Button>(); button.targetGraphic = root.GetComponent<Image>();
        button.onClick.AddListener(() => confirm());
        var labelObject = new GameObject("Arrival Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.layer = root.layer; labelObject.transform.SetParent(root.transform, false);
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        labelRect.anchorMin = new Vector2(0, .42f);
        var label = labelObject.GetComponent<Text>();
        label.font = font; label.fontSize = 30; label.text = "도착";
        label.color = new Color32(255, 225, 171, 255); label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        var view = root.AddComponent<ExitDestinationView>(); view.canvas = canvas;
        var rewardObject = new GameObject("Reward Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        rewardObject.layer = root.layer; rewardObject.transform.SetParent(root.transform, false);
        var rewardRect = (RectTransform)rewardObject.transform;
        rewardRect.anchorMin = Vector2.zero; rewardRect.anchorMax = new Vector2(1, .44f);
        rewardRect.offsetMin = rewardRect.offsetMax = Vector2.zero;
        view.rewardLabel = rewardObject.GetComponent<Text>();
        view.rewardLabel.font = font; view.rewardLabel.fontSize = 22;
        view.rewardLabel.color = label.color; view.rewardLabel.alignment = TextAnchor.MiddleCenter;
        view.rewardLabel.raycastTarget = false;
        return view;
    }

    public void Show(Vector3 position, int reward)
    {
        rewardLabel.text = $"특별재화 +{reward}";
        worldPosition = position;
        gameObject.SetActive(true);
        UpdatePosition();
    }
    public void Hide() => gameObject.SetActive(false);
    private void LateUpdate() => UpdatePosition();
    private void UpdatePosition()
    {
        Camera camera = Camera.main;
        if (camera == null || canvas == null) return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,
            camera.WorldToScreenPoint(worldPosition), uiCamera, out Vector2 local))
            ((RectTransform)transform).anchoredPosition = local;
    }
}

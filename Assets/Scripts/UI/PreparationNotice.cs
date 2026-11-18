using UnityEngine;
using UnityEngine.UI;

// Brief, non-modal feedback. Owns presentation and lifetime only.
public sealed class PreparationNotice : MonoBehaviour
{
    private Text label;
    private float hideAt;
    public static PreparationNotice Create(Canvas canvas, Font font)
    {
        var root = new GameObject("Preparation Notice", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.layer = canvas.gameObject.layer; root.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.35f, .77f);
        rect.sizeDelta = new Vector2(850, 52);
        var image = root.GetComponent<Image>(); image.color = new Color32(65, 39, 39, 245); image.raycastTarget = false;
        var child = new GameObject("Reason", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.layer = root.layer; child.transform.SetParent(root.transform, false);
        var textRect = (RectTransform)child.transform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12, 4); textRect.offsetMax = new Vector2(-12, -4);
        var label = child.GetComponent<Text>(); label.font = font; label.fontSize = 26;
        label.color = new Color32(255, 225, 171, 255); label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        var view = root.AddComponent<PreparationNotice>(); view.label = label; root.SetActive(false);
        return view;
    }
    public void Show(string message)
    {
        label.text = message; hideAt = Time.unscaledTime + 2.5f; gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }
    public void Hide() => gameObject.SetActive(false);
    private void Update() { if (Time.unscaledTime >= hideAt) Hide(); }
}

using UnityEngine;

/// <summary>Replays the inventory entrance whenever the existing toggle opens the panel.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class InventoryPanelDrop : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.48f;
    private RectTransform panel;
    private float elapsed;
    private float travel;

    private void OnEnable()
    {
        panel = (RectTransform)transform;
        panel.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        elapsed = 0f;
        travel = panel.rect.height * 1.03f;
        panel.anchoredPosition = Vector2.up * travel;
    }

    private void Update()
    {
        if (elapsed >= duration) return;
        elapsed = Mathf.Min(elapsed + Time.unscaledDeltaTime, duration);
        float t = elapsed / duration;
        // A quick descent, a small soft landing, then an exact return to rest.
        float offset = t < 0.82f
            ? 1.012f * Mathf.Pow(1f - t / 0.82f, 3f) - 0.012f
            : -0.012f * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.82f) / 0.18f));
        panel.anchoredPosition = Vector2.up * (travel * offset);
    }

    private void OnDisable()
    {
        if (panel != null) panel.anchoredPosition = Vector2.zero;
    }
}

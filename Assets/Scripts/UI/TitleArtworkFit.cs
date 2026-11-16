using UnityEngine;

// Scale the editable reference canvas uniformly into its 16:9 parent.
[ExecuteAlways]
public sealed class TitleArtworkFit : MonoBehaviour
{
    private void OnEnable() => Fit();
    private void OnRectTransformDimensionsChange() => Fit();
    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (transform.parent is not RectTransform parent) return;
        float scale = Mathf.Min(parent.rect.width / 1920f, parent.rect.height / 1080f);
        if (scale > 0f && !Mathf.Approximately(transform.localScale.x, scale))
            transform.localScale = Vector3.one * scale;
    }
}

using UnityEngine;

namespace Yut.TitleScreen
{
    // Fixed design coordinates, fitted without stretching or cropping on any aspect ratio.
    [ExecuteAlways]
    public sealed class TitleFrameFitter : MonoBehaviour
    {
        public Vector2 designSize = new Vector2(1920, 1080);
        void LateUpdate() { Fit(); }
        public void Fit()
        {
            var parent = transform.parent as RectTransform;
            if (!parent || designSize.x <= 0 || designSize.y <= 0) return;
            float fit = Mathf.Min(parent.rect.width / designSize.x, parent.rect.height / designSize.y);
            transform.localScale = new Vector3(fit, fit, 1);
        }
    }
}

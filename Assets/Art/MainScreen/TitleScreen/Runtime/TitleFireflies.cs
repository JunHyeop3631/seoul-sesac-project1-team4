using UnityEngine;
using UnityEngine.UI;
using Yut.PixelFX;

namespace Yut.TitleScreen
{
    public sealed class TitleFireflies : MonoBehaviour
    {
        public PixelFxClip clip;
        [Range(4, 48)] public int count = 24;
        [Range(.1f, 2)] public float speed = .55f;
        [Range(0, 1)] public float brightness = 1f;
        [Range(12, 72)] public float size = 56;
        public int seed = 2026;
        public float Elapsed { get; private set; }
        public int ActiveCount => motes == null ? 0 : motes.Length;
        struct Mote { public Image image; public Vector2 home; public float phase, rate, scale; }
        Mote[] motes;

        void Start() { Rebuild(); }

        [ContextMenu("Rebuild Fireflies")]
        public void Rebuild()
        {
            if (!Application.isPlaying || !clip || clip.frames == null || clip.frames.Length == 0) return;
            if (motes != null) foreach (var mote in motes) if (mote.image) Destroy(mote.image.gameObject);
            var random = new System.Random(seed);
            motes = new Mote[Mathf.Clamp(count, 4, 48)];
            for (int i = 0; i < motes.Length; i++)
            {
                float x = 40 + (float)random.NextDouble() * 1840;
                float y = 610 + (float)random.NextDouble() * 410;
                // Keep the welcome/menu lettering clear, including the full drift range.
                if (x > 650 && x < 1270 && y < 875) y = 900 + (float)random.NextDouble() * 100;
                var item = new GameObject("Firefly " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                item.transform.SetParent(transform, false);
                var image = item.GetComponent<Image>(); image.raycastTarget = false;
                image.sprite = clip.frames[0];
                var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                motes[i] = new Mote {
                    image = image, home = new Vector2(x, -y),
                    phase = (float)random.NextDouble() * 20,
                    rate = .75f + (float)random.NextDouble() * .5f,
                    scale = .65f + (float)random.NextDouble() * .8f
                };
            }
        }

        void Update()
        {
            if (motes == null || !clip || clip.fps <= 0 || clip.frames.Length == 0) return;
            Elapsed += Time.unscaledDeltaTime * speed;
            foreach (var mote in motes)
            {
                float t = Elapsed * mote.rate + mote.phase;
                int frame = Mathf.FloorToInt(t * clip.fps) % clip.frames.Length;
                mote.image.sprite = clip.frames[frame];
                mote.image.color = new Color(1, 1, 1, Mathf.Clamp01(brightness));
                mote.image.rectTransform.sizeDelta = Vector2.one * size * mote.scale;
                mote.image.rectTransform.anchoredPosition = mote.home + new Vector2(Mathf.Sin(t * .38f) * 24, Mathf.Sin(t * .27f + 1) * 13);
            }
        }
    }
}

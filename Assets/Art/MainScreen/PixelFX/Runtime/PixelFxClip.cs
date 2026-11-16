using System;
using UnityEngine;

namespace Yut.PixelFX
{
    public sealed class PixelFxClip : ScriptableObject
    {
        public Sprite[] frames;
        public float fps = 30;
        public bool loop;
        [TextArea] public string sourceJson;
        public float Duration => frames == null ? 0 : frames.Length / fps;
    }

    [Serializable] public class AtlasData { public AtlasFrame[] frames; public AtlasMeta meta; }
    [Serializable] public class AtlasFrame { public AtlasRect frame; public int duration; public bool rotated, trimmed; }
    [Serializable] public class AtlasRect { public int x, y, w, h; }
    [Serializable] public class AtlasMeta { public string image; public AtlasRect size; public FxMeta pixelfx; }
    [Serializable] public class FxMeta { public string id; public float fps; public bool loop; public int frames; public int[] pivot, frameSize; }
}

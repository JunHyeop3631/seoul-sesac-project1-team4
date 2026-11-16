using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Yut.PixelFX.Editor
{
    [ScriptedImporter(1, "pixelfx")]
    public sealed class PixelFxImporter : ScriptedImporter
    {
        public float pixelsPerUnit = 48;
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var json = File.ReadAllText(ctx.assetPath);
            var data = JsonUtility.FromJson<AtlasData>(json);
            if (data?.frames == null || data.frames.Length == 0 || data.meta?.pixelfx == null)
                throw new InvalidDataException("Missing pixelfx atlas metadata.");
            var meta = data.meta.pixelfx;
            if (meta.fps <= 0 || meta.pivot == null || meta.pivot.Length != 2 || pixelsPerUnit <= 0)
                throw new InvalidDataException("Invalid fps, pivot or pixelsPerUnit.");
            if (Path.GetFileName(data.meta.image) != data.meta.image)
                throw new InvalidDataException("Atlas image must be a sibling PNG.");
            var png = Path.Combine(Path.GetDirectoryName(ctx.assetPath), data.meta.image).Replace('\\', '/');
            ctx.DependsOnSourceAsset(png);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(png))) throw new InvalidDataException("Invalid PNG.");
            texture.name = "atlas";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (texture.width != data.meta.size.w || texture.height != data.meta.size.h)
                throw new InvalidDataException("PNG dimensions differ from JSON.");
            ctx.AddObjectToAsset("atlas", texture);
            var clip = ScriptableObject.CreateInstance<PixelFxClip>();
            clip.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            clip.fps = meta.fps; clip.loop = meta.loop; clip.sourceJson = json;
            clip.frames = new Sprite[data.frames.Length];
            for (int i = 0; i < data.frames.Length; i++)
            {
                var f = data.frames[i]; var r = f.frame;
                if (f.rotated || f.trimmed || r.w <= 0 || r.h <= 0 || r.x < 0 || r.y < 0 || r.x + r.w > texture.width || r.y + r.h > texture.height)
                    throw new InvalidDataException("Unsupported or out of bounds atlas frame.");
                var rect = new Rect(r.x, texture.height - r.y - r.h, r.w, r.h);
                var pivot = new Vector2((float)meta.pivot[0] / r.w, 1f - (float)meta.pivot[1] / r.h);
                var sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = $"{clip.name}_{i:D4}";
                clip.frames[i] = sprite;
                ctx.AddObjectToAsset($"frame_{i:D4}", sprite);
            }
            ctx.AddObjectToAsset("clip", clip);
            ctx.SetMainObject(clip);
        }
    }
}

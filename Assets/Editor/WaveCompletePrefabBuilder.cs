using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WaveCompletePrefabBuilder
{
    const string Art = "Assets/Art/WaveComplete/";
    public const string PrefabPath = "Assets/Prefabs/WaveCompleteBanner.prefab";

    [MenuItem("Tools/Yut UI/Rebuild Wave Complete Prefab")]
    public static void Build()
    {
        foreach (string path in Directory.GetFiles(Art, "*.png", SearchOption.AllDirectories))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        var root = Rect(null, "Wave Complete Banner", 1024f, 256f);
        root.anchorMin = root.anchorMax = new Vector2(.35f, 1f);
        root.anchoredPosition = new Vector2(0f, -394f);
        var whole = root.gameObject.AddComponent<CanvasGroup>();
        whole.alpha = 0f; whole.blocksRaycasts = false; whole.interactable = false;
        var artwork = Image(root, "Parchment", Load("Frames/Scroll_12.png"), 1024f, 256f);
        var content = Rect(root, "Labels", 900f, 60f);
        content.anchoredPosition = new Vector2(0, 5f);
        var labels = content.gameObject.AddComponent<CanvasGroup>();
        labels.blocksRaycasts = false; labels.interactable = false;
        var waveWord = Image(content, "Wave", Load("WaveWord.png"), 99, 34);
        var waveLabel = Label(content, "Completed Wave", "1 방어 성공");
        var divider = Image(content, "Divider", null, 1, 28);
        divider.color = new Color32(104, 64, 38, 255);
        var coin = Image(content, "Coin", Load("Coin.png"), 30, 30);
        var reward = Label(content, "Granted Reward", "+60");
        var banner = root.gameObject.AddComponent<WaveCompleteBanner>();
        var settings = new SerializedObject(banner);
        var frames = settings.FindProperty("frames"); frames.arraySize = 13;
        for (int i = 0; i < 13; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = Load("Frames/Scroll_" + i.ToString("D2") + ".png");
        Set(settings, "artwork", artwork); Set(settings, "whole", whole); Set(settings, "labels", labels);
        Set(settings, "waveWord", waveWord); Set(settings, "waveLabel", waveLabel); Set(settings, "divider", divider);
        Set(settings, "coin", coin); Set(settings, "rewardLabel", reward);
        settings.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
        Object.DestroyImmediate(root.gameObject);
        AssetDatabase.SaveAssets();
    }

    private static Sprite Load(string relative) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + relative);
    private static void Set(SerializedObject target, string name, Object value)
    { target.FindProperty(name).objectReferenceValue = value; }
    private static RectTransform Rect(Transform parent, string name, float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.layer = 5;
        var rect = obj.GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static Image Image(Transform parent, string name, Sprite sprite, float width, float height)
    {
        var image = Rect(parent, name, width, height).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }
    private static Text Label(Transform parent, string name, string value)
    {
        var text = Rect(parent, name, 250, 56).gameObject.AddComponent<Text>();
        text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Character_Info/03_Fonts/YiSunShin-Bold.otf");
        text.fontSize = 46; text.fontStyle = FontStyle.Normal;
        text.color = new Color32(72, 35, 15, 255); text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false; text.text = value;
        return text;
    }
}

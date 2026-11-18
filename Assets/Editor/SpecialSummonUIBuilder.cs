using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SpecialSummonUIBuilder
{
    const string Art = "Assets/Art/SpecialSummon/";
    const string PrefabPath = "Assets/Prefabs/SpecialSummonUI.prefab";
    const string ScenePath = "Assets/Scenes/ThrowYut.unity";

    [MenuItem("Tools/Yut UI/Build Special Summon Window")]
    public static void Build()
    {
        ImportSprites();
        BuildPrefab();
        AssetDatabase.SaveAssets();
    }

    // Used on the isolated integration project. Does not replace existing sounds or turn logic.
    public static void Install()
    {
        Build();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var canvas = GameObject.Find("Canvas").transform;
        if (canvas.Find("Special Summon UI") != null) throw new InvalidOperationException("Special Summon UI already installed");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), canvas);
        root.name = "Special Summon UI";
        var turn = UnityEngine.Object.FindFirstObjectByType<TurnController>();
        var balance = turn.GetComponent<SpecialCurrency>();
        if (balance == null) balance = turn.gameObject.AddComponent<SpecialCurrency>();
        var so = new SerializedObject(root.GetComponent<SpecialSummonUI>());
        Ref(so, "turnController", turn); Ref(so, "waveManager", UnityEngine.Object.FindFirstObjectByType<WaveManager>());
        Ref(so, "currency", balance); Ref(so, "clickSound", GameObject.Find("ButtonSound").GetComponent<AudioSource>());
        Ref(so, "hoverSound", GameObject.Find("HoverSound").GetComponent<AudioSource>()); so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(root.GetComponent<SpecialSummonUI>());

        var coin = canvas.Find("Coin HUD").GetComponent<Image>();
        coin.rectTransform.sizeDelta = new Vector2(200, 75.1f);
        coin.sprite = Load("CoinHUD_Sliced.png"); coin.type = UnityEngine.UI.Image.Type.Sliced; coin.pixelsPerUnitMultiplier = 4.36f;
        var digits = new SerializedObject(coin.GetComponent<GoldNumberDisplay>());
        digits.FindProperty("displayWidth").floatValue = 110; digits.ApplyModifiedPropertiesWithoutUndo();
        ((RectTransform)canvas.Find("Heart HUD")).sizeDelta = new Vector2(200, 75.1f);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    static void ImportSprites()
    {
        foreach (string file in Directory.GetFiles(Art, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096; importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = Path.GetFileName(file).StartsWith("Button_HUD_", StringComparison.Ordinal);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f); importer.SetTextureSettings(settings);
            importer.spriteBorder = Path.GetFileName(file) == "CoinHUD_Sliced.png" ? new Vector4(400, 130, 145, 130) : Vector4.zero;
            importer.SaveAndReimport();
        }
    }
    static void BuildPrefab()
    {
        var root = Rect(null, "Special Summon UI", Vector2.zero, Vector2.zero, false);
        Stretch(root);
        var hud = Button(root, "Special Currency Button", "Button_HUD", new Vector2(232, 118), new Vector2(266, 48), true);
        Image(hud.transform, "Crystal", "Icon_Crystal.png", new Vector2(24, 30), new Vector2(50, 70), true);
        var hudDigits = Digits(hud.transform, "Special Balance", new Vector2(80, 32), new Vector2(76, 69), new Vector2(26, 32), 0, true);

        var overlay = Rect(root, "Special Summon Modal", Vector2.zero, Vector2.zero, false); Stretch(overlay);
        var dimmer = overlay.gameObject.AddComponent<Image>(); dimmer.color = new Color(.04f, .025f, .08f, .48f); dimmer.raycastTarget = true;
        var window = Rect(overlay, "Window", new Vector2(1600, 1000), Vector2.zero, false); window.localScale = Vector3.one * .7f;
        Image(window, "Background", "BG_Window.png", new Vector2(1600, 1000), Vector2.zero, true);
        Image(window, "Title", "Text_Title.png", new Vector2(440, 151), new Vector2(580, 40), true);
        var close = Button(window, "Close", "Button_Close", new Vector2(102, 102), new Vector2(1440, 142), true);
        Image(window, "Mystery Card", "Card_Mystery.png", new Vector2(438, 684), new Vector2(100, 220), true);
        Image(window, "Headline", "Text_Headline.png", new Vector2(800, 131), new Vector2(650, 240), true);
        Image(window, "Question", "Text_Question.png", new Vector2(720, 93), new Vector2(690, 371), true);
        Image(window, "Population 3", "Text_Population.png", new Vector2(312, 78), new Vector2(896, 465), true);
        Image(window, "Currency Row", "BG_CurrencyBar.png", new Vector2(800, 138), new Vector2(650, 548), true);
        Image(window, "Owned", "Text_Owned.png", new Vector2(126, 72), new Vector2(813, 580), true);
        var owned = Digits(window, "Owned Value", new Vector2(146, 72), new Vector2(929, 580), new Vector2(58, 72), 0, true);
        Image(window, "Cost", "Text_Cost.png", new Vector2(126, 72), new Vector2(1135, 580), true);
        Digits(window, "Cost Value", new Vector2(116, 72), new Vector2(1264, 580), new Vector2(58, 72), 30, true);
        var random = Button(window, "Random Summon", "Button_RandomSummon", new Vector2(680, 170), new Vector2(710, 670), true);
        random.interactable = false;
        Image(window, "Start Cell Hint", "Text_Hint.png", new Vector2(480, 73), new Vector2(810, 837), true);
        close.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = close, selectOnDown = close, selectOnLeft = close, selectOnRight = close };
        var ui = root.gameObject.AddComponent<SpecialSummonUI>(); var so = new SerializedObject(ui);
        Ref(so, "hudButton", hud); Ref(so, "closeButton", close); Ref(so, "randomButton", random);
        Ref(so, "overlay", overlay.gameObject); Ref(so, "hudDigits", hudDigits); Ref(so, "ownedDigits", owned);
        so.ApplyModifiedPropertiesWithoutUndo(); overlay.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath); UnityEngine.Object.DestroyImmediate(root.gameObject);
    }
    static void Ref(SerializedObject so, string name, UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue = value;
    static Sprite Load(string file)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);
        if (sprite == null) throw new InvalidOperationException("Missing approved sprite: " + file);
        return sprite;
    }
    static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position, bool topLeft)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; if (parent != null) rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = topLeft ? new Vector2(0, 1) : new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = new Vector2(position.x, topLeft ? -position.y : position.y); return rect;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static Image Image(Transform parent, string name, string file, Vector2 size, Vector2 position, bool topLeft)
    {
        var image = Rect(parent, name, size, position, topLeft).gameObject.AddComponent<Image>();
        image.sprite = Load(file); image.raycastTarget = false; return image;
    }
    static Button Button(Transform parent, string name, string prefix, Vector2 size, Vector2 position, bool topLeft)
    {
        var image = Image(parent, name, prefix + "_Normal.png", size, position, topLeft); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.SpriteSwap;
        var normal = image.sprite; var hover = File.Exists(Art + prefix + "_Hover.png") ? Load(prefix + "_Hover.png") : normal;
        button.spriteState = new SpriteState { highlightedSprite = hover, selectedSprite = hover,
            pressedSprite = Load(prefix + "_Pressed.png"), disabledSprite = File.Exists(Art + prefix + "_Disabled.png") ? Load(prefix + "_Disabled.png") : normal };
        return button;
    }
    static SpecialCurrencyDigits Digits(Transform parent, string name, Vector2 size, Vector2 position, Vector2 digitSize, int value, bool topLeft)
    {
        var digits = Rect(parent, name, size, position, topLeft).gameObject.AddComponent<SpecialCurrencyDigits>();
        var so = new SerializedObject(digits); var sprites = so.FindProperty("sprites"); sprites.arraySize = 10;
        for (int i = 0; i < 10; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = Load("Digit_" + i + ".png");
        so.FindProperty("digitSize").vector2Value = digitSize; so.FindProperty("value").intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo(); return digits;
    }
}

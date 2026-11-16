using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleSceneBuilder
{
    private const string Art = "Assets/Art/MainScreen/";
    private const string ScenePath = "Assets/Scenes/Title.unity";
    [MenuItem("Tools/Title Screen/Create Title Scene")]
    public static void Build()
    {
        if (File.Exists(ScenePath))
            throw new InvalidOperationException("Title.unity already exists. Edit the existing scene instead.");

        // Additive creation preserves any unsaved work in the existing scene.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);

        var canvasObject = new GameObject("Title Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Keep the reference composition intact at other aspect ratios, with black bars.
        RectTransform composition = Rect("Composition 16x9", canvas.transform, Vector2.zero, Vector2.zero);
        composition.anchorMin = Vector2.zero;
        composition.anchorMax = Vector2.one;
        composition.offsetMin = composition.offsetMax = Vector2.zero;
        var aspect = composition.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 16f / 9f;
        // A nested canvas reference frame lets all art retain authored 1920x1080 coordinates.
        var frame = Rect("Artwork", composition, Vector2.zero, new Vector2(1920, 1080));
        frame.gameObject.AddComponent<TitleArtworkFit>();

        Picture("Background", frame, "background", 960, 540, 1920, 1080);
        Picture("Logo", frame, "logo", 960, 225, 900, 410);
        Picture("Allies", frame, "allies", 350, 685, 720, 480);
        Picture("Enemies", frame, "enemies", 1560, 600, 340, 320);

        RectTransform prompt = Rect("Start Prompt", frame, Vector2.zero, new Vector2(1920, 1080));
        CanvasGroup opacity = prompt.gameObject.AddComponent<CanvasGroup>();
        Picture("Click To Start", prompt, "prompt-click", 960, 748, 390, 67);
        Picture("Enter Hint", prompt, "hint-enter", 960, 805, 300, 30);
        Picture("Left Accent", prompt, "accent-left", 736, 748, 62, 29);
        Picture("Right Accent", prompt, "accent-right", 1184, 748, 62, 29);

        RectTransform menu = Rect("Main Menu", frame, Vector2.zero, new Vector2(1920, 1080));
        string[] names = { "Start", "Settings", "Exit" };
        string[] files = { "label-start", "label-settings", "label-exit" };
        Vector2[] sizes = { new Vector2(220, 61.5f), new Vector2(120, 67), new Vector2(163, 64) };
        float[] rowPositions = { -60, -154, -238 };
        var buttons = new Button[3];
        var labels = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var row = Rect(names[i], menu, new Vector2(0, rowPositions[i]), new Vector2(300, 84));
            Image hitArea = row.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            buttons[i] = row.gameObject.AddComponent<Button>();
            buttons[i].transition = Selectable.Transition.None;
            labels[i] = Picture("Label", row, files[i], 960, 540, sizes[i].x, sizes[i].y);
        }
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + 2) % 3],
                selectOnDown = buttons[(i + 1) % 3]
            };
        }
        RectTransform selection = Rect("Selection", menu, new Vector2(0, -60), new Vector2(300, 84));
        Picture("Left Diamond", selection, "selection-diamond", 827, 544, 31, 34);
        Picture("Right Diamond", selection, "selection-diamond", 1093, 544, 31, 34);
        Picture("Underline", selection, "selection-underline", 960, 584, 274, 25);

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var controller = canvasObject.AddComponent<TitleScreenController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("startPrompt").objectReferenceValue = prompt.gameObject;
        serialized.FindProperty("promptOpacity").objectReferenceValue = opacity;
        serialized.FindProperty("menu").objectReferenceValue = menu.gameObject;
        serialized.FindProperty("selection").objectReferenceValue = selection;
        var buttonProperty = serialized.FindProperty("menuButtons");
        var labelProperty = serialized.FindProperty("menuLabels");
        buttonProperty.arraySize = labelProperty.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            buttonProperty.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            labelProperty.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        menu.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene, ScenePath);

        // Close only clean scenes; unsaved scenes remain open and untouched.
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene other = SceneManager.GetSceneAt(i);
            if (other != scene && !other.isDirty) EditorSceneManager.CloseScene(other, true);
        }
        Selection.activeGameObject = canvasObject;
        Debug.Log("Title scene created. Build Settings and scene transitions were not changed.");
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Picture(string name, Transform parent, string file, float x, float y, float width, float height)
    {
        RectTransform rect = Rect(name, parent, new Vector2(x - 960, 540 - y), new Vector2(width, height));
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file + ".png");
        if (image.sprite == null) throw new InvalidOperationException("Missing sprite: " + file);
        image.raycastTarget = false;
        return image;
    }
}

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class InventoryBuilder
{
    const string Art = "Assets/Art/Item/UI/Inventory/";
    static Sprite Sprite(string file)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file + ".png");
        if (sprite == null) throw new System.InvalidOperationException("Missing inventory Sprite: " + file);
        return sprite;
    }
    static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var existing = parent.Find(name);
        if (existing == null) { existing = new GameObject(name, typeof(RectTransform)).transform; existing.SetParent(parent, false); existing.gameObject.layer = parent.gameObject.layer; }
        var rect = (RectTransform)existing; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    static T Component<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
    static Image Picture(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite)
    {
        var image = Component<Image>(Rect(parent, name, position, size).gameObject);
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; return image;
    }
    static Text Label(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
    {
        var text = Component<Text>(Rect(parent, name, position, size).gameObject);
        text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Character_Info/03_Fonts/YiSunShin-Bold.otf");
        text.fontSize = fontSize; text.color = new Color32(255,230,187,255); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.text = ""; return text;
    }
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before arranging inventory UI.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var turn = Object.FindFirstObjectByType<TurnController>(); var turnData = new SerializedObject(turn);
        var panel = (GameObject)turnData.FindProperty("inventoryPanel").objectReferenceValue;
        var use = Component<StageItemUseController>(turn.gameObject); var useData = new SerializedObject(use);
        useData.FindProperty("turnController").objectReferenceValue = turn;
        useData.FindProperty("inventory").objectReferenceValue = turn.GetComponent<StageInventory>(); useData.ApplyModifiedPropertiesWithoutUndo();
        var view = Component<InventoryUI>(panel); var data = new SerializedObject(view);
        data.FindProperty("turnController").objectReferenceValue = turn;
        data.FindProperty("inventory").objectReferenceValue = turn.GetComponent<StageInventory>();
        data.FindProperty("itemUse").objectReferenceValue = use;
        var catalog = Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);
        data.FindProperty("items").arraySize = catalog.Items.Count;
        for (int i = 0; i < catalog.Items.Count; i++) data.FindProperty("items").GetArrayElementAtIndex(i).objectReferenceValue = catalog.Items[i];
        foreach (var field in new[] { "slots", "icons", "highlights" }) data.FindProperty(field).arraySize = 9;
        for (int i = 0; i < 9; i++)
        {
            var slot = panel.transform.Find("Empty Slot " + (i+1));
            var image = slot.GetComponent<Image>(); image.raycastTarget = true;
            image.sprite = Sprite("아이템창_안눌림"); image.preserveAspect = true;
            var button = Component<Button>(slot.gameObject); button.targetGraphic = image;
            var colors = button.colors; colors.disabledColor = Color.white; button.colors = colors;
            // Fit the icon inside the visible selection border, excluding the sprite's transparent margins.
            var icon = Picture(slot, "Owned Icon", new Vector2(-2, 2), new Vector2(60, 60), null);
            icon.enabled = false;
            var highlight = Picture(slot, "Selection", Vector2.zero, new Vector2(142,162), Sprite("아이템창_눌림"));
            highlight.color = Color.white; highlight.enabled = false;
            highlight.transform.SetAsFirstSibling();
            data.FindProperty("slots").GetArrayElementAtIndex(i).objectReferenceValue = button;
            data.FindProperty("icons").GetArrayElementAtIndex(i).objectReferenceValue = icon;
            data.FindProperty("highlights").GetArrayElementAtIndex(i).objectReferenceValue = highlight;
        }
        var detail = Rect(panel.transform, "Item Detail", new Vector2(0,-300), new Vector2(480,300));
        // Integrate the detail into the inventory's existing backdrop instead of covering the board.
        var background = Component<Image>(detail.gameObject); background.enabled = false;
        background.sprite = null; background.material = null; background.raycastTarget = false;
        var divider = Picture(detail, "Divider", new Vector2(0,154), new Vector2(440,2), null);
        divider.color = new Color32(223,157,65,160); divider.preserveAspect = false;
        var selectedIcon = Picture(detail, "Icon", new Vector2(-175,102), new Vector2(68,68), null);
        var title = Label(detail, "Name", new Vector2(38,115), new Vector2(310,36), 26);
        var status = Label(detail, "Status", new Vector2(38,78), new Vector2(310,30), 18);
        status.color = new Color32(231,189,122,255);
        var description = Label(detail, "Description", new Vector2(0,-11), new Vector2(420,132), 20);
        description.alignment = TextAnchor.UpperLeft; description.lineSpacing = .95f;
        description.resizeTextForBestFit = true; description.resizeTextMinSize = 18; description.resizeTextMaxSize = 20;
        var useImage = Picture(detail, "Use", new Vector2(125,-118), new Vector2(180,60), Sprite("아이템정보창_사용하기버튼"));
        useImage.raycastTarget = true; var useButton = Component<Button>(useImage.gameObject); useButton.targetGraphic = useImage;
        useButton.transition = Selectable.Transition.SpriteSwap; useButton.spriteState = new SpriteState { pressedSprite = Sprite("아이템정보창_사용하기버튼_눌림") };
        var backImage = Picture(detail, "Back", new Vector2(-150,-118), new Vector2(120,50), Sprite("아이템정보창_뒤로버튼"));
        backImage.raycastTarget = true; var back = Component<Button>(backImage.gameObject); back.targetGraphic = backImage;
        back.transition = Selectable.Transition.SpriteSwap; back.spriteState = new SpriteState { pressedSprite = Sprite("아이템정보창_뒤로버튼_눌림") };
        var hint = Label(panel.transform, "Empty Hint", new Vector2(0,-300), new Vector2(440,80), 24); hint.text = "보유한 아이템이 없습니다.";
        var pending = panel.transform.Find("Pending Effect Status");
        if (pending != null)
        {
            var pendingRect = Rect(panel.transform, pending.name, new Vector2(0,-480), new Vector2(440,36));
            var pendingText = pendingRect.GetComponent<Text>(); pendingText.fontSize = 17;
            pendingText.color = new Color32(231,189,122,255);
        }
        data.FindProperty("detail").objectReferenceValue = detail.gameObject;
        data.FindProperty("selectedIcon").objectReferenceValue = selectedIcon;
        data.FindProperty("selectedName").objectReferenceValue = title;
        data.FindProperty("description").objectReferenceValue = description;
        data.FindProperty("status").objectReferenceValue = status;
        data.FindProperty("emptyHint").objectReferenceValue = hint;
        data.FindProperty("useButton").objectReferenceValue = useButton;
        data.FindProperty("backButton").objectReferenceValue = back;
        data.ApplyModifiedPropertiesWithoutUndo(); detail.gameObject.SetActive(false);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var saved = new SerializedObject(Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include));
        foreach (var field in new[] { "slots", "icons", "highlights" })
            for (int i = 0; i < 9; i++) if (saved.FindProperty(field).GetArrayElementAtIndex(i).objectReferenceValue == null) throw new System.Exception("Missing saved inventory reference: " + field + i);
    }
    public static void BuildAndRun() { Build(); InventoryVerification.ReimportAndRun(); }
}

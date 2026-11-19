using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ShopCatalogBuilder
{
    const string Folder = "Assets/Data/Shop";
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().Single(go => go.scene.IsValid() && go.name == name);
    public static void Build()
    {
        System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var panel = Find("Shop Panel"); var catalog = panel.GetComponent<ShopCatalogUI>();
        if (catalog == null) catalog = panel.AddComponent<ShopCatalogUI>();
        var data = new SerializedObject(catalog);
        data.FindProperty("turnController").objectReferenceValue = Object.FindFirstObjectByType<TurnController>();
        string[] controls = { "Throw Button", "Yut Result", "Shop Button", "Inventory Button", "Start Battle Button" };
        data.FindProperty("backgroundControls").arraySize = controls.Length;
        for (int i = 0; i < controls.Length; i++) data.FindProperty("backgroundControls").GetArrayElementAtIndex(i).objectReferenceValue = Find(controls[i]).GetComponent<CanvasGroup>();
        string[] rows = { "Soul Crystal", "Heavenly Luck Talisman", "Dokkaebi Pouch", "Dokkaebi Yut", "Heavenly Coin" };
        string[] ids = { "SoulCrystal", "HeavenlyLuckTalisman", "DokkaebiPouch", "DokkaebiYut", "HeavenlyMandateCoin" };
        string[] titles = { "영혼의 결정", "천운부", "도깨비 복 주머니", "도깨비 윷", "천명전" };
        int[] prices = { 150, 100, 200, 50, 100 };
        string[] backgrounds = {
            "전장에서 쓰러진 용사들의 혼이 오랜 세월 응축되어 만들어진 결정체.",
            "하늘의 기운을 종이에 담아낸 부적. 지닌 자에게 뜻밖의 행운을 가져다준다.",
            "도깨비가 인간의 재물을 훔쳐 모아 만든 주머니. 주인을 잘 만나면 큰 복을 가져다준다.",
            "도깨비가 인간에게 장난을 치기 위해 만들었다는 신비한 윷가락.",
            "하늘이 선택한 자에게만 허락했다는 고대 화폐. 운명을 바꾸는 대가로 사용되었다고 한다." };
        string[] effects = {
            "특별소환 재화 5개 획득.",
            "구매 즉시 스테이지 동안 윷 던지기 비용 20% 할인. 사용 조작 없음.",
            "일반재화 100, 150, 200, 250, 300 중 각각 20% 확률로 획득.",
            "다음 던지기 1회에 윷과 모 각각 25%. 효과 대기 중 추가 사용 불가.",
            "다음 전투의 처치 및 기본 완료 보상 2배. 같은 전투에 중복 적용 불가." };
        var names = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Item/UI/Shop/Shop_ItemName_Sheet.png").OfType<Sprite>()
            .OrderBy(sprite => int.Parse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1))).ToArray();
        foreach (string field in new[] { "items", "itemButtons", "rowIcons", "rowPrices", "selectionHighlights" }) data.FindProperty(field).arraySize = 5;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = Find("Shop Item - " + rows[i]); var icon = row.transform.Find("Icon").GetComponent<Image>();
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = Vector2.one * .5f;
            rowRect.anchoredPosition = new Vector2(0, 320 - 160 * i);
            string path = Folder + "/" + ids[i] + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<ShopItemDefinition>(path);
            if (item == null) { item = ScriptableObject.CreateInstance<ShopItemDefinition>(); AssetDatabase.CreateAsset(item, path); }
            var definition = new SerializedObject(item);
            definition.FindProperty("id").enumValueIndex = i; definition.FindProperty("displayName").stringValue = titles[i];
            definition.FindProperty("price").intValue = prices[i]; definition.FindProperty("background").stringValue = backgrounds[i];
            definition.FindProperty("effect").stringValue = effects[i]; definition.FindProperty("icon").objectReferenceValue = icon.sprite;
            definition.FindProperty("nameSprite").objectReferenceValue = names[i]; definition.ApplyModifiedPropertiesWithoutUndo();
            var marker = row.transform.Find("Selected Highlight");
            if (marker == null)
            {
                var go = new GameObject("Selected Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = row.layer; marker = go.transform; marker.SetParent(row.transform, false); marker.SetAsFirstSibling();
                var rect = (RectTransform)marker; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var highlight = marker.GetComponent<Image>(); highlight.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Item/UI/Shop/Shop_Item_Button_Glow.png"); highlight.raycastTarget = false; highlight.enabled = i == 0;
            data.FindProperty("items").GetArrayElementAtIndex(i).objectReferenceValue = item;
            data.FindProperty("itemButtons").GetArrayElementAtIndex(i).objectReferenceValue = row.GetComponent<Button>();
            data.FindProperty("rowIcons").GetArrayElementAtIndex(i).objectReferenceValue = icon;
            data.FindProperty("rowPrices").GetArrayElementAtIndex(i).objectReferenceValue = Numberify(row.transform.Find("Price").gameObject);
            data.FindProperty("selectionHighlights").GetArrayElementAtIndex(i).objectReferenceValue = highlight;
        }
        Find("Shop Item - Gumiho Heart").SetActive(false);
        data.FindProperty("selectedIcon").objectReferenceValue = Find("Selected Item Icon").GetComponent<Image>();
        data.FindProperty("selectedName").objectReferenceValue = Find("Selected Item Name").GetComponent<Image>();
        data.FindProperty("selectedPrice").objectReferenceValue = Numberify(Find("Price Value"));
        var description = Find("Description Text").GetComponent<Text>();
        description.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Character_Info/03_Fonts/YiSunShin-Bold.otf");
        description.fontSize = 22; description.lineSpacing = 1.05f; description.alignment = TextAnchor.UpperLeft;
        description.color = new Color32(255, 230, 187, 255); description.rectTransform.sizeDelta = new Vector2(480, 160);
        description.rectTransform.anchoredPosition = new Vector2(0, -15);
        var plate = Find("Item Description").GetComponent<RectTransform>(); plate.anchoredPosition = new Vector2(0, -620); plate.sizeDelta = new Vector2(546, 250);
        data.FindProperty("description").objectReferenceValue = description;
        var buy = Find("Buy Button").GetComponent<Button>(); buy.interactable = false; data.FindProperty("buyButton").objectReferenceValue = buy;
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets(); RepairHighlights(catalog);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    public static void RepairHighlights(ShopCatalogUI catalog)
    {
        var data = new SerializedObject(catalog); var buttons = data.FindProperty("itemButtons");
        var markers = data.FindProperty("selectionHighlights"); markers.arraySize = buttons.arraySize;
        for (int i = 0; i < buttons.arraySize; i++)
        {
            var button = (Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue;
            var marker = button.transform.Find("Selected Highlight");
            if (marker == null)
            {
                var go = new GameObject("Selected Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = button.gameObject.layer; marker = go.transform; marker.SetParent(button.transform, false); marker.SetAsFirstSibling();
                var rect = (RectTransform)marker; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var image = marker.GetComponent<Image>();
            var glow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Item/UI/Shop/Shop_Item_Button_Glow.png");
            image.sprite = glow != null ? glow : button.image.sprite;
            image.color = glow != null ? Color.white : new Color(1f,.85f,.3f,.25f);
            image.raycastTarget = false; image.enabled = i == 0; markers.GetArrayElementAtIndex(i).objectReferenceValue = image;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static GoldNumberDisplay Numberify(GameObject go)
    {
        var previous = go.GetComponent<Graphic>(); if (previous != null) previous.enabled = false;
        var number = go.GetComponent<GoldNumberDisplay>(); if (number == null) number = go.AddComponent<GoldNumberDisplay>();
        var source = new SerializedObject(Find("Coin HUD").GetComponent<GoldNumberDisplay>()); var data = new SerializedObject(number);
        data.FindProperty("digitSprites").arraySize = 10;
        for (int i = 0; i < 10; i++) data.FindProperty("digitSprites").GetArrayElementAtIndex(i).objectReferenceValue = source.FindProperty("digitSprites").GetArrayElementAtIndex(i).objectReferenceValue;
        data.FindProperty("displayWidth").floatValue = 80; data.FindProperty("digitWidth").floatValue = 24;
        data.FindProperty("digitHeight").floatValue = 34; data.FindProperty("rightPadding").floatValue = 0;
        data.ApplyModifiedPropertiesWithoutUndo(); return number;
    }
    public static void BuildAndRun() { Build(); ShopCatalogVerification.ReimportAndRun(); }
}

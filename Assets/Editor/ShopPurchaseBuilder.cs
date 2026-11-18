using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ShopPurchaseBuilder
{
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var turn = Object.FindFirstObjectByType<TurnController>();
        var catalog = Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);
        var inventory = turn.GetComponent<StageInventory>(); if (inventory == null) inventory = turn.gameObject.AddComponent<StageInventory>();
        var purchases = turn.GetComponent<ShopPurchaseController>(); if (purchases == null) purchases = turn.gameObject.AddComponent<ShopPurchaseController>();
        var purchaseData = new SerializedObject(purchases);
        purchaseData.FindProperty("turnController").objectReferenceValue = turn;
        purchaseData.FindProperty("currency").objectReferenceValue = turn.GetComponent<StageCurrency>();
        purchaseData.FindProperty("inventory").objectReferenceValue = inventory;
        purchaseData.FindProperty("products").arraySize = catalog.Items.Count;
        for (int i = 0; i < catalog.Items.Count; i++) purchaseData.FindProperty("products").GetArrayElementAtIndex(i).objectReferenceValue = catalog.Items[i];
        purchaseData.ApplyModifiedPropertiesWithoutUndo();
        var data = new SerializedObject(catalog); data.FindProperty("purchases").objectReferenceValue = purchases;
        var buttons = data.FindProperty("itemButtons"); var statuses = data.FindProperty("rowStatus"); statuses.arraySize = buttons.arraySize;
        for (int i = 0; i < buttons.arraySize; i++)
        {
            var button = (Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue;
            statuses.GetArrayElementAtIndex(i).objectReferenceValue = Label(button.transform, "Purchase Status", new Vector2(95, -31), new Vector2(180, 24), 18);
        }
        var buy = (Button)data.FindProperty("buyButton").objectReferenceValue;
        data.FindProperty("purchaseStatus").objectReferenceValue = Label(buy.transform.parent, "Selected Purchase Status", new Vector2(0, -835), new Vector2(400, 30), 22, true);
        data.ApplyModifiedPropertiesWithoutUndo();
        ShopCatalogBuilder.RepairHighlights(catalog);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var saved = new SerializedObject(Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include));
        for (int i = 0; i < 5; i++)
            if (saved.FindProperty("selectionHighlights").GetArrayElementAtIndex(i).objectReferenceValue == null)
                throw new System.InvalidOperationException("Saved shop selection highlight is missing at " + i);
    }
    static Text Label(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, bool top = false)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = parent.gameObject.layer; child = go.transform; child.SetParent(parent, false);
        }
        var rect = (RectTransform)child; rect.anchorMin = rect.anchorMax = new Vector2(.5f, top ? 1f : .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var label = child.GetComponent<Text>(); label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Character_Info/03_Fonts/YiSunShin-Bold.otf");
        label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter; label.color = new Color32(255, 220, 150, 255); label.raycastTarget = false; label.text = "";
        return label;
    }
    public static void BuildAndRun() { Build(); ShopPurchaseVerification.ReimportAndRun(); }
}

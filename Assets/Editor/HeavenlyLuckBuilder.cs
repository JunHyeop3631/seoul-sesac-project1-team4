using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HeavenlyLuckBuilder
{
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var turn = Object.FindFirstObjectByType<TurnController>();
        var effect = turn.GetComponent<HeavenlyLuckTalismanEffect>();
        if (effect == null) effect = turn.gameObject.AddComponent<HeavenlyLuckTalismanEffect>();
        var data = new SerializedObject(effect);
        data.FindProperty("purchases").objectReferenceValue = turn.GetComponent<ShopPurchaseController>();
        data.FindProperty("itemUse").objectReferenceValue = turn.GetComponent<StageItemUseController>(); data.ApplyModifiedPropertiesWithoutUndo();
        var turnData = new SerializedObject(turn);
        turnData.FindProperty("heavenlyLuck").objectReferenceValue = effect;
        var button = (GameObject)turnData.FindProperty("throwButton").objectReferenceValue;
        var cost = button.transform.Find("Throw Background/Throw Cost");
        for (int i = 0; i < 2; i++) cost.Find("Cost Digit " + i).gameObject.SetActive(false);
        var numbers = cost.Find("Current Cost");
        if (numbers == null)
        {
            numbers = new GameObject("Current Cost", typeof(RectTransform)).transform;
            numbers.SetParent(cost, false); numbers.gameObject.layer = cost.gameObject.layer;
        }
        var rect = (RectTransform)numbers; rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
        rect.sizeDelta = new Vector2(24,18); rect.anchoredPosition = new Vector2(8,0);
        var display = numbers.GetComponent<GoldNumberDisplay>(); if (display == null) display = numbers.gameObject.AddComponent<GoldNumberDisplay>();
        var source = new SerializedObject(GameObject.Find("Coin HUD").GetComponent<GoldNumberDisplay>());
        var digits = new SerializedObject(display); digits.FindProperty("digitSprites").arraySize = 10;
        for (int i = 0; i < 10; i++) digits.FindProperty("digitSprites").GetArrayElementAtIndex(i).objectReferenceValue = source.FindProperty("digitSprites").GetArrayElementAtIndex(i).objectReferenceValue;
        digits.FindProperty("digitWidth").floatValue = 12; digits.FindProperty("digitHeight").floatValue = 18;
        digits.FindProperty("displayWidth").floatValue = 24; digits.FindProperty("rightPadding").floatValue = 0;
        digits.FindProperty("value").intValue = 20; digits.ApplyModifiedPropertiesWithoutUndo();
        turnData.FindProperty("throwCostDisplay").objectReferenceValue = display; turnData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var saved = new SerializedObject(Object.FindFirstObjectByType<TurnController>());
        if (saved.FindProperty("heavenlyLuck").objectReferenceValue == null || saved.FindProperty("throwCostDisplay").objectReferenceValue == null)
            throw new System.Exception("Saved talisman/cost references are missing");
    }
    public static void BuildAndRun() { Build(); HeavenlyLuckVerification.ReimportAndRun(); }
}

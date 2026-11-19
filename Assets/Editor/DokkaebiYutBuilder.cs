using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DokkaebiYutBuilder
{
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var use = Object.FindFirstObjectByType<StageItemUseController>();
        var effect = use.GetComponent<DokkaebiYutEffect>();
        if (effect == null) effect = use.gameObject.AddComponent<DokkaebiYutEffect>();
        var data = new SerializedObject(effect);
        data.FindProperty("itemUse").objectReferenceValue = use;
        data.FindProperty("throwController").objectReferenceValue = Object.FindFirstObjectByType<YutThrowController>();
        data.ApplyModifiedPropertiesWithoutUndo();
        var thrower = new SerializedObject(Object.FindFirstObjectByType<YutThrowController>());
        thrower.FindProperty("dokkaebiYut").objectReferenceValue = effect; thrower.ApplyModifiedPropertiesWithoutUndo();
        var view = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        var child = view.transform.Find("Pending Effect Status");
        if (child == null)
        {
            child = new GameObject("Pending Effect Status", typeof(RectTransform), typeof(UnityEngine.UI.Text)).transform;
            child.SetParent(view.transform, false); child.gameObject.layer = view.gameObject.layer;
        }
        var rect = (RectTransform)child; rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
        rect.anchoredPosition = new Vector2(0,-400); rect.sizeDelta = new Vector2(440,60);
        var label = child.GetComponent<UnityEngine.UI.Text>();
        label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Character_Info/03_Fonts/YiSunShin-Bold.otf");
        label.fontSize = 24; label.color = new Color32(255,220,150,255); label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; label.text = "";
        var ui = new SerializedObject(view); ui.FindProperty("pendingEffectStatus").objectReferenceValue = label; ui.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var saved = new SerializedObject(Object.FindFirstObjectByType<DokkaebiYutEffect>());
        if (saved.FindProperty("itemUse").objectReferenceValue == null || saved.FindProperty("throwController").objectReferenceValue == null)
            throw new System.Exception("Dokkaebi yut saved references are missing");
    }
    public static void BuildAndRun() { Build(); DokkaebiYutVerification.ReimportAndRun(); }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DokkaebiPouchBuilder
{
    public static void Build()
    {
        // Reconnect the prerequisite cost display if an editor save omitted its newer fields.
        HeavenlyLuckBuilder.Build();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var use = Object.FindFirstObjectByType<StageItemUseController>();
        var effect = use.GetComponent<DokkaebiPouchEffect>();
        if (effect == null) effect = use.gameObject.AddComponent<DokkaebiPouchEffect>();
        var data = new SerializedObject(effect);
        data.FindProperty("itemUse").objectReferenceValue = use;
        data.FindProperty("currency").objectReferenceValue = use.GetComponent<StageCurrency>();
        data.ApplyModifiedPropertiesWithoutUndo();
        ShopCatalogBuilder.RepairHighlights(Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include));
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var saved = new SerializedObject(Object.FindFirstObjectByType<DokkaebiPouchEffect>());
        if (saved.FindProperty("itemUse").objectReferenceValue == null || saved.FindProperty("currency").objectReferenceValue == null)
            throw new System.Exception("Dokkaebi pouch saved references are missing");
    }
    public static void BuildAndRun() { Build(); DokkaebiPouchVerification.ReimportAndRun(); }
}

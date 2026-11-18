using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SoulCrystalBuilder
{
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var use = Object.FindFirstObjectByType<StageItemUseController>();
        var effect = use.GetComponent<SoulCrystalEffect>();
        if (effect == null) effect = use.gameObject.AddComponent<SoulCrystalEffect>();
        var data = new SerializedObject(effect);
        data.FindProperty("itemUse").objectReferenceValue = use;
        data.FindProperty("currency").objectReferenceValue = use.GetComponent<StageCurrency>();
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var saved = new SerializedObject(Object.FindFirstObjectByType<SoulCrystalEffect>());
        if (saved.FindProperty("itemUse").objectReferenceValue == null || saved.FindProperty("currency").objectReferenceValue == null)
            throw new System.Exception("Soul crystal saved references are missing");
    }
    public static void BuildAndRun() { Build(); SoulCrystalVerification.ReimportAndRun(); }
}

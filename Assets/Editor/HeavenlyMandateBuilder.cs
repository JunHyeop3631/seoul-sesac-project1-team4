using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HeavenlyMandateBuilder
{
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var use = Object.FindFirstObjectByType<StageItemUseController>();
        var effect = use.GetComponent<HeavenlyMandateEffect>();
        if (effect == null) effect = use.gameObject.AddComponent<HeavenlyMandateEffect>();
        var data = new SerializedObject(effect);
        data.FindProperty("itemUse").objectReferenceValue = use;

        data.ApplyModifiedPropertiesWithoutUndo();
        var turn = new SerializedObject(use.GetComponent<TurnController>());
        turn.FindProperty("heavenlyMandate").objectReferenceValue = effect; turn.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var saved = new SerializedObject(Object.FindFirstObjectByType<HeavenlyMandateEffect>());
        if (saved.FindProperty("itemUse").objectReferenceValue == null ||
            new SerializedObject(Object.FindFirstObjectByType<TurnController>()).FindProperty("heavenlyMandate").objectReferenceValue == null)
            throw new System.Exception("Heavenly mandate saved references are missing");
    }
    public static void BuildAndRun() { Build(); HeavenlyMandateVerification.ReimportAndRun(); }
}

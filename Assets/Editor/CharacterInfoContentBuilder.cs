using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterInfoContentBuilder
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
        .Single(go => go.scene.IsValid() && go.name == name);
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var panel = Object.FindFirstObjectByType<CharacterInfoPanel>(FindObjectsInactive.Include);
        var data = new SerializedObject(panel);
        var infoGroup = panel.GetComponent<CanvasGroup>();
        if (infoGroup == null) infoGroup = panel.gameObject.AddComponent<CanvasGroup>();
        infoGroup.alpha = 0f;
        data.FindProperty("infoContent").objectReferenceValue = infoGroup;
        var groups = data.FindProperty("defaultContent");
        string[] names = { "Throw Button", "Yut Result", "Shop Button", "Inventory Button", "Start Battle Button", "Shop Panel", "Inventory Panel" };
        groups.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var go = Find(names[i]); var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            groups.GetArrayElementAtIndex(i).objectReferenceValue = group;
        }
        var renderers = Object.FindObjectsByType<YutStick>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .SelectMany(stick => stick.GetComponentsInChildren<SpriteRenderer>(true)).Distinct().ToArray();
        var world = data.FindProperty("defaultWorldContent"); world.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++) world.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        var decorations = data.FindProperty("storyDecorations");
        var storyNames = new[] { "Story Heading", "Story Left Ornament", "Story Right Ornament" };
        decorations.arraySize = storyNames.Length;
        for (int i = 0; i < storyNames.Length; i++) decorations.GetArrayElementAtIndex(i).objectReferenceValue = Find(storyNames[i]);
        var profiles = data.FindProperty("profiles");
        if (profiles.arraySize == 5) profiles.InsertArrayElementAtIndex(5);
        var tiger = profiles.GetArrayElementAtIndex(5); tiger.FindPropertyRelative("unitKind").enumValueIndex = 5;
        tiger.FindPropertyRelative("nameSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Character_Info/02_BrushText/name-white-tiger-general.png");
        var idle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Ally/Idle/WhiteTiger_left.png");
        tiger.FindPropertyRelative("idleLeft").objectReferenceValue = idle;
        tiger.FindPropertyRelative("evolvedIdleLeft").objectReferenceValue = idle;
        // Approved White Tiger story shared by the info panel.
        tiger.FindPropertyRelative("story").stringValue = "도깨비를 봉인한 전설의 장군.\n수호자들의 간절한 부름에\n잠들었던 창을 다시 들어 올린다.";
        data.ApplyModifiedPropertiesWithoutUndo();
        var surface = panel.GetComponent<Image>(); surface.color = Color.clear; surface.raycastTarget = true;
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
            if (graphic != surface) graphic.raycastTarget = false;
        Find("Character_Info").SetActive(false);
        var evolve = Find("Character_Evolve").GetComponent<RectTransform>();
        evolve.anchoredPosition = new Vector2(0, evolve.anchoredPosition.y);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    public static void BuildAndRun() { Build(); CharacterInfoContentVerification.ReimportAndRun(); }
    public static void BuildAndRunTransition() { Build(); CharacterInfoTransitionVerification.ReimportAndRun(); }
}

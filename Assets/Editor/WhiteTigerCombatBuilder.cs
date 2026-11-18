using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WhiteTigerCombatBuilder
{
    const string Folder = "Assets/Animations/Ally/WhiteTiger";
    [MenuItem("Tools/White Tiger/Rebuild Combat Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var idleLeft = Clip("IdleLeft", "Assets/Art/Ally/Idle/WhiteTiger_left.png", true);
        var idleRight = Clip("IdleRight", "Assets/Art/Ally/Idle/WhiteTiger_right.png", true);
        var attackLeft = Clip("AttackLeft", "Assets/Art/Ally/Attack/WhiteTiger_Attack_left.png", false);
        var attackRight = Clip("AttackRight", "Assets/Art/Ally/Attack/WhiteTiger_Attack_right.png", false);
        string path = Folder + "/WhiteTiger.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        foreach (var item in machine.states) machine.RemoveState(item.state);
        var left = machine.AddState("IdleLeft"); left.motion = idleLeft;
        var right = machine.AddState("IdleRight"); right.motion = idleRight;
        var hitLeft = machine.AddState("AttackLeft"); hitLeft.motion = attackLeft;
        var hitRight = machine.AddState("AttackRight"); hitRight.motion = attackRight;
        machine.defaultState = left;
        foreach (var pair in new[] { (hitLeft, left), (hitRight, right) })
        {
            var transition = pair.Item1.AddTransition(pair.Item2);
            transition.hasExitTime = true; transition.exitTime = 1; transition.duration = 0;
        }
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/WhiteTiger.prefab");
        try
        {
            var tiger = root.GetComponent<WhiteTiger>(); var visual = tiger.VisualRenderer;
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (visual.GetComponent<AllyAnimationEvents>() == null) visual.gameObject.AddComponent<AllyAnimationEvents>();
            var effect = root.GetComponentInChildren<WhiteTigerSlash>(true);
            if (effect == null)
            {
                var child = new GameObject("Tiger Slash"); child.transform.SetParent(root.transform, false);
                effect = child.AddComponent<WhiteTigerSlash>(); child.AddComponent<SpriteRenderer>();
            }
            var renderer = effect.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = visual.sharedMaterial;
            renderer.sortingOrder = visual.sortingOrder + 1; renderer.enabled = false;
            effect.transform.localScale = new Vector3(.45f, .45f, 1f);
            var effectData = new SerializedObject(effect);
            effectData.FindProperty("visual").objectReferenceValue = renderer;
            var frames = Sprites("Assets/Art/Effect/Attack/WhiteTiger_Attack_Effect.png");
            var field = effectData.FindProperty("frames"); field.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            effectData.ApplyModifiedPropertiesWithoutUndo();
            var data = new SerializedObject(tiger);
            data.FindProperty("allyAnimator").objectReferenceValue = animator;
            data.FindProperty("slash").objectReferenceValue = effect;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/WhiteTiger.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
    public static void BuildAndRun() { Build(); WhiteTigerCombatVerification.ReimportAndRun(); }
    static Sprite[] Sprites(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(sprite => int.TryParse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1), out int index) ? index : 0).ToArray();
        if (sprites.Length == 0) throw new Exception("No imported sprites: " + path);
        return sprites;
    }
    static AnimationClip Clip(string name, string spritePath, bool idle)
    {
        var sprites = Sprites(spritePath);
        string path = Folder + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.name = name;
        int count = idle ? 1 : sprites.Length; float length = idle ? 1 : 2;
        clip.frameRate = count / length;
        var keys = new ObjectReferenceKeyframe[count];
        for (int i = 0; i < count; i++) keys[i] = new ObjectReferenceKeyframe { time = i * length / count, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = idle; settings.stopTime = length;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, idle ? Array.Empty<AnimationEvent>() :
            new[] { new AnimationEvent { time = length * 5 / count, functionName = "ApplyAttack" } });
        EditorUtility.SetDirty(clip); return clip;
    }
}

using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Asset authoring utility; does not open or save gameplay scenes.
public static class CombatEffectBuilder
{
    private const string Folder = "Assets/Animations/Effects";

    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Animations", "Effects");

        Configure("Do_Magic", "Assets/Art/Ally/Attack/Effect/Do_Magic.png", true);
        Configure("Mo_Arrow", "Assets/Art/Ally/Attack/Effect/Mo_Arrow.png", true);
        Configure("Enemy_Hit", "Assets/Art/Enemy/Effect/Enemy_Hit.png", false);
        AssetDatabase.SaveAssets();
        Debug.Log("Created 60fps combat effect clips, controllers and prefab connections.");
    }

    private static void Configure(string name, string texturePath, bool loop)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>()
            .OrderBy(sprite => int.Parse(Regex.Match(sprite.name, @"\d+$").Value)).ToArray();
        if (sprites.Length == 0)
            throw new InvalidOperationException("No sprites: " + texturePath);

        string clipPath = $"{Folder}/{name}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        clip.frameRate = 60f;
        var keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int index = 0; index < sprites.Length; index++)
            keys[index] = new ObjectReferenceKeyframe { time = index / 60f, value = sprites[index] };
        // Unity includes one sample interval after the final sprite key.
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.startTime = 0;
        settings.stopTime = sprites.Length / 60f;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        if (name == "Enemy_Hit")
            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { time = sprites.Length / 60f, functionName = "OnHitAnimationFinished" }
            });
        EditorUtility.SetDirty(clip);

        string controllerPath = $"{Folder}/{name}.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
        AnimatorState state = controller.layers[0].stateMachine.defaultState;
        state.name = name == "Enemy_Hit" ? "Hit" : "Flight";
        state.motion = clip;
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);

        string prefabPath = $"Assets/Prefabs/{name}.prefab";
        GameObject root = name == "Enemy_Hit"
            ? new GameObject(name, typeof(SpriteRenderer), typeof(Animator), typeof(HitEffect))
            : PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = sprites[0];
            if (name == "Enemy_Hit")
            {
                var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mo_Arrow.prefab");
                Material material = reference.GetComponent<SpriteRenderer>().sharedMaterial;
                renderer.sharedMaterial = material != null ? material :
                    AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                renderer.sortingOrder = 3;
            }
            Animator animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            if (name == "Enemy_Hit") UnityEngine.Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
        }
    }
}

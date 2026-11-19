using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class YutActionLayoutSetup
{
    const string Art="Assets/Art/Button/YutApprovedCards/";
    static readonly string[] Results={"Do","Gae","Geol","Yut","Mo","Backdo"};
    [Serializable] class Regions { public Region[] regions; }
    [Serializable] class Region {public string file,name;public float x,y,width,height;}
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before changing UI layout.");
        ImportCards();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        Configure(Object.FindFirstObjectByType<TurnController>());
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    static void ImportCards()
    {
        AssetDatabase.Refresh();var regions=JsonUtility.FromJson<Regions>(File.ReadAllText(Art+"Regions.json")).regions;
        foreach(var group in regions.GroupBy(r=>r.file))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Art+group.Key);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;
            importer.maxTextureSize=2048;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+group.Key);
            var factories=new SpriteDataProviderFactories();factories.Init();
            var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var existing=provider.GetSpriteRects();
            var rects=group.Select(r=>
            {
                // The old pressed sheet is one pixel taller; keep each pair's crop size equal.
                var normal=regions.Single(n=>n.name==r.name.Replace("_Pressed","_Normal"));
                var normalTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+normal.file);
                return new SpriteRect{name=r.name,pivot=Vector2.one*.5f,alignment=SpriteAlignment.Center,
                    spriteID=existing.FirstOrDefault(s=>s.name==r.name)?.spriteID??GUID.Generate(),
                    rect=new Rect(Mathf.Round(r.x*texture.width),Mathf.Round(r.y*texture.height),Mathf.Round(normal.width*normalTexture.width),Mathf.Round(normal.height*normalTexture.height))};
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
            provider.Apply();importer.SaveAndReimport();
        }
    }
    public static void Configure(TurnController turn)
    {
        var data=new SerializedObject(turn);
        var summon=(Button)data.FindProperty("summonButton").objectReferenceValue;
        var move=(Button)data.FindProperty("moveButton").objectReferenceValue;
        var result=Find("Yut Result").GetComponent<Image>();
        Place(result.rectTransform,new Vector2(.8515625f,0),new Vector2(0,535),new Vector2(350,100));
        result.preserveAspect=true;
        var actions=(GameObject)data.FindProperty("actionButtons").objectReferenceValue;
        actions.transform.SetParent(result.transform,false);
        Place((RectTransform)actions.transform,Vector2.one*.5f,new Vector2(0,-158),new Vector2(480,164));
        SetupButton(summon,new Vector2(-122,0));SetupButton(move,new Vector2(122,0));
        var visuals=turn.GetComponent<YutActionButtonVisuals>();if(visuals==null)visuals=turn.gameObject.AddComponent<YutActionButtonVisuals>();
        var view=new SerializedObject(visuals);Ref(view,"turn",turn);Ref(view,"summonButton",summon);Ref(view,"moveButton",move);
        foreach(var field in new[]{"summonCards","moveCards","summonPressed","movePressed"})
        {
            var array=view.FindProperty(field);array.arraySize=6;
            for(int i=0;i<6;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=Card(Results[i]+"_"+(field.StartsWith("summon")?"Summon":"Move")+"_"+(field.EndsWith("Pressed")?"Pressed":"Normal"));
        }
        view.ApplyModifiedPropertiesWithoutUndo();
        summon.image.sprite=Card("Do_Summon_Normal");move.image.sprite=Card("Do_Move_Normal");
        var thrower=new SerializedObject(Object.FindFirstObjectByType<YutThrowController>());
        string[] fields={"doSprite","gaeSprite","geolSprite","yutSprite","moSprite","backdoSprite"};
        for(int i=0;i<6;i++)Ref(thrower,fields[i],Card(Results[i]+"_Result"));thrower.ApplyModifiedPropertiesWithoutUndo();
        var throwButton=(GameObject)data.FindProperty("throwButton").objectReferenceValue;
        Place((RectTransform)throwButton.transform,new Vector2(.8515625f,0),new Vector2(0,410),new Vector2(460,208));
        var background=(RectTransform)throwButton.transform.Find("Throw Background");
        background.anchorMin=Vector2.zero;background.anchorMax=Vector2.one;background.offsetMin=background.offsetMax=Vector2.zero;background.localScale=Vector3.one;
        var cost=(RectTransform)background.Find("Throw Cost");
        cost.anchorMin=cost.anchorMax=cost.pivot=Vector2.one*.5f;cost.anchoredPosition=new Vector2(0,-55);cost.localScale=new Vector3(2.45f,2.45f,1);
    }
    static void SetupButton(Button button,Vector2 position)
    {
        Place((RectTransform)button.transform,Vector2.one*.5f,position,new Vector2(230,164));
        // The approved whole-card artwork contains both heading and clean lower caption.
        var image=button.GetComponent<Image>()??button.gameObject.AddComponent<Image>();
        button.targetGraphic=image;
        foreach(Transform child in button.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
        image.enabled=true;image.color=Color.white;image.material=null;
        image.preserveAspect=false;image.raycastTarget=true;
        button.transition=Selectable.Transition.SpriteSwap;
    }
    static void Place(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size)
    {rect.anchorMin=rect.anchorMax=anchor;rect.pivot=Vector2.one*.5f;rect.localScale=Vector3.one;rect.anchoredPosition=position;rect.sizeDelta=size;}
    static Sprite Card(string name)=>AssetDatabase.FindAssets("t:Texture2D",new[]{Art.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Sprite>().Single(s=>s.name==name);
    static GameObject Find(string name)=>Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(t=>t.name==name).gameObject;
    static void Ref(SerializedObject data,string name,Object value)=>data.FindProperty(name).objectReferenceValue=value;
}

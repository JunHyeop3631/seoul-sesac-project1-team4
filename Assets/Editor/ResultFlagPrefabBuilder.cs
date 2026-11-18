using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ResultFlagPrefabBuilder
{
    const string Art = "Assets/Art/ResultFlags/";
    public const string PrefabPath = "Assets/Prefabs/ResultFlag.prefab";
    [MenuItem("Tools/Yut UI/Rebuild Result Flag Prefab")]
    public static void Build()
    {
        var root = Rect(null, "Result Flag", Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.sizeDelta = Vector2.zero;
        var whole = root.gameObject.AddComponent<CanvasGroup>(); whole.alpha = 0; whole.blocksRaycasts = false; whole.interactable = false;
        var dimmer = Image(root, "Input Blocker and Dimmer", null, Vector2.zero, Vector2.zero);
        dimmer.rectTransform.anchorMin = Vector2.zero; dimmer.rectTransform.anchorMax = Vector2.one;
        dimmer.color = new Color(0, 0, 0, 0); dimmer.raycastTarget = true;
        var flag = Rect(root, "Hanging Flag", new Vector2(720, 850), new Vector2(0, 425));
        flag.pivot = new Vector2(.5f, 1);
        var banner = Image(flag, "Banner", Load("Victory/Banner_Victory.png"), new Vector2(720,850), new Vector2(0,-425));
        var title = Image(flag, "Title", Load("Victory/Title_Victory.png"), new Vector2(380,190), new Vector2(0,-200));
        var waveLabel = Image(flag, "Reached Wave Label", Load("Victory/Typography/Label_ReachedWave.png"), new Vector2(256,52), new Vector2(0,-332));
        var waveValue = Rect(flag, "Reached and Total Waves", new Vector2(510,112), new Vector2(0,-408));
        var buttons = Rect(flag, "Buttons", new Vector2(650,112), new Vector2(0,-671));
        var opacity = buttons.gameObject.AddComponent<CanvasGroup>(); opacity.alpha = 0;
        var home = Button(buttons, "Home", "Victory_home_Normal.png", new Vector2(-155,0));
        var retry = Button(buttons, "Retry", "Victory_retry_Normal.png", new Vector2(155,0));
        home.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = retry, selectOnLeft = retry };
        retry.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = home, selectOnRight = home };
        var view = root.gameObject.AddComponent<ResultFlagView>();
        var so = new SerializedObject(view);
        Set(so,"whole",whole); Set(so,"dimmer",dimmer); Set(so,"flag",flag); Set(so,"bannerImage",banner);
        Set(so,"titleImage",title); Set(so,"waveLabelImage",waveLabel); Set(so,"waveValue",waveValue);
        Set(so,"buttonOpacity",opacity); Set(so,"homeButton",home); Set(so,"retryButton",retry);
        Theme(so.FindProperty("victory"), "Victory", new Vector4(44,37,1110,1333), new Vector4(212,91,1553,834));
        Theme(so.FindProperty("defeat"), "Defeat", new Vector4(109,68,1091,1251), new Vector4(125,60,1708,820));
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath); Object.DestroyImmediate(root.gameObject);
        AssetDatabase.SaveAssets();
    }
    static void Theme(SerializedProperty prop, string name, Vector4 bannerBounds, Vector4 titleBounds)
    {
        var banner=Load(name+"/Banner_"+name+".png"); var title=Load(name+"/Title_"+name+".png");
        prop.FindPropertyRelative("banner").objectReferenceValue=banner;
        prop.FindPropertyRelative("title").objectReferenceValue=title;
        prop.FindPropertyRelative("waveLabel").objectReferenceValue=Load(name+"/Typography/Label_ReachedWave.png");
        prop.FindPropertyRelative("slash").objectReferenceValue=Load(name+"/Typography/Slash.png");
        foreach(var pair in new[]{"homeNormal","homePressed","retryNormal","retryPressed"})
        {
            string role=pair.StartsWith("home")?"home":"retry", state=pair.EndsWith("Normal")?"Normal":"Pressed";
            prop.FindPropertyRelative(pair).objectReferenceValue=Load("ButtonStates/"+name+"_"+role+"_"+state+".png");
        }
        var numbers=prop.FindPropertyRelative("numbers"); numbers.arraySize=11;
        for(int i=0;i<=10;i++) numbers.GetArrayElementAtIndex(i).objectReferenceValue=Load(name+"/Typography/Number_"+i.ToString("D2")+".png");
        // Match visible bounds, keeping source transparency intact.
        Fit(prop,"banner",banner,bannerBounds,new Vector2(720,850),new Vector2(0,-425),false);
        Fit(prop,"title",title,titleBounds,new Vector2(388,187),new Vector2(0,-200),true);
    }
    static void Fit(SerializedProperty prop,string prefix,Sprite sprite,Vector4 b,Vector2 box,Vector2 center,bool preserve)
    {
        float sx=box.x/(b.z-b.x), sy=box.y/(b.w-b.y);
        if(preserve) sx=sy=Mathf.Min(sx,sy);
        Vector2 size=sprite.rect.size;
        prop.FindPropertyRelative(prefix+"Size").vector2Value=new Vector2(size.x*sx,size.y*sy);
        prop.FindPropertyRelative(prefix+"Offset").vector2Value=center+new Vector2((size.x/2-(b.x+b.z)/2)*sx,((b.y+b.w)/2-size.y/2)*sy);
    }
    static Sprite Load(string path)
    {
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+path);
        if(!sprite) throw new System.Exception("Missing approved asset: "+Art+path);
        return sprite;
    }
    static void Set(SerializedObject so,string name,Object value) => so.FindProperty(name).objectReferenceValue=value;
    static RectTransform Rect(Transform parent,string name,Vector2 size,Vector2 position)
    {
        var obj=new GameObject(name,typeof(RectTransform)); obj.layer=5; var rect=(RectTransform)obj.transform;
        if(parent)rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,parent && parent.name=="Hanging Flag"?1f:.5f);
        rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
    }
    static Image Image(Transform parent,string name,Sprite sprite,Vector2 size,Vector2 position)
    {
        var image=Rect(parent,name,size,position).gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;return image;
    }
    static Button Button(Transform parent,string name,string asset,Vector2 position)
    {
        var image=Image(parent,name,Load("ButtonStates/"+asset),new Vector2(298,104),position);image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.SpriteSwap;return button;
    }
}

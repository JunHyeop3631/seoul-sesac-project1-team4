using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UIRefinementSetup
{
    const string Art="Assets/Art/";
    static Font Font=>AssetDatabase.LoadAssetAtPath<Font>(Art+"Character_Info/03_Fonts/YiSunShin-Bold.otf");
    public static void Apply()
    {
        AssetDatabase.Refresh();
        foreach(string file in new[]{"Panel.png","Button.png"})
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Art+"PauseMenu/"+file);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=4096;importer.SaveAndReimport();
        }
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var turn=Object.FindFirstObjectByType<TurnController>();
        YutActionLayoutSetup.Configure(turn);
        var catalog=Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var data=new SerializedObject(catalog);
        var buttons=data.FindProperty("itemButtons");var stamps=data.FindProperty("purchasedStamps");stamps.arraySize=buttons.arraySize;
        for(int i=0;i<buttons.arraySize;i++)
        {
            var row=((Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue).GetComponent<RectTransform>();
            row.anchorMin=row.anchorMax=row.pivot=new Vector2(.5f,.5f);row.anchoredPosition=new Vector2(0,320-160*i);
            var old=row.Find("Purchased Stamp");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var stamp=Image(row,"Purchased Stamp",Sprite(Art+"Item/UI/Shop/Shop_Purchased_Stamp.png"),new Vector2(375,150),Vector2.zero);
            stamp.preserveAspect=true;stamp.raycastTarget=false;stamp.enabled=false;stamps.GetArrayElementAtIndex(i).objectReferenceValue=stamp;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        BuildPause();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    static void BuildPause()
    {
        var exit=Object.FindFirstObjectByType<StageExitConfirmation>();var data=new SerializedObject(exit);
        var navigation=(GameObject)data.FindProperty("navigation").objectReferenceValue;
        navigation.transform.SetParent(exit.transform,false);
        var old=exit.transform.Find("Pause Menu");if(old!=null)Object.DestroyImmediate(old.gameObject);
        // Render and receive input above the separate shop canvas too.
        var canvas=exit.GetComponent<Canvas>();if(canvas==null)canvas=exit.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=200;
        if(exit.GetComponent<GraphicRaycaster>()==null)exit.gameObject.AddComponent<GraphicRaycaster>();
        var overlay=Image(exit.transform,"Pause Menu",null,Vector2.zero,Vector2.zero);Stretch(overlay.rectTransform);overlay.color=new Color(.035f,.018f,.03f,.66f);overlay.raycastTarget=true;
        var panel=Image(overlay.transform,"Pause Panel",Sprite(Art+"PauseMenu/Panel.png"),new Vector2(600,700),Vector2.zero);panel.raycastTarget=true;
        Label(panel.transform,"Pause Title","일시정지",new Vector2(0,274),new Vector2(350,66),42);
        var resumeGo=new GameObject("Continue",typeof(RectTransform),typeof(Image),typeof(Button));resumeGo.layer=5;resumeGo.transform.SetParent(panel.transform,false);
        var resume=resumeGo.GetComponent<Button>();resume.targetGraphic=resumeGo.GetComponent<Image>();StyleButton(resume,"계속하기",new Vector2(0,105));
        UnityEventTools.AddPersistentListener(resume.onClick,Find("ButtonSound").GetComponent<AudioSource>().Play);
        navigation.transform.SetParent(panel.transform,false);Stretch((RectTransform)navigation.transform);
        var home=(Button)data.FindProperty("homeButton").objectReferenceValue;var quit=(Button)data.FindProperty("quitButton").objectReferenceValue;
        StyleButton(home,"메인 화면",new Vector2(0,-35));StyleButton(quit,"게임 종료",new Vector2(0,-175));
        Label(panel.transform,"Pause Hint","Esc · 돌아가기",new Vector2(0,-260),new Vector2(350,38),22,new Color(.38f,.18f,.1f));
        Ref(data,"pauseMenu",overlay.gameObject);Ref(data,"resumeButton",resume);data.ApplyModifiedPropertiesWithoutUndo();
        overlay.transform.SetAsFirstSibling();overlay.gameObject.SetActive(false);
    }
    static void StyleButton(Button button,string title,Vector2 position)
    {
        var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.localScale=Vector3.one;rect.anchoredPosition=position;rect.sizeDelta=new Vector2(440,140);
        var image=button.image;image.sprite=Sprite(Art+"PauseMenu/Button.png");image.color=Color.white;image.preserveAspect=true;
        button.transition=Selectable.Transition.ColorTint;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,1,.87f);colors.pressedColor=new Color(.7f,.55f,.5f);colors.selectedColor=Color.white;colors.fadeDuration=.08f;button.colors=colors;
        foreach(var text in button.GetComponentsInChildren<Text>(true))Object.DestroyImmediate(text.gameObject);
        Label(button.transform,"Label",title,Vector2.zero,new Vector2(320,58),35);
    }
    static Text Label(Transform parent,string name,string value,Vector2 pos,Vector2 size,int fontSize,Color? color=null)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(parent,false);
        var text=go.GetComponent<Text>();text.font=Font;text.text=value;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;text.color=color??new Color(1,.91f,.73f);text.raycastTarget=false;
        text.rectTransform.anchoredPosition=pos;text.rectTransform.sizeDelta=size;return text;
    }
    static Image Image(Transform parent,string name,Sprite sprite,Vector2 size,Vector2 pos)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.sprite=sprite;image.rectTransform.sizeDelta=size;image.rectTransform.anchoredPosition=pos;return image;
    }
    static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=Vector2.one*.5f;rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
    static Sprite Sprite(string path)=>AssetDatabase.LoadAssetAtPath<Sprite>(path)??AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    static GameObject Find(string name)=>Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name==name).gameObject;
    static void Ref(SerializedObject so,string name,Object value)=>so.FindProperty(name).objectReferenceValue=value;
}

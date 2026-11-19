using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class StageExitBuilder
{
    static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var child=parent.Find(name);if(child==null){child=new GameObject(name,typeof(RectTransform)).transform;child.SetParent(parent,false);child.gameObject.layer=parent.gameObject.layer;}
        var rect=(RectTransform)child;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    static T Component<T>(GameObject go) where T:Component=>go.GetComponent<T>()??go.AddComponent<T>();
    static Text Label(Transform parent,string name,Vector2 position,Vector2 size,int fontSize,string value)
    {
        var text=Component<Text>(Rect(parent,name,position,size).gameObject);text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Character_Info/03_Fonts/YiSunShin-Bold.otf");
        text.fontSize=fontSize;text.color=new Color32(255,230,187,255);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.text=value;return text;
    }
    static Button Button(Transform parent,string name,Vector2 position,Vector2 size,string value,int fontSize=24)
    {
        var rect=Rect(parent,name,position,size);var image=Component<Image>(rect.gameObject);image.color=new Color32(84,27,22,255);image.raycastTarget=true;
        var outline=Component<Outline>(rect.gameObject);outline.effectColor=new Color32(224,161,67,255);outline.effectDistance=new Vector2(2,-2);
        var button=Component<Button>(rect.gameObject);button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.Automatic};
        Label(rect,"Label",Vector2.zero,size-Vector2.one*8,fontSize,value);return button;
    }
    public static void Build()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");var turn=Object.FindFirstObjectByType<TurnController>();var canvas=GameObject.Find("Canvas").transform;
        var root=Rect(canvas,"Stage Exit",Vector2.zero,Vector2.zero);Stretch(root);var controller=Component<StageExitConfirmation>(root.gameObject);
        var navigation=Rect(root,"Navigation",Vector2.zero,Vector2.zero);Stretch(navigation);
        var home=Button(navigation,"Home",new Vector2(1180,-225),new Vector2(84,44),"메인",22);home.GetComponent<RectTransform>().anchorMin=home.GetComponent<RectTransform>().anchorMax=new Vector2(0,1);
        var quit=Button(navigation,"Quit",new Vector2(1270,-225),new Vector2(84,44),"종료",22);quit.GetComponent<RectTransform>().anchorMin=quit.GetComponent<RectTransform>().anchorMax=new Vector2(0,1);
        var modal=Rect(root,"Confirmation",Vector2.zero,Vector2.zero);Stretch(modal);var dimmer=Component<Image>(modal.gameObject);dimmer.color=new Color(0.03f,0.02f,0.03f,.72f);dimmer.raycastTarget=true;
        var panel=Rect(modal,"Panel",Vector2.zero,new Vector2(720,360));var frame=Component<Image>(panel.gameObject);
        frame.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Item/UI/Shop/Shop_ItemDescription_Background.png").OfType<Sprite>().OrderByDescending(s=>s.rect.width*s.rect.height).First();frame.preserveAspect=true;frame.raycastTarget=true;
        var title=Label(panel,"Title",new Vector2(0,65),new Vector2(620,42),28,"");
        var message=Label(panel,"Message",new Vector2(0,0),new Vector2(620,64),23,"");
        var cancel=Button(panel,"Cancel",new Vector2(-120,-65),new Vector2(180,44),"취소");var confirm=Button(panel,"Confirm",new Vector2(120,-65),new Vector2(180,44),"이동");
        cancel.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=confirm,selectOnRight=confirm};confirm.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=cancel,selectOnRight=cancel};
        var data=new SerializedObject(controller);
        data.FindProperty("turn").objectReferenceValue=turn;data.FindProperty("actions").objectReferenceValue=Object.FindFirstObjectByType<YutActionController>();
        data.FindProperty("itemUse").objectReferenceValue=Object.FindFirstObjectByType<StageItemUseController>();data.FindProperty("purchases").objectReferenceValue=Object.FindFirstObjectByType<ShopPurchaseController>();
        data.FindProperty("currency").objectReferenceValue=Object.FindFirstObjectByType<StageCurrency>();
        data.FindProperty("navigation").objectReferenceValue=navigation.gameObject;data.FindProperty("modal").objectReferenceValue=modal.gameObject;
        data.FindProperty("homeButton").objectReferenceValue=home;data.FindProperty("quitButton").objectReferenceValue=quit;data.FindProperty("cancelButton").objectReferenceValue=cancel;data.FindProperty("confirmButton").objectReferenceValue=confirm;
        data.FindProperty("title").objectReferenceValue=title;data.FindProperty("message").objectReferenceValue=message;data.FindProperty("confirmLabel").objectReferenceValue=confirm.transform.Find("Label").GetComponent<Text>();data.ApplyModifiedPropertiesWithoutUndo();
        var state=new SerializedObject(turn);state.FindProperty("exitConfirmation").objectReferenceValue=controller;state.ApplyModifiedPropertiesWithoutUndo();modal.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        var saved=new SerializedObject(Object.FindFirstObjectByType<StageExitConfirmation>());
        foreach(var field in new[]{"turn","modal","homeButton","quitButton","cancelButton","confirmButton"})if(saved.FindProperty(field).objectReferenceValue==null)throw new System.Exception("Saved exit reference missing: "+field);
    }
    public static void BuildAndRun(){Build();StageExitVerification.ReimportAndRun();}
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
public static class UIRefinementVerification
{
    const string Key="Yut.UIRefinementVerification";
    static IEnumerator checks; static int assertions,errors,frames;
    static UIRefinementVerification(){EditorApplication.update+=Tick;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");SessionState.SetBool(Key,true);SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);EditorApplication.EnterPlaymode();}
    static void Tick(){
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(frames++<15)return;
        try{
            Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)<120,"Timeout");
            if(checks==null){Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors++;};checks=Check();}
            if(checks.MoveNext())return;Require(errors==0,"No runtime errors");
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/UIRefinementVerification.json",JsonUtility.ToJson(new Result{passed=true,assertions=assertions,runtimeErrors=errors},true));
            SessionState.SetBool(Key,false);Time.timeScale=1;EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);Time.timeScale=1;EditorApplication.Exit(1);}
    }
    [Serializable]class Result{public bool passed;public int assertions,runtimeErrors;}
    static T Get<T>(object o,string n){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)return(T)f.GetValue(o);}throw new Exception(n);}
    static void Set(object o,string n,object v){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null){f.SetValue(o,v);return;}}throw new Exception(n);}
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,a);
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static void Require(bool b,string m){assertions++;if(!b)throw new Exception(m);}
    static IEnumerator Esc()
    {
        var keyboard=InputSystem.AddDevice<Keyboard>();
        try
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape));InputSystem.Update();
            Require(keyboard.escapeKey.wasPressedThisFrame,"Virtual Escape reaches game input");
            Call(Find<StageExitConfirmation>(),"Update");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
        }
        finally {InputSystem.RemoveDevice(keyboard);}
        yield return null;yield return null;
    }
    static void RequireTopRaycast(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        Require(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),"Pause button receives top raycast: "+button.name+" hits="+string.Join(",",hits.Select(h=>h.gameObject.name))+" point="+pointer.position+" screen="+Screen.width+"x"+Screen.height);
    }
    static IEnumerator Check(){
        InputSystem.settings=UnityEngine.Object.Instantiate(InputSystem.settings);
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var turn=Find<TurnController>();var actions=Find<YutActionController>();var exit=Find<StageExitConfirmation>();
        var catalog=Find<ShopCatalogUI>();var wallet=Find<StageCurrency>();var purchases=Find<ShopPurchaseController>();
        actions.DeployAlly(YutThrowResult.Do);
        Capture("ThrowReady");yield return null;yield return null;
        string[] results={"Do","Gae","Geol","Yut","Mo","Backdo"};
        var summon=Get<Button>(turn,"summonButton");var move=Get<Button>(turn,"moveButton");
        var name=summon.image;var step=move.image;
        Vector2 nameSize=name.rectTransform.sizeDelta,stepSize=step.rectTransform.sizeDelta;
        for(int i=0;i<6;i++){
            Call(Find<YutThrowController>(),"ShowResult",(YutThrowResult)i);
            Call(turn,"ReceiveYutResult",(YutThrowResult)i);yield return null;yield return null;
            Require(name.sprite.name==results[i]+"_Summon_Normal","Whole summon card for result "+i+": "+name.sprite.name);
            Require(step.sprite.name==results[i]+"_Move_Normal","Whole movement card for result "+i+": "+step.sprite.name);
            Require(name.enabled && step.enabled && name.color.a>.99f && step.color.a>.99f,"Both whole cards are visibly enabled");
            Require(summon.spriteState.pressedSprite.name==results[i]+"_Summon_Pressed" && move.spriteState.pressedSprite.name==results[i]+"_Move_Pressed","Matching pressed card pair");
            Require(summon.spriteState.pressedSprite.rect.size==name.sprite.rect.size && move.spriteState.pressedSprite.rect.size==step.sprite.rect.size,"Normal/pressed crops have identical dimensions");
            Require(name.rectTransform.sizeDelta==nameSize&&step.rectTransform.sizeDelta==stepSize,"Button geometry preserved");
            if(i==5)Require(!Get<Button>(turn,"summonButton").interactable,"Backdo never permits summon");
            Capture("ActionResult"+i);
            if(i==1)
            {
                var press=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
                summon.OnPointerDown(press);
                Require(summon.image.overrideSprite==summon.spriteState.pressedSprite,"Actual press uses approved pressed card");
                Capture("ActionPressed");summon.OnPointerUp(press);
            }
        }
        typeof(TurnController).GetProperty("CurrentYutResult").SetValue(turn,null);Call(turn,"RefreshYutButtons");
        wallet.Grant(1000);turn.OpenShop();yield return null;
        var rows=Get<Button[]>(catalog,"itemButtons");var rects=rows.Select(b=>(RectTransform)b.transform).ToArray();
        for(int i=1;i<5;i++)Require(Mathf.Abs(rects[i-1].anchoredPosition.y-rects[i].anchoredPosition.y-160)<.1f,"Equal shop row spacing");
        Require(Mathf.Abs(rects[0].anchoredPosition.y+rects[4].anchoredPosition.y)<.1f,"Whole list vertically centered");
        catalog.SelectItem(0);catalog.BuySelected();yield return null;
        var stamps=Get<Image[]>(catalog,"purchasedStamps");Require(stamps.Length==5&&stamps[0].enabled,"Purchased stamp visible");
        Require(!stamps[0].raycastTarget&&stamps[0].sprite.name.StartsWith("Shop_Purchased_Stamp"),"Original stamp and click-through");
        Require(!Get<Button>(catalog,"buyButton").interactable,"Purchased item cannot buy again");
        int balance=wallet.Balance;catalog.BuySelected();Require(wallet.Balance==balance,"Repeated purchase no debit");Capture("ShopPurchasedStamp");
        turn.CloseShop();turn.OpenShop();yield return null;Require(stamps[0].enabled,"Stamp survives reopening");
        // Press actual Escape while shop is open: preserve its state underneath the pause modal.
        var open=Esc();while(open.MoveNext())yield return open.Current;
        var pause=Get<GameObject>(exit,"pauseMenu");
        Require(pause.activeSelf&&exit.IsOpen&&Time.timeScale==0,"Escape opens and pauses");
        Require(catalog.gameObject.activeSelf,"Shop state preserved behind pause");
        foreach(var button in new[]{Get<Button>(exit,"resumeButton"),Get<Button>(exit,"homeButton"),Get<Button>(exit,"quitButton")})RequireTopRaycast(button);
        Capture("PauseMenu");yield return null;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        Get<Button>(exit,"homeButton").OnPointerClick(pointer);Require(Get<GameObject>(exit,"modal").activeSelf&&!pause.activeSelf,"Home button requests confirmation");
        exit.Cancel();Require(pause.activeSelf&&Time.timeScale==0,"Cancel returns to paused menu");
        Get<Button>(exit,"quitButton").OnPointerClick(pointer);Require(Get<GameObject>(exit,"modal").activeSelf&&Get<Text>(exit,"confirmLabel").text=="종료","Quit button requests confirmation");exit.Cancel();
        var close=Esc();while(close.MoveNext())yield return close.Current;
        Require(!pause.activeSelf&&!exit.IsOpen&&Time.timeScale==1&&catalog.gameObject.activeSelf,"Escape resumes exact panel state");
        turn.CloseShop();
        var manager=Find<WaveManager>();var wave=Find<Wave>();
        var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=10}});
        Get<WaveData[]>(manager,"waves")[0]=data;turn.StartBattle();yield return null;
        open=Esc();while(open.MoveNext())yield return open.Current;
        Require(pause.activeSelf&&manager.IsRunning&&Time.timeScale==0,"Escape pauses actual combat");
        Get<Button>(exit,"resumeButton").OnPointerClick(pointer);Require(Time.timeScale==1&&manager.IsRunning,"Continue button resumes combat");
        foreach(var e in Get<System.Collections.Generic.HashSet<Enemy>>(wave,"spawnedEnemies").ToArray()){e.TakeDamage(int.MaxValue);e.CompleteDeathAnimation();}
        turn.OpenShop();yield return null;Require(purchases.IsPurchased(catalog.Items[0].Id)&&stamps[0].enabled,"Purchase stamp survives next wave");
        turn.CloseShop();Time.timeScale=2;Call(exit,"ToggleMenu");Call(exit,"Resume");Require(Time.timeScale==2,"Prior game speed restored");Time.timeScale=1;
    }
    static void Capture(string name)
    {
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.transform.parent!=null).ToArray();
        var sorting=canvases.Select(c=>c.overrideSorting).ToArray();
        try
        {
            // The screenshot helper temporarily raises the root canvas; retain hierarchy
            // layering for nested canvases in that offscreen rendering only.
            foreach(var c in canvases)c.overrideSorting=false;
            typeof(ShopPurchaseVerification).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{name});
        }
        finally {for(int i=0;i<canvases.Length;i++)canvases[i].overrideSorting=sorting[i];}
    }
}

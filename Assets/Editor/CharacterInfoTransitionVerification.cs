using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CharacterInfoTransitionVerification
{
    const string Key = "Yut.CharacterInfoTransitionVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static CharacterInfoTransitionVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        foreach (string path in new[] { "Assets/Scripts", "Assets/Prefabs", "Assets/Art/Ally/Idle/WhiteTiger_left.png", "Assets/Scenes/ThrowYut.unity" })
            if (AssetDatabase.IsValidFolder(path) || File.Exists(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        // Let scene startup and editor indexing settle before measuring game checks.
        if (readyFrames++ < 20) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Character info transition timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Sequential outgoing/incoming fades, no overlap, latest-target wins, rapid reverse/reselection, original color/layout restoration, gameplay preservation, pause/pool/disable/battle interruption and real rendered frames passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CharacterInfoTransitionVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static Ally Register(YutActionController a,string name,int cell)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/"+name+".prefab");
        var unit=Find<AllyPool>().Request(prefab,Get<Transform[]>(a,"cells")[cell]);
        Require((bool)Call(a,"RegisterAlly",cell,unit),"Fixture registered");return unit;
    }
    static void Click(TurnController t,Ally ally)=>Call(t,"HandleBoardClick",(Vector2)ally.VisualRenderer.bounds.center);
    static void Empty(TurnController t,YutActionController a)=>Call(t,"HandleBoardClick",(Vector2)Get<Transform[]>(a,"cells")[0].position);
    static int frameChecks;
    static void NoOverlap(CharacterInfoPanel panel)
    {
        float info=Get<CanvasGroup>(panel,"infoContent").alpha;
        float defaults=Get<CanvasGroup[]>(panel,"defaultContent").Max(g=>g.alpha);
        frameChecks++;
        if(info<0 || info>1 || defaults<0 || defaults>1 || (info>.0001f && defaults>.0001f))throw new Exception("Content overlaps or opacity outside0..1");
    }
    static IEnumerator Settle(CharacterInfoPanel panel)
    {
        double end=EditorApplication.timeSinceStartup+2;
        while(panel.IsTransitioning)
        {
            NoOverlap(panel);if(EditorApplication.timeSinceStartup>end)throw new Exception("Transition timeout");yield return null;
        }
        NoOverlap(panel);
    }
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var t=Find<TurnController>();var a=Find<YutActionController>();var wallet=Find<StageCurrency>();
        var panel=UnityEngine.Object.FindFirstObjectByType<CharacterInfoPanel>(FindObjectsInactive.Include);
        var groups=Get<CanvasGroup[]>(panel,"defaultContent");var world=Get<SpriteRenderer[]>(panel,"defaultWorldContent");var info=Get<CanvasGroup>(panel,"infoContent");
        Require(info!=null && Mathf.Abs(Get<float>(panel,"transitionDuration")-.24f)<.001f,"Configured info group and0.24-second transition");
        var background=GameObject.Find("Background").GetComponent<SpriteRenderer>();var bgSprite=background.sprite;Vector3 bgPosition=background.transform.position,bgScale=background.transform.localScale;
        var rect=panel.GetComponent<RectTransform>();Vector2 panelPosition=rect.anchoredPosition;Vector3 panelScale=rect.localScale;
        var one=Register(a,"Gae_Warrior",1);var two=Register(a,"WhiteTiger",2);var three=Register(a,"Mo_Archer_2nd_Evolution",3);
        wallet.GrantSpecial(45);Call(t,"ReceiveYutResult",YutThrowResult.Do);var result=t.CurrentYutResult;
        world[0].color=new Color(.8f,.7f,.6f,.65f);Color original=world[0].color;
        Click(t,one);
        Require(panel.IsTransitioning && panel.CurrentAlly==one && info.alpha==0 && groups.Any(g=>g.alpha>0),"Entry starts with old content visible and info hidden");
        bool defaultFade=false,infoFade=false;double end=EditorApplication.timeSinceStartup+2;
        while(panel.IsTransitioning)
        {
            NoOverlap(panel);float alpha=groups.Max(g=>g.alpha);
            if(alpha>0 && alpha<1)defaultFade=true;
            if(info.alpha>0 && info.alpha<1 && !infoFade){infoFade=true;Capture("InfoTransitionEntering");}
            Require(groups.All(g=>!g.interactable&&!g.blocksRaycasts),"Outgoing controls cannot receive clicks");
            if(EditorApplication.timeSinceStartup>end)throw new Exception("Entry timeout");yield return null;
        }
        Require(defaultFade&&infoFade && info.alpha==1 && groups.All(g=>g.alpha==0),"Both entry phases visibly animate and finish");Capture("InfoTransitionSettled");
        Click(t,two);
        Require(Get<Ally>(panel,"displayedAlly")==one && panel.CurrentAlly==two && panel.IsTransitioning,"New selection fades old data before replacement");
        yield return null;Click(t,three);var settle=Settle(panel);while(settle.MoveNext())yield return settle.Current;
        Require(panel.CurrentAlly==three && Get<Ally>(panel,"displayedAlly")==three && Get<Text>(panel,"attackValue").text==three.AttackDamage.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture),"Latest rapid target wins at settle");
        Click(t,two);yield return null;Empty(t,a);
        Require(panel.CurrentAlly==null && panel.IsTransitioning,"Empty click reverses in-flight swap");
        settle=Settle(panel);while(settle.MoveNext())yield return settle.Current;
        Require(!panel.gameObject.activeSelf && groups.All(g=>g.alpha==1&&g.interactable&&g.blocksRaycasts) && world[0].color==original,"Exit restores UI and original world sprite color");
        Click(t,one);yield return null;Empty(t,a);yield return null;Click(t,two);
        settle=Settle(panel);while(settle.MoveNext())yield return settle.Current;
        Require(panel.CurrentAlly==two && Get<Ally>(panel,"displayedAlly")==two && info.alpha==1,"Show-hide-show interruption ends at newest info");
        for(int i=0;i<12;i++)
        {
            Click(t,i%2==0?one:three);yield return null;NoOverlap(panel);
            if(i%3==0){Empty(t,a);yield return null;NoOverlap(panel);}
        }
        Click(t,two);settle=Settle(panel);while(settle.MoveNext())yield return settle.Current;
        Require(panel.CurrentAlly==two && Get<Ally>(panel,"displayedAlly")==two && !panel.IsTransitioning,"Repeated interruption leaves no stale request");
        Empty(t,a);bool exitFade=false,defaultReturn=false;end=EditorApplication.timeSinceStartup+2;
        while(panel.IsTransitioning)
        {
            NoOverlap(panel);
            if(info.alpha>0&&info.alpha<1&&!exitFade){exitFade=true;Capture("InfoTransitionLeaving");}
            if(groups.Any(g=>g.alpha>0&&g.alpha<1))defaultReturn=true;
            if(EditorApplication.timeSinceStartup>end)throw new Exception("Exit timeout");yield return null;
        }
        Require(exitFade&&defaultReturn && !panel.gameObject.activeSelf,"Both exit phases animate and end at default");Capture("InfoTransitionReturned");
        Require(background.sprite==bgSprite && background.transform.position==bgPosition && background.transform.localScale==bgScale && rect.anchoredPosition==panelPosition && rect.localScale==panelScale,"Background/frame/layout never move or scale");
        Require(wallet.Balance==120 && wallet.SpecialBalance==45 && t.CurrentYutResult==result && one.transform.parent==Get<Transform[]>(a,"cells")[1] && two.transform.parent==Get<Transform[]>(a,"cells")[2],"Animation preserves balances, prepaid result and board positions");
        Click(t,one);Time.timeScale=0;yield return null;yield return null;
        Require(!panel.gameObject.activeSelf && groups.All(g=>g.alpha==1) && world[0].color==original,"Pause cancels transition and restores content immediately");Time.timeScale=1;
        Click(t,two);Find<AllyPool>().Return(two);yield return null;yield return null;
        Require(!panel.gameObject.activeSelf && !panel.IsTransitioning,"Pooled selected target cannot leave a ghost panel");
        Click(t,three);yield return null;panel.gameObject.SetActive(false);
        Require(!panel.IsTransitioning && groups.All(g=>g.alpha==1)&&world[0].color==original,"Forced disable restores content even mid-transition");
        Click(t,one);settle=Settle(panel);while(settle.MoveNext())yield return settle.Current;
        Require(info.alpha==1 && panel.CurrentAlly==one,"Re-enable captures a clean baseline after interruption");
        // Resolve the prepaid result before exercising the actual battle transition.
        t.ChooseMove();t.SelectAlly(one);t.SelectDestination(2); // Cell02 contains the returned tiger occupancy, so use a separate legal result next if needed.
        if(t.CurrentYutResult.HasValue){t.CancelYutAction();t.ChooseSummon();}
        Require(!t.CurrentYutResult.HasValue,"Fixture resolves prepaid action through existing gameplay");
        Click(t,one);t.StartBattle();yield return null;
        Require(Find<WaveManager>().IsRunning && !panel.gameObject.activeSelf && groups.All(g=>g.alpha==1),"Battle cancels transition without leaving alpha or input locks");
        Require(frameChecks>10,"Multiple transition frames inspected without overlaps");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/CharacterInfoTransition");
        var camera = Camera.main; var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float plane = canvas.planeDistance;
        bool sorting = canvas.overrideSorting; int order = canvas.sortingOrder;
        var target = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(1920, 1080, 24); var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 1; canvas.overrideSorting = true; canvas.sortingOrder = 32767;
            for (int i = 0; i < 2; i++) { Canvas.ForceUpdateCanvases(); camera.Render(); }
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            File.WriteAllBytes("Verification/CharacterInfoTransition/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

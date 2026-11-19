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
public static class WaveCompleteRewardVerification
{
    const string Key = "Yut.WaveCompleteRewardVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static WaveCompleteRewardVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Wave complete reward verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Actual60/120 display, centered13-frame art, real-time unfold/hold/fade at1x/4x, preparation input, no duplicate payout, cap17 and terminal/next-battle lifetime guards passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; public Timing[] timings; public int[] frameWidths; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WaveCompleteRewardVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail, timings = timings.ToArray(), frameWidths = widths.ToArray() }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static void Funds(StageCurrency wallet,int amount){if(wallet.Balance>0)wallet.TrySpend(wallet.Balance);if(amount>0)wallet.Grant(amount);}
    static IEnumerator Reload()
    {
        Time.timeScale=1;EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ThrowYut.unity",new LoadSceneParameters(LoadSceneMode.Single));
        int frame=Time.frameCount;while(Time.frameCount<frame+2)yield return null;
        while(!Find<TurnController>().CanUsePreparationInput)yield return null;
    }
    [Serializable] sealed class Timing { public int speed,reward;public float opened,fadeStarted,hidden; }
    static readonly List<Timing> timings=new();static readonly List<int> widths=new();
    static WaveData Data(){var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});return data;}
    static void Frames()
    {
        int previous=0;
        for(int index=0;index<13;index++)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes("Assets/Art/WaveComplete/Frames/Scroll_"+index.ToString("D2")+".png"));
            var pixels=texture.GetPixels32();int min=texture.width,max=-1;
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>16){min=Math.Min(min,x);max=Math.Max(max,x);}
            int width=max-min+1;widths.Add(width);Require(texture.width==1024 && texture.height==256 && width>0 && width>=previous && Mathf.Abs((min+max)*.5f-511.5f)<=4,"Actual parchment frame expands symmetrically from fixed center");previous=width;UnityEngine.Object.DestroyImmediate(texture);
        }
        Require(widths[0]<widths[12]*.3f && widths[12]>900,"Opening grows from narrow center to long scroll");
    }
    static IEnumerator Check()
    {
        yield return null;
        // Simulate a terminal signal during preparation notification, before WaveCleared dispatch.
        var race=Find<TurnController>();var raceManager=Find<WaveManager>();Get<WaveData[]>(raceManager,"waves")[0]=Data();
        Action late=()=>Call(race,"LoseBattle");race.PreparationStarted+=late;race.StartBattle();var raceEnemy=Get<HashSet<Enemy>>(Find<Wave>(),"spawnedEnemies").Single();raceEnemy.TakeDamage(int.MaxValue);raceEnemy.CompleteDeathAnimation();race.PreparationStarted-=late;
        Require(race.IsBattleLost && Get<ResultFlagView>(Find<BattleStateHUD>(),"result").IsVisible && !Find<WaveCompleteBanner>().IsPlaying,"Late completion must not redisplay banner after terminal result");
        var reset=Reload();while(reset.MoveNext())yield return reset.Current;Frames();
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        foreach(int speed in new[]{1,4})
        {
            if(speed==4){var reload=Reload();while(reload.MoveNext())yield return reload.Current;}
            var turn=Find<TurnController>();var wallet=Find<StageCurrency>();var manager=Find<WaveManager>();var wave=Find<Wave>();var banner=Find<WaveCompleteBanner>();var actions=Find<YutActionController>();
            var frames=Get<Sprite[]>(banner,"frames");var artwork=Get<Image>(banner,"artwork");var labels=Get<CanvasGroup>(banner,"labels");var whole=Get<CanvasGroup>(banner,"whole");
            Require(!banner.IsPlaying && whole.alpha==0 && frames.Length==13 && frames.All(frame=>frame!=null),"Real scene starts hidden with13 linked frames");
            Require(Get<float>(banner,"unfoldSeconds")==.4f && Get<float>(banner,"holdSeconds")==1.5f && Get<float>(banner,"fadeSeconds")==.3f,"Saved phase settings .4/1.5/.3");
            var rect=(RectTransform)banner.transform;Require(rect.anchorMin==new Vector2(.35f,1) && rect.anchoredPosition==new Vector2(0,-394) && rect.sizeDelta==new Vector2(1024,256),"Approved board-top position and original frame ratio retained");
            if(speed==4)
            {
                var speedButton=Find<GameSpeedButton>().GetComponent<Button>();speedButton.onClick.Invoke();speedButton.onClick.Invoke();Funds(wallet,500);turn.OpenShop();var shop=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var purchases=Find<ShopPurchaseController>();
                Require(purchases.TryPurchase(shop.Items[1]) && purchases.TryPurchase(shop.Items[4]),"Actual talisman and mandate purchases");turn.CloseShop();Require(Find<StageItemUseController>().TryUse(ShopItemId.HeavenlyMandateCoin),"Actual mandate reserved");
            }
            Require(Time.timeScale==speed,"Requested speed is actual runtime speed");
            Get<WaveData[]>(manager,"waves")[0]=Data();Get<WaveData[]>(manager,"waves")[1]=Data();int before=wallet.Balance,announcements=0,announced=0;Action<int,int> completed=(number,reward)=>{announcements++;announced=reward;};turn.WaveCleared+=completed;
            turn.StartBattle();Require(manager.IsRunning && !banner.IsPlaying,"Battle start does not show completion scroll");var enemy=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();turn.WaveCleared-=completed;
            int expected=speed==1?60:120,kill=speed==1?2:4;double began=Time.unscaledTimeAsDouble;
            Require(announcements==1 && announced==expected && turn.CurrentTurnNumber==1 && turn.CanUsePreparationInput && !turn.IsBattleWon,"One real completion immediately enters next preparation");
            Require(wallet.Balance==before+kill+expected && banner.DisplayedWave==1 && banner.DisplayedReward==expected && banner.IsPlaying,"Banner uses completed wave and exact granted60/120");
            Require(Get<Text>(banner,"waveLabel").text=="1 방어 성공" && Get<Text>(banner,"rewardLabel").text=="+"+expected && Get<Image>(banner,"coin").sprite!=null && Get<Image>(banner,"waveWord").sprite!=null,"Correct success text,coin icon and paid amount");
            Require(labels.alpha==0 && artwork.sprite==frames[0] && whole.alpha==1,"Starts closed without stretched/early labels");
            Require(banner.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget) && banner.GetComponentsInChildren<CanvasGroup>(true).All(g=>!g.blocksRaycasts && !g.interactable) && banner.GetComponentsInChildren<Button>(true).Length==0,"All graphics/groups are nonblocking and have no confirmation button");
            int lastFrame=0;while(labels.alpha<1){int frame=Array.IndexOf(frames,artwork.sprite);Require(frame>=lastFrame,"Unfold frame order never reverses");lastFrame=frame;yield return null;}
            float opened=(float)(Time.unscaledTimeAsDouble-began);Require(opened>=.28f && opened<=.7f && artwork.sprite==frames[12],"Real-time unfold finishes near0.4s with full frame and labels");Capture(speed==1?"WaveComplete60":"WaveComplete120");
            int paid=wallet.Balance;Require(turn.CanThrowYut && Get<GameObject>(turn,"throwButton").GetComponent<Button>().interactable && !Get<ResultFlagView>(Find<BattleStateHUD>(),"result").IsVisible,"Full scroll leaves next preparation ready without result panel");
            Get<Button>(turn,"shopButton").OnPointerClick(pointer);Require(Get<GameObject>(turn,"shopPanel").activeSelf && banner.IsPlaying,"Shop input works while scroll plays");turn.CloseShop();turn.SetInventoryOpen(true);Require(Get<GameObject>(turn,"inventoryPanel").activeSelf,"Inventory input works while scroll plays");turn.SetInventoryOpen(false);
            if(speed==4)
            {
                for(int i=0;i<3;i++)Require(actions.DeployAlly(YutThrowResult.Do),"Actual summon allowed during scroll");var ally=UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsSortMode.None).First(a=>a is Do_Taoist);actions.RequestEvolution(ally);
                var evolved=UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsSortMode.None).Single(a=>a.EvolutionStage==2);actions.SelectAlly(evolved,YutThrowResult.Do);int target=Get<List<int>>(actions,"destinationCellIds")[0];Require(actions.MoveSelectedAlly(target),"Actual movement starts during scroll");while(actions.IsResolvingMove)yield return null;
                Require(actions.IsRegisteredAlly(evolved) && banner.IsPlaying,"Move completes without waiting for UI");
            }
            Get<GameObject>(turn,"throwButton").GetComponent<Button>().OnPointerClick(pointer);int cost=turn.CurrentYutThrowCost;Require(turn.IsThrowing && banner.IsPlaying && wallet.Balance==paid-cost,"Actual throw pays once while scroll remains visible");
            while(whole.alpha>=.999f && banner.IsPlaying)yield return null;float fadeStarted=(float)(Time.unscaledTimeAsDouble-began);Require(fadeStarted>=1.72f && fadeStarted<=2.16f,"Full content holds about1.5s after unfolding regardless1x/4x");
            while(banner.IsPlaying && whole.alpha>.6f)yield return null;if(banner.IsPlaying){Require(whole.alpha>0 && labels.alpha==1,"Fade reduces whole opacity without scaling labels");if(speed==1)Capture("WaveCompleteFade");}
            while(banner.IsPlaying)yield return null;float hidden=(float)(Time.unscaledTimeAsDouble-began);timings.Add(new Timing{speed=speed,reward=expected,opened=opened,fadeStarted=fadeStarted,hidden=hidden});
            Require(hidden>=2.0f && hidden<=2.7f && whole.alpha==0,"Unscaled unfold+hold+fade completes near2.2s");Require(wallet.Balance==paid-cost,"Animation phases never pay/refund additional currency");while(turn.IsThrowing)yield return null;Call(turn,"CompleteYutAction");
            banner.Play(1,expected);double preview=Time.unscaledTimeAsDouble;while(Time.unscaledTimeAsDouble-preview<.18)yield return null;if(speed==1)Capture("WaveCompleteUnfold");Require(wallet.Balance==paid-cost,"Presentation-only replay never grants reward");turn.StartBattle();Require(manager.IsRunning && !banner.IsPlaying,"Next actual battle immediately hides replaying scroll");manager.StopWave();
        }
        Require(Mathf.Abs(timings[0].hidden-timings[1].hidden)<.35f,"1x and4x UI lifetime match real-time timing");
        var capReset=Reload();while(capReset.MoveNext())yield return capReset.Current;var t=Find<TurnController>();var w=Find<StageCurrency>();var wm=Find<WaveManager>();var scroll=Find<WaveCompleteBanner>();Get<WaveData[]>(wm,"waves")[0]=Data();Funds(w,int.MaxValue-17);t.StartBattle();var leak=Get<HashSet<Enemy>>(Find<Wave>(),"spawnedEnemies").Single();Get<Action<Enemy>>(leak,"onArrived")(leak);
        Require(w.Balance==int.MaxValue && scroll.DisplayedReward==17 && Get<Text>(scroll,"rewardLabel").text=="+17" && scroll.IsPlaying,"Integer cap displays actual17 credited,not assumed60");
        int balance=w.Balance;scroll.Play(0,999);Require(w.Balance==balance && scroll.DisplayedReward==17,"Invalid wave cannot overwrite or pay");scroll.enabled=false;Require(!scroll.IsPlaying && Get<CanvasGroup>(scroll,"whole").alpha==0,"Disable hides immediately");scroll.enabled=true;
        Call(t,"LoseBattle");Call(Find<WaveCompleteHUD>(),"Show",1,60);Require(t.IsBattleLost && !scroll.IsPlaying,"Late direct presenter notification ignored after loss");
        var bossReset=Reload();while(bossReset.MoveNext())yield return bossReset.Current;var bossTurn=Find<TurnController>();var bossManager=Find<WaveManager>();Set(bossTurn,"currentTurnNumber",9);bossTurn.StartBattle();var boss=Get<HashSet<Enemy>>(Find<Wave>(),"spawnedEnemies").Single();boss.TakeDamage(int.MaxValue);Call(Find<WaveCompleteHUD>(),"Show",9,120);
        Require(bossTurn.IsBattleWon && !Find<WaveCompleteBanner>().IsPlaying,"Actual final victory has no completion scroll or late redisplay");
        var fresh=Reload();while(fresh.MoveNext())yield return fresh.Current;Require(!Find<WaveCompleteBanner>().IsPlaying && Find<StageCurrency>().Balance==120,"Fresh scene starts with hidden scroll and fresh wallet");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/WaveCompleteReward");
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
            File.WriteAllBytes("Verification/WaveCompleteReward/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class KillRewardVerification
{
    const string Key="Yut.KillRewardVerification";
    static IEnumerator checks;
    static int assertions,errors;
    public static void ReimportAndRun()
    {
        foreach(string path in new[]{"Assets/Scripts","Assets/Prefabs","Assets/Scenes/ThrowYut.unity"})
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ImportRecursive|ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        Run();
    }
    static KillRewardVerification(){EditorApplication.update+=Tick;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");SessionState.SetBool(Key,true);SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);EditorApplication.EnterPlaymode();}
    static void Tick(){
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>=90)throw new Exception("Timeout");
            if(checks==null){Application.logMessageReceived+=CountError;checks=Check();}if(checks.MoveNext())return;
            Require(errors==0,"No runtime errors during checks");Finish(true,"Normal2/fast2/boss100, immediate once-only credit, HUD/popup, escape, reuse, terminal guards and capped income passed.");
        }catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void CountError(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
    [Serializable] sealed class Result{public bool passed;public int assertions,runtimeErrors;public string unity,scene,detail;}
    static void Finish(bool passed,string detail)
    {
        Time.captureDeltaTime=0;Time.timeScale=1;Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/KillRewardVerification.json",JsonUtility.ToJson(new Result{passed=passed,assertions=assertions,runtimeErrors=errors,unity=Application.unityVersion,scene="ThrowYut (Play Mode)",detail=detail},true));
        SessionState.SetBool(Key,false);EditorApplication.Exit(passed?0:1);
    }
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(object o,string n,object v){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null){f.SetValue(o,v);return;}}throw new Exception(n);}
    static void Require(bool b,string m){assertions++;if(!b)throw new Exception($"Check {assertions}: {m}");}
    static Enemy[] Enemies()=>UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
    static IEnumerator Check(){
        yield return null;yield return null;Time.captureDeltaTime=.02f;
        var turn=UnityEngine.Object.FindFirstObjectByType<TurnController>();var manager=UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        var wallet=UnityEngine.Object.FindFirstObjectByType<StageCurrency>();
        var data=ScriptableObject.CreateInstance<WaveData>();
        Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=2,health=50},new WaveData.EnemyGroup{enemyId=EnemyId.FastEnemy,count=1,health=50},new WaveData.EnemyGroup{enemyId=EnemyId.BossEnemy,count=1,health=50}});
        Set(manager,"waves",new[]{data,data,data});Set(manager,"spawnInterval",0f);
        turn.StartBattle();while(manager.AliveEnemyCount<4)yield return null;
        var active=Enemies();var arrivals=active.ToDictionary(e=>e,e=>(Action<Enemy>)typeof(Enemy).GetField("onArrived",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(e));
        var cells=Get<Transform[]>(UnityEngine.Object.FindFirstObjectByType<YutActionController>(),"cells");int[] spots={2,7,12,17};
        for(int i=0;i<active.Length;i++){active[i].StopMoving();active[i].transform.position=cells[spots[i]].position;}
        var normal=Enemies().First(e=>e is NormalEnemy);normal.TakeDamage(50);
        Require(wallet.Balance==122,"Normal enemy must credit 2 exactly at death");
        normal.TakeDamage(50);Require(wallet.Balance==122,"Duplicate lethal call paid twice");
        var popup=GameObject.Find("Enemy Kill Rewards");Require(popup!=null,"Popup integration missing");
        int visible=popup.GetComponentsInChildren<CanvasGroup>().Count(g=>g.alpha>0);
        Require(visible==1,"Normal death must display one reward");
        var displayed=popup.GetComponentsInChildren<CanvasGroup>().Single(g=>g.alpha>0);
        Require(Mathf.Approximately(displayed.transform.localScale.x,1.6f),"Popup must be enlarged to 1.6x");
        Require(displayed.name=="Kill Reward +2","Popup must match actual income");
        normal.CompleteDeathAnimation();Require(wallet.Balance==122,"Death animation paid twice");
        Enemies().First(e=>e is FastEnemy).TakeDamage(50);Require(wallet.Balance==124,"Fast enemy must credit2");
        var boss=Enemies().First(e=>e is BossEnemy);boss.TakeDamage(50);Require(wallet.Balance==224,"Boss pays100 immediately before death animation completes");
        Require(popup.GetComponentsInChildren<CanvasGroup>().Count(g=>g.name=="Kill Reward +2")==2&&popup.GetComponentsInChildren<CanvasGroup>().Any(g=>g.name=="Kill Reward +100"),"Actual credited amounts shown");
        var hud=UnityEngine.Object.FindFirstObjectByType<CurrencyHUD>();
        Require(Get<int>(Get<GoldNumberDisplay>(hud,"display"),"value")==224,"HUD reflects all three kill incomes");
        Require(popup.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g=>!g.raycastTarget),"Popups never intercept clicks");
        Require(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null,"Real graphics capture");
        Directory.CreateDirectory("Verification/SpecialSummon");
        int captureFrame=Time.frameCount;while(Time.frameCount<captureFrame+2)yield return null;
        typeof(SpecialSummonVerification).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"kill-rewards-task21"});
        // Remaining normal escapes; moving it rapidly must not produce kill income.
        var escaped=Enemies().First(e=>e is NormalEnemy && e.CurrentHealth>0);Set(escaped,"moveSpeed",10000f);Set(escaped,"lapDurationSeconds",0f);
        var first=new GameObject("Fixture entrance").transform;var last=new GameObject("Fixture exit").transform;first.position=escaped.transform.position;last.position=first.position+Vector3.up*.001f;
        escaped.StartMoving(new[]{first,last},arrivals[escaped]);
        foreach(var enemy in Enemies().Where(e=>e.CurrentHealth<=0))enemy.CompleteDeathAnimation();
        while(manager.IsRunning)yield return null;
        Set(escaped,"moveSpeed",2f);
        Require(wallet.Balance==284 && turn.RemainingLives==9,"Escape gives no kill income; existing completion60 stays separate");
        float start=Time.time;while(Time.time-start<1)yield return null;
        Require(popup.GetComponentsInChildren<CanvasGroup>().All(g=>g.alpha==0),"Floating rewards did not expire");
        turn.StartBattle();while(manager.AliveEnemyCount<4)yield return null;
        normal=Enemies().First(e=>e is NormalEnemy && e.CurrentHealth>0);normal.TakeDamage(50);
        Require(wallet.Balance==286,"Reused enemy rewards exactly once");
        var rewards=UnityEngine.Object.FindFirstObjectByType<EnemyKillRewards>();
        // Cap: credited amount and popup must both use the actual increment.
        wallet.Grant(int.MaxValue-wallet.Balance-1);
        var fast=Enemies().First(e=>e is FastEnemy);fast.TakeDamage(50);
        Require(wallet.Balance==int.MaxValue&&popup.GetComponentsInChildren<CanvasGroup>().Any(g=>g.name=="Kill Reward +1"),"At integer cap,only actual+1 is displayed");
        wallet.TrySpend(int.MaxValue-286);
        typeof(TurnController).GetProperty("IsBattleWon").SetValue(turn,true);
        var nextBoss=Enemies().First(e=>e is BossEnemy);nextBoss.TakeDamage(50);
        Require(wallet.Balance==286,"No income after victory");typeof(TurnController).GetProperty("IsBattleWon").SetValue(turn,false);
        typeof(TurnController).GetProperty("RemainingLives").SetValue(turn,0);
        var remaining=Enemies().First(e=>e.CurrentHealth>0);remaining.TakeDamage(50);
        Require(wallet.Balance==286,"No income after loss");
        manager.StopWave();
        typeof(EnemyKillRewards).GetMethod("OnKill",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rewards,new object[]{remaining});
        Require(wallet.Balance==286,"Stale stopped-wave notification gives no income");
        typeof(TurnController).GetProperty("RemainingLives").SetValue(turn,10);
        manager.StartWave(0);while(manager.AliveEnemyCount<4)yield return null;
        int count=Get<System.Collections.IList>(rewards,"popups").Count;
        Action<int> stopDuringCredit=amount=>manager.StopWave();wallet.Changed+=stopDuringCredit;
        Enemies().First(e=>e is NormalEnemy).TakeDamage(50);wallet.Changed-=stopDuringCredit;
        Require(wallet.Balance==288&&!manager.IsRunning,"Valid kill credited once before callback stops wave");
        Require(Get<System.Collections.IList>(rewards,"popups").Count==count,"No new popup after reentrant stop");
    }
}

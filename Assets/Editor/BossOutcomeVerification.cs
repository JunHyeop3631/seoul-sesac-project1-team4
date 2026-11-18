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

[InitializeOnLoad]
public static class BossOutcomeVerification
{
    const string Key="Yut.BossOutcomeVerification";
    static IEnumerator checks;static int assertions,errors;
    static BossOutcomeVerification(){EditorApplication.update+=Tick;}
    public static void ReimportAndRun()
    {
        foreach(string path in new[]{"Assets/Scripts","Assets/Prefabs","Assets/Scenes/ThrowYut.unity"})
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ImportRecursive|ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");SessionState.SetBool(Key,true);
        SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>120)throw new Exception("Boss outcome verification timed out");
            if(checks==null){Application.logMessageReceived+=CountError;checks=Check();}if(checks.MoveNext())return;
            Require(errors==0,"No runtime errors during checks");Finish(true,"Boss reward before synchronous win,10/1-life escape loss, stop/late-event protection,pending spawns,and no generic final-wave victory passed.");
        }
        catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void CountError(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
    [Serializable] sealed class Result{public bool passed;public int assertions,runtimeErrors;public string unity,scene,detail;}
    static void Finish(bool passed,string detail)
    {
        Time.captureDeltaTime=0;Time.timeScale=1;SessionState.SetBool(Key,false);Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BossOutcomeVerification.json",JsonUtility.ToJson(new Result{passed=passed,assertions=assertions,runtimeErrors=errors,unity=Application.unityVersion,scene="ThrowYut (Play Mode)",detail=detail},true));
        EditorApplication.Exit(passed?0:1);
    }
    static FieldInfo Field(object o,string name)
    {for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)return f;}throw new Exception(name);}
    static T Get<T>(object o,string name)=>(T)Field(o,name).GetValue(o);
    static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
    static void Call(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
    static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok,string message){assertions++;if(!ok)throw new Exception($"Check {assertions}: {message}");}
    static IEnumerator Reload()
    {
        Time.captureDeltaTime=0;Time.timeScale=1;
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ThrowYut.unity",new LoadSceneParameters(LoadSceneMode.Single));
        int frame=Time.frameCount;while(Time.frameCount<frame+2)yield return null;
        while(!Find<TurnController>().CanUsePreparationInput){Time.timeScale=1;yield return null;}
    }
    static IEnumerator Check()
    {
        yield return null;yield return null;
        for(int scenario=0;scenario<5;scenario++)
        {
            if(scenario>0){var reload=Reload();while(reload.MoveNext())yield return reload.Current;}
            var turn=Find<TurnController>();var manager=Find<WaveManager>();var wave=Find<Wave>();var wallet=Find<StageCurrency>();
            Set(turn,"currentTurnNumber",9);Time.captureDeltaTime=.01f;
            Require(Get<WaveData[]>(manager,"waves").Length==10,"Real ten-wave stage retained");
            if(scenario==2)typeof(TurnController).GetProperty("RemainingLives").SetValue(turn,1);
            if(scenario>=3)
            {
                var data=ScriptableObject.CreateInstance<WaveData>();
                Set(data,"enemies",scenario==3?new[]{new WaveData.EnemyGroup{enemyId=EnemyId.BossEnemy,count=1,health=6000},new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=3,health=125}}
                    :new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});
                Get<WaveData[]>(manager,"waves")[9]=data;Set(manager,"spawnInterval",.1f);
            }
            int wins=0,losses=0,completed=0,cleared=0,ordinaryArrivals=0;
            var order=new List<string>();
            wallet.Changed+=balance=>order.Add("credit");
            turn.BattleWon+=()=>{wins++;order.Add("win");Require(wallet.Balance==220,"Reward visible inside victory callback");Require(!wave.IsRunning,"Combat stopped before victory callback");};
            turn.BattleLost+=()=>{losses++;Require(!wave.IsRunning,"Combat stopped before loss callback");};
            manager.WaveCompleted+=()=>completed++;turn.WaveCleared+=(w,r)=>cleared++;manager.EnemyArrived+=()=>ordinaryArrivals++;
            turn.StartBattle();var boss=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();
            var staleArrival=Get<Action<Enemy>>(boss,"onArrived");boss.StopMoving();
            Require(wave.IsRunning&&!turn.IsBattleWon&&!turn.IsBattleLost,"Final battle begins without terminal result");
            if(scenario==1||scenario==2)
            {
                int lives=turn.RemainingLives;
                var a=new GameObject("Fixture entrance").transform;var b=new GameObject("Fixture exit").transform;
                a.position=boss.transform.position;b.position=a.position+Vector3.up*.001f;
                Set(boss,"lapDurationSeconds",0f);Set(boss,"moveSpeed",1000f);boss.StartMoving(new[]{a,b},staleArrival);
                while(!turn.IsBattleLost)yield return null;
                Require(losses==1&&wins==0&&turn.RemainingLives==lives,"Boss escape loses at10/1 lives without ordinary life decrement");
                Require(wallet.Balance==120&&completed==0&&cleared==0&&ordinaryArrivals==0,"Escape grants no kill/base reward or ordinary completion");
            }
            else if(scenario==4)
            {
                boss.TakeDamage(int.MaxValue);boss.CompleteDeathAnimation();
                Require(!turn.IsBattleWon&&!turn.IsBattleLost&&wins==0&&completed==1,"Generic final-wave completion cannot declare victory without boss");
                Require(wallet.Balance==122&&cleared==0,"Invalid final normal-enemy fixture gets only kill2,not base60");
                continue;
            }
            else
            {
                Require(boss is BossEnemy&&boss.CurrentHealth==6000,"Actual boss type and HP6000");
                var prefab=AssetDatabase.LoadAssetAtPath<HomingProjectile>("Assets/Prefabs/Effects/Projectile/Do_Taoist_Projectile.prefab");
                var shot=Find<ProjectilePool>().Request(prefab,Vector3.zero);shot.Launch(boss,1,100,null,wave);
                boss.TakeDamage(int.MaxValue);
                Require(wins==1&&losses==0&&turn.IsBattleWon&&!turn.IsBattleLost,"Lethal damage returns with victory before death-complete call");
                Require(order.SequenceEqual(new[]{"credit","win"}),"Synchronous credit precedes victory event");
                Require(wallet.Balance==220&&completed==0&&cleared==0,"Boss100 only,no base60/generic completion");
                Require(!boss.gameObject.activeSelf&&!shot.gameObject.activeSelf&&wave.AliveEnemyCount==0,"Boss and in-flight projectile cleaned immediately");
                Require(!Get<bool>(boss,"awaitingDeathAnimation"),"Victory never waits for death animation state");
            }
            var result=Get<ResultFlagView>(Find<BattleStateHUD>(),"result");
            Require(result.IsVisible&&result.ReachedWave==10&&result.TotalWaves==10,"Existing result UI receives terminal wave10 state");
            Require(!turn.CanUsePreparationInput&&!turn.CanThrowYut&&!manager.IsRunning,"Terminal state blocks preparation and reroll");
            int balanceAfter=wallet.Balance,livesAfter=turn.RemainingLives;
            boss.TakeDamage(int.MaxValue);boss.CompleteDeathAnimation();staleArrival(boss);Call(turn,"EndBattle");Call(turn,"LoseLife");manager.StopWave();turn.StartBattle();
            Require(wallet.Balance==balanceAfter&&turn.RemainingLives==livesAfter&&wins+losses==1&&completed==0,"Repeated damage/death/escape/completion cannot change terminal result");
            Time.timeScale=1;double until=Time.timeAsDouble+.5;while(Time.timeAsDouble<until)yield return null;
            Require(!manager.IsRunning&&Find<EnemyPool>().GetComponentsInChildren<Enemy>().Length==0&&wallet.Balance==balanceAfter,"No pending spawns or income after time resumes");
        }
    }
}

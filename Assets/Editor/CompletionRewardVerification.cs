using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CompletionRewardVerification
{
    const string Key="Yut.CompletionRewardVerification";
    static IEnumerator checks;static int assertions,errors;
    static readonly List<WaveResult> results=new();
    static CompletionRewardVerification(){EditorApplication.update+=Tick;}
    public static void ReimportAndRun()
    {
        foreach(string path in new[]{"Assets/Scripts","Assets/Prefabs","Assets/Scenes/ThrowYut.unity"})
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ImportRecursive|ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        for(int i=1;i<=10;i++)AssetDatabase.ImportAsset($"Assets/Wave{i}.asset",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");SessionState.SetBool(Key,true);
        SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>120)throw new Exception("Completion reward verification timed out");
            if(checks==null){Application.logMessageReceived+=CountError;checks=Check();}if(checks.MoveNext())return;
            Require(errors==0,"No runtime errors during checks");Finish(true,"All nine actual waves with one escape each:60 once/540 total, separate kill income, carryover, reward callback reentry and no wave10 base reward passed.");
        }
        catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void CountError(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
    [Serializable] sealed class WaveResult{public int wave,kills,escaped,completionReward,balance,lives;}
    [Serializable] sealed class Result{public bool passed;public int assertions,runtimeErrors;public string unity,scene,detail;public WaveResult[] waves;}
    static void Finish(bool passed,string detail)
    {
        Time.captureDeltaTime=0;Time.timeScale=1;SessionState.SetBool(Key,false);Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CompletionRewardVerification.json",JsonUtility.ToJson(new Result{passed=passed,assertions=assertions,runtimeErrors=errors,
            unity=Application.unityVersion,scene="ThrowYut (Play Mode)",detail=detail,waves=results.ToArray()},true));EditorApplication.Exit(passed?0:1);
    }
    static FieldInfo Field(object o,string name)
    {for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)return f;}throw new Exception(name);}
    static T Get<T>(object o,string name)=>(T)Field(o,name).GetValue(o);
    static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
    static void Call(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
    static void Require(bool ok,string message){assertions++;if(!ok)throw new Exception($"Check {assertions}: {message}");}
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var turn=UnityEngine.Object.FindFirstObjectByType<TurnController>();var manager=UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        var wave=UnityEngine.Object.FindFirstObjectByType<Wave>();var wallet=UnityEngine.Object.FindFirstObjectByType<StageCurrency>();
        var waves=Get<WaveData[]>(manager,"waves");
        Require(waves.Length==10&&waves.Take(9).All(w=>w.CompletionReward==60)&&waves[9].CompletionReward==0,"Actual1-9 configured60;wave10 configured0");
        Require(wallet.Balance==120&&turn.RemainingLives==10,"Fresh actual scene start");wallet.GrantSpecial(7);
        Time.captureDeltaTime=.01f;Set(manager,"spawnInterval",.01f);
        int completed=0,cleared=0,escaped=0,prepared=0,granted=0,killsTotal=0,won=0;
        manager.WaveCompleted+=()=>completed++;manager.EnemyArrived+=()=>escaped++;
        turn.PreparationStarted+=()=>prepared++;turn.BattleWon+=()=>won++;
        turn.WaveCleared+=(number,amount)=>{cleared++;granted+=amount;Require(number==cleared&&amount==60,"WaveCleared reports exact one-based wave/credited60");};
        Action<int> reenter=balance=>{Call(turn,"EndBattle");turn.StartBattle();};wallet.Changed+=reenter;
        for(int i=0;i<9;i++)
        {
            int before=wallet.Balance,escapesBefore=escaped,killed=0,total=waves[i].Enemies.Sum(g=>g.count);
            Require(turn.CurrentTurnNumber==i&&turn.CanUsePreparationInput,"Next wave starts in usable preparation with carried balance");
            turn.StartBattle();Require(manager.IsRunning&&wave.AliveEnemyCount==1,"Actual first spawn immediate");
            var leak=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();
            int version=Get<int>(leak,"<ActivationVersion>k__BackingField");
            float speed=Get<float>(leak,"moveSpeed"),lap=Get<float>(leak,"lapDurationSeconds");
            var arrival=Get<Action<Enemy>>(leak,"onArrived");
            var p0=new GameObject("Fixture entrance").transform;var p1=new GameObject("Fixture exit").transform;
            p0.position=leak.transform.position;p1.position=p0.position+Vector3.up*.001f;
            Set(leak,"lapDurationSeconds",0f);Set(leak,"moveSpeed",1000f);
            leak.StartMoving(new[]{p0,p1},enemy=>{arrival(enemy);Set(enemy,"lapDurationSeconds",lap);Set(enemy,"moveSpeed",speed);});
            while(manager.IsRunning)
            {
                foreach(var enemy in Get<HashSet<Enemy>>(wave,"spawnedEnemies").ToArray())
                {
                    if(enemy==leak&&Get<int>(enemy,"<ActivationVersion>k__BackingField")==version)continue;
                    enemy.StopMoving();killed++;enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();
                }
                yield return null;
            }
            killsTotal+=killed;
            Require(killed==total-1&&escaped==escapesBefore+1,"One real escape,all other configured enemies killed");
            Require(wallet.Balance==before+killed*2+60,"Kill2 income and completion60 remain separate");
            Require(turn.RemainingLives==9-i&&turn.CurrentTurnNumber==i+1&&turn.CanUsePreparationInput&&!turn.IsBattleWon,"Survives leak and advances once");
            Require(completed==i+1&&cleared==i+1&&prepared==i+1&&granted==(i+1)*60,"Completion/reward/preparation each exactly once despite reentry");
            Require(wallet.SpecialBalance==7,"Special balance also carries through preparation");
            int paid=wallet.Balance;Call(turn,"EndBattle");Call(wave,"TryCompleteWave");
            Require(wallet.Balance==paid&&cleared==i+1,"Repeated completion cannot pay again");
            results.Add(new WaveResult{wave=i+1,kills=killed,escaped=1,completionReward=60,balance=wallet.Balance,lives=turn.RemainingLives});
            UnityEngine.Object.Destroy(p0.gameObject);UnityEngine.Object.Destroy(p1.gameObject);
        }
        Require(granted==540&&killsTotal==193&&escaped==9&&wallet.Balance==1046,"Nine leaks:completion540+kill386+starting120");
        wallet.Changed-=reenter;
        turn.StartBattle();var boss=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();boss.StopMoving();
        Require(boss is BossEnemy&&boss.CurrentHealth==6000,"Actual final boss data retained");
        boss.TakeDamage(int.MaxValue);Require(wallet.Balance==1146,"Boss kill100 separate from base reward");boss.CompleteDeathAnimation();
        Require(won==1&&turn.IsBattleWon&&wallet.Balance==1146&&granted==540&&cleared==9,"No extra60 on wave10;boss-only win preserves accumulated reward");
    }
}

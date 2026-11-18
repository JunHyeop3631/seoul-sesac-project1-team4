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
public static class SpecialSummonExecutionVerification
{
    const string Key = "Yut.SpecialSummonExecutionVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static SpecialSummonExecutionVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Special summon verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Cost/population matrix, actual tiger registry/spawn, atomic payment/placement, reentry protection, invalid references, state guards, candidate selection, prepaid result, HUD sync, next-wave carry and scene restart passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/SpecialSummonExecutionVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object o,string method,params object[] args) => o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static Ally[] At(YutActionController a) => ((IReadOnlyList<Ally>)Call(Get<YutBoardOccupancy>(a,"occupancy"),"GetAllies",1)).ToArray();
    static int Pop(YutActionController a) => (int)Call(Get<YutBoardOccupancy>(a,"occupancy"),"GetPopulation",1);
    static void Clear(YutActionController a)
    {
        Call(a,"ClearSelection");var units=At(a);
        if(units.Length>0) { Require((bool)Call(Get<YutBoardOccupancy>(a,"occupancy"),"TryRemoveAllies",(object)units),"Fixture occupancy cleared");foreach(var unit in units)Find<AllyPool>().Return(unit); }
    }
    static void Balance(StageCurrency wallet,int amount)
    { if(wallet.SpecialBalance>0)wallet.TrySpendSpecial(wallet.SpecialBalance);if(amount>0)wallet.GrantSpecial(amount); }
    static void Reject(SpecialSummonController summon,YutActionController actions,StageCurrency wallet,string reason)
    {
        int balance=wallet.SpecialBalance,count=At(actions).Length;
        Require(!summon.CanSummon && !summon.TrySummon(),reason+" rejects");
        Require(wallet.SpecialBalance==balance && At(actions).Length==count && !actions.IsResolvingSummon,reason+" preserves balance/units/unlocked state");
    }
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var t=Find<TurnController>();var a=Find<YutActionController>();var w=Find<StageCurrency>();var summon=Find<SpecialSummonController>();
        var prefab=AssetDatabase.LoadAssetAtPath<WhiteTiger>("Assets/Prefabs/WhiteTiger.prefab");var pool=Find<AllyPool>();
        var normal=Get<Gae_Warrior>(a,"gaeWarriorPrefab");var ui=Find<SpecialSummonUI>();
        Require(summon!=null && Get<TurnController>(summon,"turnController")==t && Get<YutActionController>(summon,"actions")==a && Get<StageCurrency>(summon,"currency")==w,"Scene executor references");
        Require(Get<Ally[]>(summon,"candidates").SequenceEqual(new Ally[]{prefab}) && SpecialSummonController.Cost==30,"Actual tiger-only registry and cost30");
        for(int population=0;population<=6;population++)foreach(int funds in new[]{0,29,30,45,60})
        {
            Clear(a);Balance(w,funds);
            for(int i=0;i<population;i++)Require(a.DeployAlly(YutThrowResult.Gae),"Normal fixture deployment");
            int charges=0,successes=0;Ally created=null;bool expected=population<=3 && funds>=30;
            Action<int> changed=amount=>
            {
                charges++;
                Require(a.IsResolvingSummon && !t.CanUsePreparationInput && !summon.CanSummon,"Payment callback keeps processing lock");
                Require(amount==funds-30 && At(a).Length==population+1 && At(a).Count(u=>u is WhiteTiger)==1,"Payment callback observes both debit and placement");
                Require(!summon.TrySummon() && !a.DeployAlly(YutThrowResult.Do),"Reentrant special/general summon rejected");
                t.ThrowYut();t.StartBattle();
                Require(!t.IsThrowing && !Find<WaveManager>().IsRunning,"Reentrant throw/battle rejected");
            };
            Action<Ally> completed=unit=>
            {
                successes++;created=unit;
                Require(a.IsResolvingSummon && !t.CanEvolve && !t.CanCancelYutAction,"Success callback retains input lock");
                Require(!summon.TrySummon() && w.SpecialBalance==funds-30,"Success callback cannot summon twice");
            };
            w.SpecialChanged+=changed;summon.Summoned+=completed;
            Require(summon.CanSummon==expected,"Boundary availability "+population+"/"+funds);
            Require(summon.TrySummon()==expected,"Boundary transaction result");
            w.SpecialChanged-=changed;summon.Summoned-=completed;
            Require(w.SpecialBalance==funds-(expected?30:0) && w.Balance==120,"Exact special debit, ordinary balance preserved");
            Require(charges==(expected?1:0) && successes==(expected?1:0) && At(a).Length==population+(expected?1:0),"One notification/unit on success; none on failure");
            Require(Pop(a)==population+(expected?3:0) && !a.IsResolvingSummon && t.CanUsePreparationInput,"Weighted population and lock restoration");
            if(expected)Require(created is WhiteTiger && created.transform.parent==Get<Transform[]>(a,"cells")[1],"Actual tiger spawned at Cell01");
        }
        Clear(a);Balance(w,45);
        Require(summon.CanSummon,"Availability before external balance change");w.TrySpendSpecial(16);Reject(summon,a,w,"Balance changed from45 to29 before click");
        Balance(w,45);Require(summon.CanSummon,"Availability before external population change");
        for(int i=0;i<4;i++)a.DeployAlly(YutThrowResult.Gae);Reject(summon,a,w,"Population changed to4 before click");Clear(a);
        Set(summon,"candidates",Array.Empty<Ally>());Reject(summon,a,w,"Empty registry");
        Set(summon,"candidates",new Ally[]{null});Reject(summon,a,w,"Missing candidate");
        Set(summon,"candidates",new Ally[]{normal});Reject(summon,a,w,"Population-one candidate");Set(summon,"candidates",new Ally[]{prefab});
        var cells=Get<Transform[]>(a,"cells");var start=cells[1];cells[1]=null;Reject(summon,a,w,"Missing Cell01");cells[1]=start;
        Set(a,"allyPool",null);Reject(summon,a,w,"Missing pool");Set(a,"allyPool",pool);
        pool.enabled=false;Reject(summon,a,w,"Disabled pool");pool.enabled=true;
        w.enabled=false;Reject(summon,a,w,"Disabled wallet");w.enabled=true;
        a.enabled=false;Reject(summon,a,w,"Disabled board controller");a.enabled=true;
        summon.enabled=false;Reject(summon,a,w,"Disabled executor");summon.enabled=true;
        Time.timeScale=0;Reject(summon,a,w,"Pause");Time.timeScale=1;
        var throwing=Find<YutThrowController>();typeof(YutThrowController).GetProperty("IsThrowing").SetValue(throwing,true);
        Reject(summon,a,w,"Throw in progress");typeof(YutThrowController).GetProperty("IsThrowing").SetValue(throwing,false);
        foreach(string property in new[]{"IsBattleWon","IsBattleLost"})
        { typeof(TurnController).GetProperty(property).SetValue(t,true);Reject(summon,a,w,property);typeof(TurnController).GetProperty(property).SetValue(t,false); }
        typeof(TurnController).GetProperty("RemainingLives").SetValue(t,0);Reject(summon,a,w,"Zero hearts");typeof(TurnController).GetProperty("RemainingLives").SetValue(t,10);
        Set(t,"preparing",false);Reject(summon,a,w,"Outside preparation");Set(t,"preparing",true);
        int notifications=0;Action<int> count=amount=>notifications++;w.SpecialChanged+=count;
        var method=typeof(StageCurrency).GetMethod("TrySpendSpecial",BindingFlags.Instance|BindingFlags.NonPublic);
        Require(!(bool)method.Invoke(w,new object[]{30,(Func<bool>)(()=>false)}) && w.SpecialBalance==45 && notifications==0,"Failed placement commit restores balance without notification");
        try { method.Invoke(w,new object[]{30,(Func<bool>)(()=>throw new InvalidOperationException("fixture"))});throw new Exception("Expected fixture exception"); }
        catch(TargetInvocationException e){Require(e.InnerException is InvalidOperationException && w.SpecialBalance==45 && notifications==0,"Throwing placement commit restores balance");}
        w.SpecialChanged-=count;
        // An in-memory second candidate proves registry selection without adding game content.
        var second=UnityEngine.Object.Instantiate(prefab);second.name="RegistryFixture";Set(summon,"candidates",new Ally[]{prefab,second});
        var randomState=UnityEngine.Random.state;UnityEngine.Random.InitState(17);var selected=new HashSet<string>();
        Action<Ally> record=unit=>selected.Add(unit.name);summon.Summoned+=record;
        for(int i=0;i<12;i++){Clear(a);Balance(w,30);Require(summon.TrySummon(),"Multi-candidate registry transaction");}
        Require(selected.Count==2,"Both registered candidates can be selected");
        summon.Summoned-=record;UnityEngine.Random.state=randomState;Set(summon,"candidates",new Ally[]{prefab});UnityEngine.Object.Destroy(second);Clear(a);
        Balance(w,45);Require(a.DeployAlly(YutThrowResult.Gae),"Pending-result fixture");
        Call(t,"ReceiveYutResult",YutThrowResult.Do);t.ChooseMove();var chosen=At(a).Single();t.SelectAlly(chosen);
        Require(summon.TrySummon() && t.CurrentYutResult==YutThrowResult.Do && At(a).Contains(chosen) && w.SpecialBalance==15,"Special summon preserves prepaid result and existing unit");
        Require(Get<SpecialCurrencyDigits>(ui,"hudDigits").Value==15 && Get<SpecialCurrencyDigits>(ui,"ownedDigits").Value==15,"Existing HUD and window numbers reflect45 to15");
        t.CancelYutAction();t.ChooseMove();t.SelectAlly(chosen);t.SelectDestination(2);
        Require(!t.CurrentYutResult.HasValue && At(a).Length==1,"Prepaid result still usable after special summon");
        var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});
        var manager=Find<WaveManager>();Get<WaveData[]>(manager,"waves")[0]=data;
        t.StartBattle();Reject(summon,a,w,"Active battle");
        var wave=Find<Wave>();var enemy=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();
        Require(t.CurrentTurnNumber==1 && t.CanUsePreparationInput && w.SpecialBalance==15 && At(a).Single() is WhiteTiger,"Next preparation preserves special balance and tiger");
        Reject(summon,a,w,"Next preparation still lacks cost30");
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ThrowYut.unity",new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;yield return null;
        Require(Find<StageCurrency>().SpecialBalance==0 && Find<StageCurrency>().Balance==120 && At(Find<YutActionController>()).Length==0,"New scene resets wallet and summoned units");
    }
}

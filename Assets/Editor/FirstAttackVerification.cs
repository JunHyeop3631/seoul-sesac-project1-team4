using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Task17 is an audit: measure current behavior without changing attack rules.
[InitializeOnLoad]
public static class FirstAttackVerification
{
    const string Key="Yut.FirstAttackVerification";
    static IEnumerator checks;
    static int assertions,errors;
    static readonly List<Observation> observations=new();
    static FirstAttackVerification(){EditorApplication.update+=Tick;}
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
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>120)throw new Exception("First attack audit timed out");
            if(checks==null){Application.logMessageReceived+=CountError;checks=Check();}
            if(checks.MoveNext())return;
            Require(errors==0,"No runtime errors during checks");
            Finish(true,"Observation checks passed: fresh/long-preparation attacks ready; short-preparation attacks preserve prior cooldown. GAP-028 remains unresolved.");
        }
        catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void CountError(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
    [Serializable] sealed class Observation
    {
        public string prefab,transition;public double freshDelay,remainingCooldown,nextWaveDelay,longPreparationDelay;
        public bool readyAtNextBattle;
    }
    [Serializable] sealed class Result
    {
        public bool checksPassed,requirementSatisfied;public int assertions,runtimeErrors;
        public string unity,scene,detail;public Observation[] observations;
    }
    static void Finish(bool passed,string detail)
    {
        Time.captureDeltaTime=0;Time.timeScale=1;SessionState.SetBool(Key,false);Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/FirstAttackVerification.json",JsonUtility.ToJson(new Result{checksPassed=passed,requirementSatisfied=false,
            assertions=assertions,runtimeErrors=errors,unity=Application.unityVersion,scene="ThrowYut (Play Mode)",detail=detail,observations=observations.ToArray()},true));
        EditorApplication.Exit(passed?0:1);
    }
    static FieldInfo Field(object o,string name)
    {for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)return f;}throw new Exception(name);}
    static T Get<T>(object o,string name)=>(T)Field(o,name).GetValue(o);
    static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
    static void AllyCall(Ally ally,string method,params object[] args)=>typeof(Ally).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ally,args);
    static void Require(bool ok,string message){assertions++;if(!ok)throw new Exception($"Check {assertions}: {message}");}
    static Enemy Target(Wave wave)=>Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var turn=UnityEngine.Object.FindFirstObjectByType<TurnController>();var manager=UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        var wave=UnityEngine.Object.FindFirstObjectByType<Wave>();var pool=UnityEngine.Object.FindFirstObjectByType<AllyPool>();
        var projectiles=UnityEngine.Object.FindFirstObjectByType<ProjectilePool>();
        var testData=ScriptableObject.CreateInstance<WaveData>();
        Set(testData,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=1000000}});
        // Real TurnController progression through enough ordinary waves; no final-win pause.
        Set(manager,"waves",Enumerable.Repeat(testData,32).ToArray());
        Time.captureDeltaTime=.01f;Time.timeScale=1;
        string[] names={"Do_Taoist","Gae_Warrior","Geol_Spearman","Yut_Maceman","Mo_Archer"};
        Vector3 origin=Get<Transform[]>(wave,"waypoints")[0].position+Vector3.left*.3f;
        foreach(string kind in names)for(int stage=1;stage<=2;stage++)
        {
            string name=kind+(stage==1?"":"_2nd_Evolution");var prefab=AssetDatabase.LoadAssetAtPath<Ally>($"Assets/Prefabs/{name}.prefab");
            var ally=pool.Request(prefab,pool.transform);ally.transform.position=origin;AllyCall(ally,"SetTargetingWave",wave);
            var attack=ally.GetComponent<ProjectileAttack>();if(attack!=null)typeof(ProjectileAttack).GetMethod("SetPool",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(attack,new object[]{projectiles});
            Require(Get<double>(ally,"nextAttackTime")==0,"Fresh pooled ally ready: "+name);
            double start=Time.timeAsDouble;turn.StartBattle();Target(wave).StopMoving();
            while(Get<double>(ally,"nextAttackTime")==0){Require(Time.timeAsDouble-start<.1,"Fresh attack begins promptly");yield return null;}
            var observation=new Observation{prefab=name,transition="forced early completion (memory fixture)",freshDelay=Time.timeAsDouble-start};
            double deadline=Get<double>(ally,"nextAttackTime");int oldFrame=Get<int>(ally,"attackStartFrame");
            var enemy=Target(wave);enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();
            Require(turn.CanUsePreparationInput&&!manager.IsRunning,"Real completion returns to preparation");
            Require(Get<double>(ally,"nextAttackTime")==deadline&&!Get<bool>(ally,"attacking"),"End clears attack but preserves cooldown: "+name);
            start=Time.timeAsDouble;observation.remainingCooldown=deadline-start;observation.readyAtNextBattle=deadline<=start;
            Require(observation.remainingCooldown>.05,"Short preparation retains measurable cooldown");
            turn.StartBattle();Target(wave).StopMoving();
            while(Get<int>(ally,"attackStartFrame")==oldFrame)
            {if(Time.timeAsDouble-start>3)throw new Exception("Next attack never started");yield return null;}
            observation.nextWaveDelay=Time.timeAsDouble-start;
            Require(observation.nextWaveDelay>=observation.remainingCooldown-.011&&observation.nextWaveDelay<=observation.remainingCooldown+.031,"Observed first attack waits for old deadline: "+name);
            deadline=Get<double>(ally,"nextAttackTime");enemy=Target(wave);enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();
            while(Time.timeAsDouble<deadline+.03)yield return null;
            oldFrame=Get<int>(ally,"attackStartFrame");start=Time.timeAsDouble;turn.StartBattle();Target(wave).StopMoving();
            while(Get<int>(ally,"attackStartFrame")==oldFrame)
            {if(Time.timeAsDouble-start>.1)throw new Exception("Long-preparation first attack delayed");yield return null;}
            observation.longPreparationDelay=Time.timeAsDouble-start;
            Require(observation.freshDelay<=.031&&observation.longPreparationDelay<=.031,"Fresh and sufficiently prepared first attacks begin within a frame");
            observations.Add(observation);enemy=Target(wave);enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();pool.Return(ally);
        }
        // Also retain the real death-animation wait for a slow attacker.
        var maceman=pool.Request(AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/Yut_Maceman.prefab"),pool.transform);
        maceman.transform.position=origin;AllyCall(maceman,"SetTargetingWave",wave);
        turn.StartBattle();Target(wave).StopMoving();while(Get<double>(maceman,"nextAttackTime")==0)yield return null;
        double naturalDeadline=Get<double>(maceman,"nextAttackTime");int previousFrame=Get<int>(maceman,"attackStartFrame");
        Target(wave).TakeDamage(int.MaxValue);
        while(manager.IsRunning)yield return null;
        var natural=new Observation{prefab="Yut_Maceman",transition="real death animation completion",remainingCooldown=naturalDeadline-Time.timeAsDouble};
        Require(natural.remainingCooldown>.5,"Real death-animation wait still leaves maceman cooldown");
        double naturalStart=Time.timeAsDouble;natural.readyAtNextBattle=naturalDeadline<=naturalStart;turn.StartBattle();Target(wave).StopMoving();
        while(Get<int>(maceman,"attackStartFrame")==previousFrame)
        {if(Time.timeAsDouble-naturalStart>3)throw new Exception("Natural next attack never started");yield return null;}
        natural.nextWaveDelay=Time.timeAsDouble-naturalStart;
        Require(Math.Abs(natural.nextWaveDelay-natural.remainingCooldown)<.031,"Natural next battle also waits for previous deadline");observations.Add(natural);
        manager.StopWave();pool.Return(maceman);
        Require(observations.Count==11&&observations.All(o=>!o.readyAtNextBattle),"Missing per-battle reset reproduced; do not label requirement satisfied");
    }
}

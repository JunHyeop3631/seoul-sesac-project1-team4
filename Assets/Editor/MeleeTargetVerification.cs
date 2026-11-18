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
public static class MeleeTargetVerification
{
    const string Key="Yut.MeleeTargetVerification";
    static IEnumerator checks;static int assertions,errors;
    static readonly List<Observation> observations=new();
    static MeleeTargetVerification(){EditorApplication.update+=Tick;}
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
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>120)throw new Exception("Melee target audit timed out");
            if(checks==null){Application.logMessageReceived+=CountError;checks=Check();}if(checks.MoveNext())return;
            Require(errors==0,"No runtime errors during checks");
            Finish(true,"56 current-behavior observations passed. Range loss/death/escape can retarget or cancel an ongoing melee attack. GAP-031 remains unresolved.");
        }
        catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void CountError(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
    [Serializable] sealed class Observation
    {
        public string prefab,scenario,direction;public float originalDamage,replacementDamage;
        public bool attackCancelled,deadlineUnchanged,matchesRequirement;public double damageAfterAttackStart;
    }
    [Serializable] sealed class Result
    {
        public bool checksPassed,requirementSatisfied;public int assertions,runtimeErrors,cases,mismatches;
        public string unity,scene,detail;public Observation[] observations;
    }
    static void Finish(bool passed,string detail)
    {
        Time.captureDeltaTime=0;Time.timeScale=1;SessionState.SetBool(Key,false);Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/MeleeTargetVerification.json",JsonUtility.ToJson(new Result{checksPassed=passed,requirementSatisfied=false,
            assertions=assertions,runtimeErrors=errors,cases=observations.Count,mismatches=observations.Count(o=>!o.matchesRequirement),
            unity=Application.unityVersion,scene="ThrowYut (Play Mode)",detail=detail,observations=observations.ToArray()},true));
        EditorApplication.Exit(passed?0:1);
    }
    static FieldInfo Field(object o,string name)
    {for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)return f;}throw new Exception(name);}
    static T Get<T>(object o,string name)=>(T)Field(o,name).GetValue(o);
    static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
    static void Require(bool ok,string message){assertions++;if(!ok)throw new Exception($"Check {assertions}: {message}");}
    static IEnumerator Check()
    {
        yield return null;yield return null;
        UnityEngine.Object.FindFirstObjectByType<TurnController>().enabled=false;
        var rewards=UnityEngine.Object.FindFirstObjectByType<EnemyKillRewards>();if(rewards!=null)rewards.enabled=false;
        var wave=UnityEngine.Object.FindFirstObjectByType<Wave>();var enemies=UnityEngine.Object.FindFirstObjectByType<EnemyPool>();
        var allies=UnityEngine.Object.FindFirstObjectByType<AllyPool>();Time.captureDeltaTime=.01f;Time.timeScale=1;
        var data=ScriptableObject.CreateInstance<WaveData>();
        Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=2,health=10000}});
        string[] names={"Gae_Warrior","Gae_Warrior_2nd_Evolution","Geol_Spearman","Geol_Spearman_2nd_Evolution"};
        string[] scenarios={"closer_other","range_alone","range_other","death_alone","death_other","escape_alone","escape_other"};
        foreach(string name in names)foreach(int sign in new[]{-1,1})foreach(string scenario in scenarios)
        {
            wave.StartWave(data,enemies,0);while(wave.AliveEnemyCount<2)yield return null;
            var pair=Get<HashSet<Enemy>>(wave,"spawnedEnemies").ToArray();var original=pair[0];var other=pair[1];
            var arrival=Get<Action<Enemy>>(original,"onArrived");
            original.StopMoving();other.StopMoving();
            var ally=allies.Request(AssetDatabase.LoadAssetAtPath<Ally>($"Assets/Prefabs/{name}.prefab"),allies.transform);
            ally.transform.position=Vector3.zero;
            original.transform.position=new Vector3(sign*.3f,0,0);
            bool replacement=scenario.EndsWith("other");
            other.transform.position=new Vector3(sign*(replacement?ally.AttackRange*.8f:ally.AttackRange+2),0,0);
            Physics2D.SyncTransforms();
            typeof(Ally).GetMethod("SetTargetingWave",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ally,new object[]{wave});
            double waiting=Time.timeAsDouble;
            while(Get<double>(ally,"nextAttackTime")==0)
            {if(Time.timeAsDouble-waiting>.1)throw new Exception("Attack did not start");yield return null;}
            Require(ally.CurrentTarget==original&&!Get<bool>(ally,"released"),"Original selected before real hit event: "+name+"/"+scenario);
            double deadline=Get<double>(ally,"nextAttackTime"),start=Time.timeAsDouble;
            var animator=Get<Animator>(ally,"animator");
            int expectedState=Animator.StringToHash(sign<0?"AttackLeft":"AttackRight");
            Require(animator.GetCurrentAnimatorStateInfo(0).shortNameHash==expectedState,"Actual attack direction");
            if(scenario=="closer_other")other.transform.position=new Vector3(sign*.1f,0,0);
            else if(scenario.StartsWith("range"))original.transform.position=new Vector3(sign*(ally.AttackRange+.5f),0,0);
            else if(scenario.StartsWith("death"))original.TakeDamage(int.MaxValue);
            else arrival(original);
            Physics2D.SyncTransforms();
            float originalBefore=original.CurrentHealth,otherBefore=other.CurrentHealth;
            while(Time.timeAsDouble<deadline-.03&&Get<bool>(ally,"attacking")&&!Get<bool>(ally,"released"))yield return null;
            var row=new Observation{prefab=name,scenario=scenario,direction=sign<0?"left":"right",
                originalDamage=originalBefore-original.CurrentHealth,replacementDamage=otherBefore-other.CurrentHealth,
                attackCancelled=!Get<bool>(ally,"attacking"),deadlineUnchanged=Get<double>(ally,"nextAttackTime")==deadline,
                damageAfterAttackStart=Time.timeAsDouble-start};
            bool control=scenario=="closer_other";
            if(control)
            {
                Require(Mathf.Abs(row.originalDamage-ally.AttackDamage)<.01f&&row.replacementDamage==0&&!row.attackCancelled,"Closer newcomer does not steal in-range attack");
                row.matchesRequirement=true;
            }
            else if(replacement)
            {
                Require(row.originalDamage==0&&Mathf.Abs(row.replacementDamage-ally.AttackDamage)<.01f&&!row.attackCancelled,"Current code transfers real event damage to replacement");
                row.matchesRequirement=false;
            }
            else
            {
                Require(row.originalDamage==0&&row.replacementDamage==0&&row.attackCancelled,"Current code cancels ongoing animation without replacement");
                yield return null;
                int state=animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                Require(state==Animator.StringToHash("IdleLeft")||state==Animator.StringToHash("IdleRight"),"Cancellation actually returns animator to Idle");
                row.matchesRequirement=false;
            }
            Require(row.deadlineUnchanged,"Retarget/cancel preserves original next attack deadline");
            observations.Add(row);wave.StopWave();allies.Return(ally);
        }
        Require(observations.Count==56&&observations.Count(o=>!o.matchesRequirement)==48,"Eight controls match,48 changed-target cases differ from specified melee finish policy");
    }
}

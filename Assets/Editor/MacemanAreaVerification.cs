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
public static class MacemanAreaVerification
{
    const string Key="Yut.MacemanAreaVerification";
    static IEnumerator checks;static int assertions,errors;
    static readonly List<Observation> observations=new();
    static MacemanAreaVerification(){EditorApplication.update+=Tick;}
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
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>120)throw new Exception("Maceman area audit timed out");
            if(checks==null){Application.logMessageReceived+=CountError;checks=Check();}if(checks.MoveNext())return;
            Require(errors==0,"No runtime errors during checks");
            Finish(true,"16 real animation-event observations confirm single-target damage and missing30-percent area damage. GAP-027 remains unresolved; range-loss primary policy stays unspecified.");
        }
        catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void CountError(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
    [Serializable] sealed class Observation
    {
        public string prefab,scenario,direction;public float expectedDirect,expectedSplash;
        public float originalDamage,nearDamage,oppositeInsideDamage,boundaryDamage,outsideDamage,deadDamage;
        public bool primaryPolicySpecified,deadlineUnchanged,oneHitOnly;
    }
    [Serializable] sealed class Result
    {
        public bool checksPassed,requirementSatisfied;public int assertions,runtimeErrors,cases;
        public string unity,scene,detail;public Observation[] observations;
    }
    static void Finish(bool passed,string detail)
    {
        Time.captureDeltaTime=0;Time.timeScale=1;SessionState.SetBool(Key,false);Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/MacemanAreaVerification.json",JsonUtility.ToJson(new Result{checksPassed=passed,requirementSatisfied=false,
            assertions=assertions,runtimeErrors=errors,cases=observations.Count,unity=Application.unityVersion,scene="ThrowYut (Play Mode)",detail=detail,observations=observations.ToArray()},true));
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
        Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=6,health=10000}});
        foreach(int stage in new[]{1,2})foreach(int sign in new[]{-1,1})
        foreach(string scenario in new[]{"valid_primary","dead_primary","escaped_primary","range_primary_unspecified"})
        {
            wave.StartWave(data,enemies,0);while(wave.AliveEnemyCount<6)yield return null;
            var targets=Get<HashSet<Enemy>>(wave,"spawnedEnemies").ToArray();
            var arrival=Get<Action<Enemy>>(targets[0],"onArrived");foreach(var enemy in targets)enemy.StopMoving();
            string name="Yut_Maceman"+(stage==1?"":"_2nd_Evolution");
            var ally=allies.Request(AssetDatabase.LoadAssetAtPath<Ally>($"Assets/Prefabs/{name}.prefab"),allies.transform);ally.transform.position=Vector3.zero;
            float radius=ally.AttackRange;
            targets[0].transform.position=new Vector3(sign*.3f,0,0);
            targets[1].transform.position=new Vector3(sign*.6f,0,0);
            targets[2].transform.position=new Vector3(-sign*(radius-.02f),0,0);
            targets[3].transform.position=new Vector3(0,radius,0);
            targets[4].transform.position=new Vector3(0,-radius-.02f,0);
            targets[5].transform.position=new Vector3(sign*.9f,0,0);targets[5].TakeDamage(int.MaxValue);
            Physics2D.SyncTransforms();
            typeof(Ally).GetMethod("SetTargetingWave",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ally,new object[]{wave});
            double waiting=Time.timeAsDouble;
            while(Get<double>(ally,"nextAttackTime")==0)
            {if(Time.timeAsDouble-waiting>.1)throw new Exception("Maceman attack did not start");yield return null;}
            Require(ally.CurrentTarget==targets[0]&&!Get<bool>(ally,"released"),"Original selected before hit");
            Require(Get<Animator>(ally,"animator").GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(sign<0?"AttackLeft":"AttackRight"),"Actual left/right animation");
            double deadline=Get<double>(ally,"nextAttackTime");
            if(scenario=="dead_primary")targets[0].TakeDamage(int.MaxValue);
            else if(scenario=="escaped_primary")arrival(targets[0]);
            else if(scenario=="range_primary_unspecified")targets[0].transform.position=new Vector3(sign*(radius+.5f),0,0);
            Physics2D.SyncTransforms();var health=targets.Select(e=>e.CurrentHealth).ToArray();
            while(Time.timeAsDouble<deadline-.03&&!Get<bool>(ally,"released"))yield return null;
            Require(Get<bool>(ally,"released"),"Actual animation hit event occurred");
            var damage=targets.Select((e,i)=>health[i]-e.CurrentHealth).ToArray();
            int receiver=scenario=="valid_primary"?0:1;
            Require(Mathf.Abs(damage[receiver]-ally.AttackDamage)<.01f,"Current single target receives100 percent");
            Require(damage.Where((d,i)=>i!=receiver).All(d=>d==0),"All other roles receive zero area damage");
            while(Time.timeAsDouble<deadline-.03)yield return null;
            var finalDamage=targets.Select((e,i)=>health[i]-e.CurrentHealth).ToArray();
            Require(finalDamage.SequenceEqual(damage),"No duplicate hit before next attack interval");
            Require(Get<double>(ally,"nextAttackTime")==deadline,"Existing cooldown preserved");
            Require(Vector3.Distance(targets[2].transform.position,ally.transform.position)<radius&&
                Vector3.Distance(targets[2].transform.position,new Vector3(sign*.3f,0,0))>radius,"Opposite target is inside caster circle but outside primary-centered circle");
            observations.Add(new Observation{prefab=name,scenario=scenario,direction=sign<0?"left":"right",expectedDirect=ally.AttackDamage,expectedSplash=ally.AttackDamage*.3f,
                originalDamage=damage[0],nearDamage=damage[1],oppositeInsideDamage=damage[2],boundaryDamage=damage[3],outsideDamage=damage[4],deadDamage=damage[5],
                primaryPolicySpecified=scenario!="range_primary_unspecified",deadlineUnchanged=true,oneHitOnly=true});
            wave.StopWave();allies.Return(ally);
        }
        Require(observations.Count==16&&observations.All(o=>o.oppositeInsideDamage==0&&o.boundaryDamage==0),"Area damage absence reproduced for both stages and directions");
    }
}

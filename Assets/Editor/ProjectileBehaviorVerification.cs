using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Task20 audit only: actual ProjectileAttack.Fire and normal projectile updates.
[InitializeOnLoad]
public static class ProjectileBehaviorVerification
{
    const string Key="Yut.ProjectileBehaviorVerification";
    static IEnumerator checks;static int assertions,errors;
    static readonly List<Observation> observations=new();
    static ProjectileBehaviorVerification(){EditorApplication.update+=Tick;}
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
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>120)throw new Exception("Projectile audit timed out");
            if(checks==null){Application.logMessageReceived+=CountError;checks=Check();}if(checks.MoveNext())return;
            Require(errors==0,"No runtime errors during checks");
            Finish(true,"52 observations: fixed arrival time and missing attacker-range invalidation differ; death/escape/reuse/stop/pause and single damage remain valid.");
        }
        catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void CountError(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
    [Serializable] sealed class Observation
    {
        public string prefab,scenario;public double flightGameSeconds;
        public float initialDistance,averageStationarySpeed,minSampleSpeed,maxSampleSpeed,targetDamage,otherDamage;
        public bool activeAfterRangeExit,pausePreserved,poolReused;
    }
    [Serializable] sealed class Result
    {
        public bool checksPassed,constantSpeedRequirementSatisfied,attackerRangeRequirementSatisfied;
        public int assertions,runtimeErrors,cases;public string unity,scene,detail;public Observation[] observations;
    }
    static void Finish(bool passed,string detail)
    {
        Time.captureDeltaTime=0;Time.timeScale=1;SessionState.SetBool(Key,false);Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ProjectileBehaviorVerification.json",JsonUtility.ToJson(new Result{checksPassed=passed,
            constantSpeedRequirementSatisfied=false,attackerRangeRequirementSatisfied=false,assertions=assertions,runtimeErrors=errors,cases=observations.Count,
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
        var allies=UnityEngine.Object.FindFirstObjectByType<AllyPool>();var projectiles=UnityEngine.Object.FindFirstObjectByType<ProjectilePool>();
        Time.captureDeltaTime=.01f;Time.timeScale=1;
        var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=2,health=10000}});
        string[] names={"Do_Taoist","Do_Taoist_2nd_Evolution","Mo_Archer","Mo_Archer_2nd_Evolution"};
        string[] cases={"near","far","moving","moving_continuous","exact_range","range_exit","exit_return","shooter_moves","death","escape","reuse","pause","wave_stop"};
        var previous=new Dictionary<HomingProjectile,HomingProjectile>();
        foreach(string name in names)foreach(string scenario in cases)
        {
            wave.StartWave(data,enemies,0);while(wave.AliveEnemyCount<2)yield return null;
            var pair=Get<HashSet<Enemy>>(wave,"spawnedEnemies").ToArray();var target=pair[0];var other=pair[1];
            var arrival=Get<Action<Enemy>>(target,"onArrived");target.StopMoving();other.StopMoving();
            var ally=allies.Request(AssetDatabase.LoadAssetAtPath<Ally>($"Assets/Prefabs/{name}.prefab"),allies.transform);ally.transform.position=Vector3.zero;
            foreach(var animator in ally.GetComponentsInChildren<Animator>())animator.enabled=false;
            typeof(Ally).GetMethod("SetTargetingWave",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ally,new object[]{wave});
            var attack=ally.GetComponent<ProjectileAttack>();typeof(ProjectileAttack).GetMethod("SetPool",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(attack,new object[]{projectiles});
            target.transform.position=new Vector3(scenario=="far"?3f:1f,0,0);other.transform.position=new Vector3(1.5f,0,0);Physics2D.SyncTransforms();
            Require(Mathf.Abs(Get<float>(attack,"flightDuration")-.3f)<.0001f,"Existing duration is0.3: "+name);
            attack.Fire(target,ally.AttackDamage);
            var projectile=UnityEngine.Object.FindObjectsByType<HomingProjectile>(FindObjectsSortMode.None).Single();
            var prefab=Get<HomingProjectile>(attack,"projectilePrefab");
            var row=new Observation{prefab=name,scenario=scenario,poolReused=previous.TryGetValue(prefab,out var old)&&old==projectile,
                initialDistance=Vector3.Distance(projectile.transform.position,target.transform.position)};
            if(previous.ContainsKey(prefab))Require(row.poolReused,"Projectile instance reused");previous[prefab]=projectile;
            double launched=Get<double>(projectile,"launchTime"),sampleTime=Time.timeAsDouble;
            Vector3 samplePosition=projectile.transform.position;var speeds=new List<float>();
            bool triggered=false,returnedInside=false;int pausedUntil=-1;Vector3 pausePosition=Vector3.zero;
            float targetBefore=target.CurrentHealth,otherBefore=other.CurrentHealth;
            while(projectile.gameObject.activeSelf)
            {
                double now=Time.timeAsDouble,elapsed=now-launched;
                if(scenario=="moving_continuous")target.transform.position=new Vector3(1f+(float)elapsed*2f,(float)elapsed*.5f,0);
                if(elapsed>1)throw new Exception("Projectile did not finish");
                if(pausedUntil>=0)
                {
                    Require(projectile.transform.position==pausePosition&&target.CurrentHealth==targetBefore,"Pause preserves flight and health");
                    if(Time.frameCount>=pausedUntil){row.pausePreserved=true;pausedUntil=-1;Time.timeScale=1;}
                }
                if(elapsed>=.08&&!triggered)
                {
                    triggered=true;
                    switch(scenario)
                    {
                        case "moving":target.transform.position=new Vector3(3f,1f,0);break;
                        case "exact_range":target.transform.position=new Vector3(ally.AttackRange,0,0);break;
                        case "range_exit":case "exit_return":target.transform.position=new Vector3(ally.AttackRange+.5f,0,0);break;
                        case "shooter_moves":ally.transform.position=new Vector3(-ally.AttackRange-1,0,0);break;
                        case "death":target.TakeDamage(int.MaxValue);targetBefore=target.CurrentHealth;break;
                        case "escape":arrival(target);break;
                        case "reuse":
                            arrival(target);var reused=enemies.RequestEnemy(EnemyId.NormalEnemy,Vector3.right,Quaternion.identity);
                            Require(reused==target,"Same enemy identity reused before projectile update");
                            typeof(Enemy).GetMethod("ConfigureHealth",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(reused,new object[]{10000});
                            Get<HashSet<Enemy>>(wave,"spawnedEnemies").Add(reused);targetBefore=reused.CurrentHealth;break;
                        case "pause":Time.timeScale=0;pausedUntil=Time.frameCount+5;pausePosition=projectile.transform.position;break;
                        case "wave_stop":wave.StopWave();break;
                    }
                }
                if(triggered&&(scenario=="range_exit"||scenario=="exit_return"||scenario=="shooter_moves")&&elapsed>=.12&&!returnedInside)
                {
                    row.activeAfterRangeExit=projectile.gameObject.activeSelf;
                    if(scenario=="exit_return")target.transform.position=Vector3.right;
                    returnedInside=true;
                }
                if(now>sampleTime)
                {
                    float distance=Vector3.Distance(samplePosition,projectile.transform.position);
                    if(distance>0)speeds.Add(distance/(float)(now-sampleTime));
                    sampleTime=now;samplePosition=projectile.transform.position;
                }
                yield return null;
            }
            row.flightGameSeconds=Time.timeAsDouble-launched;
            row.targetDamage=targetBefore-target.CurrentHealth;row.otherDamage=otherBefore-other.CurrentHealth;
            row.minSampleSpeed=speeds.Count>0?speeds.Min():0;row.maxSampleSpeed=speeds.Count>0?speeds.Max():0;
            row.averageStationarySpeed=row.initialDistance/(float)row.flightGameSeconds;
            bool invalid=scenario=="death"||scenario=="escape"||scenario=="reuse"||scenario=="wave_stop";
            Require(row.otherDamage==0,"Never retargets another enemy");
            if(invalid)Require(row.targetDamage==0&&row.flightGameSeconds<.15,"Invalid target/wave cancels before arrival with no damage");
            else
            {
                Require(Mathf.Abs(row.targetDamage-ally.AttackDamage)<.01f,"Current valid target takes exactly one configured hit");
                Require(Math.Abs(row.flightGameSeconds-.3)<.021,"Arrival follows fixed duration rather than distance");
            }
            if(scenario=="range_exit"||scenario=="exit_return"||scenario=="shooter_moves")Require(row.activeAfterRangeExit&&row.targetDamage>0,"Missing attacker-range invalidation reproduced");
            if(scenario=="pause")Require(row.pausePreserved,"Paused flight resumes");
            float after=target.CurrentHealth;double until=Time.timeAsDouble+.05;
            while(Time.timeAsDouble<until)yield return null;
            Require(target.CurrentHealth==after&&!projectile.gameObject.activeSelf,"Returned projectile cannot apply damage twice");
            observations.Add(row);wave.StopWave();allies.Return(ally);Time.timeScale=1;
        }
        foreach(string name in names)
        {
            var near=observations.Single(o=>o.prefab==name&&o.scenario=="near");var far=observations.Single(o=>o.prefab==name&&o.scenario=="far");
            Require(far.averageStationarySpeed>near.averageStationarySpeed*1.5f&&Math.Abs(far.flightGameSeconds-near.flightGameSeconds)<.021,"Distance-dependent speed with almost identical arrival time: "+name);
            var moving=observations.Single(o=>o.prefab==name&&o.scenario=="moving");
            Require(moving.maxSampleSpeed>moving.minSampleSpeed*1.5f,"Moving-target step speed changes: "+name);
            var continuous=observations.Single(o=>o.prefab==name&&o.scenario=="moving_continuous");
            Require(continuous.maxSampleSpeed>continuous.minSampleSpeed*1.5f,"Continuously moving target also changes projectile speed: "+name);
        }
        Require(observations.Count==52,"All52 projectile scenarios recorded");
    }
}

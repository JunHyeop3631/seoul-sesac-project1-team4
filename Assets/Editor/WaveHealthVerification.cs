using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[InitializeOnLoad]
public static class WaveHealthVerification
{
    const string Key="Yut.WaveHealthVerification";
    static IEnumerator checks;
    static int assertions, errors, spawned, reused;
    static readonly HashSet<int> instances = new();
    static WaveHealthVerification(){EditorApplication.update+=Tick;}
    public static void ReimportAndRun()
    {
        foreach (string path in new[]{"Assets/Scripts","Assets/Prefabs","Assets/Scenes/ThrowYut.unity"})
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ImportRecursive|ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        for(int i=1;i<=10;i++) AssetDatabase.ImportAsset($"Assets/Wave{i}.asset",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        Run();
    }
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");SessionState.SetBool(Key,true);SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);EditorApplication.EnterPlaymode();}
    static void Tick(){
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>=120) throw new Exception("Timeout");
            if(checks==null){Application.logMessageReceived+=CountError;checks=Check();}if(checks.MoveNext())return;
            Require(errors==0,"No runtime errors during checks"); Finish(true,"Ten wave assets, scene order, exact counts/HP/totals, immediate spawn, health bars, fractional damage and pooled reuse passed.");
        }catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void CountError(string m,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
    [Serializable] sealed class Result { public bool passed; public int assertions,runtimeErrors,spawnedEnemies,reusedSpawns; public string unity,scene,detail; }
    static void Finish(bool passed,string detail)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WaveHealthVerification.json",JsonUtility.ToJson(new Result{passed=passed,assertions=assertions,runtimeErrors=errors,spawnedEnemies=spawned,reusedSpawns=reused,unity=Application.unityVersion,scene="ThrowYut (Play Mode)",detail=detail},true));
        SessionState.SetBool(Key,false);EditorApplication.Exit(passed?0:1);
    }
    static T Field<T>(object o,string n){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)return (T)f.GetValue(o);}throw new Exception(n);}
    static void Require(bool b,string m){assertions++;if(!b)throw new Exception($"Check {assertions}: {m}");}
    static IEnumerator Check(){
        yield return null;yield return null;
        var manager=UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        Require(manager.WaveCount==10,"Expected ten configured stage waves");
        var waves=Field<WaveData[]>(manager,"waves");
        // Latest user-designated Google Docs tab4, chapter12, re-read 2026-11-18.
        int[] normalHp={125,240,290,270,290,355,335,360,375,0}, fastHp={0,105,130,135,145,175,180,195,200,0};
        int[] normal={12,10,12,14,14,16,18,18,20,0},fast={0,6,8,8,10,8,8,10,10,0};
        int[] totals={1500,3030,4520,4860,5510,7080,7470,8430,9500,6000};
        var wave=Field<Wave>(manager,"wave");
        var pool=Field<EnemyPool>(manager,"enemyPool");
        foreach(EnemyId id in Enum.GetValues(typeof(EnemyId)))
        {
            var unit=pool.RequestEnemy(id,Vector3.zero,Quaternion.identity);
            int fallback=Field<int>(unit,"maxHealth");
            typeof(Enemy).GetMethod("ConfigureHealth",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(unit,new object[]{125});unit.TakeDamage(.5f);
            Require(unit.CurrentHealth==124.5f,"Fractional damage before return "+id);
            int version=Field<int>(unit,"<ActivationVersion>k__BackingField");pool.ReturnEnemy(unit);
            var next=pool.RequestEnemy(id,Vector3.zero,Quaternion.identity);
            Require(next==unit&&Field<int>(next,"<ActivationVersion>k__BackingField")>version&&next.CurrentHealth==fallback,"Same pooled instance resets to prefab default "+id);
            typeof(Enemy).GetMethod("ConfigureHealth",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(next,new object[]{6000});
            Require(next.CurrentHealth==6000&&Mathf.Approximately(Field<Image>(next,"healthBarFill").fillAmount,1),"Reconfigured full health bar "+id);
            next.TakeDamage(3000);
            Require(Mathf.Approximately(Field<Image>(next,"healthBarFill").fillAmount,.5f),"Health bar uses spawn maximum "+id);
            pool.ReturnEnemy(next);
        }
        // Speed up spawning in memory only; never save test timing or enemy changes.
        typeof(WaveManager).GetField("spawnInterval",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,.03f);
        for(int i=0;i<10;i++){
            var groups=waves[i].Enemies;
            Require(waves[i]==AssetDatabase.LoadAssetAtPath<WaveData>($"Assets/Wave{i+1}.asset"),"Scene wave registration order "+(i+1));
            Require(groups.Sum(g=>(int)g.enemyId==0?g.count:0)==normal[i],"Normal count "+(i+1));
            Require(groups.Sum(g=>(int)g.enemyId==1?g.count:0)==fast[i],"Fast count "+(i+1));
            Require(groups.Sum(g=>(int)g.enemyId==2?g.count:0)==(i==9?1:0),"Boss count "+(i+1));
            Require(groups.All(g=>g.health==(g.enemyId==EnemyId.BossEnemy?6000:g.enemyId==EnemyId.FastEnemy?fastHp[i]:normalHp[i])),"Serialized per-enemy HP "+(i+1));
            Require(groups.Sum(g=>g.count*g.health)==totals[i],"Total HP "+(i+1));
            int[] seen={0,0,0};manager.StartWave(i);
            Require(wave.AliveEnemyCount==1,"First spawn immediate");
            while(wave.IsRunning){
                foreach(var enemy in Field<System.Collections.Generic.HashSet<Enemy>>(wave,"spawnedEnemies").ToArray()){
                    seen[enemy is BossEnemy?2:enemy is FastEnemy?1:0]++;
                    spawned++;if(!instances.Add(enemy.GetInstanceID()))reused++;
                    int expected=enemy is BossEnemy?6000:enemy is FastEnemy?fastHp[i]:normalHp[i];
                    Require(enemy.CurrentHealth==expected,"Wave "+(i+1)+" spawned HP expected "+expected+", got "+enemy.CurrentHealth);
                    var bar=Field<UnityEngine.UI.Image>(enemy,"healthBarFill");
                    Require(bar!=null&&Mathf.Approximately(bar.fillAmount,1),"Full health bar on spawn");
                    enemy.TakeDamage(expected/2f);
                    Require(enemy.CurrentHealth==expected/2f,"Fractional damage preserved");
                    Require(Mathf.Abs(bar.fillAmount-.5f)<.001f,"Health bar uses wave maximum");
                    enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();
                }
                yield return null;
            }
            Require(seen[0]==normal[i]&&seen[1]==fast[i]&&seen[2]==(i==9?1:0),"Runtime spawn counts "+(i+1));
        }
        Require(spawned==203&&reused>0,"All203 enemies spawned with actual pooled reuse");
    }
}

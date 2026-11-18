using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WaveHealthVerification
{
    const string Key="Yut.WaveHealthVerification";
    static IEnumerator checks;
    static WaveHealthVerification(){EditorApplication.update+=Tick;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");SessionState.SetBool(Key,true);SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);EditorApplication.EnterPlaymode();}
    static void Tick(){
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)<120,"Timeout");
            if(checks==null)checks=Check();if(checks.MoveNext())return;
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WaveHealthVerification.json","{\"passed\":true,\"tenWaves\":true,\"enemyCounts\":true,\"bossOnlyWave10\":true,\"allTenSpawnAndComplete\":true,\"waveHealthAndPoolReset\":true,\"healthBarRatio\":true,\"fractionalDamage\":true}");
            SessionState.SetBool(Key,false);EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
    static T Field<T>(object o,string n){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)return (T)f.GetValue(o);}throw new Exception(n);}
    static void Require(bool b,string m){if(!b)throw new Exception(m);}
    static IEnumerator Check(){
        yield return null;yield return null;
        var manager=UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        Require(manager.WaveCount==10,"Expected ten configured stage waves");
        var waves=Field<WaveData[]>(manager,"waves");
        int[] normalHp={250,480,580,540,580,710,670,720,750,0}, fastHp={0,210,260,270,290,350,360,390,400,0};
        int[] normal={12,10,12,14,14,16,18,18,20,0},fast={0,6,8,8,10,8,8,10,10,0};
        var wave=Field<Wave>(manager,"wave");
        // Speed up spawning in memory only; never save test timing or enemy changes.
        typeof(WaveManager).GetField("spawnInterval",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,.03f);
        for(int i=0;i<10;i++){
            var groups=waves[i].Enemies;
            Require(groups.Sum(g=>(int)g.enemyId==0?g.count:0)==normal[i],"Normal count "+(i+1));
            Require(groups.Sum(g=>(int)g.enemyId==1?g.count:0)==fast[i],"Fast count "+(i+1));
            Require(groups.Sum(g=>(int)g.enemyId==2?g.count:0)==(i==9?1:0),"Boss count "+(i+1));
            int[] seen={0,0,0};manager.StartWave(i);
            Require(wave.AliveEnemyCount==1,"First spawn immediate");
            while(wave.IsRunning){
                foreach(var enemy in Field<System.Collections.Generic.HashSet<Enemy>>(wave,"spawnedEnemies").ToArray()){
                    seen[enemy is BossEnemy?2:enemy is FastEnemy?1:0]++;
                                        int expected=enemy is BossEnemy?12000:enemy is FastEnemy?fastHp[i]:normalHp[i];
                    Require(enemy.CurrentHealth==expected,"Wave "+(i+1)+" spawned HP expected "+expected+", got "+enemy.CurrentHealth);
                    var bar=Field<UnityEngine.UI.Image>(enemy,"healthBarFill");
                    Require(bar==null||Mathf.Approximately(bar.fillAmount,1),"Full health bar on spawn");
                    enemy.TakeDamage(expected/2f);
                    Require(enemy.CurrentHealth==expected/2f,"Fractional damage preserved");
                    Require(bar==null||Mathf.Abs(bar.fillAmount-.5f)<.001f,"Health bar uses wave maximum");
                    enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();
                }
                yield return null;
            }
            Require(seen[0]==normal[i]&&seen[1]==fast[i]&&seen[2]==(i==9?1:0),"Runtime spawn counts "+(i+1));
        }
    }
}
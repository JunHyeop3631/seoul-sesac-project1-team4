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
    static KillRewardVerification(){EditorApplication.update+=Tick;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");SessionState.SetBool(Key,true);SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);EditorApplication.EnterPlaymode();}
    static void Tick(){
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)<90,"Timeout");
            if(checks==null)checks=Check();if(checks.MoveNext())return;
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/KillRewardVerification.json","{\"passed\":true,\"normal2\":true,\"fast1\":true,\"bossUnchanged\":true,\"noDuplicate\":true,\"noEscapeReward\":true,\"popupExpires\":true,\"pooledReuse\":true,\"completionRewardSeparate\":true}");
            SessionState.SetBool(Key,false);EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(object o,string n,object v){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null){f.SetValue(o,v);return;}}throw new Exception(n);}
    static void Require(bool b,string m){if(!b)throw new Exception(m);}
    static Enemy[] Enemies()=>UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
    static IEnumerator Check(){
        yield return null;yield return null;
        var turn=UnityEngine.Object.FindFirstObjectByType<TurnController>();var manager=UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        var wallet=UnityEngine.Object.FindFirstObjectByType<StageCurrency>();
        var data=ScriptableObject.CreateInstance<WaveData>();
        Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=2,health=50},new WaveData.EnemyGroup{enemyId=EnemyId.FastEnemy,count=1,health=50},new WaveData.EnemyGroup{enemyId=EnemyId.BossEnemy,count=1,health=50}});
        Set(manager,"waves",new[]{data,data,data});Set(manager,"spawnInterval",0f);
        turn.StartBattle();for(int i=0;i<6;i++)yield return null;
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
        Enemies().First(e=>e is FastEnemy).TakeDamage(50);Require(wallet.Balance==123,"Fast enemy must credit 1");
        typeof(SpecialSummonVerification).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"kill-rewards-both"});
        var boss=Enemies().First(e=>e is BossEnemy);boss.TakeDamage(50);Require(wallet.Balance==123,"Boss payout changed without authorization");
        // Remaining normal escapes; moving it rapidly must not produce kill income.
        var escaped=Enemies().First(e=>e is NormalEnemy && e.CurrentHealth>0);Set(escaped,"moveSpeed",10000f);
        foreach(var enemy in Enemies().Where(e=>e.CurrentHealth<=0))enemy.CompleteDeathAnimation();
        while(manager.IsRunning)yield return null;
        Set(escaped,"moveSpeed",2f);
        Require(wallet.Balance==183 && turn.RemainingLives==9,"Escape paid kill income or completion reward changed");
        float start=Time.time;while(Time.time-start<1)yield return null;
        Require(popup.GetComponentsInChildren<CanvasGroup>().All(g=>g.alpha==0),"Floating rewards did not expire");
        turn.StartBattle();for(int i=0;i<6;i++)yield return null;
        normal=Enemies().First(e=>e is NormalEnemy && e.CurrentHealth>0);normal.TakeDamage(50);
        Require(wallet.Balance==185,"Reused enemy failed to reward exactly once");
        typeof(SpecialSummonVerification).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"kill-reward-preview"});
    }
}

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
public static class DefeatStopVerification
{
    const string Key = "Yut.DefeatStopVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static DefeatStopVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        foreach (string path in new[] {"Assets/Scripts", "Assets/Prefabs", "Assets/Scenes/ThrowYut.unity"})
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetBool(Key, true); SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    public static void RunBattleStartRegression()
    {
        // This cached copy may predate the marked-stick animation assets.
        // Import the actual prefab dependencies before testing a real throw.
        foreach (string path in AssetDatabase.GetDependencies("Assets/Prefabs/YutStick.prefab", true))
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        BattleStartVerification.ReimportAndRun();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Defeat stop verification timed out");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks"); Finish(true, "Real lethal arrival, pending spawns, dying enemies, attacks/projectiles, stale callbacks, loss/completion race and normal reward reentry passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1;
        SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/DefeatStopVerification.json", JsonUtility.ToJson(new Result { passed=passed, assertions=assertions,
            runtimeErrors=errors, unity=Application.unityVersion, scene="ThrowYut (Play Mode)", detail=detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object obj, string name)
    {
        for (Type t=obj.GetType(); t!=null; t=t.BaseType)
        { var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic); if(f!=null)return f; }
        throw new Exception(name);
    }
    static T Get<T>(object obj,string name)=>(T)Field(obj,name).GetValue(obj);
    static void Set(object obj,string name,object value)=>Field(obj,name).SetValue(obj,value);
    static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
    static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok,string message){assertions++;if(!ok)throw new Exception($"Check {assertions}: {message}");}
    static Ally Register(YutActionController actions,Ally prefab)
    {
        var unit=UnityEngine.Object.Instantiate(prefab,Get<Transform[]>(actions,"cells")[1]);
        Require((bool)Call(actions,"RegisterAlly",1,unit),"Ally fixture registered");return unit;
    }
    static void OneLife(TurnController turn)=>typeof(TurnController).GetProperty("RemainingLives").SetValue(turn,1);
    static Transform[] ShortRoute(Enemy enemy)
    {
        Set(enemy,"lapDurationSeconds",0f);Set(enemy,"moveSpeed",1000f);
        var first=new GameObject("Fixture entrance").transform;var last=new GameObject("Fixture corner").transform;
        first.position=enemy.transform.position;last.position=first.position+Vector3.up*.001f;
        return new[]{first,last};
    }
    static IEnumerator Reload()
    {
        Time.captureDeltaTime=0;Time.timeScale=1;
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ThrowYut.unity",new LoadSceneParameters(LoadSceneMode.Single));
        int frame=Time.frameCount;
        while(Time.frameCount<frame+2)yield return null;
        while(!Find<TurnController>().CanUsePreparationInput){Time.timeScale=1;yield return null;}
    }
    static IEnumerator Check()
    {
        yield return null;yield return null;Time.captureDeltaTime=.05f;
        var turn=Find<TurnController>();var manager=Find<WaveManager>();var wave=Find<Wave>();var pool=Find<EnemyPool>();
        var wallet=Find<StageCurrency>();var actions=Find<YutActionController>();
        var melee=Register(actions,Get<Gae_Warrior>(actions,"gaeWarriorPrefab"));
        var ranged=Register(actions,Get<Do_Taoist>(actions,"doTaoistPrefab"));
        Set(manager,"spawnInterval",.05f);OneLife(turn);
        int losses=0,completed=0,cleared=0,ended=0;
        turn.BattleLost+=()=>losses++;manager.WaveCompleted+=()=>completed++;turn.WaveCleared+=(w,r)=>cleared++;wave.Ended+=()=>ended++;
        turn.StartBattle();
        while(wave.AliveEnemyCount<5)yield return null;
        var enemies=Get<HashSet<Enemy>>(wave,"spawnedEnemies").ToArray();
        var stale=Get<Action<Enemy>>(enemies[0],"onArrived");
        foreach(var enemy in enemies)enemy.StopMoving();
        enemies[1].TakeDamage(int.MaxValue);
        Require(Get<HashSet<Enemy>>(wave,"dyingEnemies").Count==1,"Dying enemy awaits animation before lethal arrival");
        int balance=wallet.Balance,defeated=wave.DefeatedEnemyCount;
        var attack=ranged.GetComponent<ProjectileAttack>();Set(attack,"flightDuration",100f);attack.Fire(enemies[2],9999);
        var projectiles=UnityEngine.Object.FindObjectsByType<HomingProjectile>(FindObjectsSortMode.None);
        Require(projectiles.Length==1,"Actual pooled projectile launched before defeat");
        Set(melee,"attacking",true);Set(melee,"released",false);
        Set(melee,"<CurrentTarget>k__BackingField",enemies[2]);Set(melee,"targetActivationVersion",Get<int>(enemies[2],"<ActivationVersion>k__BackingField"));
        enemies[0].StartMoving(ShortRoute(enemies[0]),stale);
        while(turn.RemainingLives>0)yield return null;
        Require(losses==1&&ended==1&&completed==0&&cleared==0&&!turn.IsBattleWon,"Lethal actual arrival emits one loss/stop and no completion/reward");
        Require(!wave.IsRunning&&wave.AliveEnemyCount==0&&Get<HashSet<Enemy>>(wave,"dyingEnemies").Count==0,"Wave stopped and both tracking sets cleared");
        Require(enemies.All(e=>!e.gameObject.activeSelf&&!e.IsMoving),"Alive and dying enemies returned immediately");
        Require(projectiles.All(p=>!p.gameObject.activeSelf)&&melee.CurrentTarget==null&&!Get<bool>(melee,"attacking"),"In-flight projectile and current melee attack cancelled synchronously");
        Require(wallet.Balance==balance&&wave.DefeatedEnemyCount==defeated,"Cleanup is neither a kill nor a completion reward");
        var healths=enemies.Select(e=>e.CurrentHealth).ToArray();
        // Resume time explicitly: stopped combat must not depend on the defeat UI's pause.
        Time.timeScale=1;
        for(int i=0;i<30;i++)yield return null;
        Require(!wave.IsRunning&&pool.GetComponentsInChildren<Enemy>().Length==0&&wallet.Balance==balance,"No pending spawns or rewards after time resumes");
        melee.ApplyAttack();attack.Fire(enemies[2],9999);stale(enemies[0]);
        foreach(var enemy in enemies){enemy.CompleteDeathAnimation();enemy.TakeDamage(9999);}
        manager.StopWave();Call(turn,"LoseLife");Call(turn,"EndBattle");Call(wave,"TryCompleteWave");turn.StartBattle();
        Require(losses==1&&ended==1&&completed==0&&cleared==0&&turn.RemainingLives==0&&!turn.CanUsePreparationInput,"Repeated and stale callbacks cannot reopen or finish lost battle");
        Require(enemies.Select(e=>e.CurrentHealth).SequenceEqual(healths)&&wallet.Balance==balance,"No delayed damage or reward");
        var reload=Reload();while(reload.MoveNext())yield return reload.Current;
        turn=Find<TurnController>();manager=Find<WaveManager>();wave=Find<Wave>();wallet=Find<StageCurrency>();
        Time.captureDeltaTime=.05f;Set(manager,"spawnInterval",.01f);
        var testData=ScriptableObject.CreateInstance<WaveData>();
        Set(testData,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});
        var saved=Get<WaveData[]>(manager,"waves");saved[0]=testData;
        OneLife(turn);losses=completed=cleared=0;
        turn.BattleLost+=()=>losses++;manager.WaveCompleted+=()=>completed++;turn.WaveCleared+=(w,r)=>cleared++;
        turn.StartBattle();var last=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();
        var arrival=Get<Action<Enemy>>(last,"onArrived");last.StartMoving(ShortRoute(last),arrival);
        while(turn.RemainingLives>0)yield return null;
        Require(losses==1&&completed==0&&cleared==0&&wallet.Balance==120&&!wave.IsRunning,"Final enemy lethal arrival wins race against ordinary wave completion");
        reload=Reload();while(reload.MoveNext())yield return reload.Current;
        turn=Find<TurnController>();manager=Find<WaveManager>();wave=Find<Wave>();wallet=Find<StageCurrency>();
        Time.captureDeltaTime=.05f;Set(manager,"spawnInterval",.01f);
        testData=ScriptableObject.CreateInstance<WaveData>();
        Set(testData,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=2,health=125}});
        Get<WaveData[]>(manager,"waves")[0]=testData;
        completed=cleared=losses=0;manager.WaveCompleted+=()=>completed++;turn.WaveCleared+=(w,r)=>cleared++;turn.BattleLost+=()=>losses++;
        turn.StartBattle();while(wave.AliveEnemyCount<2)yield return null;
        Action<int> reentry=amount=>Call(turn,"EndBattle");wallet.Changed+=reentry;
        foreach(var enemy in Get<HashSet<Enemy>>(wave,"spawnedEnemies").ToArray()){enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();}
        wallet.Changed-=reentry;
        Require(completed==1&&cleared==1&&losses==0&&turn.CurrentTurnNumber==1&&turn.CanUsePreparationInput,"Normal completion still advances exactly once despite wallet callback reentry");
        Require(wallet.Balance==184,"Two existing normal kill rewards plus one60 completion reward");
        Call(turn,"EndBattle");Call(wave,"TryCompleteWave");
        Require(wallet.Balance==184&&completed==1&&cleared==1,"Repeated completion cannot pay twice");
    }
}

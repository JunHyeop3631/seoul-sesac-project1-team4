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
public static class WhiteTigerCombatVerification
{
    const string Key = "Yut.WhiteTigerCombatVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static WhiteTigerCombatVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Tiger combat verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Four directional sectors, inclusive radius/angle boundaries, invalid/dead/escaped targets, duplicates, natural hit events, pause, effect completion, two-second cadence, wave stop and pool reuse passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WhiteTigerCombatVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static void Call(object o,string method,params object[] args)
    { for(Type type=o.GetType();type!=null;type=type.BaseType) { var m=type.GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(m!=null){m.Invoke(o,args);return;} }throw new Exception(method); }
    static Vector3 Offset(Vector3 origin,float angle,float distance) => origin + new Vector3(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad),0)*distance;
    static IEnumerator Check()
    {
        yield return null;yield return null;
        Find<TurnController>().enabled=false;Find<EnemyKillRewards>().enabled=false;
        var wave=Find<Wave>();var enemies=Find<EnemyPool>();var allies=Find<AllyPool>();
        var prefab=AssetDatabase.LoadAssetAtPath<WhiteTiger>("Assets/Prefabs/WhiteTiger.prefab");
        var controller=prefab.GetComponentInChildren<Animator>().runtimeAnimatorController;
        Require(controller!=null && controller.animationClips.Select(c=>c.name).Distinct().Count()==4,"Four actual idle/attack clips");
        foreach(var clip in controller.animationClips.Where(c=>c.name.StartsWith("Attack")))
        {
            var keys=AnimationUtility.GetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"));
            Require(keys.Select(k=>k.value).Distinct().Count()==9 && Mathf.Abs(clip.length-2)<.01f,"Nine attack frames over two seconds");
            Require(clip.events.Length==1 && clip.events[0].functionName=="ApplyAttack","One real strike event per clip");
        }
        var data=ScriptableObject.CreateInstance<WaveData>();
        Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=10,health=1000}});
        Time.captureDeltaTime=.01f; Time.timeScale=1;
        Vector3 origin=GameObject.Find("Cells").GetComponentsInChildren<YutCell>().Single(c=>c.CellId==22).transform.position;
        foreach(string scenario in new[]{"right","left","up_right","down_left","flip","dead_primary","escaped_primary","out_primary","pause","stopped_before_hit"})
        {
            float angle=scenario=="left"?180:scenario=="up_right"?45:scenario=="down_left"?225:0;
            wave.StartWave(data,enemies,0);while(wave.AliveEnemyCount<10)yield return null;
            var targets=Get<HashSet<Enemy>>(wave,"spawnedEnemies").ToArray();
            var arrival=Get<Action<Enemy>>(targets[8],"onArrived");
            var primaryArrival=Get<Action<Enemy>>(targets[0],"onArrived");
            foreach(var enemy in targets)enemy.StopMoving();
            float[] distances={.3f,2,3.5f,2,2,3.501f,2,1,1,.9f};
            float[] angles={0,59.9f,0,60,60.1f,0,180,0,0,0};
            for(int i=0;i<targets.Length;i++)targets[i].transform.position=Offset(origin,angle+angles[i],distances[i]);
            targets[7].TakeDamage(int.MaxValue);arrival(targets[8]);
            var extra=targets[9].gameObject.AddComponent<CircleCollider2D>();extra.radius=.1f;extra.isTrigger=true;
            var tiger=(WhiteTiger)allies.Request(prefab,allies.transform);tiger.transform.position=origin;
            Call(tiger,"SetTargetingWave",wave);Physics2D.SyncTransforms();
            double begin=Time.timeAsDouble;
            while(Get<double>(tiger,"nextAttackTime")==0){Require(Time.timeAsDouble-begin<.1,"Ready first attack");yield return null;}
            Require(tiger.CurrentTarget==targets[0],"Nearest original target selected");
            var animator=tiger.GetComponentInChildren<Animator>();
            Require(animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(Mathf.Cos(angle*Mathf.Deg2Rad)>0?"AttackRight":"AttackLeft"),"Facing matches target");
            if(scenario=="flip")
            {
                for(int i=0;i<targets.Length;i++)targets[i].transform.position=Offset(origin,180+angles[i],distances[i]);
                angle=180;
            }
            if(scenario=="dead_primary")targets[0].TakeDamage(int.MaxValue);
            if(scenario=="escaped_primary")primaryArrival(targets[0]);
            if(scenario=="out_primary")targets[0].transform.position=Offset(origin,0,5);
            Physics2D.SyncTransforms();
            var before=targets.Select(e=>e.CurrentHealth).ToArray();
            if(scenario=="pause")
            {
                Time.timeScale=0;float progress=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                tiger.ApplyAttack();for(int i=0;i<4;i++)yield return null;
                Require(!Get<bool>(tiger,"released") && targets.Select(e=>e.CurrentHealth).SequenceEqual(before),"Paused event does no damage");
                Require(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime-progress)<.001f,"Paused animation does not advance");
                Time.timeScale=1;
            }
            if(scenario=="stopped_before_hit")
            {
                wave.StopWave();tiger.ApplyAttack();
                Require(targets.Select(e=>e.CurrentHealth).SequenceEqual(before),"Stopped-wave late event does no damage");
                Require(!Get<WhiteTigerSlash>(tiger,"slash").IsPlaying,"Stopped-wave effect is hidden");
                allies.Return(tiger);UnityEngine.Object.Destroy(extra);continue;
            }
            double deadline=Get<double>(tiger,"nextAttackTime");
            while(!Get<bool>(tiger,"released")){if(Time.timeAsDouble>deadline+.1)throw new Exception("Strike event never occurred");yield return null;}
            Require(Math.Abs((Time.timeAsDouble-begin)-2*5f/9)<.04,"Natural animation strikes at sixth frame");
            for(int i=0;i<targets.Length;i++)
            {
                bool hit=i==0||i==1||i==2||i==3||i==9;
                if(i==0 && (scenario=="dead_primary"||scenario=="escaped_primary"||scenario=="out_primary"))hit=false;
                Require(Mathf.Abs(before[i]-targets[i].CurrentHealth-(hit?100:0))<.01f,scenario+" sector damage role "+i);
            }
            Require(tiger.CurrentTarget==(scenario=="dead_primary"||scenario=="escaped_primary"||scenario=="out_primary"?targets[9]:targets[0]),"Valid target retained or nearest replacement used");
            var slash=Get<WhiteTigerSlash>(tiger,"slash");Require(slash.IsPlaying,"Strike visual plays at damage event");
            var after=targets.Select(e=>e.CurrentHealth).ToArray();
            tiger.ApplyAttack();tiger.GetComponentInChildren<AllyAnimationEvents>().ApplyAttack();
            Require(targets.Select(e=>e.CurrentHealth).SequenceEqual(after),"Repeated animation events cannot double-hit");
            if(scenario=="right")
            {
                double captureAt=Time.timeAsDouble+.25;while(Time.timeAsDouble<captureAt)yield return null;
                Capture("WhiteTigerStrike");
                Time.timeScale=0;var effectRenderer=slash.GetComponent<SpriteRenderer>();Vector3 position=slash.transform.position;Sprite frame=effectRenderer.sprite;
                for(int i=0;i<4;i++)yield return null;
                Require(slash.transform.position==position && effectRenderer.sprite==frame,"Pause freezes moving slash");Time.timeScale=1;
            }
            while(Time.timeAsDouble<deadline-.04)yield return null;
            Require(targets.Select(e=>e.CurrentHealth).SequenceEqual(after),"No damage during travelling effect or before next attack");
            Require(!slash.IsPlaying,"Slash completes and hides");
            while(Get<double>(tiger,"nextAttackTime")==deadline)yield return null;
            Require(Math.Abs(Get<double>(tiger,"nextAttackTime")-deadline-2)<.03,"Successive attacks start two seconds apart");
            wave.StopWave();Require(!slash.IsPlaying && tiger.CurrentTarget==null && Get<double>(tiger,"nextAttackTime")==0,"Wave end clears combat, effect and tiger first-attack deadline");
            tiger.ApplyAttack();Require(targets.Select(e=>e.CurrentHealth).SequenceEqual(after),"Wave-end stale event does no damage");
            allies.Return(tiger);var reused=(WhiteTiger)allies.Request(prefab,allies.transform);
            Require(reused==tiger,"Actual tiger pool reuse");reused.ApplyAttack();Require(!Get<WhiteTigerSlash>(reused,"slash").IsPlaying,"Reused tiger cannot release stale attack");allies.Return(reused);
            UnityEngine.Object.Destroy(extra);
        }
        Time.captureDeltaTime=0;Time.timeScale=1;
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/WhiteTigerCombat");
        var camera = Camera.main; var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float plane = canvas.planeDistance;
        bool sorting = canvas.overrideSorting; int order = canvas.sortingOrder;
        var target = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(1920, 1080, 24); var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 1; canvas.overrideSorting = true; canvas.sortingOrder = 32767;
            for (int i = 0; i < 2; i++) { Canvas.ForceUpdateCanvases(); camera.Render(); }
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            File.WriteAllBytes("Verification/WhiteTigerCombat/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

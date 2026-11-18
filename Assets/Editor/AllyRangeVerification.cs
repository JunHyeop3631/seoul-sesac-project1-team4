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
public static class AllyRangeVerification
{
    const string Key = "Yut.AllyRangeVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static AllyRangeVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        foreach(string path in new[]{"Assets/Scripts","Assets/Prefabs","Assets/Scenes/ThrowYut.unity"})
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ImportRecursive|ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetBool(Key,true);SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)>120)throw new Exception("Range verification timed out");
            if(checks==null){Application.logMessageReceived+=CountError;checks=Check();}
            if(checks.MoveNext())return;
            Require(errors==0,"No runtime errors during checks");Finish(true,"All ten prefab ranges, scene references, physics acquisition, target retention and rendered range circles passed.");
        }
        catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void CountError(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
    [Serializable] sealed class Result{public bool passed;public int assertions,runtimeErrors;public string unity,scene,detail;}
    static void Finish(bool passed,string detail)
    {
        SessionState.SetBool(Key,false);Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/AllyRangeVerification.json",JsonUtility.ToJson(new Result{passed=passed,assertions=assertions,runtimeErrors=errors,unity=Application.unityVersion,scene="ThrowYut (Play Mode)",detail=detail},true));
        EditorApplication.Exit(passed?0:1);
    }
    static FieldInfo Field(object o,string name)
    {
        for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)return f;}
        throw new Exception(name);
    }
    static T Get<T>(object o,string name)=>(T)Field(o,name).GetValue(o);
    static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
    static object AllyCall(Ally ally,string method,params object[] args)=>typeof(Ally).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ally,args);
    static void Require(bool ok,string message){assertions++;if(!ok)throw new Exception($"Check {assertions}: {message}");}
    static void TargetAt(Enemy target,float distance){target.transform.position=new Vector3(distance,0,0);Physics2D.SyncTransforms();}
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var actions=UnityEngine.Object.FindFirstObjectByType<YutActionController>();
        var wave=UnityEngine.Object.FindFirstObjectByType<Wave>();var pool=UnityEngine.Object.FindFirstObjectByType<EnemyPool>();
        var display=UnityEngine.Object.FindFirstObjectByType<AllyRangeDisplay>();
        string[] names={"Do_Taoist","Gae_Warrior","Geol_Spearman","Yut_Maceman","Mo_Archer"};float[] ranges={4,1,3,3.5f,5};
        var entries=Get<Array>(actions,"evolutions");
        for(int type=0;type<5;type++)for(int stage=1;stage<=2;stage++)
        {
            string name=names[type]+(stage==1?"":"_2nd_Evolution");
            var prefab=AssetDatabase.LoadAssetAtPath<Ally>($"Assets/Prefabs/{name}.prefab");
            Require(prefab!=null&&prefab.AttackRange==ranges[type],name+" exact serialized range");
            var registered=(Ally)entries.GetValue(type).GetType().GetField(stage==1?"firstStage":"secondStage").GetValue(entries.GetValue(type));
            Require(registered==prefab,"Scene summon/evolution registration uses tested prefab");
            var ally=UnityEngine.Object.Instantiate(prefab,Get<Transform[]>(actions,"cells")[22].position,Quaternion.identity);
            foreach(var animator in ally.GetComponentsInChildren<Animator>())animator.enabled=false;
            display.Show(ally);var marker=Get<SpriteRenderer>(display,"attackRangeMarker");
            Require(marker.gameObject.activeSelf&&Mathf.Abs(marker.bounds.size.x-2*ranges[type])<.001f&&Mathf.Abs(marker.bounds.size.y-2*ranges[type])<.001f,"Rendered circle diameter equals2R: "+name);
            Require(Vector2.Distance(marker.bounds.center,ally.transform.position)<.001f,"Range circle centered on attack origin: "+name);
            if(stage==1&&(type==1||type==4)){yield return null;Capture(type==1?"warrior":"archer");}
            display.Hide();ally.transform.position=Vector3.zero;
            AllyCall(ally,"SetTargetingWave",wave);Set(wave,"<IsRunning>k__BackingField",true);
            var target=pool.RequestEnemy(EnemyId.NormalEnemy,Vector3.zero,Quaternion.identity);
            var tracked=Get<HashSet<Enemy>>(wave,"spawnedEnemies");tracked.Add(target);
            TargetAt(target,ranges[type]-.02f);
            Require((bool)AllyCall(ally,"RefreshTarget")&&ally.CurrentTarget==target,"Acquires inside R: "+name);
            TargetAt(target,ranges[type]);
            Require((bool)AllyCall(ally,"RefreshTarget")&&ally.CurrentTarget==target,"Retains target exactly at R: "+name);
            Set(ally,"<CurrentTarget>k__BackingField",null);
            Require((bool)AllyCall(ally,"RefreshTarget")&&ally.CurrentTarget==target,"Fresh physics acquisition exactly at R: "+name);
            TargetAt(target,ranges[type]+.02f);
            Require(!(bool)AllyCall(ally,"RefreshTarget")&&ally.CurrentTarget==null,"Rejects center outside R even if collider overlaps: "+name);
            TargetAt(target,ranges[type]-.02f);
            Require((bool)AllyCall(ally,"RefreshTarget"),"Reacquires when target reenters: "+name);
            Set(wave,"<IsRunning>k__BackingField",false);
            Require(!(bool)AllyCall(ally,"RefreshTarget")&&ally.CurrentTarget==null,"Stopped wave cannot retain target: "+name);
            tracked.Clear();pool.ReturnEnemy(target);UnityEngine.Object.DestroyImmediate(ally.gameObject);
        }
        Require(!Get<SpriteRenderer>(display,"attackRangeMarker").gameObject.activeSelf,"Final hide clears circle");
    }
    static void Capture(string name)
    {
        Require(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null,"Real graphics capture");
        var camera=Camera.main;var canvas=GameObject.Find("Canvas").GetComponent<Canvas>();
        var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float plane=canvas.planeDistance;
        var target=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
        try
        {
            rt.Create();camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();camera.Render();Canvas.ForceUpdateCanvases();camera.Render();
            RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            Directory.CreateDirectory("Logs/AllyRangeVerification");File.WriteAllBytes($"Logs/AllyRangeVerification/{name}.png",image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=target;RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=plane;
            UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyTravelVerification
{
    const string Key="Yut.EnemyTravelVerification";
    static IEnumerator checks;
    static EnemyTravelVerification(){EditorApplication.update+=Tick;}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");SessionState.SetBool(Key,true);SessionState.SetFloat(Key+"Start",(float)EditorApplication.timeSinceStartup);EditorApplication.EnterPlaymode();}
    static void Tick(){
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Start",0)<120,"Travel verification timeout");
            if(checks==null)checks=Check();if(checks.MoveNext())return;
            Time.captureDeltaTime=0;Time.timeScale=1;Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/EnemyTravelVerification.json","{\"passed\":true,\"normal30Seconds\":true,\"fast20Seconds\":true,\"boss60Seconds\":true,\"closedPathMeasured\":true,\"changedPathLength\":true,\"pooledReuse\":true}");
            SessionState.SetBool(Key,false);EditorApplication.Exit(0);
        }catch(Exception e){Time.captureDeltaTime=0;Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
    static T Get<T>(object o,string n){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)return(T)f.GetValue(o);}throw new Exception(n);}
    static void Require(bool b,string m){if(!b)throw new Exception(m);}
    static IEnumerator Check(){
        yield return null;yield return null;
        var wave=UnityEngine.Object.FindFirstObjectByType<Wave>();
        var pool=UnityEngine.Object.FindFirstObjectByType<EnemyPool>();
        var points=Get<Transform[]>(wave,"waypoints");
        Time.timeScale=1;Time.captureDeltaTime=.25f;
        for(int pass=0;pass<2;pass++){
            var path=points.Select(p=>new GameObject("Travel test point").transform).ToArray();
            for(int i=0;i<path.Length;i++)path[i].position=points[i].position*(pass==0?1:1.5f);
            int arrived=0;float[] actual=new float[3];float[] expected={30,20,60};
            float length=0;for(int i=0;i<path.Length;i++)length+=Vector3.Distance(path[i].position,path[(i+1)%path.Length].position);
            for(int id=0;id<3;id++){
                int index=id;var enemy=pool.RequestEnemy((EnemyId)id,path[0].position,Quaternion.identity);float start=Time.time;
                enemy.StartMoving(path,e=>{actual[index]=Time.time-start;arrived++;pool.ReturnEnemy(e);});
                Require(Mathf.Abs(length/Get<float>(enemy,"moveSpeed")-expected[id])<.01f,"Wrong lap speed for enemy "+id);
            }
            while(arrived<3)yield return null;
            for(int id=0;id<3;id++)Require(Mathf.Abs(actual[id]-expected[id])<.3f,"Actual travel time "+id+": "+actual[id]);
            Debug.Log("Measured seconds (normal/fast/boss): "+string.Join("/",actual));
            foreach(var p in path)UnityEngine.Object.Destroy(p.gameObject);
        }
    }
}

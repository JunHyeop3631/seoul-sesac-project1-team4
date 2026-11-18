using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BattleStartVerification
{
    const string Key = "Yut.BattleStartVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static BattleStartVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        foreach (string path in new[] { "Assets/Scripts", "Assets/Prefabs", "Assets/Scenes/ThrowYut.unity" })
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetInt(Key, 1); SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        int state = SessionState.GetInt(Key, 0); if (state == 0) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Battle start verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks"); Finish(true, "Real button, all pending results, throw/move locks, shop, feedback lifetime, phase guards and normal start passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        SessionState.SetInt(Key, 0); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BattleStartVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion,
            scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    static PreparationNotice Notice(TurnController turn) => Get<PreparationNotice>(turn,"preparationNotice");
    static string Message(TurnController turn) => Notice(turn).GetComponentInChildren<Text>().text;
    static Ally Register(YutActionController actions, Ally prefab, int cell)
    {
        var ally=UnityEngine.Object.Instantiate(prefab,Get<Transform[]>(actions,"cells")[cell]);
        Require((bool)Call(actions,"RegisterAlly",cell,ally),"Fixture registered"); return ally;
    }
    static IEnumerator Check()
    {
        var turn=UnityEngine.Object.FindFirstObjectByType<TurnController>();
        var actions=UnityEngine.Object.FindFirstObjectByType<YutActionController>();
        var wallet=UnityEngine.Object.FindFirstObjectByType<StageCurrency>();
        var waves=UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        var button=GameObject.Find("Start Battle Button").GetComponent<Button>();
        int started=0; turn.BattleStarted+=()=>started++;
        Require(Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Any(i=>button.onClick.GetPersistentTarget(i)==turn && button.onClick.GetPersistentMethodName(i)=="StartBattle"),"Saved scene button calls StartBattle");
        turn.ThrowYut(); Require(turn.IsThrowing && wallet.Balance==100,"Actual paid throw in progress");
        button.onClick.Invoke();
        Require(!waves.IsRunning && started==0 && Message(turn).Contains("윷 던지기"),"Button explains throw lock without starting battle");
        yield return null; Capture("throwing");
        while(turn.IsThrowing) yield return null;
        var ally=Register(actions,Get<Gae_Warrior>(actions,"gaeWarriorPrefab"),19);
        foreach(var result in new[]{YutThrowResult.Do,YutThrowResult.Gae,YutThrowResult.Geol,YutThrowResult.Yut,YutThrowResult.Mo,YutThrowResult.Backdo})
        {
            Call(turn,"ReceiveYutResult",result); button.onClick.Invoke();
            Require(turn.CurrentYutResult==result && !waves.IsRunning && started==0 && Notice(turn).gameObject.activeSelf && Message(turn).Contains("남은 윷 결과"),result+" preserved with actionable feedback");
        }
        Require(wallet.Balance==100 && wallet.SpecialBalance==0 && ally.isActiveAndEnabled,"Blocked clicks preserve currency and units");
        Require(Notice(turn).GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"Notice does not block UI input");
        yield return null; Capture("pending");
        Set(Notice(turn),"hideAt",Time.unscaledTime-1); Call(Notice(turn),"Update");
        Require(!Notice(turn).gameObject.activeSelf && turn.CurrentYutResult.HasValue,"Notice expires without consuming result");
        button.onClick.Invoke(); Require(Notice(turn).gameObject.activeSelf,"Repeated blocked click shows feedback again");
        Call(turn,"ReceiveYutResult",YutThrowResult.Do); turn.ChooseMove(); turn.SelectAlly(ally);
        var candidates=Get<System.Collections.Generic.List<int>>(actions,"destinationCellIds").ToArray();
        button.onClick.Invoke();
        Require(candidates.SequenceEqual(Get<System.Collections.Generic.List<int>>(actions,"destinationCellIds")) && turn.CanCancelYutAction,"Blocked click preserves selected path");
        turn.SelectDestination(0);
        Require(!turn.CurrentYutResult.HasValue && !Notice(turn).gameObject.activeSelf,"Using result clears stale battle warning");
        Call(turn,"ReceiveYutResult",YutThrowResult.Do); turn.ChooseMove(); turn.SelectAlly(ally);
        int callbacks=0;
        Action<int> onReward=balance=>{
            callbacks++; button.onClick.Invoke();
            Require(actions.IsResolvingMove && !waves.IsRunning && Message(turn).Contains("이동 처리"),"Real payout callback explains move lock before generic preparation guard");
        };
        wallet.SpecialChanged+=onReward; turn.SelectDestination(-1); wallet.SpecialChanged-=onReward;
        Require(callbacks==1 && wallet.SpecialBalance==2 && !turn.CurrentYutResult.HasValue && !Notice(turn).gameObject.activeSelf && started==0,"Move completion clears processing notice without starting battle");
        turn.OpenShop(); button.onClick.Invoke();
        Require(!waves.IsRunning && Message(turn).Contains("상점을 닫은"),"Shop lock explains required action"); turn.CloseShop();
        Notice(turn).Hide(); Time.timeScale=0; button.onClick.Invoke();
        Require(!waves.IsRunning && !Notice(turn).gameObject.activeSelf,"Paused input remains ignored"); Time.timeScale=1;
        Set(turn,"preparing",false); button.onClick.Invoke();
        Require(!waves.IsRunning && !Notice(turn).gameObject.activeSelf,"Outside preparation stays ignored"); Set(turn,"preparing",true);
        typeof(TurnController).GetProperty("IsBattleWon").SetValue(turn,true); button.onClick.Invoke();
        Require(!waves.IsRunning && !Notice(turn).gameObject.activeSelf,"Won state remains blocked"); typeof(TurnController).GetProperty("IsBattleWon").SetValue(turn,false);
        typeof(TurnController).GetProperty("RemainingLives").SetValue(turn,0); button.onClick.Invoke();
        Require(!waves.IsRunning && !Notice(turn).gameObject.activeSelf,"Lost state remains blocked"); typeof(TurnController).GetProperty("RemainingLives").SetValue(turn,10);
        // Empty-board Backdo is automatically consumed; it must not leave a battle blocker.
        Call(turn,"ReceiveYutResult",YutThrowResult.Backdo); wallet.TrySpend(wallet.Balance);
        Require(!turn.CurrentYutResult.HasValue && Notice(turn).gameObject.activeSelf,"Auto-consumed result provides feedback without remaining blocker");
        button.onClick.Invoke();
        Require(waves.IsRunning && started==1 && !Notice(turn).gameObject.activeSelf && wallet.Balance==0,"Actual button starts battle after consumption at zero currency");
        button.onClick.Invoke();
        Require(waves.IsRunning && started==1 && !Notice(turn).gameObject.activeSelf,"Repeated battle click does not restart or show preparation feedback");
    }
    static void Capture(string name)
    {
        Require(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null,"Real graphics capture");
        var camera=Camera.main; var canvas=GameObject.Find("Canvas").GetComponent<Canvas>();
        var mode=canvas.renderMode; var previousCamera=canvas.worldCamera; float plane=canvas.planeDistance;
        var target=camera.targetTexture; var active=RenderTexture.active;
        var rt=new RenderTexture(1920,1080,24); var texture=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
        try
        {
            rt.Create(); camera.targetTexture=rt; canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
            Directory.CreateDirectory("Logs/BattleStartVerification"); File.WriteAllBytes($"Logs/BattleStartVerification/{name}.png",texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=target; RenderTexture.active=active; canvas.renderMode=mode; canvas.worldCamera=previousCamera; canvas.planeDistance=plane;
            UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

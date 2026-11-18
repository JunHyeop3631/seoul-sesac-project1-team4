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
public static class EvolutionButtonPolicyVerification
{
    const string Key = "Yut.EvolutionButtonPolicyVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static EvolutionButtonPolicyVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Evolution button policy timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Five real one/two-star families and tiger, shortage/no-op/no-notice, actual pointer evolution, hidden stale callbacks, result/throw/pause/summon/terminal/battle guards and preparation recovery passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/EvolutionButtonPolicyVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static Ally[] At(YutActionController a,int cell)=>((IReadOnlyList<Ally>)Call(Get<YutBoardOccupancy>(a,"occupancy"),"GetAllies",cell)).ToArray();
    static Ally Register(YutActionController a,Ally prefab,int cell)
    {var unit=Find<AllyPool>().Request(prefab,Get<Transform[]>(a,"cells")[cell]);Require((bool)Call(a,"RegisterAlly",cell,unit),"Actual prefab registered");return unit;}
    static void Clear(YutActionController a,int cell)
    {var units=At(a,cell);if(units.Length==0)return;Find<AllyRangeDisplay>().Hide();Require((bool)Call(Get<YutBoardOccupancy>(a,"occupancy"),"TryRemoveAllies",(object)units),"Fixture occupancy removed");foreach(var u in units)Find<AllyPool>().Return(u);}
    static void Select(TurnController t,Ally unit)=>Call(t,"HandleBoardClick",(Vector2)unit.VisualRenderer.bounds.center);
    static IEnumerator Settle(CharacterInfoPanel panel)
    {double end=EditorApplication.timeSinceStartup+2;while(panel.IsTransitioning){if(EditorApplication.timeSinceStartup>end)throw new Exception("Info transition timeout");yield return null;}}
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var t=Find<TurnController>();var a=Find<YutActionController>();var w=Find<StageCurrency>();var range=Find<AllyRangeDisplay>();
        var panel=UnityEngine.Object.FindFirstObjectByType<CharacterInfoPanel>(FindObjectsInactive.Include);
        var buttons=UnityEngine.Object.FindFirstObjectByType<AllyActionButtons>(FindObjectsInactive.Include);var evolve=Get<Button>(buttons,"evolveButton");
        int notifications=0;a.EvolutionRequested+=unit=>notifications++;
        int notices=UnityEngine.Object.FindObjectsByType<PreparationNotice>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        foreach(string name in new[]{"Do_Taoist","Gae_Warrior","Geol_Spearman","Yut_Maceman","Mo_Archer"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/"+name+".prefab");var one=Register(a,prefab,1);Select(t,one);
            Require(evolve.gameObject.activeInHierarchy && !evolve.interactable,"One-star shortage remains visible but disabled "+name);
            int eventsBefore=notifications;var before=At(a,1);evolve.OnPointerClick(pointer);evolve.onClick.Invoke();buttons.RequestEvolution();a.RequestEvolution(one);
            Require(At(a,1).SequenceEqual(before) && one.gameObject.activeSelf && notifications==eventsBefore,"Insufficient material clicks/direct calls do nothing");
            Require(w.Balance==120 && w.SpecialBalance==0 && UnityEngine.Object.FindObjectsByType<PreparationNotice>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==notices,"No spending or failure notice");
            if(name=="Gae_Warrior"){var settle=Settle(panel);while(settle.MoveNext())yield return settle.Current;Capture("EvolutionOneStarShortage");}
            Register(a,prefab,1);Call(buttons,"LateUpdate");Require(evolve.gameObject.activeSelf && !evolve.interactable,"Two materials still visibly disabled");
            Register(a,prefab,1);Call(buttons,"LateUpdate");Require(evolve.gameObject.activeInHierarchy && evolve.interactable && a.CanEvolve(one),"Three matching materials enable real button");
            evolve.OnPointerClick(pointer);
            Require(notifications==eventsBefore+1 && At(a,1).Length==1 && At(a,1)[0].EvolutionStage==2 && !one.gameObject.activeSelf,"Actual pointer click evolves once");
            Require(!evolve.gameObject.activeSelf && !evolve.interactable,"Successful evolution hides stale button immediately");
            var two=At(a,1).Single();Select(t,two);
            Require(panel.CurrentAlly==two && !evolve.gameObject.activeSelf && !a.CanEvolve(two),"Evolved unit info remains available but evolution button is hidden");
            evolve.onClick.Invoke();buttons.RequestEvolution();a.RequestEvolution(two);
            Require(At(a,1).Single()==two && notifications==eventsBefore+1 && w.Balance==120,"Hidden stale callbacks cannot evolve two-star unit");
            if(name=="Gae_Warrior"){var settle=Settle(panel);while(settle.MoveNext())yield return settle.Current;Capture("EvolutionTwoStarHidden");}
            Clear(a,1);
        }
        var tiger=Register(a,AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/WhiteTiger.prefab"),1);Select(t,tiger);
        Require(panel.CurrentAlly==tiger && !evolve.gameObject.activeSelf && !evolve.interactable && !a.CanEvolve(tiger),"Tiger has information and no evolution button despite stage1");
        int tigerEvents=notifications;evolve.onClick.Invoke();buttons.RequestEvolution();a.RequestEvolution(tiger);
        Require(At(a,1).Single()==tiger && notifications==tigerEvents,"Tiger never evolves through hidden or direct callback");
        var wait=Settle(panel);while(wait.MoveNext())yield return wait.Current;Capture("EvolutionTigerHidden");Clear(a,1);
        var gae=AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/Gae_Warrior.prefab");var taoist=AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/Do_Taoist.prefab");
        var selected=Register(a,gae,1);Register(a,taoist,1);Register(a,taoist,1);Register(a,gae,2);Register(a,gae,2);Select(t,selected);
        Require(evolve.gameObject.activeSelf && !evolve.interactable,"Other types or other-cell materials do not enable evolution");Clear(a,1);Clear(a,2);
        selected=Register(a,gae,1);Register(a,gae,1);Register(a,gae,1);Select(t,selected);w.GrantSpecial(45);
        Call(t,"ReceiveYutResult",YutThrowResult.Do);Call(buttons,"LateUpdate");
        Require(evolve.gameObject.activeSelf && !evolve.interactable,"Prepaid result blocks execution without hiding one-star button");
        int guardedEvents=notifications;evolve.onClick.Invoke();Require(At(a,1).Length==3 && t.CurrentYutResult==YutThrowResult.Do && notifications==guardedEvents,"Result-state callback preserves materials/result");
        t.ChooseMove();t.SelectAlly(selected);t.SelectDestination(2);Call(buttons,"LateUpdate");Require(!t.CurrentYutResult.HasValue && evolve.interactable,"Consuming result restores eligibility");
        var throwing=Find<YutThrowController>();typeof(YutThrowController).GetProperty("IsThrowing").SetValue(throwing,true);Call(buttons,"LateUpdate");
        Require(evolve.gameObject.activeSelf && !evolve.interactable,"Throwing keeps one-star shown but disabled");evolve.onClick.Invoke();Require(At(a,2).Length==3 && notifications==guardedEvents,"Throw-state callback cannot evolve");
        typeof(YutThrowController).GetProperty("IsThrowing").SetValue(throwing,false);Call(buttons,"LateUpdate");Require(evolve.interactable,"Throw completion restores button");
        Time.timeScale=0;Call(buttons,"LateUpdate");evolve.onClick.Invoke();Require(!evolve.gameObject.activeInHierarchy && !evolve.gameObject.activeSelf && At(a,2).Length==3,"Pause hides selection and blocks stale callback");Time.timeScale=1;range.Show(selected);
        var summon=Find<SpecialSummonController>();bool guarded=false;Action<Ally> callback=unit=>
        {guarded=true;evolve.onClick.Invoke();Require(a.IsResolvingSummon && !a.CanEvolve(selected) && At(a,2).Length==3,"Special-summon transaction blocks reentrant evolution");};
        summon.Summoned+=callback;Require(summon.TrySummon() && guarded,"Actual summon transaction guard exercised");summon.Summoned-=callback;
        Require(w.Balance==120 && w.SpecialBalance==15 && notifications==guardedEvents,"Blocked evolution never spends either wallet");
        foreach(string state in new[]{"IsBattleWon","IsBattleLost"})
        {
            typeof(TurnController).GetProperty(state).SetValue(t,true);Call(buttons,"LateUpdate");evolve.onClick.Invoke();
            Require(!evolve.gameObject.activeSelf && At(a,2).Length==3 && notifications==guardedEvents,state+" hides and blocks button");
            typeof(TurnController).GetProperty(state).SetValue(t,false);range.Show(selected);
        }
        t.StartBattle();yield return null;evolve.onClick.Invoke();
        Require(Find<WaveManager>().IsRunning && !evolve.gameObject.activeSelf && At(a,2).Length==3 && notifications==guardedEvents,"Actual battle hides button and rejects stale callback");
        Find<WaveManager>().StopWave();Set(t,"preparing",true);range.Show(selected);
        Require(evolve.gameObject.activeInHierarchy && evolve.interactable,"Next preparation restores eligible one-star button");
        selected.gameObject.SetActive(false);Call(buttons,"LateUpdate");evolve.onClick.Invoke();Require(!evolve.gameObject.activeSelf && notifications==guardedEvents,"Inactive target hides and rejects evolution");
        Require(notifications==5 && w.Balance==120 && w.SpecialBalance==15,"Exactly five intended evolutions and no unintended spending");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/EvolutionButtonPolicy");
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
            File.WriteAllBytes("Verification/EvolutionButtonPolicy/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

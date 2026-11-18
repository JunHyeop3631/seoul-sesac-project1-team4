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
public static class WhiteTigerBoardVerification
{
    const string Key = "Yut.WhiteTigerBoardVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static WhiteTigerBoardVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Tiger board verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Tiger prefab, mixed capacity, row layout, solo movement/backward/branch, changed-capacity rejection, no evolution, exit15, previews, pooling and battle input protection passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WhiteTigerBoardVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    static Ally[] At(YutActionController a, int cell) => ((IReadOnlyList<Ally>)Call(Get<YutBoardOccupancy>(a, "occupancy"), "GetAllies", cell)).ToArray();
    static int Pop(YutActionController a, int cell) => (int)Call(Get<YutBoardOccupancy>(a, "occupancy"), "GetPopulation", cell);
    static int[] Candidates(YutActionController a) => Get<List<int>>(a, "destinationCellIds").ToArray();
    static Ally Register(YutActionController a, Ally prefab, int cell)
    {
        var unit = Find<AllyPool>().Request(prefab, Get<Transform[]>(a, "cells")[cell]);
        Require((bool)Call(a, "RegisterAlly", cell, unit), "Fixture registered at " + cell); return unit;
    }
    static void Begin(TurnController t, Ally unit, YutThrowResult result)
    { Call(t, "ReceiveYutResult", result); t.ChooseMove(); t.SelectAlly(unit); }
    static string TextOf(Component component) => string.Join("|", component.GetComponentsInChildren<Text>().Select(t => t.text));
    static ExitDestinationView Exit(YutActionController a) => Get<ExitDestinationView>(a, "exitDestinationView");
    static void Confirm(YutActionController a) => Exit(a).GetComponent<Button>().onClick.Invoke();
    static IEnumerator Check()
    {
        yield return null; yield return null;
        var a = Find<YutActionController>(); var t = Find<TurnController>(); var wallet = Find<StageCurrency>();
        var tiger = AssetDatabase.LoadAssetAtPath<WhiteTiger>("Assets/Prefabs/WhiteTiger.prefab");
        var normal = Get<Gae_Warrior>(a, "gaeWarriorPrefab"); var pool = Find<AllyPool>();
        Require(tiger != null && Get<WhiteTiger>(a, "whiteTigerPrefab") == tiger, "Actual prefab and scene reference");
        Require(tiger.PopulationCost == 3 && !tiger.CanStack && !tiger.CanEvolve && tiger.ExitReward == 15, "Tiger rules");
        Require(tiger.AttackDamage == 100 && tiger.AttackRange == 3.5f && tiger.AttacksPerSecond == .5f, "100 ATK,3.5 range,2-second interval");
        Require(tiger.VisualRenderer != null && tiger.VisualRenderer.sprite != null, "Existing tiger idle sprite connected");
        for (int i=0;i<3;i++) Require(a.DeployAlly(YutThrowResult.Gae), "Normal deployment before tiger");
        Require(a.CanDeploy(tiger) && a.DeployAlly(tiger), "Three normal plus tiger deployment allowed");
        var mixed = At(a,1); var mixedTiger = mixed.Single(u => u is WhiteTiger);
        Require(Pop(a,1)==6 && mixed.Length==4, "Mixed occupancy counts six");
        Require(!a.CanDeploy(tiger) && !a.DeployAlly(tiger) && !a.DeployAlly(YutThrowResult.Do) && At(a,1).Length==4, "Over-capacity placement is atomic");
        Require(mixedTiger.transform.localPosition == new Vector3(0,.25f,0), "Tiger uses whole upper row");
        Require(mixed.Where(u=>!(u is WhiteTiger)).All(u=>u.transform.localPosition.y==-.25f) && mixed.Select(u=>u.transform.localPosition).Distinct().Count()==4, "Normals occupy separate lower row");
        var pair = new[]{Register(a,tiger,5),Register(a,tiger,5)};
        Require(Pop(a,5)==6 && pair[0].transform.localPosition.x==0 && pair[1].transform.localPosition.x==0 && pair[0].transform.localPosition.y>pair[1].transform.localPosition.y, "Two tigers reserve two rows");
        Require(!(bool)Call(Get<YutBoardOccupancy>(a,"occupancy"),"HasSpace",5,3), "Third tiger rejected");
        for(int i=0;i<3;i++) Register(a,normal,6);
        var blocked = Register(a,tiger,10); for(int i=0;i<3;i++) Register(a,normal,11);
        yield return null; Capture("WhiteTigerBoard");
        Begin(t,pair[1],YutThrowResult.Do);
        Require(Get<List<Ally>>(a,"selectedAllies").SequenceEqual(new[]{pair[1]}), "Select second tiger alone");
        Require(TextOf(Get<MovePreviewLabel>(a,"sourcePreview"))=="선택 1기 · 인구 3" && TextOf(Get<MovePreviewLabel[]>(a,"destinationPreviews")[6])=="예상 인구 6/6", "Tiger source and mixed-destination preview");
        t.SelectDestination(6);
        Require(Pop(a,5)==3 && Pop(a,6)==6 && At(a,5).Single()==pair[0] && At(a,6).Contains(pair[1]), "Only selected tiger moves");
        Require(pair[0].transform.localPosition==new Vector3(0,.25f,0), "Remaining tiger compacts upper row");
        Require(!a.CanEvolve(pair[1]) && !pair[1].CanEvolve, "Tiger cannot evolve");
        a.RequestEvolution(pair[1]); Require(At(a,6).Contains(pair[1]), "Evolution request preserves tiger");
        Begin(t,pair[1],YutThrowResult.Backdo); Require(Candidates(a).Contains(5), "Tiger backward candidate"); t.SelectDestination(5);
        Require(At(a,5).Contains(pair[1]) && Pop(a,5)==6 && Pop(a,6)==3, "Backward returns one tiger alongside another");
        Begin(t,blocked,YutThrowResult.Do); Require(Candidates(a).Contains(11), "Population-six destination initially valid");
        Register(a,normal,11);
        Require(!a.MoveSelectedAlly(11) && At(a,10).Contains(blocked) && Pop(a,11)==4 && t.CurrentYutResult.HasValue, "Commit rechecks changed capacity without movement or result loss");
        t.CancelYutAction();
        Begin(t,blocked,YutThrowResult.Do); Require(!Candidates(a).Contains(11), "Four normals exclude incoming tiger"); t.CancelYutAction();
        var exits = new[]{Register(a,tiger,19),Register(a,tiger,19)};
        Begin(t,exits[0],YutThrowResult.Do); t.SelectDestination(0);
        Require(Pop(a,0)==3 && Pop(a,19)==3 && wallet.SpecialBalance==0, "Exact last-cell landing has no reward");
        Begin(t,exits[0],YutThrowResult.Backdo); t.SelectDestination(28);
        Require(At(a,28).Contains(exits[0]) && wallet.SpecialBalance==0, "Backward from last cell has no reward");
        Begin(t,exits[0],YutThrowResult.Gae);
        Require(TextOf(Exit(a)).Contains("특별재화 +15") && Get<List<Ally>>(a,"selectedAllies").Count==1, "Tiger exit preview fifteen");
        t.CancelYutAction(); Confirm(a);
        Require(exits[0].gameObject.activeSelf && Pop(a,28)==3 && wallet.SpecialBalance==0, "Cancel/stale button preserves tiger");
        t.ChooseMove(); t.SelectAlly(exits[0]); Confirm(a);
        Require(!exits[0].gameObject.activeSelf && Pop(a,28)==0 && wallet.SpecialBalance==15 && At(a,19).Single()==exits[1], "One tiger returns and pays fifteen");
        Confirm(a); Require(wallet.SpecialBalance==15, "No duplicate payout");
        Begin(t,exits[1],YutThrowResult.Gae); Confirm(a);
        Require(!exits[1].gameObject.activeSelf && Pop(a,19)==0 && wallet.SpecialBalance==30, "Second tiger exits independently");
        var reused=Register(a,tiger,22);
        Require((reused==exits[0] || reused==exits[1]) && reused.PopulationCost==3 && reused.ExitReward==15, "Pool reuse retains tiger rules");
        Begin(t,reused,YutThrowResult.Yut);
        Require(Candidates(a).SequenceEqual(new[]{16,YutBoard.ExitCellId}) && TextOf(Get<MovePreviewLabel[]>(a,"destinationPreviews")[16])=="예상 인구 3/6" && TextOf(Exit(a)).Contains("특별재화 +15"), "Forward branch and exit preview use single tiger");
        yield return null; Capture("WhiteTigerPreview"); t.CancelYutAction();
        var authored=UnityEngine.Object.Instantiate(tiger,Get<Transform[]>(a,"cells")[18]);
        Require((bool)Call(a,"RegisterAlly",18,authored), "Scene-authored tiger registration");
        Begin(t,authored,YutThrowResult.Geol); Confirm(a);
        Require(!authored.gameObject.activeSelf && wallet.SpecialBalance==45 && Pop(a,18)==0, "Scene-authored tiger gets pool ownership and exit reward");
        Require(wallet.Balance==120 && wallet.SpecialBalance==45 && !Find<WaveManager>().IsRunning, "Development placement never charges special summon or starts combat");
        t.StartBattle();
        Require(!a.CanDeploy(tiger) && !a.DeployAlly(tiger), "Shared placement blocks during battle");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/WhiteTiger");
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
            File.WriteAllBytes("Verification/WhiteTiger/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

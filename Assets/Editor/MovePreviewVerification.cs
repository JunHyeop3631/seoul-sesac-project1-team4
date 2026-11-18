using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class MovePreviewVerification
{
    const string Key = "Yut.MovePreviewVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static MovePreviewVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Preview verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks"); Finish(true, "Population, branches, reward preview/commit, cancel, live refresh and rendering passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        SessionState.SetInt(Key, 0); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/MovePreviewVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion,
            scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    static Ally Register(YutActionController actions, Ally prefab, int cell)
    {
        var ally = UnityEngine.Object.Instantiate(prefab, Get<Transform[]>(actions, "cells")[cell]);
        Require((bool)Call(actions, "RegisterAlly", cell, ally), "Fixture registered"); return ally;
    }
    static void Begin(TurnController turn, Ally ally, YutThrowResult result)
    { Call(turn, "ReceiveYutResult", result); turn.ChooseMove(); turn.SelectAlly(ally); }
    static string TextOf(Component view) => view.GetComponentInChildren<Text>().text;
    static string Destination(YutActionController actions, int cell) => TextOf(Get<MovePreviewLabel[]>(actions, "destinationPreviews")[cell]);
    static int Pop(YutActionController actions, int cell) => (int)Call(Get<YutBoardOccupancy>(actions, "occupancy"), "GetPopulation", cell);
    static int[] Candidates(YutActionController actions) => Get<List<int>>(actions, "destinationCellIds").ToArray();
    static IEnumerator Check()
    {
        var actions = UnityEngine.Object.FindFirstObjectByType<YutActionController>();
        var turn = UnityEngine.Object.FindFirstObjectByType<TurnController>();
        var wallet = UnityEngine.Object.FindFirstObjectByType<StageCurrency>();
        var gae = Get<Gae_Warrior>(actions, "gaeWarriorPrefab"); var taoist = Get<Do_Taoist>(actions, "doTaoistPrefab");
        var group = Enumerable.Range(0, 3).Select(_ => Register(actions, gae, 22)).ToArray();
        Register(actions, taoist, 16); Register(actions, taoist, 16);
        Begin(turn, group[1], YutThrowResult.Yut);
        Require(TextOf(Get<MovePreviewLabel>(actions, "sourcePreview")) == "선택 3기 · 인구 3", "Selected stack count and weighted population");
        Require(Candidates(actions).SequenceEqual(new[] {16, -1}) && Destination(actions, 16) == "예상 인구 5/6", "Normal and exit branch shown together");
        var exit = Get<ExitDestinationView>(actions, "exitDestinationView");
        Require(exit.GetComponentsInChildren<Text>().Any(t => t.text == "특별재화 +6"), "Three Gae preview six");
        Require(Pop(actions, 22) == 3 && Pop(actions, 16) == 2 && wallet.SpecialBalance == 0 && wallet.Balance == 120, "Preview has no population or wallet side effects");
        Require(UnityEngine.Object.FindObjectsByType<MovePreviewLabel>(FindObjectsSortMode.None).All(v => v.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget)), "Labels never intercept clicks");
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        var button = exit.GetComponent<Button>(); button.OnPointerEnter(pointer); button.OnPointerDown(pointer);
        yield return null;
        Require((bool)typeof(Selectable).GetMethod("IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, null), "Live refresh preserves pointer-down state");
        button.OnPointerUp(pointer); button.OnPointerExit(pointer);
        yield return null; Capture("branches");
        Register(actions, taoist, 16); yield return null;
        Require(Destination(actions, 16) == "예상 인구 6/6", "Live destination occupancy refresh");
        Register(actions, taoist, 16); yield return null;
        Require(Candidates(actions).SequenceEqual(new[] {-1}) && !Get<MovePreviewLabel[]>(actions, "destinationPreviews")[16].gameObject.activeSelf, "Newly over-capacity candidate and label disappear");
        turn.CancelYutAction();
        Require(!exit.gameObject.activeSelf && !Get<MovePreviewLabel>(actions, "sourcePreview").gameObject.activeSelf && wallet.SpecialBalance == 0 && Pop(actions, 22) == 3, "Cancel clears all preview without payout");
        turn.ChooseMove(); turn.SelectAlly(group[0]); exit.GetComponent<Button>().onClick.Invoke();
        Require(wallet.SpecialBalance == 6 && Pop(actions, 22) == 0 && group.All(a => !a.gameObject.activeSelf) && !exit.gameObject.activeSelf, "Confirmed exit matches preview and hides UI");
        var movers = Enumerable.Range(0, 3).Select(_ => Register(actions, gae, 5)).ToArray();
        Register(actions, taoist, 6); Register(actions, taoist, 6);
        Begin(turn, movers[0], YutThrowResult.Do);
        Require(Destination(actions, 6) == "예상 인구 5/6" && !exit.gameObject.activeSelf, "Normal-only reselection replaces exit preview");
        yield return null; Capture("population"); turn.SelectDestination(6);
        Require(Pop(actions, 6) == 5 && Pop(actions, 5) == 0 && wallet.SpecialBalance == 6 && !turn.CurrentYutResult.HasValue, "Normal commit matches expected population without exit reward");
        var singleton = Register(actions, gae, 0);
        Begin(turn, singleton, YutThrowResult.Backdo);
        Require(Destination(actions, 19) == "예상 인구 1/6" && Destination(actions, 28) == "예상 인구 1/6" && !exit.gameObject.activeSelf, "Backward branch previews both cells and no exit");
        turn.CancelYutAction();
        // A synthetic population cost verifies the shared weighted path; actual White Tiger remains task25.
        var weighted = Register(actions, taoist, 10);
        typeof(Ally).GetField("populationCost", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weighted, 3);
        Begin(turn, weighted, YutThrowResult.Do);
        Require(TextOf(Get<MovePreviewLabel>(actions, "sourcePreview")) == "선택 1기 · 인구 3" && Destination(actions, 11) == "예상 인구 3/6", "Population is not unit count");
        turn.CancelYutAction();
        foreach (object entry in Get<Array>(actions, "evolutions"))
        foreach (string field in new[] { "firstStage", "secondStage" })
        {
            var prefab = (Ally)entry.GetType().GetField(field).GetValue(entry);
            var unit = Register(actions, prefab, 19); int before = wallet.SpecialBalance;
            Begin(turn, unit, YutThrowResult.Gae);
            Require(exit.GetComponentsInChildren<Text>().Any(t => t.text == $"특별재화 +{prefab.ExitReward}") && wallet.SpecialBalance == before, prefab.name + " reward preview only");
            exit.GetComponent<Button>().onClick.Invoke();
            Require(wallet.SpecialBalance == before + prefab.ExitReward, prefab.name + " payout equals preview");
        }
        var changing = Register(actions, gae, 3); Begin(turn, changing, YutThrowResult.Do);
        Register(actions, gae, 3); yield return null;
        Require(Candidates(actions).Length == 0 && !Get<MovePreviewLabel>(actions, "sourcePreview").gameObject.activeSelf, "Stack membership change clears stale selection");
        Require(wallet.Balance == 120 && !UnityEngine.Object.FindFirstObjectByType<WaveManager>().IsRunning, "Preview never spends ordinary currency or starts battle");
    }
    static void Capture(string name)
    {
        Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "Real graphics capture");
        var camera = Camera.main; var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float plane = canvas.planeDistance;
        var target = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(1920, 1080, 24); var texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        try
        {
            rt.Create(); camera.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
            Directory.CreateDirectory("Logs/MovePreviewVerification"); File.WriteAllBytes($"Logs/MovePreviewVerification/{name}.png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = target; RenderTexture.active = active;
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane;
            UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

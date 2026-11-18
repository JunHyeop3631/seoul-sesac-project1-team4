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

[InitializeOnLoad]
public static class UnavailableResultVerification
{
    const string Key = "Yut.UnavailableResultVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static UnavailableResultVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Unavailable result verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks"); Finish(true, "All results, final candidate checks, button state, notice, no refund, repeat input and preparation recovery passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        SessionState.SetInt(Key, 0); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/UnavailableResultVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion,
            scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    static Ally Register(YutActionController actions, Ally prefab, int cell)
    {
        var ally = UnityEngine.Object.Instantiate(prefab, Get<Transform[]>(actions, "cells")[cell]);
        if (!(bool)Call(actions, "RegisterAlly", cell, ally)) throw new Exception("Fixture registration failed"); return ally;
    }
    static void Remove(YutActionController actions, Ally ally)
    {
        Call(Get<YutBoardOccupancy>(actions, "occupancy"), "TryRemoveAllies", new List<Ally> {ally});
        Get<AllyPool>(actions, "allyPool").Return(ally);
    }
    static void Receive(TurnController turn, YutThrowResult result) => Call(turn, "ReceiveYutResult", result);
    static IEnumerator Check()
    {
        var actions = UnityEngine.Object.FindFirstObjectByType<YutActionController>();
        var turn = UnityEngine.Object.FindFirstObjectByType<TurnController>();
        var wallet = UnityEngine.Object.FindFirstObjectByType<StageCurrency>();
        var thrower = UnityEngine.Object.FindFirstObjectByType<YutThrowController>();
        var normal = new[] {YutThrowResult.Do,YutThrowResult.Gae,YutThrowResult.Geol,YutThrowResult.Yut,YutThrowResult.Mo};
        var gae = Get<Gae_Warrior>(actions, "gaeWarriorPrefab"); var taoist = Get<Do_Taoist>(actions, "doTaoistPrefab");
        // One actual paid throw, then deterministic result delivery exercises all rule branches.
        turn.ThrowYut(); Require(turn.IsThrowing && wallet.Balance == 100, "Actual throw charges twenty once");
        while (turn.IsThrowing) yield return null;
        foreach (var result in normal)
        {
            Receive(turn,result);
            Require(turn.CurrentYutResult == result && actions.CanDeploy(result) && Get<Button>(turn,"summonButton").interactable && !Get<Button>(turn,"moveButton").interactable, result + " summon-only result remains usable");
        }
        Receive(turn,YutThrowResult.Backdo);
        var notice = Get<PreparationNotice>(turn,"preparationNotice");
        Require(!turn.CurrentYutResult.HasValue && notice.gameObject.activeSelf && notice.GetComponentInChildren<Text>().text.Contains("빽도"), "Empty-board Backdo consumed with reason");
        Require(wallet.Balance == 100 && turn.CanThrowYut && !Get<Image>(thrower,"resultImage").enabled && !Get<GameObject>(turn,"actionButtons").activeSelf, "No refund, image/choice cleared, paid throw can resume");
        Require(notice.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget), "Reason never blocks preparation input");
        yield return null; Capture();
        Set(notice,"hideAt",Time.unscaledTime - 1); Call(notice,"Update");
        Require(!notice.gameObject.activeSelf, "Brief notice expires");
        var starters = Enumerable.Range(0,6).Select(_=>Register(actions,gae,1)).ToArray();
        Receive(turn,YutThrowResult.Do);
        Require(turn.CurrentYutResult == YutThrowResult.Do && !actions.CanDeploy(YutThrowResult.Do) && !Get<Button>(turn,"summonButton").interactable && Get<Button>(turn,"moveButton").interactable, "Full deployment cell disables summon but preserves valid move");
        turn.ChooseSummon(); Require(turn.CurrentYutResult == YutThrowResult.Do, "Direct disabled summon does not consume usable result");
        // Disabled components keep registered capacity but cannot be movement sources.
        // Synthetic blockers make all six results unavailable without changing graph rules.
        var blockers = new List<Ally>();
        foreach (int cell in new[] {0,2,3,4,5,6})
        for (int i=0;i<6;i++) { var blocker=Register(actions,taoist,cell); blocker.enabled=false; blockers.Add(blocker); }
        foreach (var result in normal.Concat(new[]{YutThrowResult.Backdo}))
        {
            Require(!actions.CanDeploy(result) && !actions.CanMove(result), result + " no final candidates in blocked fixture");
            Receive(turn,result);
            Require(!turn.CurrentYutResult.HasValue && Get<PreparationNotice>(turn,"preparationNotice").gameObject.activeSelf && wallet.Balance == 100 && wallet.SpecialBalance == 0, result + " automatically consumed with notice and no refund/payout");
        }
        turn.ChooseSummon(); turn.ChooseMove(); turn.SelectDestination(-1); turn.CancelYutAction();
        Require(!turn.CurrentYutResult.HasValue && starters.All(a=>a.isActiveAndEnabled) && wallet.Balance==100, "Repeated actions after auto-consume are harmless");
        foreach(var blocker in blockers) Remove(actions,blocker);
        Receive(turn,YutThrowResult.Do); turn.ChooseMove(); turn.SelectAlly(starters[0]);
        Require(Get<List<int>>(actions,"destinationCellIds").Contains(2), "Candidate remains selectable after unavailable result");
        var changes = Enumerable.Range(0,6).Select(_=>Register(actions,taoist,2)).ToArray();
        foreach(var unit in changes) unit.enabled=false;
        Call(turn,"LateUpdate");
        Require(!turn.CurrentYutResult.HasValue && Get<List<int>>(actions,"destinationCellIds").Count==0 && Get<List<Ally>>(actions,"selectedAllies").Count==0, "Pending result rechecks changed candidates and clears selection");
        foreach(var unit in changes) Remove(actions,unit);
        foreach(var unit in starters) unit.enabled=false;
        var exitUnit=Register(actions,taoist,0);
        foreach(var result in normal)
        {
            Receive(turn,result);
            Require(turn.CurrentYutResult==result && !actions.CanDeploy(result) && actions.CanMove(result), result + " exit-only final move preserves result");
        }
        Receive(turn,YutThrowResult.Do); turn.ChooseMove(); turn.SelectAlly(exitUnit);
        turn.SelectDestination(-1);
        Require(wallet.SpecialBalance==3 && !turn.CurrentYutResult.HasValue, "Retained exit can confirm normally");
        // Guarded phases must not discard a pending result.
        foreach(var unit in starters) unit.enabled=true;
        Receive(turn,YutThrowResult.Do); var saved=turn.CurrentYutResult;
        foreach(var unit in starters) unit.enabled=false;
        Time.timeScale=0; Call(turn,"LateUpdate"); Require(turn.CurrentYutResult==saved, "Pause preserves pending result"); Time.timeScale=1;
        Set(turn,"preparing",false); Call(turn,"LateUpdate"); Receive(turn,YutThrowResult.Backdo);
        Require(turn.CurrentYutResult==saved, "Outside preparation ignores delivery and automatic consumption"); Set(turn,"preparing",true);
        Call(turn,"CompleteYutAction"); wallet.TrySpend(wallet.Balance);
        foreach(var unit in starters) Remove(actions,unit);
        Receive(turn,YutThrowResult.Backdo);
        Require(!turn.CurrentYutResult.HasValue && wallet.Balance==0 && !turn.CanThrowYut && turn.CanUsePreparationInput, "Zero balance still recovers preparation after unavailable result");
        turn.StartBattle();
        Require(UnityEngine.Object.FindFirstObjectByType<WaveManager>().IsRunning && !Get<PreparationNotice>(turn,"preparationNotice").gameObject.activeSelf, "Battle starts after automatic consumption even with zero currency");
    }
    static void Capture()
    {
        Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "Real graphics capture");
        var camera=Camera.main; var canvas=GameObject.Find("Canvas").GetComponent<Canvas>();
        var mode=canvas.renderMode; var previousCamera=canvas.worldCamera; float plane=canvas.planeDistance;
        var target=camera.targetTexture; var active=RenderTexture.active;
        var rt=new RenderTexture(1920,1080,24); var texture=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
        try
        {
            rt.Create(); camera.targetTexture=rt; canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
            Directory.CreateDirectory("Logs/UnavailableResultVerification"); File.WriteAllBytes("Logs/UnavailableResultVerification/reason.png",texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=target; RenderTexture.active=active; canvas.renderMode=mode; canvas.worldCamera=previousCamera; canvas.planeDistance=plane;
            UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

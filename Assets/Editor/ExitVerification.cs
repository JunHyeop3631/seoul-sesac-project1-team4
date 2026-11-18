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

// Batch verification against the saved scene. Never saves runtime fixtures.
[InitializeOnLoad]
public static class ExitVerification
{
    const string Key = "Yut.ExitVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static ExitVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        foreach (string path in new[] { "Assets/Scripts", "Assets/Prefabs", "Assets/Scenes/ThrowYut.unity" })
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);
        Run();
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetInt(Key, 1); SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        int state = SessionState.GetInt(Key, 0); if (state == 0) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120)
                throw new Exception("Exit verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Exit boundary, arrival UI, cancellation, pool/occupancy, rewards, regrouping and shared special balance passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    [Serializable] sealed class Result
    {
        public bool passed; public string unity, scene, detail; public int assertions, runtimeErrors;
    }
    static void Finish(bool passed, string detail)
    {
        SessionState.SetInt(Key, 0); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ExitVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)",
            detail = detail, assertions = assertions, runtimeErrors = errors }, true));
        Debug.Log($"Exit verification: {(passed ? "PASS" : "FAIL")} ({assertions} checks)");
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static void Require(bool ok, string message)
    { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    static Ally[] At(YutActionController actions, int cell) =>
        ((IReadOnlyList<Ally>)Call(Get<YutBoardOccupancy>(actions, "occupancy"), "GetAllies", cell)).ToArray();
    static int Pop(YutActionController actions, int cell) => (int)Call(Get<YutBoardOccupancy>(actions, "occupancy"), "GetPopulation", cell);
    static int[] Candidates(YutActionController actions) => Get<List<int>>(actions, "destinationCellIds").ToArray();
    static Ally Register(YutActionController actions, Ally prefab, int cell)
    {
        var ally = UnityEngine.Object.Instantiate(prefab, Get<Transform[]>(actions, "cells")[cell]);
        if (!(bool)Call(actions, "RegisterAlly", cell, ally)) throw new Exception("Fixture registration failed");
        return ally;
    }
    static void Begin(TurnController turn, Ally ally, YutThrowResult result)
    { Call(turn, "ReceiveYutResult", result); turn.ChooseMove(); turn.SelectAlly(ally); }
    static ExitDestinationView View(YutActionController actions) => Get<ExitDestinationView>(actions, "exitDestinationView");
    static void ConfirmExit(YutActionController actions) => View(actions).GetComponent<Button>().onClick.Invoke();
    static IEnumerator Check()
    {
        var results = new[] { YutThrowResult.Do, YutThrowResult.Gae, YutThrowResult.Geol, YutThrowResult.Yut, YutThrowResult.Mo };
        for (int i = 0; i < results.Length; i++)
        {
            Require(YutBoard.GetDestinationCells(0, results[i]).SequenceEqual(new[] {YutBoard.ExitCellId}), "Last cell exits for forward step " + (i + 1));
            foreach (int cell in new[] {19,28})
                Require(YutBoard.GetDestinationCells(cell, results[i]).SequenceEqual(new[] {i == 0 ? 0 : YutBoard.ExitCellId}), "Stop/overflow at cell " + cell);
            foreach (int cell in new[] {18,27})
                Require(YutBoard.GetDestinationCells(cell, results[i]).SequenceEqual(new[] {i == 0 ? (cell == 18 ? 19 : 28) : i == 1 ? 0 : YutBoard.ExitCellId}),
                    "Exact last-cell landing and larger overflow at cell " + cell);
        }
        Require(YutBoard.CellCount == 29 && YutBoard.GetDestinationCells(22, YutThrowResult.Yut).SequenceEqual(new[] {16,YutBoard.ExitCellId}) &&
            YutBoard.GetDestinationCells(22, YutThrowResult.Mo).SequenceEqual(new[] {17,YutBoard.ExitCellId}), "Branch can offer both normal and exit destinations");
        Require(YutBoard.GetDestinationCells(1, YutThrowResult.Backdo).SequenceEqual(new[] {0}) &&
            YutBoard.GetDestinationCells(0, YutThrowResult.Backdo).SequenceEqual(new[] {19,28}), "Backward graph never includes virtual exit");
        var actions = Find<YutActionController>(); var turn = Find<TurnController>(); var wallet = Find<StageCurrency>();
        var legacy = Find<SpecialCurrency>(); var specialUI = Find<SpecialSummonUI>();
        Require(wallet.SpecialBalance == 0 && legacy.Balance == 0 && wallet.Balance == 120, "Scene starts with one shared special balance at zero");
        var entries = Get<Array>(actions, "evolutions"); int[] rewards = {3,2,3,5,5};
        for (int i = 0; i < entries.Length; i++)
        {
            object entry = entries.GetValue(i);
            var one = (Ally)entry.GetType().GetField("firstStage").GetValue(entry);
            var two = (Ally)entry.GetType().GetField("secondStage").GetValue(entry);
            Require(one.ExitReward == rewards[i] && two.ExitReward == rewards[i] * 3, "Both star levels use reward table row " + i);
        }
        var gae = Get<Gae_Warrior>(actions, "gaeWarriorPrefab"); var taoist = Get<Do_Taoist>(actions, "doTaoistPrefab");
        var exiting = Enumerable.Range(0, 3).Select(_ => Register(actions, gae, 19)).ToArray();
        var lastOccupants = Enumerable.Range(0, 6).Select(_ => Register(actions, taoist, 0)).ToArray();
        Begin(turn, exiting[0], YutThrowResult.Do); turn.SelectDestination(0);
        Require(!Candidates(actions).Contains(YutBoard.ExitCellId) && At(actions, 19).Length == 3 && turn.CurrentYutResult.HasValue && wallet.SpecialBalance == 0,
            "Full last cell blocks exact landing rather than turning it into an exit");
        Begin(turn, exiting[1], YutThrowResult.Gae);
        Require(Candidates(actions).SequenceEqual(new[] {YutBoard.ExitCellId}) && View(actions).gameObject.activeSelf &&
            View(actions).GetComponentInChildren<Text>().text == "도착", "Overflow offers actual arrival button even when last cell is full");
        Call(actions, "TryGetDestinationAt", new object[] { new Vector2(-100,-100), 0 });
        Require(wallet.SpecialBalance == 0 && exiting.All(a => a.isActiveAndEnabled), "Showing or hit-testing exit does not grant or remove anything");
        yield return null;
        Capture();
        turn.CancelYutAction();
        Require(!View(actions).gameObject.activeSelf && At(actions, 19).Length == 3 && turn.CurrentYutResult == YutThrowResult.Gae && wallet.SpecialBalance == 0,
            "Cancellation hides arrival UI and preserves units, result and balance");
        ConfirmExit(actions);
        Require(At(actions, 19).Length == 3 && wallet.SpecialBalance == 0, "Stale arrival-button callback cannot confirm cancelled exit");
        turn.ChooseMove(); turn.SelectAlly(exiting[0]);
        var other = Register(actions, Get<Geol_Spearman>(actions, "geolSpearmanPrefab"), 5);
        turn.SelectAlly(other);
        Require(!View(actions).gameObject.activeSelf && !Candidates(actions).Contains(YutBoard.ExitCellId), "Selecting another unit removes exit candidate");
        turn.SelectAlly(exiting[2]);
        int notifications = 0;
        Action<int> reentrant = amount => {
            notifications++;
            Require(actions.IsResolvingMove && !turn.CanUsePreparationInput && !turn.CanCancelYutAction && !turn.CanEvolve,
                "Exit payout locks preparation before invoking listeners");
            Require(!actions.MoveSelectedAlly(YutBoard.ExitCellId) && !actions.DeployAlly(YutThrowResult.Do), "Reentrant move or summon is rejected");
            turn.SelectDestination(YutBoard.ExitCellId); turn.CancelYutAction(); turn.ThrowYut(); turn.StartBattle();
        };
        wallet.SpecialChanged += reentrant; ConfirmExit(actions); wallet.SpecialChanged -= reentrant;
        Require(wallet.SpecialBalance == 6 && notifications == 1 && wallet.Balance == 120 && !turn.CurrentYutResult.HasValue && !actions.IsResolvingMove,
            "Three Gae allies pay six once, consume result once and release processing lock");
        Require(Pop(actions, 19) == 0 && exiting.All(a => !a.gameObject.activeSelf) && lastOccupants.All(a => a.isActiveAndEnabled),
            "Exited stack returns to pool and clears only its own occupancy");
        ConfirmExit(actions); turn.SelectDestination(YutBoard.ExitCellId);
        Require(wallet.SpecialBalance == 6 && !View(actions).gameObject.activeSelf, "Repeated confirm never grants a second reward");
        Require(legacy.Balance == 6 && Get<SpecialCurrencyDigits>(specialUI, "hudDigits").Value == 6 &&
            Get<SpecialCurrencyDigits>(specialUI, "ownedDigits").Value == 6, "Stage reward synchronizes existing HUD and summon-window balance");
        Begin(turn, lastOccupants[0], YutThrowResult.Mo); ConfirmExit(actions);
        Require(Pop(actions, 0) == 3 && wallet.SpecialBalance == 15 && lastOccupants.Take(3).All(a => !a.gameObject.activeSelf) &&
            ((List<Ally>)Call(Get<YutBoardOccupancy>(actions, "occupancy"), "GetStack", lastOccupants[3])).SequenceEqual(lastOccupants.Skip(3)),
            "Exiting first of two stacks leaves and regroups the other three");
        Require(lastOccupants.Skip(3).All(a => a.transform.localPosition.y > 0), "Remaining stack compacts into freed upper slots");
        Begin(turn, lastOccupants[5], YutThrowResult.Do); ConfirmExit(actions);
        Require(Pop(actions, 0) == 0 && wallet.SpecialBalance == 24, "Remaining stack exits independently and frees last cell");
        var archer = Register(actions, Get<Mo_Archer>(actions, "moArcherPrefab"), 28);
        Begin(turn, archer, YutThrowResult.Do); turn.SelectDestination(0);
        Require(At(actions, 0).Contains(archer) && wallet.SpecialBalance == 24 && !View(actions).gameObject.activeSelf,
            "Exact last-cell landing keeps unit and population without reward");
        Begin(turn, archer, YutThrowResult.Backdo); turn.SelectDestination(28);
        Require(wallet.SpecialBalance == 24 && At(actions, 28).Contains(archer), "Backward movement from last cell is not an exit");
        Begin(turn, archer, YutThrowResult.Geol); ConfirmExit(actions);
        Require(wallet.SpecialBalance == 29 && !archer.isActiveAndEnabled, "Excess distance on diagonal still exits and rewards");
        int expected = 29;
        foreach (object entry in entries)
        foreach (string field in new[] { "firstStage", "secondStage" })
        {
            var prefab = (Ally)entry.GetType().GetField(field).GetValue(entry);
            var unit = Register(actions, prefab, 0); expected += prefab.ExitReward;
            Begin(turn, unit, YutThrowResult.Mo); ConfirmExit(actions);
            Require(wallet.SpecialBalance == expected && !unit.isActiveAndEnabled && Pop(actions, 0) == 0 && !turn.CurrentYutResult.HasValue,
                prefab.name + " actual exit reward matches table and clears occupancy");
        }
        var materials = Enumerable.Range(0, 3).Select(_ => Register(actions, gae, 0)).ToArray();
        actions.RequestEvolution(materials[0]); var evolved = At(actions, 0).Single();
        Require(evolved.EvolutionStage == 2 && evolved.ExitReward == 6, "Evolving three materials preserves their total exit value");
        Begin(turn, evolved, YutThrowResult.Yut); ConfirmExit(actions); expected += 6;
        Require(wallet.SpecialBalance == expected && legacy.Balance == expected, "Evolved exit pays exactly six to the shared balance");
        legacy.Grant(2); expected += 2;
        Require(wallet.SpecialBalance == expected && legacy.Balance == expected, "Existing HUD-facing Grant delegates to the same balance");
        Require(wallet.TrySpendSpecial(1) && legacy.Balance == expected - 1 && Get<SpecialCurrencyDigits>(specialUI, "hudDigits").Value == expected - 1,
            "Special spending also synchronizes existing HUD source");
        Require(Find<WaveManager>().IsRunning == false && turn.RemainingLives == 10 && wallet.Balance == 120,
            "Exit never starts battle, loses hearts, or spends ordinary currency");
    }
    static void Capture()
    {
        Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "Real graphics device is available for arrival UI capture");
        var camera = Camera.main; var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        var mode = canvas.renderMode; var uiCamera = canvas.worldCamera; float plane = canvas.planeDistance;
        var target = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        try
        {
            rt.Create(); camera.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0,0,1920,1080), 0,0); texture.Apply();
            Directory.CreateDirectory("Logs/ExitVerification"); File.WriteAllBytes("Logs/ExitVerification/arrival.png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = target; RenderTexture.active = active;
            canvas.renderMode = mode; canvas.worldCamera = uiCamera; canvas.planeDistance = plane;
            UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

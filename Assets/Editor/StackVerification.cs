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

// Batch only; probes and scene mutations are never saved.
[InitializeOnLoad]
public static class StackVerification
{
    const string Key = "Yut.StackVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static StackVerification() { EditorApplication.update += Tick; }
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
        SessionState.SetInt(Key, 1);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        int state = SessionState.GetInt(Key, 0);
        if (state == 0) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 90)
                throw new Exception("Stack verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Three-unit membership, selected-stack movement, capacity, regrouping and cross-stack evolution passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    [Serializable] sealed class Result
    {
        public bool passed;
        public string unity, scene, detail;
        public int assertions, runtimeErrors;
    }
    static void Finish(bool passed, string detail)
    {
        SessionState.SetInt(Key, 0);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/StackVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)",
            detail = detail, assertions = assertions, runtimeErrors = errors }, true));
        Debug.Log($"Stack verification: {(passed ? "PASS" : "FAIL")} ({assertions} checks)");
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static void Require(bool ok, string message)
    { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    static Ally[] Stack(YutBoardOccupancy board, Ally ally) => ((List<Ally>)Call(board, "GetStack", ally)).ToArray();
    static Ally[] At(YutActionController actions, int cell) =>
        ((IReadOnlyList<Ally>)Call(Get<YutBoardOccupancy>(actions, "occupancy"), "GetAllies", cell)).ToArray();
    static Ally Register(YutActionController actions, Ally prefab, int cell)
    {
        var ally = UnityEngine.Object.Instantiate(prefab, Get<Transform[]>(actions, "cells")[cell]);
        if (!(bool)Call(actions, "RegisterAlly", cell, ally)) throw new Exception("Fixture registration failed");
        return ally;
    }
    static void BeginMove(TurnController turn, Ally ally, YutThrowResult result = YutThrowResult.Do)
    { Call(turn, "ReceiveYutResult", result); turn.ChooseMove(); turn.SelectAlly(ally); }
    static IEnumerator Reload()
    { SceneManager.LoadScene("ThrowYut"); for (int i = 0; i < 8; i++) yield return null; }
    static IEnumerator Check()
    {
        var actions = Find<YutActionController>();
        var entries = Get<Array>(actions, "evolutions");
        Require(entries.Length == 5, "Five normal unit pairs registered");
        foreach (object entry in entries)
        foreach (string stage in new[] { "firstStage", "secondStage" })
        {
            var prefab = (Ally)entry.GetType().GetField(stage).GetValue(entry);
            var board = new YutBoardOccupancy();
            var units = new List<Ally>();
            for (int i = 0; i < 6; i++)
            {
                var unit = UnityEngine.Object.Instantiate(prefab); units.Add(unit);
                Require((bool)Call(board, "TryAddAlly", 0, unit), "Normal prefab fixture fits population limit");
                if (units.Count < 4) continue;
                Ally[] first = units.Take(3).ToArray(), second = units.Skip(3).ToArray();
                Require(first.All(a => Stack(board, a).SequenceEqual(first)) &&
                    second.All(a => Stack(board, a).SequenceEqual(second)),
                    prefab.name + " at count " + units.Count + " has correct disjoint stacks");
            }
            UnityEngine.Object.Destroy(units[1].gameObject);
            yield return null;
            var remaining = units.Where(a => a != null).ToArray();
            Require(Stack(board, remaining[0]).SequenceEqual(remaining.Take(3)) &&
                Stack(board, remaining[4]).SequenceEqual(remaining.Skip(3)), "Removal repartitions surviving stack members");
        }
        var reload = Reload(); while (reload.MoveNext()) yield return null;
        actions = Find<YutActionController>(); var turn = Find<TurnController>();
        for (int i = 0; i < 4; i++) actions.DeployAlly(YutThrowResult.Do);
        var source = At(actions, 1);
        var gae = Get<Gae_Warrior>(actions, "gaeWarriorPrefab");
        for (int i = 0; i < 3; i++) Register(actions, gae, 2);
        BeginMove(turn, source[1]);
        Require(Get<List<Ally>>(actions, "selectedAllies").SequenceEqual(source.Take(3)), "Click selects its first three-unit stack");
        Require(Get<List<int>>(actions, "destinationCellIds").Contains(2), "Three-unit stack fits three spaces even with four same-kind source units");
        turn.SelectDestination(2);
        Require(At(actions, 1).SequenceEqual(source.Skip(3)) && At(actions, 2).Length == 6 && !turn.CurrentYutResult.HasValue,
            "Only clicked stack moves; fourth unit stays and result consumes once");
        actions.DeployAlly(YutThrowResult.Do); actions.DeployAlly(YutThrowResult.Do);
        var boardScene = Get<YutBoardOccupancy>(actions, "occupancy");
        Require(Stack(boardScene, source[3]).Length == 3, "New summons join remaining source stack automatically");
        BeginMove(turn, source[3]); turn.SelectDestination(2);
        Require(At(actions, 1).Length == 3 && At(actions, 2).Length == 6 && turn.CurrentYutResult.HasValue,
            "Capacity failure cannot partially move a stack or consume result");

        reload = Reload(); while (reload.MoveNext()) yield return null;
        actions = Find<YutActionController>(); turn = Find<TurnController>();
        for (int i = 0; i < 3; i++) actions.DeployAlly(YutThrowResult.Do);
        source = At(actions, 1); var doPrefab = Get<Do_Taoist>(actions, "doTaoistPrefab");
        Ally resident0 = Register(actions, doPrefab, 2), resident1 = Register(actions, doPrefab, 2);
        BeginMove(turn, source[2]); turn.SelectDestination(2);
        boardScene = Get<YutBoardOccupancy>(actions, "occupancy");
        Require(Stack(boardScene, resident0).SequenceEqual(new[] { resident0, resident1, source[0] }) &&
            Stack(boardScene, source[2]).SequenceEqual(new[] { source[1], source[2] }), "Arrival merges into 3+2 in destination registration order");
        actions.DeployAlly(YutThrowResult.Do); Ally sixth = At(actions, 1)[0];
        BeginMove(turn, sixth); turn.SelectDestination(2);
        Require(Stack(boardScene, sixth).SequenceEqual(new[] { source[1], source[2], sixth }), "Next arrival completes destination 3+3");
        var cells = Get<Transform[]>(actions, "cells");
        Require(source.All(a => a.transform.parent == cells[2]) && sixth.transform.parent == cells[2], "Moved stack transforms match destination occupancy");
        actions.RequestEvolution(sixth);
        var survivors = new[] { source[0], source[1], source[2] };
        var evolved = At(actions, 2).Single(a => a.EvolutionStage == 2);
        Require(!resident0.isActiveAndEnabled && !resident1.isActiveAndEnabled && !sixth.isActiveAndEnabled && evolved != null,
            "Evolution can consume clicked unit plus materials across stack boundary");
        Require(Stack(boardScene, source[0]).SequenceEqual(survivors) && Stack(boardScene, evolved).SequenceEqual(new[] { evolved }),
            "Evolution repartitions survivors and separates star levels");
        Ally mixed = Register(actions, Get<Gae_Warrior>(actions, "gaeWarriorPrefab"), 2);
        Require(Stack(boardScene, mixed).SequenceEqual(new[] { mixed }) && Stack(boardScene, source[0]).Length == 3,
            "Different unit kinds never join the same stack");

        reload = Reload(); while (reload.MoveNext()) yield return null;
        actions = Find<YutActionController>(); turn = Find<TurnController>();
        actions.DeployAlly(YutThrowResult.Do); actions.DeployAlly(YutThrowResult.Do);
        Ally clicked = At(actions, 1)[0]; BeginMove(turn, clicked);
        actions.DeployAlly(YutThrowResult.Do); turn.SelectDestination(2);
        Require(At(actions, 1).Length == 3 && At(actions, 2).Length == 0 && turn.CurrentYutResult.HasValue &&
            Get<List<Ally>>(actions, "selectedAllies").Count == 0, "Membership change invalidates stale selection without moving a partial stack");
        turn.SelectAlly(clicked); turn.SelectDestination(2);
        Require(At(actions, 2).Length == 3 && !turn.CurrentYutResult.HasValue, "Reselecting refreshed stack allows normal movement");
        source = At(actions, 2);
        actions.SelectAlly(source[0], YutThrowResult.Backdo);
        Require(Get<List<Ally>>(actions, "selectedAllies").Count == 3 && Get<List<int>>(actions, "destinationCellIds").SequenceEqual(new[] { 1 }),
            "Backward graph preserves all three stack members without history state");
        BeginMove(turn, source[2], YutThrowResult.Backdo); turn.SelectDestination(1);
        Require(At(actions, 1).Length == 3 && At(actions, 2).Length == 0 && !turn.CurrentYutResult.HasValue,
            "Allowed backward graph moves all three stack members together");
    }
}

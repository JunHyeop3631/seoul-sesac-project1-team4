using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Batch-only real-scene verification. Runtime probes are never saved to assets.
[InitializeOnLoad]
public static class PopulationVerification
{
    const string Key = "Yut.PopulationVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static PopulationVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        AssetDatabase.ImportAsset("Assets/Scripts", ImportAssetOptions.ImportRecursive |
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset("Assets/Prefabs", ImportAssetOptions.ImportRecursive |
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset("Assets/Scenes/ThrowYut.unity", ImportAssetOptions.ForceUpdate |
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
                throw new Exception("Population verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during verification");
            Finish(true, "Normal prefabs, weighted add/move/replace, candidate filtering, and commit recheck passed.");
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
        File.WriteAllText("Logs/PopulationVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)",
            detail = detail, assertions = assertions, runtimeErrors = errors }, true));
        Debug.Log($"Population verification: {(passed ? "PASS" : "FAIL")} ({assertions} checks)");
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static void Require(bool ok, string message)
    { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static int Pop(YutBoardOccupancy board, int cell) => (int)Call(board, "GetPopulation", cell);
    static bool Add(YutBoardOccupancy board, int cell, Ally ally) => (bool)Call(board, "TryAddAlly", cell, ally);
    static bool Move(YutBoardOccupancy board, Ally[] allies, int cell) => (bool)Call(board, "TryMoveAllies", allies, cell);
    static bool Replace(YutBoardOccupancy board, int cell, Ally[] allies, Ally replacement) =>
        (bool)Call(board, "TryReplaceAllies", cell, allies, replacement);
    static void SetCost(Ally ally, int cost) => typeof(Ally).GetField("populationCost",
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ally, cost);
    static Ally Probe(int cost = 1)
    {
        var ally = new GameObject("Population code probe").AddComponent<Ally>();
        SetCost(ally, cost); return ally;
    }
    static IEnumerator Check()
    {
        var actions = Find<YutActionController>();
        var evolutions = Get<Array>(actions, "evolutions");
        Require(evolutions.Length == 5, "Five ordinary evolution pairs are registered");
        foreach (object entry in evolutions)
        foreach (string stage in new[] { "firstStage", "secondStage" })
        {
            var prefab = (Ally)entry.GetType().GetField(stage).GetValue(entry);
            Require(prefab != null && prefab.PopulationCost == 1, "Registered " + stage + " prefab population is 1");
        }
        var board = new YutBoardOccupancy();
        var normal = new Ally[6];
        for (int i = 0; i < 6; i++) { normal[i] = Probe(); Require(Add(board, 0, normal[i]), "Six normal units fit"); }
        Require(Pop(board, 0) == 6 && !Add(board, 0, Probe()), "Seventh normal unit rejected");
        for (int count = 0; count <= 6; count++)
        {
            var boundary = new YutBoardOccupancy();
            for (int i = 0; i < count; i++) Add(boundary, 0, Probe());
            Require(Add(boundary, 0, Probe(3)) == (count <= 3), "Population3 admission boundary at normal count " + count);
            Require(Pop(boundary, 0) == count + (count <= 3 ? 3 : 0), "Failed admission preserves occupancy");
        }
        var pair = new YutBoardOccupancy();
        Require(Add(pair, 0, Probe(3)) && Add(pair, 0, Probe(3)) && !Add(pair, 0, Probe(3)) && Pop(pair, 0) == 6,
            "Two population3 probes fit; third rejected");
        Require(!(bool)Call(pair, "HasSpace", 0, int.MaxValue) && !(bool)Call(pair, "HasSpace", 1, 0),
            "Huge and nonpositive incoming population rejected without overflow");
        var moving = new YutBoardOccupancy();
        var heavy = Probe(3); var light = Probe();
        Add(moving, 0, heavy); Add(moving, 0, light);
        for (int i = 0; i < 3; i++) Add(moving, 1, Probe());
        Require(!Move(moving, new[] { heavy, light }, 1) && Pop(moving, 0) == 4 && Pop(moving, 1) == 3,
            "Whole group uses population4 rather than unit count2; no partial move");
        Require(Move(moving, new[] { heavy }, 1) && Pop(moving, 0) == 1 && Pop(moving, 1) == 6,
            "Population3 group moves into exactly three remaining spaces");
        Require(Move(moving, new[] { heavy }, 1) && Pop(moving, 1) == 6, "Same-cell move does not count population twice");
        Require(!Move(moving, new[] { light, light }, 2), "Duplicate move entries rejected");
        Require(!Replace(board, 0, new[] { normal[0], normal[1], normal[2] }, Probe(4)) && Pop(board, 0) == 6,
            "Replacement capacity is rechecked before materials are removed");
        Require(Replace(board, 0, new[] { normal[0], normal[1], normal[2] }, Probe()) && Pop(board, 0) == 4,
            "Normal evolution replacement frees two population");
        UnityEngine.Object.Destroy(normal[3].gameObject);
        yield return null;
        Require(Pop(board, 0) == 3 && Add(board, 0, Probe(3)), "Destroyed references release population");

        for (int i = 0; i < 6; i++) Require(actions.DeployAlly(YutThrowResult.Do), "Real scene normal summon up to six");
        var turn = Find<TurnController>();
        Call(turn, "ReceiveYutResult", YutThrowResult.Do);
        turn.ChooseSummon();
        Require(turn.CurrentYutResult == YutThrowResult.Do && !actions.DeployAlly(YutThrowResult.Do),
            "Full start cell rejects summon and preserves unused result");
        SceneManager.LoadScene("ThrowYut");
        for (int i = 0; i < 8; i++) yield return null;
        actions = Find<YutActionController>(); turn = Find<TurnController>();
        for (int i = 0; i < 3; i++) actions.DeployAlly(YutThrowResult.Do);
        var occupancy = Get<YutBoardOccupancy>(actions, "occupancy");
        var cells = Get<Transform[]>(actions, "cells");
        var occupants = (IReadOnlyList<Ally>)Call(occupancy, "GetAllies", 1);
        Ally selected = occupants[0]; SetCost(selected, 3);
        var blockerPrefab = Get<Gae_Warrior>(actions, "gaeWarriorPrefab");
        var blockers = new Ally[2];
        for (int i = 0; i < blockers.Length; i++)
        {
            blockers[i] = UnityEngine.Object.Instantiate(blockerPrefab, cells[2]);
            Require((bool)Call(actions, "RegisterAlly", 2, blockers[i]), "Register destination fixture");
        }
        Call(turn, "ReceiveYutResult", YutThrowResult.Do); turn.ChooseMove(); turn.SelectAlly(selected);
        var destinations = Get<List<int>>(actions, "destinationCellIds");
        Require(!destinations.Contains(2), "Candidate excludes population5 group plus destination population2");
        Move(occupancy, new[] { blockers[0] }, 3); turn.SelectAlly(selected);
        Require(destinations.Contains(2), "Candidate includes total population6");
        Move(occupancy, new[] { blockers[0] }, 2);
        turn.SelectDestination(2);
        Require(selected.transform.parent == cells[1] && Pop(occupancy, 1) == 5 && Pop(occupancy, 2) == 2 &&
            turn.CurrentYutResult.HasValue, "Commit rechecks changed capacity and preserves units and result");
        Move(occupancy, new[] { blockers[0] }, 3); turn.SelectAlly(selected); turn.SelectDestination(2);
        Require(selected.transform.parent == cells[2] && Pop(occupancy, 1) == 0 && Pop(occupancy, 2) == 6 &&
            !turn.CurrentYutResult.HasValue, "Real movement commits entire weighted group and updates occupancy");
        actions.RequestEvolution(selected);
        Require(!selected.isActiveAndEnabled && Pop(occupancy, 2) == 2,
            "Real evolution replaces population5 materials with population1 evolved unit");
    }
}

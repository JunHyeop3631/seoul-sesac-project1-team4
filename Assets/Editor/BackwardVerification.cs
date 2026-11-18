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

// Batch-only verification of task06; runtime fixtures are not saved.
[InitializeOnLoad]
public static class BackwardVerification
{
    const string Key = "Yut.BackwardVerification";
    static IEnumerator checks;
    static int assertions, errors;
    // Independent expected rows from the task01 specification, not derived from runtime edges.
    static readonly int[][] Expected = {
        new[] {19,28}, new[] {0}, new[] {1}, new[] {2}, new[] {3}, new[] {4}, new[] {5},
        new[] {6}, new[] {7}, new[] {8}, new[] {9}, new[] {10}, new[] {11}, new[] {12},
        new[] {13}, new[] {14,24}, new[] {15}, new[] {16}, new[] {17}, new[] {18},
        new[] {5}, new[] {20}, new[] {21,26}, new[] {22}, new[] {23}, new[] {10},
        new[] {25}, new[] {22}, new[] {27}
    };
    static BackwardVerification() { EditorApplication.update += Tick; }
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
                throw new Exception("Backward verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during verification");
            Finish(true, "All 29 backward rows, mixed-route stacks, branches, population recheck, boundary no-reward and evolution passed.");
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
        SessionState.SetInt(Key, 0); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/BackwardVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)",
            detail = detail, assertions = assertions, runtimeErrors = errors }, true));
        Debug.Log($"Backward verification: {(passed ? "PASS" : "FAIL")} ({assertions} checks)");
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
    static int[] Candidates(YutActionController actions) => Get<List<int>>(actions, "destinationCellIds").ToArray();
    static Ally Register(YutActionController actions, Ally prefab, int cell)
    {
        var ally = UnityEngine.Object.Instantiate(prefab, Get<Transform[]>(actions, "cells")[cell]);
        if (!(bool)Call(actions, "RegisterAlly", cell, ally)) throw new Exception("Fixture registration failed");
        return ally;
    }
    static void Begin(TurnController turn, Ally selected, YutThrowResult result = YutThrowResult.Backdo)
    { Call(turn, "ReceiveYutResult", result); turn.ChooseMove(); turn.SelectAlly(selected); }
    // Reposition through the real occupancy API solely to set up independent graph cases.
    static void FixtureAt(YutActionController actions, Ally[] units, int cell)
    {
        if (!(bool)Call(Get<YutBoardOccupancy>(actions, "occupancy"), "TryMoveAllies", units, cell))
            throw new Exception("Fixture move failed");
        foreach (Ally unit in units) unit.transform.SetParent(Get<Transform[]>(actions, "cells")[cell], false);
        Call(actions, "ArrangeAllies", cell);
    }
    static IEnumerator Check()
    {
        Require(YutBoard.DeploymentCellId == 1 && YutBoard.LastCellId == 0, "Spawn and last-cell names match specification");
        for (int cell = 0; cell < 29; cell++)
            Require(YutBoard.GetDestinationCells(cell, YutThrowResult.Backdo).SequenceEqual(Expected[cell]), "Backward graph row " + cell);
        Require(YutBoard.GetDestinationCells(4, YutThrowResult.Geol).SequenceEqual(new[] {7}) &&
            YutBoard.GetDestinationCells(20, YutThrowResult.Geol).SequenceEqual(new[] {23}) &&
            YutBoard.GetDestinationCells(25, YutThrowResult.Geol).SequenceEqual(new[] {27}) &&
            YutBoard.GetDestinationCells(22, YutThrowResult.Gae).SequenceEqual(new[] {24,28}), "Forward corner/center rules remain intact");
        var actions = Find<YutActionController>(); var turn = Find<TurnController>(); var wallet = Find<StageCurrency>();
        Require(!actions.CanMove(YutThrowResult.Backdo), "Empty board has no backward action");
        Call(turn, "ReceiveYutResult", YutThrowResult.Backdo);
        Require(!turn.CurrentYutResult.HasValue, "Empty-board backward result still auto-consumes");
        var prefab = Get<Do_Taoist>(actions, "doTaoistPrefab");
        Ally left0 = Register(actions, prefab, 21), left1 = Register(actions, prefab, 21), right = Register(actions, prefab, 26);
        Begin(turn, left0, YutThrowResult.Do); turn.SelectDestination(22);
        Begin(turn, right, YutThrowResult.Do); turn.SelectDestination(22);
        Ally[] stack = { left0, left1, right };
        Require(At(actions, 22).Length == 3 && actions.CanMove(YutThrowResult.Backdo), "Two different incoming routes merge into one usable stack");
        Begin(turn, right);
        foreach (Ally member in stack)
        {
            turn.SelectAlly(member);
            Require(Get<List<Ally>>(actions, "selectedAllies").Count == 3 && Candidates(actions).SequenceEqual(new[] {21,26}),
                "Every member of mixed-route stack has the same two backward options");
        }
        var markers = Get<SpriteRenderer[]>(actions, "destinationMarkers");
        Require(markers[21].gameObject.activeSelf && markers[26].gameObject.activeSelf,
            "Both center backward options are visibly activated as candidate markers");
        turn.SelectDestination(23);
        Require(turn.CurrentYutResult.HasValue && At(actions, 22).Length == 3, "Forward-only direction cannot be chosen as backward move");
        turn.SelectDestination(21);
        Require(At(actions, 21).Length == 3 && At(actions, 22).Length == 0 && !turn.CurrentYutResult.HasValue,
            "First center branch moves all three and consumes one result");
        Begin(turn, left0, YutThrowResult.Do); turn.SelectDestination(22);
        Begin(turn, left1); turn.SelectDestination(26);
        Require(At(actions, 26).Length == 3 && !turn.CurrentYutResult.HasValue, "Other center branch can be selected after returning forward");

        for (int cell = 0; cell < 29; cell++)
        {
            FixtureAt(actions, stack, cell); Begin(turn, right);
            Require(Candidates(actions).SequenceEqual(Expected[cell]) && Get<List<Ally>>(actions, "selectedAllies").Count == 3,
                "Real controller candidates at cell " + cell + " match graph regardless of earlier route");
        }
        FixtureAt(actions, stack, 1);
        int normal = wallet.Balance, special = wallet.SpecialBalance;
        Begin(turn, left0); turn.SelectDestination(0);
        Require(At(actions, 0).Length == 3 && wallet.Balance == normal && wallet.SpecialBalance == special &&
            stack.All(a => a.isActiveAndEnabled), "Start backward stops at last cell without exit or rewards");
        Begin(turn, right); turn.SelectDestination(19);
        Begin(turn, left1); turn.SelectDestination(18);
        Require(At(actions, 18).Length == 3, "Repeated backward movement follows graph instead of undoing last move");
        FixtureAt(actions, stack, 0); Begin(turn, left0); turn.SelectDestination(28);
        Require(At(actions, 28).Length == 3 && wallet.SpecialBalance == special, "Last-cell diagonal backward branch has no exit reward");
        FixtureAt(actions, stack, 15); Begin(turn, left1);
        Require(Candidates(actions).SequenceEqual(new[] {14,24}), "Lower-left backward branch offers outer and diagonal choices");
        turn.SelectDestination(24);
        Require(At(actions, 24).Length == 3, "Lower-left chosen backward branch moves full stack");
        FixtureAt(actions, stack, 22); Begin(turn, left0);
        FixtureAt(actions, stack, 5); turn.SelectDestination(21);
        Require(At(actions, 5).Length == 3 && At(actions, 21).Length == 0 && turn.CurrentYutResult.HasValue,
            "Moving the source invalidates stale route candidates even when membership is unchanged");
        FixtureAt(actions, stack, 22);
        var gae = Get<Gae_Warrior>(actions, "gaeWarriorPrefab");
        var blockers = new Ally[4];
        for (int i = 0; i < 4; i++) blockers[i] = Register(actions, gae, 21);
        for (int i = 0; i < 3; i++) Register(actions, gae, 26);
        Begin(turn, right);
        Require(Candidates(actions).SequenceEqual(new[] {26}), "Population3 stack rejects two free spaces and accepts exactly three");
        Register(actions, gae, 26); turn.SelectDestination(26);
        Require(At(actions, 22).Length == 3 && At(actions, 26).Length == 4 && turn.CurrentYutResult.HasValue,
            "Capacity change after candidate display rejects whole backward stack and preserves result");
        turn.SelectAlly(left0);
        Require(Candidates(actions).Length == 0, "Both blocked backward destinations disappear on reselection");
        FixtureAt(actions, new[] { blockers[0] }, 20); turn.SelectAlly(left1);
        Require(Candidates(actions).SequenceEqual(new[] {21}), "Freeing three population restores only the permitted available branch");
        turn.SelectDestination(21);
        Require(At(actions, 22).Length == 0 && At(actions, 21).Length == 6 && wallet.Balance == normal && wallet.SpecialBalance == special,
            "Successful backward commit maintains population and both balances");
        SceneManager.LoadScene("ThrowYut"); for (int i = 0; i < 8; i++) yield return null;
        actions = Find<YutActionController>(); turn = Find<TurnController>();
        var entry = Get<Array>(actions, "evolutions").GetValue(0);
        var evolvedPrefab = (Ally)entry.GetType().GetField("secondStage").GetValue(entry);
        var evolved = Register(actions, evolvedPrefab, 0);
        Begin(turn, evolved);
        Require(Candidates(actions).SequenceEqual(new[] {19,28}), "Evolved unit registered without travel history has all backward options");
        turn.SelectDestination(28); Begin(turn, evolved); turn.SelectDestination(27);
        Require(At(actions, 27).Contains(evolved) && !turn.CurrentYutResult.HasValue, "Evolved unit also supports repeated graph backward moves");
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Batch-only task07 checks. Never saves runtime changes or test input settings.
[InitializeOnLoad]
public static class CancelActionVerification
{
    const string Key = "Yut.CancelActionVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static CancelActionVerification() { EditorApplication.update += Tick; }
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
                throw new Exception("Cancellation verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Right-click/Escape cancellation, preserved paid result, summon switching, Backdo limits and phase guards passed.");
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
        File.WriteAllText("Logs/CancelActionVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)",
            detail = detail, assertions = assertions, runtimeErrors = errors }, true));
        Debug.Log($"Cancellation verification: {(passed ? "PASS" : "FAIL")} ({assertions} checks)");
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static void Require(bool ok, string message)
    { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    static Ally[] Allies() => UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsSortMode.None);
    static int[] Destinations(YutActionController actions) => Get<List<int>>(actions, "destinationCellIds").ToArray();
    static Ally[] At(YutActionController actions, int cell) =>
        ((IReadOnlyList<Ally>)Call(Get<YutBoardOccupancy>(actions, "occupancy"), "GetAllies", cell)).ToArray();
    static void ResultFixture(TurnController turn, YutThrowController thrower, YutThrowResult result)
    { Call(thrower, "ShowResult", result); Call(turn, "ReceiveYutResult", result); }
    static void Escape(TurnController turn)
    {
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape)); InputSystem.Update();
            Require(keyboard.escapeKey.wasPressedThisFrame, "Virtual Escape reaches game input");
            Call(turn, "LateUpdate");
        }
        finally { InputSystem.RemoveDevice(keyboard); }
    }
    static void RightClick(TurnController turn)
    {
        var mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right)); InputSystem.Update();
            Require(mouse.rightButton.wasPressedThisFrame, "Virtual right-click reaches game input");
            Call(turn, "LateUpdate");
        }
        finally { InputSystem.RemoveDevice(mouse); }
    }
    static void CheckCancelled(TurnController turn, YutActionController actions, StageCurrency wallet,
        YutThrowResult result, int balance, int special, Dictionary<Ally, Transform> parents)
    {
        Require(turn.CurrentYutResult == result && !turn.CanCancelYutAction &&
            Get<object>(turn, "chosenAction").ToString() == "None", "Cancel returns to choice and preserves exact result");
        Require(Destinations(actions).Length == 0 && Get<List<Ally>>(actions, "selectedAllies").Count == 0 &&
            Get<SpriteRenderer[]>(actions, "destinationMarkers").All(m => m == null || !m.gameObject.activeSelf),
            "Selected stack and all candidate markers are cleared");
        Require(wallet.Balance == balance && wallet.SpecialBalance == special && Allies().Length == parents.Count &&
            parents.All(p => p.Key.isActiveAndEnabled && p.Key.transform.parent == p.Value), "Units, positions and both balances are preserved");
        Require(!turn.CanThrowYut && !Get<GameObject>(turn, "throwButton").activeSelf &&
            Get<GameObject>(turn, "actionButtons").activeSelf && Get<Button>(turn, "moveButton").interactable &&
            Get<Button>(turn, "summonButton").interactable == (result != YutThrowResult.Backdo),
            "Choice controls restore while reroll remains blocked and Backdo summon rule holds");
    }
    static IEnumerator Check()
    {
        InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var turn = Find<TurnController>(); var actions = Find<YutActionController>();
        var thrower = Find<YutThrowController>(); var wallet = Find<StageCurrency>(); var waves = Find<WaveManager>();
        turn.CancelYutAction();
        Require(!turn.CanCancelYutAction && !turn.CurrentYutResult.HasValue && wallet.Balance == 120,
            "Cancel is harmless before any result exists");
        actions.DeployAlly(YutThrowResult.Do); Ally ally = Allies()[0]; wallet.GrantSpecial(9);
        Get<GameObject>(turn, "throwButton").GetComponent<Button>().onClick.Invoke();
        Require(thrower.IsThrowing && wallet.Balance == 100, "Real throw pays its cost before cancellation tests");
        turn.CancelYutAction(); Escape(turn);
        Require(thrower.IsThrowing && !turn.CanCancelYutAction && wallet.Balance == 100, "Cancel cannot interrupt or refund throw animation");
        while (thrower.IsThrowing) yield return null;
        Require(turn.CurrentYutResult.HasValue, "Paid result is available with a movable ally");
        var original = turn.CurrentYutResult.Value;
        var parents = Allies().ToDictionary(a => a, a => a.transform.parent);
        int normalEvents = 0, specialEvents = 0;
        wallet.Changed += _ => normalEvents++; wallet.SpecialChanged += _ => specialEvents++;
        turn.ChooseMove();
        Require(turn.CanCancelYutAction && !Get<Button>(turn, "summonButton").interactable, "Movement mode can be cancelled before selecting an ally");
        RightClick(turn); CheckCancelled(turn, actions, wallet, original, 100, 9, parents);
        turn.ChooseMove(); turn.SelectAlly(ally);
        int staleDestination = Destinations(actions)[0];
        Require(Get<List<Ally>>(actions, "selectedAllies").Count > 0, "Fixture has a selected stack and candidates");
        RightClick(turn); CheckCancelled(turn, actions, wallet, original, 100, 9, parents);
        Require(Get<Image>(thrower, "resultImage").enabled, "Cancel preserves visible real throw result");
        turn.SelectDestination(staleDestination);
        Require(!actions.MoveSelectedAlly(staleDestination) && turn.CurrentYutResult == original && ally.transform.parent == parents[ally],
            "Old candidate callback cannot commit after cancellation");
        turn.ChooseMove(); turn.SelectAlly(ally); Escape(turn);
        CheckCancelled(turn, actions, wallet, original, 100, 9, parents);
        turn.CancelYutAction(); turn.ThrowYut(); thrower.Throw(); turn.StartBattle();
        Require(turn.CurrentYutResult == original && !thrower.IsThrowing && !waves.IsRunning &&
            normalEvents == 0 && specialEvents == 0, "Repeated cancel causes no refunds, reroll, battle entry or currency notifications");
        if (original == YutThrowResult.Backdo)
        {
            turn.ChooseSummon();
            Require(turn.CurrentYutResult == original && Allies().Length == parents.Count, "Cancelled real Backdo cannot summon");
            turn.ChooseMove(); turn.SelectAlly(ally); turn.SelectDestination(Destinations(actions)[0]);
            Require(!turn.CurrentYutResult.HasValue, "Real Backdo remains usable by moving after cancel");
        }
        else
        {
            Get<Button>(turn, "summonButton").onClick.Invoke();
            Require(!turn.CurrentYutResult.HasValue && Allies().Length == parents.Count + 1, "Real paid result switches from movement to summon");
            turn.CancelYutAction();
            Require(!turn.CurrentYutResult.HasValue && Allies().Length == parents.Count + 1, "Completed summon cannot be undone by cancel");
        }
        Require(wallet.Balance == 100 && wallet.SpecialBalance == 9, "Resolving the paid result neither refunds nor charges again");

        // Deterministic normal-result fixture covers the summon transition even if real RNG gave Backdo.
        ResultFixture(turn, thrower, YutThrowResult.Do); turn.ChooseMove(); turn.SelectAlly(ally);
        turn.OpenShop(); turn.SetInventoryOpen(true); Escape(turn);
        Require(Get<GameObject>(turn, "shopPanel").activeSelf && Get<GameObject>(turn, "inventoryPanel").activeSelf,
            "Cancel does not reset unrelated open shop or inventory panels");
        turn.CloseShop(); turn.SetInventoryOpen(false);
        int count = Allies().Length;
        Get<Button>(turn, "summonButton").onClick.Invoke();
        Require(!turn.CurrentYutResult.HasValue && Allies().Length == count + 1 && wallet.Balance == 100,
            "Same normal result summons after Escape without another payment");
        turn.ChooseSummon(); turn.CancelYutAction();
        Require(Allies().Length == count + 1 && !turn.CurrentYutResult.HasValue, "Repeated summon or cancel cannot duplicate or undo completed action");
        ResultFixture(turn, thrower, YutThrowResult.Backdo); turn.ChooseMove(); turn.SelectAlly(ally); RightClick(turn);
        Require(turn.CurrentYutResult == YutThrowResult.Backdo && !Get<Button>(turn, "summonButton").interactable &&
            Mathf.Approximately(Get<CanvasGroup>(turn, "summonButtonGroup").alpha, .35f), "Cancelled Backdo preserves disabled and dimmed summon control");
        turn.ChooseSummon();
        Require(turn.CurrentYutResult == YutThrowResult.Backdo && Allies().Length == count + 1, "Direct summon callback cannot bypass Backdo restriction");
        turn.ChooseMove(); turn.SelectAlly(ally); turn.SelectDestination(Destinations(actions)[0]);
        Require(!turn.CurrentYutResult.HasValue && wallet.Balance == 100, "Cancelled Backdo can be selected and moved again");

        while (At(actions, 1).Length < 6) Require(actions.DeployAlly(YutThrowResult.Gae), "Fill start cell for failed summon case");
        ResultFixture(turn, thrower, YutThrowResult.Do); turn.ChooseMove(); turn.SelectAlly(At(actions, 1)[0]);
        RightClick(turn); count = Allies().Length; turn.ChooseSummon();
        Require(turn.CurrentYutResult == YutThrowResult.Do && Allies().Length == count && wallet.Balance == 100 &&
            Get<object>(turn, "chosenAction").ToString() == "None", "Failed summon after cancel keeps result and allows another choice");
        turn.ChooseMove(); turn.SelectAlly(At(actions, 1)[0]); turn.SelectDestination(2);
        Require(!turn.CurrentYutResult.HasValue && At(actions, 2).Length > 0, "Failed summon can return to movement and complete normally");
        Require(wallet.TrySpend(100) && wallet.Balance == 0, "Set zero balance through wallet API");
        ResultFixture(turn, thrower, YutThrowResult.Do); turn.ChooseMove(); turn.SelectAlly(ally); Escape(turn);
        Require(turn.CurrentYutResult == YutThrowResult.Do && wallet.Balance == 0 && !turn.CanThrowYut &&
            Get<Button>(turn, "summonButton").interactable, "Zero balance still allows cancelling and using a prepaid result");
        turn.ChooseSummon();
        Require(!turn.CurrentYutResult.HasValue && wallet.Balance == 0, "Prepaid summon completes after zero-balance cancellation");
        turn.StartBattle();
        Require(waves.IsRunning && !turn.CanCancelYutAction, "Battle starts after result completion with no cancellation state left");
        float speed = Time.timeScale; turn.CancelYutAction(); Escape(turn); RightClick(turn);
        Require(waves.IsRunning && !turn.CurrentYutResult.HasValue && wallet.Balance == 0 && Time.timeScale == speed,
            "Combat cancellation inputs do not alter progression, balance, or speed");
    }
}

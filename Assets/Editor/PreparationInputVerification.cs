using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Play Mode integration checks against the saved ThrowYut scene; never saves test changes.</summary>
[InitializeOnLoad]
public static class PreparationInputVerification
{
    private const string Key = "Yut.PreparationInputVerification";
    private static IEnumerator checks;
    private static int assertions, errors;
    private static string Report => Path.GetFullPath("Logs/PreparationInputVerification.json");

    static PreparationInputVerification() { EditorApplication.update += Tick; }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetInt(Key, 1);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        int state = SessionState.GetInt(Key, 0);
        if (state == 0) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0f) > 120)
                throw new Exception("Preparation input verification timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null)
            {
                Application.logMessageReceived += CountErrors;
                checks = CheckScene();
            }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors");
            Finish(true, "Preparation / throw / result / combat / next preparation / terminal states passed.");
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false, exception.ToString()); }
    }

    private static void CountErrors(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }

    [Serializable]
    private sealed class Result
    {
        public bool passed;
        public string unity, scene, detail;
        public int assertions, runtimeErrors;
    }

    private static void Finish(bool passed, string detail)
    {
        SessionState.SetInt(Key, 0);
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, JsonUtility.ToJson(new Result {
            passed = passed, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)",
            detail = detail, assertions = assertions, runtimeErrors = errors
        }, true));
        Debug.Log("Preparation input verification: " + (passed ? "PASS" : "FAIL") + " (" + assertions + " checks)");
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Check " + assertions + ": " + message);
    }

    private static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    private static T Get<T>(object target, string field) => (T)Field(target, field).GetValue(target);
    private static void Set(object target, string field, object value) => Field(target, field).SetValue(target, value);
    private static FieldInfo Field(object target, string field) => target.GetType().GetField(field,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new Exception("Missing field: " + field);
    private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static Ally[] Allies() => UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsSortMode.None);

    private static void ClickAlly(TurnController turn, Ally ally)
    {
        var mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            Vector2 position = Camera.main.WorldToScreenPoint(ally.VisualRenderer.bounds.center);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            InputSystem.Update();
            Require(mouse.leftButton.wasPressedThisFrame, "Virtual mouse delivers a game input frame");
            Call(turn, "LateUpdate");
        }
        finally { InputSystem.RemoveDevice(mouse); }
    }

    private static void KillActiveEnemies()
    {
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            enemy.TakeDamage(int.MaxValue);
            enemy.CompleteDeathAnimation();
        }
    }

    private static IEnumerator CheckScene()
    {
        // A batch editor has no focused Game view. Clone settings so the test's
        // virtual pointer reaches game code without changing any project asset.
        InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var turn = Find<TurnController>();
        var actions = Find<YutActionController>();
        var waves = Find<WaveManager>();
        var thrower = Find<YutThrowController>();
        var range = Find<AllyRangeDisplay>();
        var shop = Get<GameObject>(turn, "shopPanel");
        var inventory = Get<GameObject>(turn, "inventoryPanel");
        var shopButton = Get<Button>(turn, "shopButton");
        var inventoryToggle = Get<Toggle>(turn, "inventoryToggle");
        var info = Get<CharacterInfoPanel>(turn, "characterInfoPanel");
        var buttons = info.transform.parent.GetComponentsInChildren<AllyActionButtons>(true).Single();
        Require(turn && actions && waves && thrower && range && shop && inventory && shopButton && inventoryToggle && info,
            "Scene references are assigned");
        Require(turn.CanUsePreparationInput && turn.CanThrowYut && turn.CanEvolve, "Initial preparation is usable");
        Require(shopButton.interactable && inventoryToggle.interactable, "Initial panel controls enabled");
        Set(turn, "currentTurnNumber", int.MaxValue);
        turn.StartBattle();
        Require(!waves.IsRunning && turn.CanUsePreparationInput, "Rejected wave start preserves preparation");
        Set(turn, "currentTurnNumber", 0);
        for (int i = 0; i < 3; i++) Require(actions.DeployAlly(YutThrowResult.Do), "Spawn preparation evolution materials");
        var ally = Allies()[0];
        Require(actions.CanEvolve(ally), "Three matching allies can evolve during idle preparation");
        ClickAlly(turn, ally);
        Require(Get<bool>(range, "isVisible") && buttons.gameObject.activeInHierarchy, "Board click opens range and buttons");
        buttons.RequestInfo();
        Require(info.gameObject.activeInHierarchy, "Preparation information opens");

        Call(turn, "ReceiveYutResult", YutThrowResult.Do);
        Require(!turn.CanEvolve && !actions.CanEvolve(ally), "Unused result prevents evolution");
        buttons.SendMessage("LateUpdate");
        Require(!Get<Button>(buttons, "evolveButton").interactable, "Evolution button follows unused-result policy");
        actions.RequestEvolution(ally);
        Require(Allies().Length == 3 && ally.isActiveAndEnabled, "Rejected evolution consumes no materials");
        turn.ChooseMove();
        turn.SelectAlly(ally);
        var destinations = Get<System.Collections.Generic.List<int>>(actions, "destinationCellIds");
        Require(destinations.Count > 0, "Preparation movement candidates exist");
        turn.StartBattle();
        Require(!waves.IsRunning && turn.CurrentYutResult.HasValue, "Unused result blocks battle without discarding it");
        turn.SelectDestination(destinations[0]);
        Require(!turn.CurrentYutResult.HasValue && turn.CanEvolve, "Successful movement consumes result and restores evolution");

        shopButton.onClick.Invoke();
        Require(shop.activeInHierarchy && !turn.CanThrowYut, "Saved shop button uses guarded open path");
        thrower.Throw(); turn.ThrowYut(); turn.StartBattle();
        Require(!thrower.IsThrowing && !waves.IsRunning, "Open shop blocks direct throw and battle entry");
        var closeButton = shop.GetComponentsInChildren<Button>(true).Single(b =>
            Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentMethodName(i) == "CloseShop"));
        closeButton.onClick.Invoke();
        Require(!shop.activeSelf && turn.CanThrowYut, "Saved shop close button restores throw availability");

        turn.ThrowYut();
        Require(thrower.IsThrowing && !turn.CanEvolve && !actions.CanEvolve(ally), "Actual throw animation blocks evolution");
        Require(!shopButton.interactable && !inventoryToggle.interactable, "Panel controls disabled during throw");
        shopButton.onClick.Invoke(); inventoryToggle.onValueChanged.Invoke(true);
        actions.RequestEvolution(ally);
        Require(!shop.activeSelf && !inventory.activeSelf && Allies().Length == 3, "Throw rejects panels and preserves materials");
        turn.StartBattle();
        Require(!waves.IsRunning, "Throw animation prevents battle start");
        while (thrower.IsThrowing) yield return null;
        Require(shopButton.interactable && inventoryToggle.interactable, "Panel controls recover after throw");
        if (turn.CurrentYutResult == YutThrowResult.Backdo)
        {
            turn.ChooseMove(); turn.SelectAlly(ally); turn.SelectDestination(destinations[0]);
        }
        else if (turn.CurrentYutResult.HasValue) turn.ChooseSummon();
        Require(!turn.CurrentYutResult.HasValue, "Actual throw result resolves normally");

        range.Show(ally); buttons.RequestInfo(); inventoryToggle.isOn = true;
        Require(info.gameObject.activeSelf && inventory.activeSelf && inventoryToggle.isOn, "Panels open before battle");
        actions.SelectAlly(ally, YutThrowResult.Do);
        Require(destinations.Count > 0, "Fixture contains stale candidates before transition");
        int oldDestination = destinations[0];
        int count = Allies().Length;
        Transform oldParent = ally.transform.parent;
        Set(waves, "spawnInterval", 0f);
        turn.StartBattle();
        Require(waves.IsRunning && !turn.CanUsePreparationInput, "Successful battle enters blocked state");
        Require(!inventory.activeSelf && !inventoryToggle.isOn && !shop.activeSelf && !info.gameObject.activeSelf,
            "Transition closes panels and resets inventory toggle");
        Require(!Get<bool>(range, "isVisible") && !buttons.gameObject.activeSelf && destinations.Count == 0,
            "Transition removes range, ally controls, and movement candidates");
        Require(!shopButton.interactable && !inventoryToggle.interactable, "Battle disables preparation panel controls");
        ClickAlly(turn, ally);
        Require(!Get<bool>(range, "isVisible"), "Actual board click cannot select ally in battle");
        shopButton.onClick.Invoke(); inventoryToggle.onValueChanged.Invoke(true);
        info.Show(ally); range.Show(ally); buttons.SetTarget(ally); buttons.RequestInfo();
        Require(!shop.activeSelf && !inventory.activeSelf && !inventoryToggle.isOn && !info.gameObject.activeSelf &&
            !Get<bool>(range, "isVisible"), "Direct UI callbacks cannot bypass battle lock");
        Require(!actions.DeployAlly(YutThrowResult.Do) && !actions.CanMove(YutThrowResult.Do) && !actions.CanEvolve(ally),
            "Battle blocks summon, movement eligibility, and evolution eligibility");
        actions.SelectAlly(ally, YutThrowResult.Do);
        Require(!actions.MoveSelectedAlly(oldDestination) && destinations.Count == 0, "Battle rejects stale movement selection");
        actions.RequestEvolution(ally); turn.SelectAlly(ally); turn.SelectDestination(oldDestination);
        turn.ChooseMove(); turn.ChooseSummon(); thrower.Throw(); turn.ThrowYut();
        Require(Allies().Length == count && ally.transform.parent == oldParent && !thrower.IsThrowing,
            "Rejected battle actions preserve units, positions, and throw state");
        while (waves.IsRunning) { KillActiveEnemies(); yield return null; }
        Require(turn.CurrentTurnNumber == 1 && turn.CanUsePreparationInput && !turn.IsBattleWon, "Wave completion restores next preparation");
        Require(shopButton.interactable && inventoryToggle.interactable && turn.CanThrowYut, "Next preparation restores controls");
        ClickAlly(turn, ally); buttons.RequestInfo();
        Require(Get<bool>(range, "isVisible") && info.gameObject.activeSelf, "Board selection and info recover in next preparation");
        shopButton.onClick.Invoke(); Require(shop.activeSelf, "Shop recovers in next preparation"); closeButton.onClick.Invoke();
        inventoryToggle.isOn = true;
        Require(inventory.activeSelf, "Inventory recovers with a single toggle click");
        inventoryToggle.isOn = false;
        Require(actions.CanEvolve(ally), "Evolution becomes available again after combat");
        actions.RequestEvolution(ally);
        Require(!ally.isActiveAndEnabled && Allies().Count(a => a.EvolutionStage == 2) == 1 && Allies().Length == count - 2,
            "Allowed evolution consumes three materials and produces one evolved ally");

        while (!turn.IsBattleWon)
        {
            turn.StartBattle();
            while (waves.IsRunning) { KillActiveEnemies(); yield return null; }
        }
        Require(turn.IsBattleWon && !turn.CanUsePreparationInput, "Victory remains blocked after wave stops");
        shopButton.onClick.Invoke(); inventoryToggle.onValueChanged.Invoke(true); thrower.Throw();
        Require(!shop.activeSelf && !inventory.activeSelf && !thrower.IsThrowing, "Victory cannot reopen preparation");
        SceneManager.LoadScene("ThrowYut");
        for (int i = 0; i < 8; i++) yield return null;
        turn = Find<TurnController>(); actions = Find<YutActionController>();
        Require(turn.CanUsePreparationInput, "Reloaded scene enters fresh preparation");
        turn.OpenShop(); turn.SetInventoryOpen(true);
        for (int i = 0; i < 10; i++) Call(turn, "LoseLife");
        Require(turn.RemainingLives == 0 && !turn.CanUsePreparationInput && !turn.CanThrowYut,
            "Defeat never looks like preparation even when wave is not running");
        Require(!Get<GameObject>(turn, "shopPanel").activeSelf && !Get<GameObject>(turn, "inventoryPanel").activeSelf,
            "Defeat cleans existing panels");
        Require(!actions.DeployAlly(YutThrowResult.Do), "Defeat blocks direct summon");
    }
}

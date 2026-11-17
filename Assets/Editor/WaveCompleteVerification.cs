using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runs the real ThrowYut scene. No gameplay/test data changes are saved.</summary>
[InitializeOnLoad]
public static class WaveCompleteVerification
{
    const string Key = "Yut.WaveCompleteVerification";
    static IEnumerator checks;
    static int errors;
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/WaveComplete"));
    static WaveCompleteVerification() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        Directory.CreateDirectory(Output);
        SessionState.SetInt(Key, 1);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        int state = SessionState.GetInt(Key, 0);
        if (state == 0) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 90)
        { Fail(new Exception("Wave complete verification timeout")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null)
            {
                Application.logMessageReceived += CountError;
                checks = CheckScene();
            }
            if (checks.MoveNext()) return;
            Require(errors == 0, "Runtime error(s) during validation");
            File.WriteAllText(Output + "/report.json", "{\"passed\":true,\"unity\":\"" + Application.unityVersion +
                "\",\"realScenePlayMode\":true,\"startScrollRemoved\":true,\"actualReward\":73,\"balanceBefore\":120,\"balanceAfter\":193,\"preparationInputDuringBanner\":true,\"deathAnimationWait\":true,\"leakStillRewards\":true,\"expandedToTenWaves\":true,\"finalVictoryUnchanged\":true,\"defeatNoReward\":true,\"restartResets\":true,\"currencyBoundaries\":true,\"errors\":0}");
            SessionState.SetInt(Key, 0);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Fail(e); }
    }
    static void CountError(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    static void Set(object obj, string field, object value)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) continue;
            info.SetValue(obj, value); return;
        }
        throw new Exception("Missing test field " + field);
    }
    static T Get<T>(object obj, string field)
    { return (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj); }
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static Enemy[] Enemies() => UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
    static IEnumerator CheckScene()
    {
        var turn = Find<TurnController>();
        var waves = Find<WaveManager>();
        var wallet = Find<StageCurrency>();
        var banner = Find<WaveCompleteBanner>();
        Require(turn && waves && wallet && banner, "Scene integration references missing");
        Require(wallet.Balance == 120 && !banner.IsPlaying, "Initial balance/banner state");
        // A non-default reward proves the display and wallet are driven by actual wave data.
        WaveData[] data = Get<WaveData[]>(waves, "waves").Select(d => UnityEngine.Object.Instantiate(d)).ToArray();
        Set(data[0], "completionReward", 73);
        Set(waves, "waves", data);
        Set(waves, "spawnInterval", 0f);
        int announcements = 0;
        int completedNumber = 0;
        int granted = 0;
        turn.WaveCleared += (number, reward) => { announcements++; completedNumber = number; granted = reward; };
        turn.StartBattle();
        Require(waves.IsRunning && !banner.IsPlaying, "Start should run battle without banner");
        Require(Find<BattleStateHUD>().transform.Find("Start Wave Panel") == null, "Old start panel remains in scene");
        for (int i = 0; i < 8; i++) yield return null;
        var enemies = Enemies();
        Require(enemies.Length == 3, "Test fixture should have 3 enemies");
        // Let one leak; survivors still receive completion reward.
        Set(enemies[0], "moveSpeed", 10000f);
        foreach (var enemy in enemies.Skip(1)) enemy.TakeDamage(int.MaxValue);
        Require(wallet.Balance == 120 && announcements == 0 && waves.IsRunning,
            "Reward granted before death/arrival processing finished");
        while (waves.IsRunning) yield return null;
        Require(turn.RemainingLives == 9 && turn.CurrentTurnNumber == 1, "Surviving leak / next preparation changed");
        Require(wallet.Balance == 193 && announcements == 1 && completedNumber == 1 && granted == 73,
            "Reward must grant exactly once with completed wave 1 and actual amount 73");
        Require(banner.IsPlaying && banner.DisplayedWave == 1 && banner.DisplayedReward == 73, "Banner data mismatch");
        var coin = GameObject.Find("Coin HUD").GetComponent<GoldNumberDisplay>();
        Require(Get<int>(coin, "value") == wallet.Balance, "Coin HUD not bound to balance");
        foreach (var graphic in banner.GetComponentsInChildren<Graphic>(true))
            Require(!graphic.raycastTarget, "Banner graphic blocks input");
        foreach (var group in banner.GetComponentsInChildren<CanvasGroup>(true))
            Require(!group.blocksRaycasts && !group.interactable, "Banner group blocks input");
        float readyAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - readyAt < .48f) yield return null;
        Capture("wave-1-reward-73");
        // Exercise the same button event used by the player, while the banner is visible.
        var throwObject = Get<GameObject>(turn, "throwButton");
        var thrower = Get<YutThrowController>(turn, "throwController");
        Require(throwObject.activeInHierarchy && banner.IsPlaying, "Preparation button not available during banner");
        throwObject.GetComponent<Button>().onClick.Invoke();
        Require(thrower.IsThrowing, "Yut button does not accept input during banner");
        foreach (var enemy in enemies) enemy.CompleteDeathAnimation();
        Require(wallet.Balance == 193 && announcements == 1, "Repeated death callback duplicated payout");
        while (banner.IsPlaying) yield return null;
        Require(Get<CanvasGroup>(banner, "whole").alpha == 0, "Banner did not disappear");
        while (thrower.IsThrowing) yield return null;
        // Clear this test throw via the normal summon action if needed.
        if (turn.CurrentYutResult.HasValue) turn.ChooseSummon();
        Require(!turn.CurrentYutResult.HasValue, "Test could not resolve preparation action");
        banner.Play(1, 73);
        turn.StartBattle();
        Require(waves.IsRunning && !banner.IsPlaying, "New battle does not clear an in-flight banner");
        while (waves.IsRunning)
        {
            foreach (var enemy in Enemies()) enemy.TakeDamage(int.MaxValue);
            yield return null;
        }
        Require(turn.IsBattleWon && wallet.Balance == 193 && announcements == 1 && !banner.IsPlaying,
            "Final victory must not award intermediate reward or show banner");
        var hud = Find<BattleStateHUD>();
        var result = Get<ResultFlagView>(hud, "result");
        Require(result.IsVisible && result.ReachedWave == 2 && result.TotalWaves == 2, "Victory flag missing");
        while (!result.ButtonsReady) yield return null;
        Capture("final-victory");
        SceneManager.LoadScene("ThrowYut");
        for (int i = 0; i < 8; i++) yield return null;
        turn = Find<TurnController>(); waves = Find<WaveManager>(); wallet = Find<StageCurrency>(); banner = Find<WaveCompleteBanner>();
        Require(wallet.Balance == 120 && turn.CurrentTurnNumber == 0 && !banner.IsPlaying, "Restart did not reset");
        // Growing the registered list to ten requires no UI/progression code change.
        var expanded = Enumerable.Range(0, 10).Select(i => UnityEngine.Object.Instantiate(Get<WaveData[]>(waves, "waves")[0])).ToArray();
        Set(waves, "waves", expanded);
        Set(waves, "spawnInterval", 0f);
        Set(turn, "currentTurnNumber", 8);
        turn.StartBattle();
        while (waves.IsRunning)
        {
            foreach (var enemy in Enemies()) enemy.TakeDamage(int.MaxValue);
            yield return null;
        }
        Require(turn.CurrentTurnNumber == 9 && !turn.IsBattleWon && wallet.Balance == 180 &&
            banner.IsPlaying && banner.DisplayedWave == 9, "Expanded wave 9 should announce and enter wave 10 preparation");
        turn.StartBattle();
        while (waves.IsRunning)
        {
            foreach (var enemy in Enemies()) enemy.TakeDamage(int.MaxValue);
            yield return null;
        }
        Require(turn.IsBattleWon && wallet.Balance == 180 && !banner.IsPlaying, "Expanded wave 10 should only show final victory");
        SceneManager.LoadScene("ThrowYut");
        for (int i = 0; i < 8; i++) yield return null;
        turn = Find<TurnController>(); waves = Find<WaveManager>(); wallet = Find<StageCurrency>(); banner = Find<WaveCompleteBanner>();
        for (int i = 0; i < 9; i++) typeof(TurnController).GetMethod("LoseLife", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(turn, null);
        Set(waves, "spawnInterval", 0f);
        turn.StartBattle();
        while (turn.RemainingLives > 0)
        {
            foreach (var enemy in Enemies()) Set(enemy, "moveSpeed", 10000f);
            yield return null;
        }
        Require(turn.RemainingLives == 0 && !turn.IsBattleWon && wallet.Balance == 120 && !banner.IsPlaying,
            "Defeat grants a reward or completion banner");
        result = Get<ResultFlagView>(Find<BattleStateHUD>(), "result");
        Require(result.IsVisible && Time.timeScale == 0, "Defeat flag must stop the ended battle");
        while (!result.ButtonsReady) yield return null;
        Capture("defeat-no-completion");
        var probe = new GameObject("Currency boundary test").AddComponent<StageCurrency>();
        int changes = 0; probe.Changed += _ => changes++;
        Require(probe.Grant(-1) == 0 && probe.Grant(0) == 0 && probe.Balance == 120 && changes == 0,
            "Zero/negative reward changed balance");
        Require(probe.Grant(int.MaxValue) == int.MaxValue - 120 && probe.Balance == int.MaxValue &&
            probe.Grant(60) == 0 && changes == 1, "Currency overflow/actual-credit mismatch");
        UnityEngine.Object.Destroy(probe.gameObject);
    }
    static void Capture(string name)
    {
        var camera = Camera.main;
        var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        var previousMode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        var previousPlane = canvas.planeDistance;
        var previousOverride = canvas.overrideSorting;
        var previousOrder = canvas.sortingOrder;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        try
        {
            rt.Create();
            camera.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.overrideSorting = true; canvas.sortingOrder = 32767;
            Canvas.ForceUpdateCanvases(); camera.Render();
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            canvas.renderMode = previousMode; canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousPlane; canvas.overrideSorting = previousOverride; canvas.sortingOrder = previousOrder;
            UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
    static void Fail(Exception exception)
    {
        SessionState.SetInt(Key, 0); Debug.LogException(exception); EditorApplication.Exit(1);
    }
}

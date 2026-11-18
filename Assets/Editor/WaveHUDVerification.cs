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
public static class WaveHUDVerification
{
    const string Key = "Yut.WaveHUDVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static WaveHUDVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        foreach (string path in new[] { "Assets/Scripts", "Assets/Fonts", "Assets/Scenes/ThrowYut.unity" })
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
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("HUD verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Actual HUD preparation, pending/alive/escaped counts, kills, next wave, zero hearts, boss preparation/damage/win/escape and rendered previews passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WaveHUDVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static void Refresh()
    {
        foreach (var hud in new MonoBehaviour[] { Find<WaveHUD>(), Find<HeartHUD>() })
            hud.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
    }
    static void Labels(string wave, string enemies, string hearts)
    {
        Refresh(); var hud = Find<WaveHUD>();
        Require(Get<Text>(hud, "waveNumberText").text == wave, "Wave label " + wave);
        Require(Get<Text>(hud, "enemyCountText").text == enemies, "Enemy label " + enemies);
        Require(Get<Text>(Find<HeartHUD>(), "livesText").text == hearts, "Heart label " + hearts);
    }
    static IEnumerator Reload()
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1;
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ThrowYut.unity", new LoadSceneParameters(LoadSceneMode.Single));
        int frame = Time.frameCount; while (Time.frameCount < frame + 2) yield return null;
        while (!Find<TurnController>().CanUsePreparationInput) yield return null;
    }
    static IEnumerator Check()
    {
        yield return null; yield return null;
        var turn = Find<TurnController>(); var manager = Find<WaveManager>(); var wave = Find<Wave>();
        Labels("1 / 10", "남은 적 12", "10 / 10"); Capture("HUDPreparation");
        Set(manager, "spawnInterval", .01f); Time.captureDeltaTime = .02f;
        turn.StartBattle();
        var first = Get<HashSet<Enemy>>(wave, "spawnedEnemies").Single();
        var firstArrival = Get<Action<Enemy>>(first, "onArrived"); first.StopMoving();
        Require(wave.AliveEnemyCount == 1, "First spawn leaves eleven pending");
        Labels("1 / 10", "남은 적 12", "10 / 10");
        firstArrival(first);
        Labels("1 / 10", "남은 적 12", "9 / 10");
        while (wave.AliveEnemyCount < 11) { foreach (var e in Get<HashSet<Enemy>>(wave, "spawnedEnemies")) e.StopMoving(); yield return null; }
        foreach (var e in Get<HashSet<Enemy>>(wave, "spawnedEnemies").ToArray())
        {
            e.StopMoving(); e.TakeDamage(int.MaxValue);
            Labels("1 / 10", "남은 적 " + (12 - wave.DefeatedEnemyCount), "9 / 10");
            e.CompleteDeathAnimation();
        }
        Labels("2 / 10", "남은 적 " + manager.GetTotalEnemyCount(1), "9 / 10");
        Require(turn.CanUsePreparationInput && manager.Boss == null, "Next preparation resets defeated and boss state");
        typeof(TurnController).GetMethod("LoseLife", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(turn, null);
        Require(turn.RemainingLives == 8, "Life changes refresh through same HUD source");
        typeof(TurnController).GetProperty("RemainingLives").SetValue(turn, 1);
        typeof(TurnController).GetMethod("LoseLife", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(turn, null);
        Labels("2 / 10", "남은 적 " + manager.GetTotalEnemyCount(1), "0 / 10");

        for (int scenario = 0; scenario < 2; scenario++)
        {
            var reload = Reload(); while (reload.MoveNext()) yield return reload.Current;
            turn = Find<TurnController>(); manager = Find<WaveManager>(); wave = Find<Wave>();
            Set(turn, "currentTurnNumber", 9); manager.PrepareWave();
            Labels("10 / 10", "남은 적 1\n보스 6000 / 6000", "10 / 10");
            if (scenario == 0) Capture("HUDBossPreparation");
            turn.StartBattle(); var boss = manager.Boss;
            var bossArrival = Get<Action<Enemy>>(boss, "onArrived"); boss.StopMoving();
            Require(boss is BossEnemy, "HUD references actual spawned boss");
            boss.TakeDamage(125);
            Labels("10 / 10", "남은 적 1\n보스 5875 / 6000", "10 / 10");
            if (scenario == 0)
            {
                Capture("HUDBossDamage"); boss.TakeDamage(int.MaxValue);
                Require(turn.IsBattleWon && !boss.gameObject.activeSelf, "Boss wins and pools immediately");
                Labels("10 / 10", "남은 적 0\n보스 0 / 6000", "10 / 10");
            }
            else
            {
                bossArrival(boss);
                Require(turn.IsBattleLost && !boss.gameObject.activeSelf, "Boss escape loses and pools");
                Labels("10 / 10", "남은 적 1\n보스 5875 / 6000", "10 / 10");
            }
        }
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/HUD");
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
            File.WriteAllBytes("Verification/HUD/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

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

// Runs only in the isolated validation project; scene fixtures are never saved.
[InitializeOnLoad]
public static class ResultFlagVerification
{
    const string Key = "Yut.ResultFlagVerification";
    static IEnumerator checks;
    static int errors;
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/ResultFlags"));
    static ResultFlagVerification() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetInt(Key, 1);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        int state = SessionState.GetInt(Key, 0);
        if (state == 0) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120)
        { Fail(new Exception("Result verification timeout")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "Runtime errors: " + errors);
            File.WriteAllText(Output + "/report.json", "{\"passed\":true,\"unity\":\"" + Application.unityVersion +
                "\",\"realScenePlayMode\":true,\"victory2of2\":true,\"victory10of10\":true,\"defeat7of12\":true,\"intermediateWaveUnchanged\":true,\"actualReward73\":true,\"unscaledEntrance\":true,\"backgroundInputBlocked\":true,\"allFourPressedSprites\":true,\"buttonDimensionsStable\":true,\"retryResetsStage\":true,\"homeAndTitleStartWork\":true,\"duplicateResultIgnored\":true,\"errors\":0}");
            SessionState.SetInt(Key, 0); EditorApplication.Exit(0);
        }
        catch (Exception e) { Fail(e); }
    }
    static void CountError(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static T Prop<T>(object obj, string name) => (T)obj.GetType().GetProperty(name).GetValue(obj);
    static void Set(object obj, string name, object value)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) continue;
            f.SetValue(obj, value); return;
        }
        throw new Exception("Missing fixture field: " + name);
    }
    static MonoBehaviour View() => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(c => c && c.GetType().Name == "ResultFlagView");
    static Enemy[] Enemies() => UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
    static IEnumerator Check()
    {
        var turn = Find<TurnController>(); var waves = Find<WaveManager>(); var wallet = Find<StageCurrency>();
        // Preserve the explicit 2/2 result fixture independently of the real stage size.
        var data = Get<WaveData[]>(waves, "waves").Take(2).Select(UnityEngine.Object.Instantiate).ToArray();
        Set(data[0], "completionReward", 73); Set(waves, "waves", data); Set(waves, "spawnInterval", 0f);
        int firstKills=data[0].Enemies.Sum(g=>g.count*(g.enemyId==EnemyId.NormalEnemy?2:g.enemyId==EnemyId.FastEnemy?1:0));
        int finalKills=data[1].Enemies.Sum(g=>g.count*(g.enemyId==EnemyId.NormalEnemy?2:g.enemyId==EnemyId.FastEnemy?1:0));
        int announced = 0; turn.WaveCleared += (_, __) => announced++;
        turn.StartBattle();
        while (waves.IsRunning) { foreach (var e in Enemies()) e.TakeDamage(int.MaxValue); yield return null; }
        Require(!turn.IsBattleWon && turn.CurrentTurnNumber == 1 && wallet.Balance == 193 + firstKills && announced == 1,
            "Intermediate progression/reward changed");
        Require(Find<WaveCompleteBanner>().IsPlaying, "Intermediate completion banner missing");
        Require(View() == null || !Prop<bool>(View(), "IsVisible"), "Result shown on intermediate wave");
        turn.StartBattle();
        while (!turn.IsBattleWon) { foreach (var e in Enemies()) e.TakeDamage(int.MaxValue); yield return null; }
        var view = View();
        Require(view != null, "Victory event has no integrated result flag with buttons");
        Require(Prop<bool>(view, "IsVisible") && Prop<int>(view, "ReachedWave") == 2 && Prop<int>(view, "TotalWaves") == 2,
            "Actual 2/2 wave value missing");
        Require(wallet.Balance == 193 + firstKills + finalKills && !Find<WaveCompleteBanner>().IsPlaying, "Final result changed reward/scroll behavior");
        Require(Time.timeScale == 0 && !Prop<bool>(view, "ButtonsReady"), "Result must freeze battle and gate entrance input");
        var flag = Get<RectTransform>(view, "flag"); float startY = flag.anchoredPosition.y;
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < .4f) yield return null;
        Require(flag.anchoredPosition.y < startY, "Drop did not move with timeScale zero");
        Capture("victory-entrance");
        while (!Prop<bool>(view, "ButtonsReady")) yield return null;
        Require(Mathf.Abs(flag.anchoredPosition.y - 425f) < .01f && Mathf.Abs(flag.localEulerAngles.z) < .01f, "Flag did not settle");
        var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(10, 10) };
        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(view.transform), "Result does not block background clicks");
        Capture("victory-2-of-2");
        foreach (var item in PressChecks(view, "victory")) yield return item;
        Get<Button>(view, "retryButton").onClick.Invoke();
        while (view != null) yield return null;
        for (int i = 0; i < 12; i++) yield return null;
        turn = Find<TurnController>(); waves = Find<WaveManager>(); wallet = Find<StageCurrency>();
        Require(turn && turn.CurrentTurnNumber == 0 && turn.RemainingLives == 10 && !turn.IsBattleWon &&
            wallet.Balance == 120 && Time.timeScale == 1 && !Prop<bool>(View(), "IsVisible"), "Retry did not reset stage");
        var expanded = Enumerable.Range(0, 12).Select(i => UnityEngine.Object.Instantiate(Get<WaveData[]>(waves, "waves")[0])).ToArray();
        Set(waves, "waves", expanded); Set(waves, "spawnInterval", 0f); Set(turn, "currentTurnNumber", 6);
        for (int i = 0; i < 9; i++) typeof(TurnController).GetMethod("LoseLife", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(turn, null);
        turn.StartBattle();
        while (turn.RemainingLives > 0) { foreach (var e in Enemies()) Set(e, "moveSpeed", 10000f); yield return null; }
        view = View();
        Require(!turn.IsBattleWon && Prop<int>(view, "ReachedWave") == 7 && Prop<int>(view, "TotalWaves") == 12 && wallet.Balance == 120,
            "Defeat wave count / priority / reward changed");
        while (!Prop<bool>(view, "ButtonsReady")) yield return null;
        // Repeated notifications must not replay the animation or overwrite its values.
        typeof(BattleStateHUD).GetMethod("ShowDefeat", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Find<BattleStateHUD>(), null);
        Require(Prop<bool>(view, "ButtonsReady"), "Duplicate defeat replayed entrance");
        Capture("defeat-7-of-12");
        foreach (var item in PressChecks(view, "defeat")) yield return item;
        Get<Button>(view, "homeButton").onClick.Invoke();
        while (SceneManager.GetActiveScene().name != "Title") yield return null;
        for (int i = 0; i < 12; i++) yield return null;
        Require(SceneManager.GetActiveScene().name == "Title" && Time.timeScale == 1 && Find<TitleScreenController>(), "Home did not load title");
        var title = Find<TitleScreenController>(); title.ShowMenu(); Get<Button[]>(title, "menuButtons")[0].onClick.Invoke();
        while (SceneManager.GetActiveScene().name != "ThrowYut") yield return null;
        for (int i = 0; i < 12; i++) yield return null;
        Require(SceneManager.GetActiveScene().name == "ThrowYut" && Find<TurnController>().CurrentTurnNumber == 0, "Title Start cannot return to new game");
        turn = Find<TurnController>(); waves = Find<WaveManager>();
        expanded = Enumerable.Range(0, 10).Select(i => UnityEngine.Object.Instantiate(Get<WaveData[]>(waves, "waves")[0])).ToArray();
        Set(waves, "waves", expanded); Set(waves, "spawnInterval", 0f); Set(turn, "currentTurnNumber", 9);
        turn.StartBattle();
        while (!turn.IsBattleWon) { foreach (var e in Enemies()) e.TakeDamage(int.MaxValue); yield return null; }
        view = View(); while (!Prop<bool>(view, "ButtonsReady")) yield return null;
        Require(Prop<int>(view, "ReachedWave") == 10 && Prop<int>(view, "TotalWaves") == 10, "Ten-wave expansion not automatic");
        Capture("victory-10-of-10");
    }
    static System.Collections.Generic.IEnumerable<object> PressChecks(MonoBehaviour view, string theme)
    {
        foreach (string name in new[] { "homeButton", "retryButton" })
        {
            var button = Get<Button>(view, name); var rect = (RectTransform)button.transform;
            var size = rect.sizeDelta; var position = rect.anchoredPosition;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            button.OnPointerDown(pointer); yield return null;
            Require(button.image.overrideSprite == button.spriteState.pressedSprite && button.spriteState.pressedSprite != button.image.sprite,
                theme + " pressed sprite missing " + name);
            Require(size == rect.sizeDelta && position == rect.anchoredPosition && rect.localScale == Vector3.one, "Button jumps/resizes when pressed");
            Capture(theme + "-" + name + "-pressed");
            button.OnPointerUp(pointer); yield return null;
            Require(button.image.overrideSprite == button.image.sprite, "Released button did not reset");
        }
    }
    static void Capture(string name)
    {
        var camera = Camera.main; var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        var mode = canvas.renderMode; var previousCamera = canvas.worldCamera;
        float previousPlane = canvas.planeDistance;
        bool previousOverride = canvas.overrideSorting; int previousOrder = canvas.sortingOrder;
        var target = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        try
        {
            rt.Create(); camera.targetTexture = rt; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            canvas.overrideSorting = true; canvas.sortingOrder = 32767;
            Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = target; RenderTexture.active = active; canvas.renderMode = mode; canvas.worldCamera = previousCamera;
            canvas.planeDistance = previousPlane; canvas.overrideSorting = previousOverride; canvas.sortingOrder = previousOrder;
            UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
    static void Fail(Exception e)
    {
        File.WriteAllText(Output + "/failure.txt", e.ToString());
        SessionState.SetInt(Key, 0); Debug.LogException(e); EditorApplication.Exit(1);
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class SpecialSummonVerification
{
    const string Key = "Yut.SpecialSummonVerification";
    static IEnumerator checks;
    static int errors;
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/SpecialSummon"));
    static SpecialSummonVerification() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetInt(Key, 1); SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        int state = SessionState.GetInt(Key, 0);
        if (state == 0) return;
        try
        {
            Require(EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) < 90, "Special summon verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 10) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "Runtime errors: " + errors);
            File.WriteAllText(Output + "/report.json", "{\"passed\":true,\"initialBalanceZero\":true,\"hudAndPopupBalanceSynced\":true,\"noSpendOrSpawn\":true,\"modalBlocksBackground\":true,\"closeRestoresInput\":true,\"combatAndPauseBlocked\":true,\"preparationReopens\":true,\"pressedSizesStable\":true,\"existingAudioSourcesPreserved\":true,\"errors\":0}");
            SessionState.SetInt(Key, 0); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogError("Special summon verification failed: " + e); SessionState.SetInt(Key, 0); EditorApplication.Exit(1); }
    }
    static void CountError(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static MonoBehaviour Find(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(o => o && o.GetType().Name == name);
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static T Prop<T>(object obj, string property) => (T)obj.GetType().GetProperty(property).GetValue(obj);
    static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
    static IEnumerator Check()
    {
        var ui = Find("SpecialSummonUI"); Require(ui, "Special summon HUD/window is missing from the gameplay scene");
        var currency = Find("SpecialCurrency"); Require(currency, "Special currency source missing");
        var open = Get<Button>(ui, "hudButton"); var close = Get<Button>(ui, "closeButton");
        var random = Get<Button>(ui, "randomButton"); var overlay = Get<GameObject>(ui, "overlay");
        var hudDigits = Get<MonoBehaviour>(ui, "hudDigits"); var ownedDigits = Get<MonoBehaviour>(ui, "ownedDigits");
        Require(Prop<int>(currency, "Balance") == 0 && !overlay.activeSelf && open.interactable, "Initial preparation state incorrect");
        Require(Find<StageCurrency>().Balance == 120, "Ordinary currency changed");
        Capture("hud-preparation");
        int units = UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsSortMode.None).Length;
        open.onClick.Invoke(); yield return null;
        Require(Prop<bool>(ui, "IsOpen") && overlay.activeSelf && !random.interactable, "Open-at-zero / disabled summon failed");
        Require(Time.timeScale == 1, "Opening window changed game speed");
        Require(EventSystem.current.currentSelectedGameObject == close.gameObject, "Modal focus not on close");
        var point = new PointerEventData(EventSystem.current) { position = new Vector2(12, 12) };
        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(point, hits);
        Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(overlay.transform), "Popup does not block background input");
        Capture("popup-zero");
        Call(currency, "Grant", 45); yield return null;
        Require(Prop<int>(hudDigits, "Value") == 45 && Prop<int>(ownedDigits, "Value") == 45, "Balance displays do not update together");
        Require(!random.interactable, "UI-only version must not offer a nonfunctional summon");
        random.onClick.Invoke(); yield return null;
        Require(Prop<int>(currency, "Balance") == 45 && UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsSortMode.None).Length == units, "Window spent currency or spawned a unit");
        foreach (var button in new[] { close, open })
        {
            if (button == open) { close.onClick.Invoke(); yield return null; }
            var rect = (RectTransform)button.transform; var size = rect.sizeDelta; var position = rect.anchoredPosition;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            button.OnPointerDown(pointer); yield return null;
            Require(button.image.overrideSprite == button.spriteState.pressedSprite, "Pressed sprite missing");
            Require(rect.sizeDelta == size && rect.anchoredPosition == position, "Button size/position changed on press");
            Capture(button == open ? "hud-pressed" : "close-pressed");
            button.OnPointerUp(pointer); yield return null;
        }
        Require(!Prop<bool>(ui, "IsOpen"), "Close failed");
        Call(ui, "Open"); Time.timeScale = 0; yield return null; yield return null;
        Require(!Prop<bool>(ui, "IsOpen") && !open.interactable, "Paused state did not close/block window");
        Time.timeScale = 1; yield return null; Call(ui, "Open"); Require(Prop<bool>(ui, "IsOpen"), "Preparation window did not reopen");
        var turn = Find<TurnController>(); var waves = Find<WaveManager>();
        turn.StartBattle(); yield return null;
        Require(waves.IsRunning && !Prop<bool>(ui, "IsOpen") && !open.interactable, "Combat did not close/block window");
        Call(ui, "Open"); Require(!Prop<bool>(ui, "IsOpen"), "Combat allowed Open method");
        while (waves.IsRunning) { foreach (var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)) e.TakeDamage(int.MaxValue); yield return null; }
        yield return null;
        Require(open.interactable && Find<WaveCompleteBanner>().IsPlaying, "Completion scroll / preparation transition regressed");
        open.onClick.Invoke(); yield return null;
        Require(Prop<bool>(ui, "IsOpen") && Prop<int>(currency, "Balance") == 45, "Balance did not carry to next preparation");
        Require(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length == 6, "Existing audio setup changed");
        close.onClick.Invoke(); yield return null;
        Require(!Prop<bool>(ui, "IsOpen") && Time.timeScale == 1, "Closing did not restore preparation");
    }
    static void Capture(string name)
    {
        var camera = Camera.main; var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; var plane = canvas.planeDistance;
        bool sorting = canvas.overrideSorting; int order = canvas.sortingOrder;
        var target = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(1920, 1080, 24); var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 1; canvas.overrideSorting = true; canvas.sortingOrder = 32767; Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

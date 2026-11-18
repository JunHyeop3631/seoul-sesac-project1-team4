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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class SpecialSummonWindowVerification
{
    const string Key = "Yut.SpecialSummonWindowVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static SpecialSummonWindowVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        foreach (string path in new[] { "Assets/Scripts", "Assets/Prefabs", "Assets/Art/Ally/Idle/WhiteTiger_left.png", "Assets/Scenes/ThrowYut.unity" })
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
        // Let scene startup and editor indexing settle before measuring game checks.
        if (readyFrames++ < 20) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Special summon window verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "HUD geometry, shortage/open rules, live availability, actual button45-to15, auto-close, no-space3/4/6, duplicate protection, modal keyboard/pointer focus, pause/combat/terminal guards, audio preservation and rendered previews passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/SpecialSummonWindowVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object o,string method,params object[] args) => o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static Ally[] At(YutActionController a) => ((IReadOnlyList<Ally>)Call(Get<YutBoardOccupancy>(a,"occupancy"),"GetAllies",1)).ToArray();
    static void Clear(YutActionController a)
    {
        Call(a,"ClearSelection");var units=At(a);
        if(units.Length>0){Require((bool)Call(Get<YutBoardOccupancy>(a,"occupancy"),"TryRemoveAllies",(object)units),"Fixture removed");foreach(var unit in units)Find<AllyPool>().Return(unit);}
    }
    static void Balance(StageCurrency w,int value){if(w.SpecialBalance>0)w.TrySpendSpecial(w.SpecialBalance);w.GrantSpecial(value);}
    static float Width(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return Vector3.Distance(corners[0],corners[3]);}
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var ui=Find<SpecialSummonUI>();var t=Find<TurnController>();var a=Find<YutActionController>();var w=Find<StageCurrency>();var manager=Find<WaveManager>();
        var executor=Find<SpecialSummonController>();var hud=Get<Button>(ui,"hudButton");var close=Get<Button>(ui,"closeButton");var random=Get<Button>(ui,"randomButton");var label=Get<Text>(ui,"availabilityText");
        var hudDigits=Get<SpecialCurrencyDigits>(ui,"hudDigits");var owned=Get<SpecialCurrencyDigits>(ui,"ownedDigits");
        var overlay=Get<GameObject>(ui,"overlay");int sounds=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length;
        Require(Get<SpecialSummonController>(ui,"executor")==executor,"Installed UI references real executor");
        Require(!ui.IsOpen && hud.interactable && w.SpecialBalance==0,"Initial HUD remains accessible at zero");
        Require(Mathf.Abs(Width((RectTransform)hud.transform)*.95f-Width(GameObject.Find("Coin HUD").GetComponent<RectTransform>()))<.01,"Visible HUD frame matches ordinary currency width, accounting for transparent padding");
        Require(Mathf.Abs(((RectTransform)hud.transform).rect.width/((RectTransform)hud.transform).rect.height-232f/118)<.001,"HUD image aspect ratio preserved");
        Capture("SpecialSummonHUD");hud.onClick.Invoke();yield return null;
        Require(ui.IsOpen && !random.interactable && label.text.Contains("특별재화"),"Open at zero, disabled execution, shortage text");
        Require(Time.timeScale==1 && EventSystem.current.currentSelectedGameObject==close.gameObject,"Open preserves speed and selects close");
        var raycasts=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=new Vector2(12,12)},raycasts);
        Require(raycasts.Count>0 && raycasts[0].gameObject.transform.IsChildOf(overlay.transform),"Modal blocks background pointer input");
        random.onClick.Invoke();Require(w.SpecialBalance==0 && At(a).Length==0 && ui.IsOpen,"Forced disabled callback cannot summon");
        Capture("SpecialSummonInsufficient");w.GrantSpecial(45);yield return null;
        Require(random.interactable && hudDigits.Value==45 && owned.Value==45 && label.text=="","45 updates both labels and enables summon");
        Require(close.navigation.selectOnDown==random && random.navigation.selectOnDown==close,"Keyboard focus confined to close/summon");
        Capture("SpecialSummonReady");
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
        foreach(var button in new[]{random,close,hud})
        {
            bool wasOpen=ui.IsOpen;if(button==hud)ui.Close();
            var rect=(RectTransform)button.transform;var size=rect.sizeDelta;var pos=rect.anchoredPosition;var scale=rect.localScale;
            button.OnPointerEnter(pointer);yield return null;
            Require(button.image.overrideSprite==button.spriteState.highlightedSprite,"Hover sprite "+button.name);
            button.OnPointerDown(pointer);yield return null;
            Require(button.image.overrideSprite==button.spriteState.pressedSprite && rect.sizeDelta==size && rect.anchoredPosition==pos && rect.localScale==scale,"Pressed sprite preserves geometry "+button.name);
            if(button==random)Capture("SpecialSummonPressed");
            button.OnPointerUp(pointer);button.OnPointerExit(pointer);
            if(button==hud && wasOpen)ui.Open();
        }
        random.OnPointerClick(pointer);yield return null;
        Require(!ui.IsOpen && w.SpecialBalance==15 && hudDigits.Value==15 && owned.Value==15 && At(a).Single() is WhiteTiger,"Real click45 to15 creates one tiger and automatically closes");
        random.onClick.Invoke();ui.Summon();Require(w.SpecialBalance==15 && At(a).Length==1,"Closed/stale callbacks cannot spend twice");
        hud.onClick.Invoke();yield return null;Require(ui.IsOpen && !random.interactable,"Reopening at15 allowed but execution disabled");
        ui.Close();Clear(a);Balance(w,60);for(int i=0;i<4;i++)a.DeployAlly(YutThrowResult.Gae);
        ui.Open();yield return null;Require(ui.IsOpen && !random.interactable && label.text.Contains("공간") && label.text.Contains("3"),"Population4 blocks execution and explains required3");
        Capture("SpecialSummonNoSpace");random.onClick.Invoke();Require(w.SpecialBalance==60 && At(a).Length==4 && ui.IsOpen,"No-space callback preserves wallet and units");
        Require(!Get<GameObject>(ui,"startCellHint").activeSelf && label.color.r<.4f,"Shortage replaces normal hint with dark readable text");
        var removed=At(a)[0];Call(Get<YutBoardOccupancy>(a,"occupancy"),"TryRemoveAllies",(object)new[]{removed});Find<AllyPool>().Return(removed);Call(a,"ArrangeAllies",1);
        yield return null;Require(random.interactable && label.text=="" && Get<GameObject>(ui,"startCellHint").activeSelf,"Population3 re-enables execution and restores normal hint");
        random.OnPointerClick(pointer);Require(!ui.IsOpen && w.SpecialBalance==30 && At(a).Length==4,"Three ordinary plus tiger succeeds and closes");
        Clear(a);Balance(w,60);ui.Open();yield return null;random.OnPointerClick(pointer);ui.Open();yield return null;
        Require(random.interactable && At(a).Length==1 && w.SpecialBalance==30,"Second tiger permitted after reopen");
        random.OnPointerClick(pointer);ui.Open();yield return null;
        Require(!random.interactable && At(a).Length==2 && w.SpecialBalance==0 && label.text.Contains("공간"),"Two tigers fill capacity and third is blocked");
        ui.Close();Clear(a);Balance(w,45);a.DeployAlly(YutThrowResult.Gae);
        Call(t,"ReceiveYutResult",YutThrowResult.Do);t.ChooseMove();var chosen=At(a).Single();t.SelectAlly(chosen);ui.Open();
        var keyboard=InputSystem.AddDevice<Keyboard>();
        try { InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape));InputSystem.Update();Call(t,"LateUpdate");
            Require(ui.IsOpen && t.CurrentYutResult==YutThrowResult.Do && Get<Ally>(a,"selectedAlly")==chosen,"Modal Escape does not close/cancel underlying move"); }
        finally { InputSystem.RemoveDevice(keyboard); }
        ui.Close();Require(t.CurrentYutResult==YutThrowResult.Do && w.SpecialBalance==45 && At(a).Contains(chosen),"Close preserves result/balance/unit");
        t.CancelYutAction();t.ChooseMove();t.SelectAlly(chosen);t.SelectDestination(2);Clear(a);
        ui.Open();Time.timeScale=0;yield return null;yield return null;
        Require(!ui.IsOpen && !hud.interactable,"Pause closes and blocks entry");ui.Open();Require(!ui.IsOpen,"Paused direct Open rejected");Time.timeScale=1;yield return null;
        typeof(YutThrowController).GetProperty("IsThrowing").SetValue(Find<YutThrowController>(),true);ui.Open();Require(!ui.IsOpen,"Throwing blocks entry");typeof(YutThrowController).GetProperty("IsThrowing").SetValue(Find<YutThrowController>(),false);
        foreach(string property in new[]{"IsBattleWon","IsBattleLost"})
        {typeof(TurnController).GetProperty(property).SetValue(t,true);ui.Open();Require(!ui.IsOpen,property+" blocks entry even with hearts");typeof(TurnController).GetProperty(property).SetValue(t,false);}
        ui.Open();t.StartBattle();yield return null;
        Require(manager.IsRunning && !ui.IsOpen && !hud.interactable && hudDigits.Value==45,"Battle blocks clicks but retains HUD balance");ui.Open();random.onClick.Invoke();Require(!ui.IsOpen && w.SpecialBalance==45,"Battle rejects entry/execution");
        manager.StopWave();Set(t,"preparing",true);yield return null;ui.Open();Require(ui.IsOpen,"Preparation can reopen");
        ui.enabled=false;Require(!ui.IsOpen,"Disabling UI dismisses modal");ui.enabled=true;yield return null;ui.Open();
        Require(ui.IsOpen && random.interactable && UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==sounds,"Re-enable restores binding without adding sound sources");
        ui.Close();Require(Time.timeScale==1 && w.Balance>=120,"Close never changes speed or spends ordinary currency");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/SpecialSummonWindow");
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
            File.WriteAllBytes("Verification/SpecialSummonWindow/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

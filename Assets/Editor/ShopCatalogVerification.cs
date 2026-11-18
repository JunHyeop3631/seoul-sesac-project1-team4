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
public static class ShopCatalogVerification
{
    const string Key = "Yut.ShopCatalogVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static ShopCatalogVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Shop catalog verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Five exact product definitions, actual row pointer clicks, icon/name/price/background/effect sync, excluded Gumiho, no-spend browsing, layout/text fit, reopen and preparation guards passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ShopCatalogVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var t=Find<TurnController>();var wallet=Find<StageCurrency>();var panel=Get<GameObject>(t,"shopPanel");
        var catalog=panel.GetComponent<ShopCatalogUI>();Require(catalog!=null,"Saved scene contains shop catalog");
        var items=catalog.Items;Require(items.Count==5 && items.Select(i=>i.Id).Distinct().Count()==5,"Exactly five distinct fixed products");
        string[] names={"영혼의 결정","천운부","도깨비 복 주머니","도깨비 윷","천명전"};int[] prices={150,100,200,50,100};
        string[] background={"전장에서 쓰러진 용사들의 혼이 오랜 세월 응축되어 만들어진 결정체.","하늘의 기운을 종이에 담아낸 부적. 지닌 자에게 뜻밖의 행운을 가져다준다.","도깨비가 인간의 재물을 훔쳐 모아 만든 주머니. 주인을 잘 만나면 큰 복을 가져다준다.","도깨비가 인간에게 장난을 치기 위해 만들었다는 신비한 윷가락.","하늘이 선택한 자에게만 허락했다는 고대 화폐. 운명을 바꾸는 대가로 사용되었다고 한다."};
        string[] effect={"특별소환 재화 5개 획득.","구매 즉시 스테이지 동안 윷 던지기 비용 20% 할인. 사용 조작 없음.","일반재화 100, 150, 200, 250, 300 중 각각 20% 확률로 획득.","다음 던지기 1회에 윷과 모 각각 25%. 효과 대기 중 추가 사용 불가.","다음 전투의 처치 및 기본 완료 보상 2배. 같은 전투에 중복 적용 불가."};
        var buttons=Get<Button[]>(catalog,"itemButtons");var icons=Get<Image[]>(catalog,"rowIcons");var values=Get<GoldNumberDisplay[]>(catalog,"rowPrices");var highlights=Get<Image[]>(catalog,"selectionHighlights");
        var description=Get<Text>(catalog,"description");var selectedIcon=Get<Image>(catalog,"selectedIcon");var selectedName=Get<Image>(catalog,"selectedName");
        Require(!panel.activeSelf,"Shop initially closed");Get<Button>(t,"shopButton").onClick.Invoke();yield return null;
        Require(panel.activeSelf && catalog.SelectedItem==items[0],"Real HUD button opens first product");
        Require(Get<CanvasGroup[]>(catalog,"backgroundControls").All(g=>g.alpha==0&&!g.interactable&&!g.blocksRaycasts),"Underlying Yut/shop/inventory/battle controls cannot cover catalog");
        Require(values.All(v=>v.GetComponent<Graphic>()==null || !v.GetComponent<Graphic>().enabled),"Legacy static prices cannot overlap generated digits");
        Require(buttons.Count(b=>b.gameObject.activeInHierarchy)==5,"Only five sale rows active");
        var excluded=Resources.FindObjectsOfTypeAll<GameObject>().Single(g=>g.scene.IsValid()&&g.name=="Shop Item - Gumiho Heart");Require(!excluded.activeSelf,"Gumiho artwork preserved but sale row hidden");
        Require(!Get<Button>(catalog,"buyButton").interactable,"Initial soul-crystal purchase is disabled at120 below150");
        int selectionEvents=0;catalog.SelectionChanged+=item=>selectionEvents++;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        for(int i=0;i<items.Count;i++)
        {
            var rect=buttons[i].GetComponent<RectTransform>();Vector2 position=rect.anchoredPosition,size=rect.sizeDelta;
            buttons[i].OnPointerClick(pointer);yield return null;
            Require(items[i].DisplayName==names[i] && items[i].Price==prices[i],"Fixed product name and price "+i);
            Require(items[i].Background==background[i] && items[i].Effect==effect[i],"Exact supplied background and final effect "+i);
            Require(catalog.SelectedItem==items[i] && selectedIcon.sprite==items[i].Icon && selectedName.sprite==items[i].NameSprite,"Selected icon/name match actual product");
            Require(Get<int>(values[i],"value")==prices[i] && Get<int>(Get<GoldNumberDisplay>(catalog,"selectedPrice"),"value")==prices[i],"Row and selected numeric prices match");
            Require(icons[i].sprite==items[i].Icon && icons[i].preserveAspect && selectedIcon.preserveAspect && selectedName.preserveAspect,"Original icon/name sprite aspect ratios");
            Require(description.text=="배경: "+background[i]+"\n\n효과: "+effect[i] && !description.text.Contains("추후"),"No placeholder or previous product description");
            Require(highlights.Where((h,index)=>h.enabled!=(index==i)).Count()==0,"Exactly one selection highlight");
            Require(rect.anchoredPosition==position && rect.sizeDelta==size,"Selection preserves row layout");
            Canvas.ForceUpdateCanvases();
            Require(description.preferredHeight<=description.rectTransform.rect.height+1,"Full description fits visible text area");
            Require(wallet.Balance==120 && wallet.SpecialBalance==0 && t.CurrentYutResult==null,"Selection never spends or applies effects");
            Capture("ShopProduct"+(i+1));
        }
        Require(selectionEvents==4,"Selection event changes only when actual item changes");
        Require(!catalog.SelectItem(-1) && !catalog.SelectItem(5) && catalog.SelectedItem==items[4],"Invalid indices preserve current choice");
        var close=panel.GetComponentsInChildren<Button>(true).Single(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="CloseShop"));
        close.OnPointerClick(pointer);Require(!panel.activeSelf,"Existing close button works");
        Require(Get<CanvasGroup[]>(catalog,"backgroundControls").All(g=>g.alpha==1&&g.interactable&&g.blocksRaycasts),"Closing restores underlying controls");
        Require(!catalog.SelectItem(0) && catalog.SelectedItem==items[4],"Closed catalog rejects stale selection callback");
        t.OpenShop();Require(catalog.SelectedItem==items[4] && Get<int>(Get<GoldNumberDisplay>(catalog,"selectedPrice"),"value")==100,"Reopen preserves latest product");
        wallet.TrySpend(120);buttons[0].OnPointerClick(pointer);Require(catalog.SelectedItem==items[0] && wallet.Balance==0,"Zero ordinary balance still permits product browsing");
        t.CloseShop();wallet.Grant(120);
        Call(t,"ReceiveYutResult",YutThrowResult.Do);t.OpenShop();buttons[3].OnPointerClick(pointer);
        Require(t.CurrentYutResult==YutThrowResult.Do && catalog.SelectedItem==items[3] && wallet.Balance==120,"Browsing preserves a prepaid Yut result");
        t.CloseShop();t.ChooseSummon();Require(!t.CurrentYutResult.HasValue,"Existing result remains usable after shop browsing");
        t.OpenShop();Time.timeScale=0;yield return null;yield return null;
        Require(!panel.activeSelf && !catalog.SelectItem(0),"Pause dismisses and blocks catalog");Time.timeScale=1;
        var throwing=Find<YutThrowController>();typeof(YutThrowController).GetProperty("IsThrowing").SetValue(throwing,true);t.OpenShop();Require(!panel.activeSelf,"Throw animation blocks shop entry");typeof(YutThrowController).GetProperty("IsThrowing").SetValue(throwing,false);
        foreach(string state in new[]{"IsBattleWon","IsBattleLost"}){typeof(TurnController).GetProperty(state).SetValue(t,true);t.OpenShop();Require(!panel.activeSelf,state+" blocks shop entry");typeof(TurnController).GetProperty(state).SetValue(t,false);}
        t.StartBattle();Require(Find<WaveManager>().IsRunning,"Actual battle starts");t.OpenShop();buttons[2].onClick.Invoke();
        Require(!panel.activeSelf && !catalog.SelectItem(2) && wallet.Balance==120,"Combat/stale callbacks cannot browse or spend");
        Find<WaveManager>().StopWave();Set(t,"preparing",true);t.OpenShop();Require(panel.activeSelf && catalog.SelectedItem==items[3],"Next preparation retains last valid choice");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/ShopCatalog");
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
            File.WriteAllBytes("Verification/ShopCatalog/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

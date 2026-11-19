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
public static class StageExitVerification
{
    const string Key = "Yut.StageExitVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static StageExitVerification() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += ObserveQuit; }
    public static void ReimportAndRun()
    {
        foreach (string path in new[] { "Assets/Scripts", "Assets/Prefabs", "Assets/Art/Ally/Idle/WhiteTiger_left.png", "Assets/Scenes/ThrowYut.unity" })
            if (AssetDatabase.IsValidFolder(path) || File.Exists(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetBool(Key, true); SessionState.SetBool(Key+"Quit",false); SessionState.SetBool(Key+"QuitDone",false); SessionState.SetBool(Key+"QuitExited",false);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if(SessionState.GetBool(Key+"Quit",false))
        {
            if(SessionState.GetBool(Key+"QuitDone",false))
            {
                assertions=Math.Max(assertions,SessionState.GetInt(Key+"Assertions",0));errors=Math.Max(errors,SessionState.GetInt(Key+"Errors",0));
                try { Require(SessionState.GetBool(Key+"QuitExited",false),"Actual confirmed quit exits Play Mode");Require(errors==0,"No runtime errors through quit cleanup");Finish(true,"Cancel preservation, confirmed home/new game, direct result home and actual editor quit passed."); }
                catch(Exception e){Finish(false,e.ToString());}
            }
            return;
        }
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        // Let scene startup and editor indexing settle before measuring game checks.
        if (readyFrames++ < 20) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Stage exit verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Cancel preserves preparation panels/focus/speed, throw and combat; modal/state/transaction guards; confirmed home discards progress; result-home retains direct policy; actual editor quit confirmed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) {errors++;SessionState.SetInt(Key+"Errors",errors);} }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); SessionState.SetBool(Key+"Quit",false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/StageExitVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static void Funds(StageCurrency wallet,int amount){if(wallet.Balance>0)wallet.TrySpend(wallet.Balance);if(amount>0)wallet.Grant(amount);}
    static IEnumerator Reload()
    {
        Time.timeScale=1;EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/ThrowYut.unity",new LoadSceneParameters(LoadSceneMode.Single));
        int frame=Time.frameCount;while(Time.frameCount<frame+2)yield return null;
        while(!Find<TurnController>().CanUsePreparationInput)yield return null;
    }
    static void ObserveQuit(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key+"Quit",false))return;
        if(state==PlayModeStateChange.ExitingPlayMode)SessionState.SetBool(Key+"QuitExited",true);
        if(state==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(Key+"QuitDone",true);
    }
    static string Snapshot()
    {
        var t=Find<TurnController>();var w=Find<StageCurrency>();var inv=Find<StageInventory>();var actions=Find<YutActionController>();var items=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var ui=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);var info=Get<CharacterInfoPanel>(t,"characterInfoPanel");
        return string.Join("|",w.Balance,w.SpecialBalance,t.CurrentTurnNumber,t.RemainingLives,t.CurrentYutResult,t.CurrentYutThrowCost,t.BattleRewardMultiplier,
            string.Join(",",inv.OwnedItems.OrderBy(id=>id)),string.Join(",",items.Items.Select(item=>Find<ShopPurchaseController>().IsPurchased(item.Id))),
            Find<DokkaebiYutEffect>().HasPending,Find<HeavenlyMandateEffect>().HasPending,Find<HeavenlyMandateEffect>().IsActive,Find<GameSpeedButton>().CurrentSpeed,
            string.Join(",",Enumerable.Range(0,YutBoard.CellCount).Select(cell=>Call(Get<YutBoardOccupancy>(actions,"occupancy"),"GetPopulation",cell))),
            Get<GameObject>(t,"shopPanel").activeSelf,Get<GameObject>(t,"inventoryPanel").activeSelf,ui.SelectedItem==null?"none":ui.SelectedItem.Id.ToString(),items.SelectedItem==null?"none":items.SelectedItem.Id.ToString(),
            info.gameObject.activeSelf,info.CurrentAlly==null?0:info.CurrentAlly.GetInstanceID(),Get<bool>(Find<AllyRangeDisplay>(),"isVisible"),Find<SpecialSummonUI>().IsOpen);
    }
    static void Fresh()
    {
        var t=Find<TurnController>();Require(t!=null && t.CurrentTurnNumber==0 && t.RemainingLives==10 && t.CanUsePreparationInput && Time.timeScale==1,"New game after confirmed home starts wave1,10 hearts,1x");
        Require(Find<StageCurrency>().Balance==120 && Find<StageCurrency>().SpecialBalance==0 && Find<StageInventory>().OwnedItems.Count==0,"New wallet120/0 and inventory empty");
        var catalog=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);Require(catalog.Items.All(item=>!Find<ShopPurchaseController>().IsPurchased(item.Id)) && !Find<DokkaebiYutEffect>().HasPending && !Find<HeavenlyMandateEffect>().IsApplied && t.CurrentYutThrowCost==20,"Purchases and all reserved effects/discount reset");
        Require(UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0 && !Find<StageExitConfirmation>().IsOpen && !Find<StageExitConfirmation>().IsCommitting,"No old units or confirmation state survives");
    }
    static GameObject[] ObjectsInScene()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
    static IEnumerator Check()
    {
        yield return null;Fresh();
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        var turn=Find<TurnController>();var wallet=Find<StageCurrency>();var actions=Find<YutActionController>();var use=Find<StageItemUseController>();var exit=Find<StageExitConfirmation>();var home=Get<Button>(exit,"homeButton");var quit=Get<Button>(exit,"quitButton");var cancel=Get<Button>(exit,"cancelButton");var confirm=Get<Button>(exit,"confirmButton");
        Require(!exit.IsOpen && !Get<GameObject>(exit,"modal").activeSelf,"Saved confirmation starts closed");Funds(wallet,1000);wallet.GrantSpecial(45);Require(actions.DeployAlly(YutThrowResult.Do),"Actual unit fixture exists");
        turn.OpenShop();var shop=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);foreach(var item in shop.Items)Require(Find<ShopPurchaseController>().TryPurchase(item),"Purchase actual fixed item");turn.CloseShop();
        Require(use.TryUse(ShopItemId.DokkaebiYut) && use.TryUse(ShopItemId.HeavenlyMandateCoin),"Two real item reservations before exit");
        var speed=Find<GameSpeedButton>();speed.GetComponent<Button>().onClick.Invoke();speed.GetComponent<Button>().onClick.Invoke();Require(speed.CurrentSpeed==4 && Time.timeScale==4,"Actual speed4 before confirmation");Call(turn,"ReceiveYutResult",YutThrowResult.Gae);turn.SetInventoryOpen(true);
        var inventory=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);inventory.SelectSlot(0);yield return null;
        var previous=Get<Button[]>(inventory,"slots")[0].gameObject;UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(previous);string before=Snapshot();exit.RequestHome();
        Require(exit.IsOpen && turn.IsExitConfirmationOpen && Time.timeScale==0 && Get<Text>(exit,"message").text.Contains("사라집니다") && Get<Text>(exit,"confirmLabel").text=="이동","Actual home request opens destructive-action warning and pauses without commit");
        exit.RequestQuit();Require(Get<Text>(exit,"title").text.Contains("메인"),"Repeated different request does not replace active confirmation");
        var panel=Get<Text>(exit,"title").transform.parent.GetComponent<RectTransform>();var frame=panel.GetComponent<Image>();float visibleHalf=Mathf.Min(panel.rect.height,panel.rect.width/frame.sprite.rect.width*frame.sprite.rect.height)*.5f;
        Require(Mathf.Abs(Get<Text>(exit,"title").rectTransform.anchoredPosition.y)+Get<Text>(exit,"title").rectTransform.rect.height*.5f<visibleHalf && Mathf.Abs(confirm.GetComponent<RectTransform>().anchoredPosition.y)+confirm.GetComponent<RectTransform>().rect.height*.5f<visibleHalf,"Title and buttons fit visible aspect-preserved frame");
        Require(Get<Text>(exit,"title").preferredHeight<=Get<Text>(exit,"title").rectTransform.rect.height && Get<Text>(exit,"message").preferredHeight<=Get<Text>(exit,"message").rectTransform.rect.height,"Confirmation text fits without clipping");
        Capture("StageExitHomeConfirm");yield return null;Canvas.ForceUpdateCanvases();var hits=new List<UnityEngine.EventSystems.RaycastResult>();var center=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=new Vector2(Screen.width*.5f,Screen.height*.5f)};UnityEngine.EventSystems.EventSystem.current.RaycastAll(center,hits);
        Require(hits.Count>0 && hits[0].gameObject.transform.IsChildOf(Get<GameObject>(exit,"modal").transform),"Confirmation blocks underlying raycasts: "+string.Join(",",hits.Select(h=>h.gameObject.name))+" screen="+Screen.width+"x"+Screen.height+" modal="+Get<GameObject>(exit,"modal").GetComponent<RectTransform>().rect+" depth="+Get<GameObject>(exit,"modal").GetComponent<Image>().depth);
        for(int i=0;i<5;i++)yield return null;
        Require(Snapshot()==before && !turn.CanUsePreparationInput,"Inventory selection,owned items,result,wallet and reservations stay unchanged while open");
        speed.GetComponent<Button>().onClick.Invoke();turn.ThrowYut();turn.StartBattle();Require(Time.timeScale==0 && !actions.DeployAlly(YutThrowResult.Do) && !use.TryUse(ShopItemId.SoulCrystal) && Snapshot()==before,"Forced background speed/throw/battle/summon/use inputs cannot change progress");Capture("StageExitHomeConfirm");
        cancel.OnPointerClick(pointer);Require(!exit.IsOpen && Time.timeScale==4 && Snapshot()==before && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==previous,"Cancel restores exact speed/focus/open inventory and progress");
        exit.Cancel();confirm.onClick.Invoke();Require(!exit.IsCommitting && Snapshot()==before,"Stale cancel/confirm after close has no effect");
        Action<int> currencyCallback=balance=>{exit.RequestHome();Require(wallet.IsNotifying && !exit.IsOpen,"Exit request rejected during currency notification transaction");};wallet.Changed+=currencyCallback;wallet.Grant(1);wallet.Changed-=currencyCallback;Require(!wallet.IsNotifying,"Currency notification guard releases");
        turn.OpenShop();shop.SelectItem(2);yield return null;before=Snapshot();exit.RequestQuit();for(int i=0;i<4;i++)yield return null;
        Require(Get<Text>(exit,"confirmLabel").text=="종료" && Snapshot()==before && shop.gameObject.activeSelf,"Quit confirmation retains shop selection/visibility");cancel.OnPointerClick(pointer);Require(Time.timeScale==4 && Snapshot()==before,"Quit cancel returns unchanged shop");turn.CloseShop();
        var ally=UnityEngine.Object.FindFirstObjectByType<Ally>();var range=Find<AllyRangeDisplay>();range.Show(ally);var info=Get<CharacterInfoPanel>(turn,"characterInfoPanel");info.Show(ally);yield return null;before=Snapshot();float blend=Get<float>(info,"blend");exit.RequestHome();for(int i=0;i<4;i++)yield return null;
        Require(Snapshot()==before && Get<float>(info,"blend")==blend && info.CurrentAlly==ally,"Info/range/transition state preserved during confirmation pause");cancel.OnPointerClick(pointer);Require(Time.timeScale==4 && Snapshot()==before,"Cancel preserves selected info target");info.gameObject.SetActive(false);range.Hide();
        var special=Find<SpecialSummonUI>();special.Open();yield return null;before=Snapshot();exit.RequestQuit();for(int i=0;i<3;i++)yield return null;special.Summon();Require(Snapshot()==before && special.IsOpen,"Special window remains open and summon is blocked while confirmation waits");cancel.OnPointerClick(pointer);Require(Time.timeScale==4 && special.IsOpen && Snapshot()==before,"Special window returns unchanged after cancel");special.Close();
        Call(turn,"CompleteYutAction");int paidBefore=wallet.Balance;turn.ThrowYut();Require(turn.IsThrowing && wallet.Balance==paidBefore-16,"Actual discounted pending throw begins before confirmation");before=Snapshot();exit.RequestHome();for(int i=0;i<4;i++)yield return null;Require(turn.IsThrowing && Snapshot()==before,"Throw is suspended without result completion or extra debit");cancel.OnPointerClick(pointer);while(turn.IsThrowing)yield return null;Require(wallet.Balance==paidBefore-16 && !Find<DokkaebiYutEffect>().HasPending,"Cancel resumes the same throw,paid once");Call(turn,"CompleteYutAction");
        var manager=Find<WaveManager>();var wave=Find<Wave>();var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=10000}});Get<WaveData[]>(manager,"waves")[0]=data;turn.StartBattle();var enemy=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();
        Require(manager.IsRunning && Find<HeavenlyMandateEffect>().IsActive,"Actual combat starts with active mandate");before=Snapshot();var position=enemy.transform.position;float hp=enemy.CurrentHealth;exit.RequestQuit();for(int i=0;i<5;i++)yield return null;
        Require(manager.IsRunning && enemy.transform.position==position && enemy.CurrentHealth==hp && Snapshot()==before,"Combat movement,health,wallet and active multiplier frozen before decision");Capture("StageExitQuitConfirm");cancel.OnPointerClick(pointer);Require(Time.timeScale==4 && manager.IsRunning && Snapshot()==before,"Combat cancel preserves running battle and multiplier");
        exit.RequestHome();var oldObjects=ObjectsInScene();var oldExit=exit;int loads=0;UnityEngine.Events.UnityAction<Scene,LoadSceneMode> loaded=(scene,mode)=>loads++;SceneManager.sceneLoaded+=loaded;
        Require(Snapshot()==before,"No reset before home confirmation");confirm.OnPointerClick(pointer);oldExit.Confirm();oldExit.Cancel();oldExit.RequestQuit();while(turn!=null)yield return null;for(int i=0;i<8;i++)yield return null;
        Require(loads==1 && SceneManager.GetActiveScene().name=="Title" && Time.timeScale==1 && oldObjects.All(obj=>obj==null) && Find<TurnController>()==null,"Confirm home loads Title once and discards all old gameplay instances");oldExit.RequestHome();yield return null;Require(loads==1,"Late old exit callback cannot navigate again");SceneManager.sceneLoaded-=loaded;
        var title=Find<TitleScreenController>();title.ShowMenu();Get<Button[]>(title,"menuButtons")[0].OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left});for(int i=0;i<12;i++)yield return null;Fresh();
        // A late arrival can finish combat after a confirmation request; result owns the pause then.
        var t=Find<TurnController>();var wm=Find<WaveManager>();var runtime=Find<Wave>();var nav=Find<StageExitConfirmation>();Set(t,"<RemainingLives>k__BackingField",1);var last=ScriptableObject.CreateInstance<WaveData>();Set(last,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});Get<WaveData[]>(wm,"waves")[0]=last;t.StartBattle();var finalEnemy=Get<HashSet<Enemy>>(runtime,"spawnedEnemies").Single();var arrival=Get<Action<Enemy>>(finalEnemy,"onArrived");nav.RequestQuit();arrival(finalEnemy);
        var result=Get<ResultFlagView>(Find<BattleStateHUD>(),"result");Require(t.IsBattleLost && result.IsVisible && !nav.IsOpen && Time.timeScale==0,"Late terminal callback dismisses confirmation without unpausing result");nav.Cancel();nav.RequestHome();Require(!nav.IsOpen && Time.timeScale==0,"Terminal state cannot reopen game exit or restore old speed");while(!result.ButtonsReady)yield return null;
        var resultHome=Get<Button>(result,"homeButton");resultHome.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left});while(t!=null)yield return null;for(int i=0;i<8;i++)yield return null;
        Require(SceneManager.GetActiveScene().name=="Title" && Time.timeScale==1,"Result home keeps existing direct confirmed-button navigation");title=Find<TitleScreenController>();title.ShowMenu();Get<Button[]>(title,"menuButtons")[0].onClick.Invoke();for(int i=0;i<12;i++)yield return null;Fresh();
        var quitting=Find<StageExitConfirmation>();quitting.RequestQuit();Require(quitting.IsOpen && !quitting.IsCommitting && EditorApplication.isPlaying,"Quit is not executed before explicit confirmation");
        SessionState.SetBool(Key+"Quit",true);Get<Button>(quitting,"confirmButton").OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left});
        Require(quitting.IsCommitting && !quitting.IsOpen && Time.timeScale==1,"Confirmed quit commits once and restores global time before exit");quitting.Confirm();quitting.RequestHome();SessionState.SetInt(Key+"Assertions",assertions);SessionState.SetInt(Key+"Errors",errors);yield return null;
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/StageExit");
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
            File.WriteAllBytes("Verification/StageExit/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

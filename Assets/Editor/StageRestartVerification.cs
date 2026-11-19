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
public static class StageRestartVerification
{
    const string Key = "Yut.StageRestartVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static StageRestartVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Stage restart verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Actual retry after normal loss/boss loss/victory and title new game, single-load guards, complete scene-owned reset, destroyed old pools/units/UI and restored120/0/10/1x passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/StageRestartVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
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
    static Ally[] Allies()=>UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
    static GameObject[] ObjectsInScene()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
    static void Fresh()
    {
        var t=Find<TurnController>();var w=Find<StageCurrency>();var actions=Find<YutActionController>();var waves=Find<WaveManager>();var thrower=Find<YutThrowController>();var purchases=Find<ShopPurchaseController>();
        Require(SceneManager.GetActiveScene().name=="ThrowYut" && t.CurrentTurnNumber==0 && t.RemainingLives==10 && t.MaxLives==10 && !t.IsBattleWon && !t.IsBattleLost,"Fresh stage starts at wave1,10 hearts and no terminal outcome");
        Require(w.Balance==120 && w.SpecialBalance==0 && Find<StageInventory>().OwnedItems.Count==0,"Fresh balances120/0 and empty ownership");
        var catalog=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);Require(catalog.Items.All(item=>!purchases.IsPurchased(item.Id)),"Every product starts unsold");
        Require(!Find<HeavenlyLuckTalismanEffect>().IsApplied && !Find<DokkaebiYutEffect>().HasPending && !Find<HeavenlyMandateEffect>().IsApplied && t.CurrentYutThrowCost==20 && t.BattleRewardMultiplier==1,"No discount/pending/active item effects survive");
        Require(Time.timeScale==1 && Find<GameSpeedButton>().CurrentSpeed==1,"Time and speed control reset1x");
        Require(t.CurrentYutResult==null && thrower.LastResult==null && !thrower.IsThrowing && !Get<Image>(thrower,"resultImage").enabled,"No stored result/throw animation/result image survives");
        Require(!actions.IsResolvingMove && !actions.IsResolvingSummon && Get<Ally>(actions,"selectedAlly")==null && Get<List<Ally>>(actions,"selectedAllies").Count==0 && Get<List<int>>(actions,"destinationCellIds").Count==0,"Action selection/candidates/processing locks reset");
        var occupancy=Get<YutBoardOccupancy>(actions,"occupancy");Require(Enumerable.Range(0,YutBoard.CellCount).All(cell=>(int)Call(occupancy,"GetPopulation",cell)==0),"Every board cell starts with zero population");
        Require(UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0 && UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0 && UnityEngine.Object.FindObjectsByType<HomingProjectile>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0,"Active and pooled units/enemies/projectiles all gone");
        Require(Get<IDictionary>(Find<AllyPool>(),"pools").Count==0 && Get<IDictionary>(Find<EnemyPool>(),"pools").Count==0 && Get<IDictionary>(Find<ProjectilePool>(),"pools").Count==0,"New ally/enemy/projectile pools start empty");
        Require(!waves.IsRunning && waves.DefeatedEnemyCount==0 && waves.AliveEnemyCount==0 && waves.WaveCount==10,"Wave execution/counts reset to real ten-wave stage");
        Require(!Get<GameObject>(t,"shopPanel").activeSelf && !Get<GameObject>(t,"inventoryPanel").activeSelf && !Get<Toggle>(t,"inventoryToggle").isOn && !Find<SpecialSummonUI>().IsOpen,"Shop/inventory/toggle/special window start closed");
        var info=Get<CharacterInfoPanel>(t,"characterInfoPanel");var view=Get<ResultFlagView>(Find<BattleStateHUD>(),"result");Require(!info.gameObject.activeSelf && info.CurrentAlly==null && !Get<bool>(Find<AllyRangeDisplay>(),"isVisible") && !view.IsVisible && !view.ButtonsReady && !Find<WaveCompleteBanner>().IsPlaying,"Info/range/result/completion presentation reset");
        Require(Get<int>(GameObject.Find("Coin HUD").GetComponent<GoldNumberDisplay>(),"value")==120 && Get<SpecialCurrencyDigits>(Find<SpecialSummonUI>(),"hudDigits").Value==0 && Get<SpecialCurrencyDigits>(Find<SpecialSummonUI>(),"ownedDigits").Value==0,"All currency displays synchronized to new wallet");
        Require(Get<Text>(Find<HeartHUD>(),"livesText").text=="10 / 10" && Get<Text>(Find<WaveHUD>(),"waveNumberText").text=="1 / 10","Fresh heart/wave HUD labels");
        Require(t.CanUsePreparationInput && t.CanThrowYut && Get<GameObject>(t,"throwButton").GetComponent<Button>().interactable && Get<Button>(t,"shopButton").interactable && Get<Toggle>(t,"inventoryToggle").interactable,"Fresh preparation controls are usable");
    }
    static IEnumerator Dirty()
    {
        var t=Find<TurnController>();var w=Find<StageCurrency>();var actions=Find<YutActionController>();Call(t,"CompleteYutAction");Funds(w,2000);
        var speed=Find<GameSpeedButton>();speed.GetComponent<Button>().onClick.Invoke();if(speed.CurrentSpeed!=4)speed.GetComponent<Button>().onClick.Invoke();Require(speed.CurrentSpeed==4 && Time.timeScale==4,"Dirty fixture uses actual speed button4x");
        t.ThrowYut();while(t.IsThrowing)yield return null;Require(Find<YutThrowController>().LastResult.HasValue,"Real throw stores previous faces/result");Call(t,"CompleteYutAction");
        for(int i=0;i<3;i++)Require(actions.DeployAlly(YutThrowResult.Do),"Deploy three actual evolution materials");var material=Allies().First(a=>a is Do_Taoist);actions.RequestEvolution(material);
        Require(Allies().Any(a=>a.EvolutionStage==2) && Find<AllyPool>().GetComponentsInChildren<Ally>(true).Any(a=>!a.gameObject.activeSelf),"Evolution creates2star and retained inactive materials in pool");
        Require(actions.DeployAlly(YutThrowResult.Gae),"Deploy actual ordinary units");w.GrantSpecial(100);Require(Find<SpecialSummonController>().TrySummon() && Allies().Any(a=>a is WhiteTiger),"Actual special summon creates tiger and spends30");
        t.OpenShop();var shop=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var purchases=Find<ShopPurchaseController>();foreach(var item in shop.Items)Require(purchases.TryPurchase(item),"Dirty stage purchases all five actual products");t.CloseShop();
        var use=Find<StageItemUseController>();Require(use.TryUse(ShopItemId.DokkaebiYut) && use.TryUse(ShopItemId.HeavenlyMandateCoin),"Dirty stage has two consumed pending effects");
        Require(Find<StageInventory>().OwnedItems.Count==3 && Find<HeavenlyLuckTalismanEffect>().IsApplied && Find<DokkaebiYutEffect>().HasPending && Find<HeavenlyMandateEffect>().HasPending && w.SpecialBalance==70,"Dirty ownership,discount,pending and separate wallet present");
        var selected=Allies().First(a=>a.EvolutionStage==2);Call(t,"ReceiveYutResult",YutThrowResult.Do);actions.SelectAlly(selected,YutThrowResult.Do);Get<CharacterInfoPanel>(t,"characterInfoPanel").Show(selected);
        Require(Get<CharacterInfoPanel>(t,"characterInfoPanel").gameObject.activeSelf && Get<Ally>(actions,"selectedAlly")!=null,"Dirty ally/info selection present");
        t.OpenShop();Require(Get<GameObject>(t,"shopPanel").activeSelf,"Shop opens in dirty preparation");t.SetInventoryOpen(true);var inventory=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);Require(inventory.SelectSlot(0) && Get<GameObject>(t,"inventoryPanel").activeSelf,"Dirty inventory selection open before battle");
        Call(t,"CompleteYutAction");
        var prefab=AssetDatabase.LoadAssetAtPath<HomingProjectile>("Assets/Prefabs/Effects/Projectile/Do_Taoist_Projectile.prefab");var shot=Find<ProjectilePool>().Request(prefab,Vector3.zero);Call(shot,"Finish");Require(Get<IDictionary>(Find<ProjectilePool>(),"pools").Count>0,"Dirty projectile pool allocated");
    }
    static IEnumerator Check()
    {
        yield return null;Fresh();
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        foreach(int scenario in new[]{0,1,2})
        {
            var dirty=Dirty();while(dirty.MoveNext())yield return dirty.Current;
            var turn=Find<TurnController>();var wallet=Find<StageCurrency>();var manager=Find<WaveManager>();var runtime=Find<Wave>();
            Set(turn,"currentTurnNumber",scenario==0?2:9);Set(turn,"<RemainingLives>k__BackingField",scenario==0?1:scenario==1?4:7);
            if(scenario==0){var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});Get<WaveData[]>(manager,"waves")[2]=data;}
            turn.StartBattle();Require(manager.IsRunning && !Get<GameObject>(turn,"inventoryPanel").activeSelf && Find<HeavenlyMandateEffect>().IsActive,"Battle enters with active reward effect and closes dirty UI");
            var enemy=Get<HashSet<Enemy>>(runtime,"spawnedEnemies").Single();var projectile=Find<ProjectilePool>().Request(AssetDatabase.LoadAssetAtPath<HomingProjectile>("Assets/Prefabs/Effects/Projectile/Do_Taoist_Projectile.prefab"),Vector3.zero);projectile.Launch(enemy,1,60,null,runtime);
            if(scenario==2)enemy.TakeDamage(int.MaxValue);
            else
            {
                var arrival=Get<Action<Enemy>>(enemy,"onArrived");var a=new GameObject("Fixture entrance").transform;var b=new GameObject("Fixture exit").transform;a.position=enemy.transform.position;b.position=a.position+Vector3.up*.001f;
                Set(enemy,"lapDurationSeconds",0f);Set(enemy,"moveSpeed",1000f);enemy.StartMoving(new[]{a,b},arrival);while(!turn.IsBattleLost)yield return null;
            }
            var result=Get<ResultFlagView>(Find<BattleStateHUD>(),"result");Require(result.IsVisible && !result.ButtonsReady && Time.timeScale==0 && (scenario==2?turn.IsBattleWon:turn.IsBattleLost),"Actual normal loss,boss loss or victory pauses and opens result");
            Require(result.ReachedWave==(scenario==0?3:10) && result.TotalWaves==10,"Actual result keeps terminal stage wave");
            Require(wallet.Balance!=120 && wallet.SpecialBalance==70 && Find<StageInventory>().OwnedItems.Count==3 && Find<DokkaebiYutEffect>().HasPending,"Progress remains intact until retry input");
            var retry=Get<Button>(result,"retryButton");var home=Get<Button>(result,"homeButton");var stale=retry.onClick;int loads=0,totalLoads=0;UnityEngine.Events.UnityAction<Scene,LoadSceneMode> loaded=(scene,mode)=>{totalLoads++;if(scene.name=="ThrowYut")loads++;};SceneManager.sceneLoaded+=loaded;
            retry.onClick.Invoke();Require(loads==0 && !Get<bool>(result,"navigating"),"Forced early retry callback ignored during entrance");while(!result.ButtonsReady)yield return null;
            if(scenario==0)Capture("StageRestartDefeat");
            var previousObjects=ObjectsInScene();int oldHandle=SceneManager.GetActiveScene().handle;var oldWallet=wallet;var oldPools=new UnityEngine.Object[]{Find<AllyPool>(),Find<EnemyPool>(),Find<ProjectilePool>()};
            retry.OnPointerClick(pointer);retry.onClick.Invoke();home.onClick.Invoke();retry.onClick.Invoke();Require(Get<bool>(result,"navigating") && !result.ButtonsReady && loads==0,"Actual retry locks immediately and ignores rapid retry/home reentry");
            while(turn!=null)yield return null;for(int i=0;i<12;i++)yield return null;
            Require(loads==1 && totalLoads==1 && SceneManager.GetActiveScene().handle!=oldHandle,"Exactly one gameplay scene reload despite repeated inputs");
            Require(previousObjects.All(obj=>obj==null) && oldWallet==null && oldPools.All(obj=>obj==null),"All old scene objects,active/inactive pools and wallet destroyed");Fresh();
            stale.Invoke();yield return null;Require(loads==1 && !Get<ResultFlagView>(Find<BattleStateHUD>(),"result").IsVisible,"Late old result callback cannot reload or reopen new stage");SceneManager.sceneLoaded-=loaded;
            if(scenario==0)Capture("StageRestartFresh");
            var fresh=Find<TurnController>();fresh.ThrowYut();Require(fresh.IsThrowing && Find<StageCurrency>().Balance==100,"Fresh retry accepts actual first throw and pays20");while(fresh.IsThrowing)yield return null;
        }
        SceneManager.LoadScene("Title");for(int i=0;i<8;i++)yield return null;var title=Find<TitleScreenController>();Require(title!=null,"Real title scene loaded for new-game path");title.ShowMenu();Time.timeScale=4;
        var start=Get<Button[]>(title,"menuButtons")[0];var oldStart=start.onClick;var titleObjects=ObjectsInScene();int titleLoads=0;UnityEngine.Events.UnityAction<Scene,LoadSceneMode> newGame=(scene,mode)=>{if(scene.name=="ThrowYut")titleLoads++;};SceneManager.sceneLoaded+=newGame;
        start.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left});
        oldStart.Invoke();for(int i=0;i<12;i++)yield return null;
        Require(titleLoads==1 && titleObjects.All(obj=>obj==null),"Actual title start loads game once,late title callback ignored");Fresh();oldStart.Invoke();yield return null;Require(titleLoads==1,"Old title callback stays harmless after new-game startup");SceneManager.sceneLoaded-=newGame;
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/StageRestart");
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
            File.WriteAllBytes("Verification/StageRestart/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

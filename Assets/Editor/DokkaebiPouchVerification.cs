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
public static class DokkaebiPouchVerification
{
    const string Key = "Yut.DokkaebiPouchVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static DokkaebiPouchVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Dokkaebi pouch verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "All five equal-probability outcomes through actual UI, one payout/consumption, HUD, no item rewards/restock, reentry/state/cap guards, carryover and scene reset passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/DokkaebiPouchVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
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
    static UnityEngine.Random.State StateFor(int index)
    {
        for(int seed=0;seed<1000;seed++){UnityEngine.Random.InitState(seed);var state=UnityEngine.Random.state;if(UnityEngine.Random.Range(0,5)==index)return state;}
        throw new Exception("Unable to find deterministic outcome fixture");
    }
    static IEnumerator Check()
    {
        yield return null;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        for(int index=0;index<5;index++)
        {
            if(index>0){var reload=Reload();while(reload.MoveNext())yield return reload.Current;}
            var turn=Find<TurnController>();var w=Find<StageCurrency>();var inv=Find<StageInventory>();var use=Find<StageItemUseController>();var effect=Find<DokkaebiPouchEffect>();var purchase=Find<ShopPurchaseController>();
            var shop=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var view=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            Require(effect!=null && !use.TryUse(ShopItemId.DokkaebiPouch) && !((IStageItemEffect)effect).TryApply(),"Unowned use and direct effect bypass never grant");
            Funds(w,400);w.GrantSpecial(45);turn.OpenShop();Require(purchase.TryPurchase(shop.Items[1]) && purchase.TryPurchase(shop.Items[2]),"Real talisman and pouch purchases");
            Require(w.Balance==100 && inv.OwnedItems.Count==2 && turn.CurrentYutThrowCost==16,"Buying pouch costs200 and never automatically grants rewards");
            turn.SetInventoryOpen(true);Require(view.SelectSlot(1) && view.SelectedItem==shop.Items[2],"Actual owned pouch selected after passive");
            while(Mathf.Abs(((RectTransform)view.transform).anchoredPosition.y)>.1f)yield return null;
            var button=Get<Button>(view,"useButton");Require(button.interactable,"Real pouch effect enables use");
            if(index==0)Capture("DokkaebiPouchReady");
            var purchasedBefore=shop.Items.Select(item=>purchase.IsPurchased(item.Id)).ToArray();int reward=100+index*50;int credits=0,consumptions=0;var order=new List<string>();
            Action<int> paid=balance=>{
                credits++;order.Add("currency");
                Require(balance==100+reward && !inv.Contains(ShopItemId.DokkaebiPouch) && use.IsUsing,"Wallet observer sees full selected reward and consumed item atomically");
                Require(Get<int>(GameObject.Find("Coin HUD").GetComponent<GoldNumberDisplay>(),"value")==balance,"General HUD already reflects reward inside callback");
                Require(!use.TryUse(ShopItemId.DokkaebiPouch) && !((IStageItemEffect)effect).TryApply(),"Executor/direct effect callback cannot draw twice");
                Require(!purchase.TryPurchase(shop.Items[0]) && !Find<SpecialSummonController>().TrySummon() && !Find<YutActionController>().DeployAlly(YutThrowResult.Do),"Reward callback blocks purchase and summons");
                turn.ThrowYut();turn.StartBattle();Require(!turn.IsThrowing && !Find<WaveManager>().IsRunning,"Reward callback blocks throw and battle");
            };
            Action consumed=()=>{consumptions++;order.Add("inventory");Require(w.Balance==100+reward && !inv.Contains(ShopItemId.DokkaebiPouch) && use.IsUsing,"Inventory notification sees committed reward");};
            UnityEngine.Random.state=StateFor(index);w.Changed+=paid;inv.Changed+=consumed;button.OnPointerClick(pointer);w.Changed-=paid;inv.Changed-=consumed;
            Require(credits==1 && consumptions==1 && order.SequenceEqual(new[]{"currency","inventory"}),"One ordered payout and one consumption");
            Require(w.Balance==100+reward && w.SpecialBalance==45 && inv.OwnedItems.Count==1 && inv.Contains(ShopItemId.HeavenlyLuckTalisman) && !use.IsUsing,"Exact outcome without special currency/item grants");
            Require(shop.Items.Select(item=>purchase.IsPurchased(item.Id)).SequenceEqual(purchasedBefore) && !purchase.TryPurchase(shop.Items[2]),"No stock changes, pouch remains sold out");
            Require(turn.CurrentYutThrowCost==16 && use.IsApplied(ShopItemId.HeavenlyLuckTalisman),"Pouch preserves passive discount");
            Require(view.SelectedItem==null && !Get<GameObject>(view,"detail").activeSelf && Get<Image[]>(view,"icons")[0].sprite==shop.Items[1].Icon,"Consumed pouch clears selection and compacts existing item only");
            button.onClick.Invoke();var randomBefore=UnityEngine.Random.state;Require(!use.TryUse(ShopItemId.DokkaebiPouch) && w.Balance==100+reward && UnityEngine.Random.state.Equals(randomBefore),"Repeated/stale use never redraws or grants");
            if(index==4){yield return null;Capture("DokkaebiPouchGranted");}
        }
        var reset=Reload();while(reset.MoveNext())yield return reset.Current;
        var t=Find<TurnController>();var wallet=Find<StageCurrency>();var inventory=Find<StageInventory>();var executor=Find<StageItemUseController>();var realEffect=Find<DokkaebiPouchEffect>();var purchases=Find<ShopPurchaseController>();
        var catalog=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var ui=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        Funds(wallet,300);t.OpenShop();Require(purchases.TryPurchase(catalog.Items[2]),"Guard fixture uses real pouch purchase");t.SetInventoryOpen(true);ui.SelectSlot(0);var useButton=Get<Button>(ui,"useButton");
        t.SetInventoryOpen(false);useButton.onClick.Invoke();Require(inventory.Contains(ShopItemId.DokkaebiPouch) && wallet.Balance==100,"Closed-window late input never consumes");
        t.SetInventoryOpen(true);ui.SelectSlot(0);Time.timeScale=0;useButton.onClick.Invoke();Require(!executor.TryUse(ShopItemId.DokkaebiPouch) && inventory.Contains(ShopItemId.DokkaebiPouch) && wallet.Balance==100,"Pause rejects UI/direct use without consumption");Time.timeScale=1;
        foreach(string state in new[]{"<IsBattleWon>k__BackingField","<IsBattleLost>k__BackingField"}){Set(t,state,true);Require(!executor.TryUse(ShopItemId.DokkaebiPouch) && wallet.Balance==100 && inventory.Contains(ShopItemId.DokkaebiPouch),"Terminal states preserve item and funds");Set(t,state,false);}
        var thrower=Get<YutThrowController>(t,"throwController");Set(thrower,"<IsThrowing>k__BackingField",true);Require(!executor.TryUse(ShopItemId.DokkaebiPouch),"Throwing rejects use");Set(thrower,"<IsThrowing>k__BackingField",false);
        realEffect.enabled=false;Require(!executor.TryUse(ShopItemId.DokkaebiPouch),"Disabled effect unregisters");realEffect.enabled=true;
        wallet.enabled=false;Require(!executor.TryUse(ShopItemId.DokkaebiPouch),"Disabled wallet rejects use");wallet.enabled=true;
        inventory.enabled=false;Require(!executor.TryUse(ShopItemId.DokkaebiPouch),"Disabled inventory rejects use");inventory.enabled=true;
        executor.enabled=false;Require(!executor.TryUse(ShopItemId.DokkaebiPouch),"Disabled executor rejects use");executor.enabled=true;
        Funds(wallet,int.MaxValue-299);var beforeRandom=UnityEngine.Random.state;Require(!executor.TryUse(ShopItemId.DokkaebiPouch) && inventory.Contains(ShopItemId.DokkaebiPouch) && wallet.Balance==int.MaxValue-299 && UnityEngine.Random.state.Equals(beforeRandom),"Insufficient full-reward capacity rejects before draw/consumption");Funds(wallet,100);
        t.SetInventoryOpen(false);var manager=Find<WaveManager>();var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});Get<WaveData[]>(manager,"waves")[0]=data;
        t.StartBattle();Require(manager.IsRunning && !executor.TryUse(ShopItemId.DokkaebiPouch) && inventory.Contains(ShopItemId.DokkaebiPouch) && wallet.Balance==100,"Actual combat never consumes or draws");
        var wave=Find<Wave>();var enemy=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();Require(t.CurrentTurnNumber==1 && executor.CanUse(ShopItemId.DokkaebiPouch),"Next actual preparation restores pouch use");
        Set(t,"<CurrentYutResult>k__BackingField",(YutThrowResult?)YutThrowResult.Mo);t.SetInventoryOpen(true);ui.SelectSlot(0);yield return null;int before=wallet.Balance;UnityEngine.Random.state=StateFor(2);useButton.OnPointerClick(pointer);
        Require(wallet.Balance==before+200 && !inventory.Contains(ShopItemId.DokkaebiPouch) && t.CurrentYutResult==YutThrowResult.Mo,"Next-wave payout preserves existing Yut result");
        var reloadAgain=Reload();while(reloadAgain.MoveNext())yield return reloadAgain.Current;
        Require(Find<StageCurrency>().Balance==120 && Find<StageCurrency>().SpecialBalance==0 && Find<StageInventory>().OwnedItems.Count==0 && !Find<StageItemUseController>().TryUse(ShopItemId.DokkaebiPouch),"Fresh scene resets item and wallet");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/DokkaebiPouch");
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
            File.WriteAllBytes("Verification/DokkaebiPouch/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

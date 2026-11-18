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
public static class ShopPurchaseVerification
{
    const string Key = "Yut.ShopPurchaseVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static ShopPurchaseVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Shop purchase verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Five exact-price purchases, insufficient guards, atomic observer state, duplicate/reentry rejection, owned vs sold-out lifetime, consumption/no-restock, wave carry, scene reset and state guards passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/ShopPurchaseVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
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
    static int Population(YutActionController a,int cell)=>(int)Call(Get<YutBoardOccupancy>(a,"occupancy"),"GetPopulation",cell);
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        for(int index=0;index<5;index++)
        {
            if(index>0){var reload=Reload();while(reload.MoveNext())yield return reload.Current;}
            var t=Find<TurnController>();var wallet=Find<StageCurrency>();var inventory=Find<StageInventory>();var purchase=Find<ShopPurchaseController>();var actions=Find<YutActionController>();
            var catalog=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var product=catalog.Items[index];
            Require(purchase!=null && inventory!=null && purchase.Currency==wallet,"Scene-owned purchase/inventory references");
            Require(inventory.OwnedItems.Count==0 && !purchase.IsPurchased(product.Id),"Fresh stage starts unsold/unowned");
            Funds(wallet,product.Price-1);t.OpenShop();var rows=Get<Button[]>(catalog,"itemButtons");var buy=Get<Button>(catalog,"buyButton");var status=Get<Text>(catalog,"purchaseStatus");rows[index].OnPointerClick(pointer);
            Require(!buy.interactable && status.text=="재화 부족" && Get<Text[]>(catalog,"rowStatus")[index].text=="재화 부족","Price-minus-one is visibly insufficient");
            buy.onClick.Invoke();catalog.BuySelected();Require(!purchase.TryPurchase(product) && wallet.Balance==product.Price-1 && !inventory.Contains(product.Id),"Insufficient forced/direct clicks never charge/grant");
            if(index==0){yield return null;Capture("ShopPurchaseInsufficient");}
            wallet.Grant(1);Require(buy.interactable && status.text=="구매 가능","Exact balance immediately enables button");
            int debits=0,grants=0,completions=0;var order=new List<string>();
            Action<int> debit=balance=>
            {
                debits++;order.Add("debit");
                Require(balance==0 && purchase.IsPurchasing && !t.CanUsePreparationInput && purchase.IsPurchased(product.Id) && inventory.Contains(product.Id),"Wallet observer sees debit/ownership/sold-out atomically");
                Require(!purchase.TryPurchase(product) && !purchase.TryPurchase(catalog.Items[(index+1)%5]),"Reentrant same/other purchase rejected");
                Require(!actions.DeployAlly(YutThrowResult.Do) && !Find<SpecialSummonController>().TrySummon(),"Reentrant ordinary/special summon rejected");
                t.ThrowYut();t.StartBattle();
                Require(!t.IsThrowing && !Find<WaveManager>().IsRunning,"Reentrant throw/battle rejected");
            };
            Action grant=()=>{grants++;order.Add("inventory");Require(purchase.IsPurchasing && wallet.Balance==0 && purchase.IsPurchased(product.Id),"Inventory notification retains transaction lock and committed state");catalog.BuySelected();};
            Action<ShopItemDefinition> completed=item=>{completions++;order.Add("purchased");Require(item==product && purchase.IsPurchasing && !purchase.TryPurchase(product),"Success event once and reentry blocked");};
            wallet.Changed+=debit;inventory.Changed+=grant;purchase.Purchased+=completed;
            buy.OnPointerClick(pointer);
            wallet.Changed-=debit;inventory.Changed-=grant;purchase.Purchased-=completed;
            Require(debits==1 && grants==1 && completions==1 && order.SequenceEqual(new[]{"debit","inventory","purchased"}),"One ordered debit/inventory/success event");
            Require(wallet.Balance==0 && inventory.OwnedItems.Count==1 && inventory.Contains(product.Id) && purchase.IsPurchased(product.Id) && !purchase.IsPurchasing,"Exact price charged, one item owned, lock released");
            Require(!buy.interactable && status.text=="구매 완료" && Get<Text[]>(catalog,"rowStatus")[index].text=="구매 완료","Purchase completion visible in selected panel and row");
            Require(Get<int>(GameObject.Find("Coin HUD").GetComponent<GoldNumberDisplay>(),"value")==0,"General HUD reflects exact debit");
            wallet.Grant(product.Price*2);int balance=wallet.Balance;
            buy.onClick.Invoke();catalog.BuySelected();Require(!purchase.TryPurchase(product) && wallet.Balance==balance && inventory.OwnedItems.Count==1,"Repeated callbacks with sufficient funds cannot repurchase");
            var forged=UnityEngine.Object.Instantiate(product);Require(!purchase.TryPurchase(forged) && !purchase.TryPurchase(null),"Unregistered copied definition and null rejected");UnityEngine.Object.Destroy(forged);
            t.CloseShop();t.OpenShop();Require(purchase.IsPurchased(product.Id) && inventory.Contains(product.Id) && status.text=="구매 완료","Close/reopen preserves sold-out and ownership");
            if(index==0)
            {
                Require((bool)Call(inventory,"TryRemove",product.Id) && !inventory.Contains(product.Id),"Consumption fixture removes owned consumable");
                Require(purchase.IsPurchased(product.Id) && !purchase.TryPurchase(product) && wallet.Balance==balance,"Consumed item stays sold out without new debit");
                yield return null;Capture("ShopPurchaseSoldOut");
            }
            Require(wallet.SpecialBalance==0 && t.CurrentYutResult==null && Population(actions,1)==0,"Shop transaction leaves special wallet, Yut result and board unchanged");
        }
        var reset=Reload();while(reset.MoveNext())yield return reset.Current;
        var turn=Find<TurnController>();var w=Find<StageCurrency>();var inv=Find<StageInventory>();var purchases=Find<ShopPurchaseController>();var ui=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);
        Funds(w,1000);w.GrantSpecial(45);turn.OpenShop();
        var rowButtons=Get<Button[]>(ui,"itemButtons");var purchaseButton=Get<Button>(ui,"buyButton");
        int total=0;
        for(int i=0;i<5;i++){rowButtons[i].OnPointerClick(pointer);Require(purchaseButton.interactable,"Next unsold item remains purchasable");purchaseButton.OnPointerClick(pointer);total+=ui.Items[i].Price;}
        Require(total==600 && w.Balance==400 && w.SpecialBalance==45 && inv.OwnedItems.Count==5 && ui.Items.All(item=>purchases.IsPurchased(item.Id)),"All five cost600, remain owned and sold-out");
        Require(Get<Text[]>(ui,"rowStatus").All(label=>label.text=="구매 완료"),"All five sold-out badges shown");yield return null;Capture("ShopPurchaseAllSoldOut");
        Call(inv,"TryRemove",ShopItemId.SoulCrystal);turn.CloseShop();
        var manager=Find<WaveManager>();var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});Get<WaveData[]>(manager,"waves")[0]=data;
        turn.StartBattle();var wave=Find<Wave>();var enemy=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();
        Require(turn.CurrentTurnNumber==1 && inv.OwnedItems.Count==4 && ui.Items.All(item=>purchases.IsPurchased(item.Id)),"Next wave never restocks even consumed item");turn.OpenShop();Require(!purchaseButton.interactable && Get<Text>(ui,"purchaseStatus").text=="구매 완료","Next preparation shows bought status");
        reset=Reload();while(reset.MoveNext())yield return reset.Current;
        turn=Find<TurnController>();w=Find<StageCurrency>();inv=Find<StageInventory>();purchases=Find<ShopPurchaseController>();ui=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);
        Require(w.Balance==120 && inv.OwnedItems.Count==0 && ui.Items.All(item=>!purchases.IsPurchased(item.Id)),"Scene restart restores initial wallet/stock/inventory");
        Funds(w,1000);var first=ui.Items[0];ui.BuySelected();Require(w.Balance==1000 && inv.OwnedItems.Count==0,"Closed UI stale buy callback rejected");
        turn.OpenShop();Time.timeScale=0;yield return null;Require(!purchases.TryPurchase(first) && w.Balance==1000,"Paused purchase rejected");Time.timeScale=1;
        var throwing=Find<YutThrowController>();typeof(YutThrowController).GetProperty("IsThrowing").SetValue(throwing,true);Require(!purchases.TryPurchase(first),"Throwing rejects purchase");typeof(YutThrowController).GetProperty("IsThrowing").SetValue(throwing,false);
        foreach(string state in new[]{"IsBattleWon","IsBattleLost"}){typeof(TurnController).GetProperty(state).SetValue(turn,true);Require(!purchases.TryPurchase(first),state+" rejects purchase");typeof(TurnController).GetProperty(state).SetValue(turn,false);}
        inv.enabled=false;Require(!purchases.TryPurchase(first) && w.Balance==1000,"Missing active inventory prevents debit");inv.enabled=true;
        w.enabled=false;Require(!purchases.TryPurchase(first),"Disabled wallet rejected");w.enabled=true;
        purchases.enabled=false;Require(!purchases.TryPurchase(first),"Disabled purchase controller rejected");purchases.enabled=true;
        turn.CloseShop();turn.StartBattle();Require(Find<WaveManager>().IsRunning && !purchases.TryPurchase(first) && w.Balance==1000 && inv.OwnedItems.Count==0,"Actual battle rejects purchase and preserves wallet/inventory");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/ShopPurchase");
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
            File.WriteAllBytes("Verification/ShopPurchase/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

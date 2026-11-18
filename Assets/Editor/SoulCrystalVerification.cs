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
public static class SoulCrystalVerification
{
    const string Key = "Yut.SoulCrystalVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static SoulCrystalVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Soul crystal verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Real soul crystal UI use, exact +5 and one consumption, atomic notifications, both special displays, state/reentry/cap guards, carryover and scene reset passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/SoulCrystalVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
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
    static void Special(StageCurrency w,int value){if(w.SpecialBalance>0)w.TrySpendSpecial(w.SpecialBalance);w.GrantSpecial(value);}
    static IEnumerator Check()
    {
        yield return null;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        foreach(int initial in new[]{0,25,45})
        {
            if(initial!=0){var reload=Reload();while(reload.MoveNext())yield return reload.Current;}
            var t=Find<TurnController>();var inv=Find<StageInventory>();var use=Find<StageItemUseController>();var effect=Find<SoulCrystalEffect>();var w=Find<StageCurrency>();
            var ui=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);var shop=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var purchases=Find<ShopPurchaseController>();
            var summon=Find<SpecialSummonUI>();var hud=Get<SpecialCurrencyDigits>(summon,"hudDigits");var owned=Get<SpecialCurrencyDigits>(summon,"ownedDigits");
            Require(effect!=null && !use.TryUse(ShopItemId.SoulCrystal),"Saved effect present but unowned use rejected");
            Require(!((IStageItemEffect)effect).TryApply(),"Direct effect bypass cannot grant currency");
            Funds(w,200);Special(w,initial);t.OpenShop();Require(purchases.TryPurchase(shop.Items[0]),"Real shop purchase grants soul crystal");
            Require(w.Balance==50 && w.SpecialBalance==initial && purchases.IsPurchased(ShopItemId.SoulCrystal),"Purchase costs150 without automatically applying consumable");
            t.SetInventoryOpen(true);var slots=Get<Button[]>(ui,"slots");slots[0].OnPointerClick(pointer);yield return new WaitForSecondsRealtime(.6f);
            var button=Get<Button>(ui,"useButton");Require(button.interactable && use.CanUse(ShopItemId.SoulCrystal) && ui.SelectedItem==shop.Items[0],"Registered real effect enables selected soul use button");
            if(initial==0)Capture("SoulCrystalReady");
            int specialEvents=0,inventoryEvents=0;var order=new List<string>();
            Action<int> granted=balance=>{
                specialEvents++;order.Add("special");
                Require(balance==initial+5 && !inv.Contains(ShopItemId.SoulCrystal) && use.IsUsing,"Special observer sees full reward and already-consumed item under lock");
                Require(hud.Value==balance && owned.Value==balance && Find<SpecialCurrency>().Balance==balance,"Both displays and compatibility currency already synchronized");
                Require(!use.TryUse(ShopItemId.SoulCrystal) && !((IStageItemEffect)effect).TryApply(),"Executor and direct-effect reentry cannot double grant");
                Require(!purchases.TryPurchase(shop.Items[1]) && !Find<SpecialSummonController>().TrySummon() && !Find<YutActionController>().DeployAlly(YutThrowResult.Do),"Reward callback blocks purchase and summons");
                t.StartBattle();t.ThrowYut();Require(!Find<WaveManager>().IsRunning && !t.IsThrowing,"Reward callback blocks battle and throw");
            };
            Action consumed=()=>{inventoryEvents++;order.Add("inventory");Require(w.SpecialBalance==initial+5 && !inv.Contains(ShopItemId.SoulCrystal) && use.IsUsing,"Inventory notification observes committed reward");};
            w.SpecialChanged+=granted;inv.Changed+=consumed;button.OnPointerClick(pointer);w.SpecialChanged-=granted;inv.Changed-=consumed;
            Require(specialEvents==1 && inventoryEvents==1 && order.SequenceEqual(new[]{"special","inventory"}),"Exactly one grant and one consumption notification");
            Require(w.SpecialBalance==initial+5 && w.Balance==50 && inv.OwnedItems.Count==0 && !use.IsUsing,"One consumption gives exactly five special and preserves general balance");
            Require(ui.SelectedItem==null && !Get<GameObject>(ui,"detail").activeSelf && slots.All(b=>!b.interactable),"Used item disappears and empty slots/selection refresh");
            button.onClick.Invoke();Require(!use.TryUse(ShopItemId.SoulCrystal) && w.SpecialBalance==initial+5,"Repeated/stale input never grants twice");
            Require(purchases.IsPurchased(ShopItemId.SoulCrystal) && !purchases.TryPurchase(shop.Items[0]),"Consumed product stays sold out");
            t.SetInventoryOpen(false);summon.Open();Require(summon.IsOpen && hud.Value==initial+5 && owned.Value==initial+5,"Reopened special window shows actual earned balance");
            Require(Get<Button>(summon,"randomButton").interactable==(initial+5>=30),"Crossing25 to30 enables actual special summon button");
            if(initial==0){yield return null;Capture("SoulCrystalGranted");}summon.Close();
        }
        var reset=Reload();while(reset.MoveNext())yield return reset.Current;
        var turn=Find<TurnController>();var wallet=Find<StageCurrency>();var inventory=Find<StageInventory>();var executor=Find<StageItemUseController>();var realEffect=Find<SoulCrystalEffect>();
        var view=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);var catalog=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var purchase=Find<ShopPurchaseController>();
        Funds(wallet,200);turn.OpenShop();Require(purchase.TryPurchase(catalog.Items[0]),"Guard fixture uses real purchased item");turn.SetInventoryOpen(true);view.SelectSlot(0);
        var useButton=Get<Button>(view,"useButton");turn.SetInventoryOpen(false);useButton.onClick.Invoke();Require(inventory.Contains(ShopItemId.SoulCrystal) && wallet.SpecialBalance==0,"Late closed-window input never consumes");
        turn.SetInventoryOpen(true);view.SelectSlot(0);Time.timeScale=0;useButton.onClick.Invoke();Require(!executor.TryUse(ShopItemId.SoulCrystal) && inventory.Contains(ShopItemId.SoulCrystal) && wallet.SpecialBalance==0,"Pause rejects UI and direct use without consumption");Time.timeScale=1;
        foreach(string state in new[]{"<IsBattleWon>k__BackingField","<IsBattleLost>k__BackingField"}){Set(turn,state,true);Require(!executor.TryUse(ShopItemId.SoulCrystal) && inventory.Contains(ShopItemId.SoulCrystal) && wallet.SpecialBalance==0,"Terminal states preserve item and wallet");Set(turn,state,false);}
        var thrower=Get<YutThrowController>(turn,"throwController");Set(thrower,"<IsThrowing>k__BackingField",true);Require(!executor.TryUse(ShopItemId.SoulCrystal) && inventory.Contains(ShopItemId.SoulCrystal),"Throwing rejects consumption");Set(thrower,"<IsThrowing>k__BackingField",false);
        realEffect.enabled=false;Require(!executor.TryUse(ShopItemId.SoulCrystal) && inventory.Contains(ShopItemId.SoulCrystal),"Disabled effect unregisters safely");realEffect.enabled=true;
        wallet.enabled=false;Require(!executor.TryUse(ShopItemId.SoulCrystal),"Disabled wallet rejects effect");wallet.enabled=true;
        inventory.enabled=false;Require(!executor.TryUse(ShopItemId.SoulCrystal),"Disabled inventory rejects use");inventory.enabled=true;
        executor.enabled=false;Require(!executor.TryUse(ShopItemId.SoulCrystal),"Disabled executor rejects use");executor.enabled=true;
        Special(wallet,int.MaxValue-4);Require(!executor.TryUse(ShopItemId.SoulCrystal) && inventory.Contains(ShopItemId.SoulCrystal) && wallet.SpecialBalance==int.MaxValue-4,"Partial integer-cap credit rejected without consuming");Special(wallet,0);
        turn.SetInventoryOpen(false);var manager=Find<WaveManager>();var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});Get<WaveData[]>(manager,"waves")[0]=data;
        turn.StartBattle();Require(manager.IsRunning && !executor.TryUse(ShopItemId.SoulCrystal) && inventory.Contains(ShopItemId.SoulCrystal) && wallet.SpecialBalance==0,"Actual combat never consumes or grants");
        var wave=Find<Wave>();var enemy=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();
        Require(turn.CurrentTurnNumber==1 && executor.CanUse(ShopItemId.SoulCrystal),"Next actual preparation restores item use");
        Set(turn,"<CurrentYutResult>k__BackingField",(YutThrowResult?)YutThrowResult.Mo);
        turn.SetInventoryOpen(true);view.SelectSlot(0);yield return null;useButton.OnPointerClick(pointer);
        Require(wallet.SpecialBalance==5 && !inventory.Contains(ShopItemId.SoulCrystal) && turn.CurrentYutResult==YutThrowResult.Mo,"Use after wave transition grants once and preserves existing Yut result");
        var again=Reload();while(again.MoveNext())yield return again.Current;
        var newWallet=Find<StageCurrency>();var newUse=Find<StageItemUseController>();
        Require(newWallet.Balance==120 && newWallet.SpecialBalance==0 && Find<StageInventory>().OwnedItems.Count==0 && !newUse.TryUse(ShopItemId.SoulCrystal),"New scene resets wallet/ownership without residual effect");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/SoulCrystal");
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
            File.WriteAllBytes("Verification/SoulCrystal/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

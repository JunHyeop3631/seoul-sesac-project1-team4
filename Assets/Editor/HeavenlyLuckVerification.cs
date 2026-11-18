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
public static class HeavenlyLuckVerification
{
    const string Key = "Yut.HeavenlyLuckVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static HeavenlyLuckVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Heavenly luck verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Immediate purchase discount, passive status, cost display, exact16/reentry/insufficient guards, stage carryover, unchanged rewards and fresh-scene20 passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/HeavenlyLuckVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
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
    static IEnumerator Check()
    {
        yield return null;
        var turn=Find<TurnController>();var w=Find<StageCurrency>();var inv=Find<StageInventory>();var effect=Find<HeavenlyLuckTalismanEffect>();var use=Find<StageItemUseController>();var purchase=Find<ShopPurchaseController>();
        var shop=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var view=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        var display=Get<GoldNumberDisplay>(turn,"throwCostDisplay");var button=Get<GameObject>(turn,"throwButton").GetComponent<Button>();var thrower=Find<YutThrowController>();
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        Require(TurnController.YutThrowCost==20 && turn.CurrentYutThrowCost==20 && !effect.IsApplied && Get<int>(display,"value")==20,"Fresh saved scene uses base20 and matching price display");
        Funds(w,19);Require(!turn.CanThrowYut && !button.interactable,"Without talisman19 is insufficient");Funds(w,20);Require(turn.CanThrowYut && button.interactable,"Without talisman20 is enough");
        Funds(w,99);turn.OpenShop();shop.SelectItem(1);shop.BuySelected();Require(!purchase.IsPurchased(ShopItemId.HeavenlyLuckTalisman) && !effect.IsApplied && turn.CurrentYutThrowCost==20 && w.Balance==99,"Insufficient purchase never activates discount");
        Funds(w,116);int debits=0,notifications=0;
        Action<int> paid=balance=>{debits++;Require(balance==16 && effect.IsApplied && use.IsApplied(ShopItemId.HeavenlyLuckTalisman) && turn.CurrentYutThrowCost==16 && inv.Contains(ShopItemId.HeavenlyLuckTalisman),"Purchase debit observer already sees applied discount and ownership");Require(Get<int>(display,"value")==16,"Price display already refreshed inside debit notification");Require(!purchase.TryPurchase(shop.Items[1]) && !use.TryUse(ShopItemId.HeavenlyLuckTalisman),"Purchase callback cannot stack or consume passive");};
        Action<ShopItemDefinition> bought=item=>{notifications++;Require(effect.IsApplied && turn.CurrentYutThrowCost==16,"Purchase success observer sees immediate discount");};
        w.Changed+=paid;purchase.Purchased+=bought;Get<Button>(shop,"buyButton").OnPointerClick(pointer);w.Changed-=paid;purchase.Purchased-=bought;
        Require(debits==1 && notifications==1 && w.Balance==16 && w.SpecialBalance==0 && inv.OwnedItems.Count==1,"Price100 charged once, passive owned, special currency unchanged");
        turn.SetInventoryOpen(true);Require(view.SelectSlot(0),"Owned talisman can be selected");while(Mathf.Abs(((RectTransform)view.transform).anchoredPosition.y)>.1f)yield return null;
        Require(Get<Text>(view,"status").text=="적용 중" && !Get<Button>(view,"useButton").gameObject.activeSelf,"Real purchased talisman shows applied with no use button");
        Get<Button>(view,"useButton").onClick.Invoke();Require(!use.TryUse(ShopItemId.HeavenlyLuckTalisman) && inv.Contains(ShopItemId.HeavenlyLuckTalisman) && w.Balance==16,"Forced use never consumes passive");Capture("HeavenlyLuckApplied");
        turn.SetInventoryOpen(false);Require(turn.CanThrowYut && button.interactable && Get<int>(display,"value")==16,"Balance16 enables next real throw");yield return null;Capture("HeavenlyLuckCost16");
        Funds(w,15);Require(!turn.CanThrowYut && !button.interactable && button.image.color.r<.6f,"Discounted15 visibly insufficient");button.onClick.Invoke();thrower.Throw();Require(!thrower.IsThrowing && w.Balance==15,"Insufficient actual/forced throw is uncharged");
        Funds(w,16);int charges=0;Action<int> reenter=balance=>{charges++;Require(thrower.IsThrowing && balance==0,"Throw locks before16 debit notification");turn.ThrowYut();thrower.Throw();};w.Changed+=reenter;button.OnPointerClick(pointer);w.Changed-=reenter;
        Require(thrower.IsThrowing && charges==1 && w.Balance==0,"Actual button spends exactly16 once with reentrant inputs");
        for(int i=0;i<4;i++){turn.ThrowYut();thrower.Throw();}Require(w.Balance==0,"Throw animation repeated clicks never charge again");while(thrower.IsThrowing)yield return null;
        Require(w.Balance==0 && turn.CurrentYutThrowCost==16,"Completion never charges/refunds and discount persists");
        Set(turn,"<CurrentYutResult>k__BackingField",(YutThrowResult?)YutThrowResult.Do);Funds(w,16);button.onClick.Invoke();Require(w.Balance==16 && !thrower.IsThrowing,"Existing result prevents discounted rethrow");Set(turn,"<CurrentYutResult>k__BackingField",null);
        Time.timeScale=0;turn.ThrowYut();Require(w.Balance==16 && !thrower.IsThrowing,"Paused throw never pays16");Time.timeScale=1;
        turn.SetInventoryOpen(true);turn.ThrowYut();Require(w.Balance==16 && !thrower.IsThrowing,"Open inventory blocks discounted throw");turn.SetInventoryOpen(false);
        turn.OpenShop();turn.ThrowYut();Require(w.Balance==16 && !thrower.IsThrowing,"Open shop blocks discounted throw");turn.CloseShop();
        Funds(w,200);Require(!purchase.TryPurchase(shop.Items[1]) && w.Balance==200 && turn.CurrentYutThrowCost==16,"Sufficient funds cannot repurchase/stack passive");
        turn.SetInventoryOpen(true);view.SelectSlot(0);Require(Get<Text>(view,"status").text=="적용 중","Close/reopen retains applied state");turn.SetInventoryOpen(false);
        var rewards=Find<EnemyKillRewards>();Require(Get<int>(rewards,"normalReward")==2 && Get<int>(rewards,"fastReward")==2 && Get<int>(rewards,"bossReward")==100,"Kill reward bases remain2/2/100");
        var manager=Find<WaveManager>();var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});Get<WaveData[]>(manager,"waves")[0]=data;
        int credited=0;Action<int,int> cleared=(wave,amount)=>credited=amount;turn.WaveCleared+=cleared;turn.StartBattle();Require(manager.IsRunning && effect.IsApplied && turn.CurrentYutThrowCost==16,"Discount remains in combat");
        int before=w.Balance;turn.ThrowYut();Require(w.Balance==before && !thrower.IsThrowing,"Combat blocks discounted throw");
        var waveRuntime=Find<Wave>();var enemy=Get<HashSet<Enemy>>(waveRuntime,"spawnedEnemies").Single();enemy.TakeDamage(int.MaxValue);Require(w.Balance==before+2,"Real normal kill still pays2");enemy.CompleteDeathAnimation();turn.WaveCleared-=cleared;
        Require(turn.CurrentTurnNumber==1 && credited==60 && w.Balance==before+62 && turn.CurrentYutThrowCost==16 && effect.IsApplied,"Actual next prep keeps discount and completion remains60");
        turn.SetInventoryOpen(true);view.SelectSlot(0);Require(Get<Text>(view,"status").text=="적용 중" && inv.Contains(ShopItemId.HeavenlyLuckTalisman),"Next wave retains owned passive and applied UI");turn.SetInventoryOpen(false);
        Funds(w,16);button.OnPointerClick(pointer);Require(thrower.IsThrowing && w.Balance==0,"Next-wave real throw also spends16");while(thrower.IsThrowing)yield return null;Set(turn,"<CurrentYutResult>k__BackingField",null);
        Set(turn,"currentTurnNumber",9);before=w.Balance;turn.StartBattle();var boss=Get<HashSet<Enemy>>(waveRuntime,"spawnedEnemies").Single();boss.TakeDamage(int.MaxValue);
        Require(turn.IsBattleWon && w.Balance==before+100 && turn.CurrentYutThrowCost==16,"Actual final boss reward remains100 without discounted reward base");
        var reload=Reload();while(reload.MoveNext())yield return reload.Current;
        var fresh=Find<TurnController>();var freshWallet=Find<StageCurrency>();Require(freshWallet.Balance==120 && freshWallet.SpecialBalance==0 && fresh.CurrentYutThrowCost==20 && !Find<HeavenlyLuckTalismanEffect>().IsApplied && Find<StageInventory>().OwnedItems.Count==0,"New scene resets applied state and cost20");
        Require(Get<int>(Get<GoldNumberDisplay>(fresh,"throwCostDisplay"),"value")==20,"New scene price display resets20");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/HeavenlyLuck");
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
            File.WriteAllBytes("Verification/HeavenlyLuck/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

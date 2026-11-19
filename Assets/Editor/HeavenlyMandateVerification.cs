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
public static class HeavenlyMandateVerification
{
    const string Key = "Yut.HeavenlyMandateVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static HeavenlyMandateVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Heavenly mandate verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Next-battle reservation, exact4/4/120/200 rewards and popups, leak survival, boss credit-before-win, expiration/no carry, duplicate/state/reentry guards and reset passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/HeavenlyMandateVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
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
    static WaveData Data(params WaveData.EnemyGroup[] groups)
    {var data=ScriptableObject.CreateInstance<WaveData>();Set(data,"enemies",groups);return data;}
    static WaveData.EnemyGroup Group(EnemyId id,int count=1)=>new WaveData.EnemyGroup{enemyId=id,count=count,health=125};
    static IEnumerator Escape(Enemy enemy)
    {
        var arrived=Get<Action<Enemy>>(enemy,"onArrived");var a=new GameObject("Fixture entrance").transform;var b=new GameObject("Fixture exit").transform;
        a.position=enemy.transform.position;b.position=a.position+Vector3.up*.001f;Set(enemy,"lapDurationSeconds",0f);Set(enemy,"moveSpeed",1000f);enemy.StartMoving(new[]{a,b},arrived);
        int version=Get<int>(enemy,"<ActivationVersion>k__BackingField");
        while(enemy.gameObject.activeSelf && Get<int>(enemy,"<ActivationVersion>k__BackingField")==version)yield return null;
        UnityEngine.Object.Destroy(a.gameObject);UnityEngine.Object.Destroy(b.gameObject);
    }
    static IEnumerator Check()
    {
        yield return null;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        var t=Find<TurnController>();var w=Find<StageCurrency>();var inv=Find<StageInventory>();var use=Find<StageItemUseController>();var effect=Find<HeavenlyMandateEffect>();var purchase=Find<ShopPurchaseController>();
        var shop=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var ui=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);var manager=Find<WaveManager>();var wave=Find<Wave>();
        Require(effect!=null && !effect.HasPending && !effect.IsActive && t.BattleRewardMultiplier==1 && !use.TryUse(ShopItemId.HeavenlyMandateCoin) && !((IStageItemEffect)effect).TryApply(),"Fresh saved effect cannot be used unowned or bypassed");
        Funds(w,1000);w.GrantSpecial(45);t.OpenShop();foreach(var item in shop.Items)Require(purchase.TryPurchase(item),"Real fixed item purchase fixture");
        Require(w.Balance==400 && inv.OwnedItems.Count==5 && !effect.IsApplied && t.CurrentYutThrowCost==16,"Coin purchase100 does not activate it and talisman still discounts");
        Call(t,"ReceiveYutResult",YutThrowResult.Gae);t.SetInventoryOpen(true);Require(ui.SelectSlot(4) && ui.SelectedItem==shop.Items[4],"Actual coin slot selected");var button=Get<Button>(ui,"useButton");yield return null;
        int uses=0;Action consumed=()=>{uses++;Require(effect.HasPending && !effect.IsActive && !inv.Contains(ShopItemId.HeavenlyMandateCoin) && use.IsUsing && t.BattleRewardMultiplier==1,"Consumption observer sees pending, not current-battle activation");Require(!use.TryUse(ShopItemId.HeavenlyMandateCoin),"Use callback cannot stack");};inv.Changed+=consumed;button.OnPointerClick(pointer);inv.Changed-=consumed;
        Require(uses==1 && effect.HasPending && !effect.IsActive && w.Balance==400 && t.CurrentYutResult==YutThrowResult.Gae && purchase.IsPurchased(ShopItemId.HeavenlyMandateCoin),"Use reserves next combat, consumes once and preserves money/result/sold-out");
        Require(use.TryUse(ShopItemId.DokkaebiYut) && use.TryUse(ShopItemId.SoulCrystal) && w.SpecialBalance==50,"Other pending effects coexist and soul reward stays5");
        var random=UnityEngine.Random.state;int pouchReward=100+UnityEngine.Random.Range(0,5)*50;UnityEngine.Random.state=random;int beforePouch=w.Balance;Require(use.TryUse(ShopItemId.DokkaebiPouch) && w.Balance==beforePouch+pouchReward && effect.HasPending,"Pouch item payout is not multiplied by combat reservation");
        while(Mathf.Abs(((RectTransform)ui.transform).anchoredPosition.y)>.1f)yield return null;yield return null;
        var pending=Get<Text>(ui,"pendingEffectStatus");Require(pending.text.Contains("도깨비 윷") && pending.text.Contains("천명전") && pending.preferredHeight<=pending.rectTransform.rect.height,"Both pending effect lines fit existing inventory status area");Capture("HeavenlyMandatePending");
        Call(inv,"TryAdd",ShopItemId.HeavenlyMandateCoin);Call(inv,"NotifyChanged");ui.SelectSlot(1);yield return null;button.onClick.Invoke();Require(inv.Contains(ShopItemId.HeavenlyMandateCoin) && effect.HasPending && !button.interactable && !use.TryUse(ShopItemId.HeavenlyMandateCoin),"Duplicate ownership fixture remains unconsumed during same preparation");
        Set(t,"<CurrentYutResult>k__BackingField",null);t.SetInventoryOpen(false);var waveAssets=Get<WaveData[]>(manager,"waves");var original=waveAssets[0];waveAssets[0]=null;t.StartBattle();Require(t.CanUsePreparationInput && effect.HasPending && !effect.IsActive && t.BattleRewardMultiplier==1 && !manager.IsRunning,"Invalid wave start restores reservation without applying or losing it");waveAssets[0]=original;
        waveAssets[0]=Data(Group(EnemyId.NormalEnemy),Group(EnemyId.FastEnemy));waveAssets[1]=Data(Group(EnemyId.NormalEnemy));Set(manager,"spawnInterval",0f);
        int baseBalance=w.Balance,clears=0,completion=0;var credits=new List<int>();int previous=w.Balance;
        Action<int> credit=balance=>{credits.Add(balance-previous);previous=balance;Require(effect.IsActive && t.BattleRewardMultiplier==2,"Every credit callback retains this battle multiplier through settlement");t.StartBattle();Call(t,"EndBattle");Require(!use.TryUse(ShopItemId.HeavenlyMandateCoin),"Reward callback cannot consume another coin or reenter settlement");};
        Action<int,int> cleared=(number,amount)=>{clears++;completion=amount;Require(number==1 && !effect.IsApplied && t.BattleRewardMultiplier==1 && !manager.IsRunning,"Multiplier cleared before next-preparation/completion observers");t.StartBattle();Require(!manager.IsRunning && t.CurrentTurnNumber==1,"Completion callback cannot start reentrant next combat during settlement");};
        int starts=0;Action started=()=>{starts++;Require(effect.IsActive && !effect.HasPending && t.BattleRewardMultiplier==2,"BattleStarted observes activation before kills");};t.BattleStarted+=started;w.Changed+=credit;t.WaveCleared+=cleared;t.StartBattle();t.BattleStarted-=started;
        while(Get<HashSet<Enemy>>(wave,"spawnedEnemies").Count<2)yield return null;
        Require(starts==1 && manager.IsRunning && effect.IsActive && !use.TryUse(ShopItemId.HeavenlyMandateCoin) && inv.Contains(ShopItemId.HeavenlyMandateCoin),"Actual combat active, duplicate coin retained and unusable");
        var normal=Get<HashSet<Enemy>>(wave,"spawnedEnemies").OfType<NormalEnemy>().Single();normal.TakeDamage(int.MaxValue);Require(w.Balance==baseBalance+4,"Normal kill2 becomes4");
        var rewards=Find<EnemyKillRewards>();Require(rewards.GetComponentsInChildren<CanvasGroup>().Any(g=>g.name=="Kill Reward +4"),"Floating kill popup shows actual4");Capture("HeavenlyMandateKill4");normal.CompleteDeathAnimation();
        var fast=Get<HashSet<Enemy>>(wave,"spawnedEnemies").OfType<FastEnemy>().Single();fast.TakeDamage(int.MaxValue);Require(w.Balance==baseBalance+8,"Fast kill2 becomes4");fast.CompleteDeathAnimation();w.Changed-=credit;t.WaveCleared-=cleared;
        Require(credits.SequenceEqual(new[]{4,4,120}) && completion==120 && clears==1 && w.Balance==baseBalance+128 && t.CurrentTurnNumber==1,"Exactly4/4/120 paid once");
        Require(!effect.IsApplied && t.BattleRewardMultiplier==1 && use.CanUse(ShopItemId.HeavenlyMandateCoin) && inv.Contains(ShopItemId.HeavenlyMandateCoin) && Find<DokkaebiYutEffect>().HasPending,"Next prep clears only coin effect and keeps duplicate item/other waiting effect");
        Require(w.SpecialBalance==50 && t.CurrentYutThrowCost==16 && manager.GetCompletionReward(0)==60,"Special balance, discounted throw and base reward60 unchanged");
        int settled=w.Balance;Call(t,"EndBattle");Call(wave,"TryCompleteWave");Require(w.Balance==settled && !purchase.TryPurchase(shop.Items[4]),"Duplicate completion and restock rejected");
        t.StartBattle();var next=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();next.TakeDamage(int.MaxValue);next.CompleteDeathAnimation();Require(w.Balance==settled+62 && t.CurrentTurnNumber==2 && !effect.IsApplied,"Following battle receives only2+60 even with coin still owned");
        foreach(int scenario in new[]{0,1,2,3,4})
        {
            var reload=Reload();while(reload.MoveNext())yield return reload.Current;
            var turn=Find<TurnController>();var wallet=Find<StageCurrency>();var inventory=Find<StageInventory>();var executor=Find<StageItemUseController>();var mandate=Find<HeavenlyMandateEffect>();var wm=Find<WaveManager>();var runtime=Find<Wave>();var buying=Find<ShopPurchaseController>();
            var catalog=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var view=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            turn.OpenShop();Require(buying.TryPurchase(catalog.Items[4]),"Real coin purchase per scenario");turn.SetInventoryOpen(true);view.SelectSlot(0);yield return null;Get<Button>(view,"useButton").OnPointerClick(pointer);turn.SetInventoryOpen(false);
            int before=wallet.Balance,wins=0,losses=0,done=0;var order=new List<string>();turn.WaveCleared+=(n,r)=>{done++;Require(r==120,"Leaked surviving combat still completes for120");};
            turn.BattleWon+=()=>{wins++;order.Add("win");Require(wallet.Balance==before+200 && !mandate.IsApplied && turn.BattleRewardMultiplier==1,"Victory observes boss200 already credited and multiplier cleared");};
            turn.BattleLost+=()=>{losses++;Require(!mandate.IsApplied && turn.BattleRewardMultiplier==1 && !wm.IsRunning,"Loss clears multiplier and stops combat");};
            if(scenario==0 || scenario==1)
            {
                Get<WaveData[]>(wm,"waves")[0]=Data(Group(EnemyId.NormalEnemy,scenario==0?2:1));Set(wm,"spawnInterval",0f);if(scenario==1)Set(turn,"<RemainingLives>k__BackingField",1);
            }
            else {Set(turn,"currentTurnNumber",9);if(scenario==4)Set(turn,"<RemainingLives>k__BackingField",1);}
            turn.StartBattle();Require(mandate.IsActive && wm.IsRunning,"Reserved multiplier active in normal or final combat");
            if(scenario==0)
            {
                while(Get<HashSet<Enemy>>(runtime,"spawnedEnemies").Count<2)yield return null;
                var leaked=Get<HashSet<Enemy>>(runtime,"spawnedEnemies").First();var escape=Escape(leaked);while(escape.MoveNext())yield return escape.Current;
                Require(turn.RemainingLives==9 && wallet.Balance==before && mandate.IsActive,"Actual leak gives no kill credit and retains active multiplier");
                var killed=Get<HashSet<Enemy>>(runtime,"spawnedEnemies").Single();killed.TakeDamage(int.MaxValue);killed.CompleteDeathAnimation();Require(done==1 && wallet.Balance==before+124 && !mandate.IsApplied,"Surviving leak gets kill4 and completion120 once");
            }
            else if(scenario==2)
            {
                var boss=Get<HashSet<Enemy>>(runtime,"spawnedEnemies").Single();Require(boss is BossEnemy && wm.GetCompletionReward(9)==0,"Actual final boss has no generic base completion");
                wallet.Changed+=balance=>{order.Add("credit");Require(balance==before+200 && mandate.IsActive,"Boss credit callback retains2x before win");};boss.TakeDamage(int.MaxValue);
                Require(wins==1 && losses==0 && order.SequenceEqual(new[]{"credit","win"}) && wallet.Balance==before+200 && done==0,"Final boss200 paid before immediate victory without120");boss.CompleteDeathAnimation();Require(wallet.Balance==before+200,"Late boss death never pays again");
            }
            else
            {
                var enemy=Get<HashSet<Enemy>>(runtime,"spawnedEnemies").Single();int lives=turn.RemainingLives;var escape=Escape(enemy);while(escape.MoveNext())yield return escape.Current;
                Require(losses==1 && wins==0 && done==0 && wallet.Balance==before && !mandate.IsApplied,"Normal lethal leak or boss escape loses without rewards/carryover");
                Require(turn.RemainingLives==(scenario==1?0:lives),"Boss escape preserves remaining hearts while normal leak spends last heart");
            }
        }
        var reset=Reload();while(reset.MoveNext())yield return reset.Current;
        var guard=Find<TurnController>();var coin=Find<HeavenlyMandateEffect>();var bag=Find<StageInventory>();var controller=Find<StageItemUseController>();var goods=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var buy=Find<ShopPurchaseController>();var panel=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        Require(!coin.IsApplied && guard.BattleRewardMultiplier==1 && bag.OwnedItems.Count==0 && Find<StageCurrency>().Balance==120,"Fresh scene resets reservation and multiplier");guard.OpenShop();buy.TryPurchase(goods.Items[4]);guard.SetInventoryOpen(true);panel.SelectSlot(0);var useButton=Get<Button>(panel,"useButton");
        Time.timeScale=0;useButton.onClick.Invoke();Require(!controller.TryUse(ShopItemId.HeavenlyMandateCoin) && bag.Contains(ShopItemId.HeavenlyMandateCoin) && !coin.IsApplied,"Paused coin use rejected without consumption");Time.timeScale=1;
        guard.SetInventoryOpen(false);useButton.onClick.Invoke();Require(!coin.IsApplied && bag.Contains(ShopItemId.HeavenlyMandateCoin),"Closed-window stale use rejected");
        coin.enabled=false;Require(!controller.TryUse(ShopItemId.HeavenlyMandateCoin),"Disabled coin effect unregisters");coin.enabled=true;
        var throwing=Find<YutThrowController>();Set(throwing,"<IsThrowing>k__BackingField",true);Require(!controller.TryUse(ShopItemId.HeavenlyMandateCoin),"Throwing prevents use");Set(throwing,"<IsThrowing>k__BackingField",false);
        foreach(string state in new[]{"<IsBattleWon>k__BackingField","<IsBattleLost>k__BackingField"}){Set(guard,state,true);Require(!controller.TryUse(ShopItemId.HeavenlyMandateCoin) && bag.Contains(ShopItemId.HeavenlyMandateCoin),"Terminal state prevents coin consumption");Set(guard,state,false);}
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/HeavenlyMandate");
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
            File.WriteAllBytes("Verification/HeavenlyMandate/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

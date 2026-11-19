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
public static class DokkaebiYutVerification
{
    const string Key = "Yut.DokkaebiYutVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static DokkaebiYutVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Dokkaebi yut verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Exact28-ticket weights, six actual adjusted results/faces/marked animations, one paid use, normal fallback, pending/reentry/state guards, wave carryover and scene reset passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/DokkaebiYutVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
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
    static YutThrowResult Classify(int mask,int marked)
    {
        int count=Enumerable.Range(0,4).Count(i=>(mask&(1<<i))!=0);
        if(count==0)return YutThrowResult.Mo;if(count==4)return YutThrowResult.Yut;if(count==2)return YutThrowResult.Gae;if(count==3)return YutThrowResult.Geol;
        return (mask&(1<<marked))!=0?YutThrowResult.Backdo:YutThrowResult.Do;
    }
    static UnityEngine.Random.State TicketState(int ticket)
    {
        for(int seed=0;seed<10000;seed++){UnityEngine.Random.InitState(seed);var state=UnityEngine.Random.state;UnityEngine.Random.Range(0,4);if(UnityEngine.Random.Range(0,28)==ticket)return state;}
        throw new Exception("Ticket fixture unavailable");
    }
    static YutThrowResult PredictNormal()
    {
        var state=UnityEngine.Random.state;int marked=UnityEngine.Random.Range(0,4),mask=0;
        for(int i=0;i<4;i++)if(UnityEngine.Random.Range(0,2)!=0)mask|=1<<i;
        UnityEngine.Random.state=state;return Classify(mask,marked);
    }
    static void Faces(YutThrowController thrower,YutThrowResult expected)
    {
        var sticks=Get<YutStick[]>(thrower,"sticks");int mask=0,marked=-1,marks=0;
        for(int i=0;i<4;i++)
        {
            var stick=sticks[i];bool belly=Get<YutFace>(stick,"currentFace")==YutFace.Belly;bool mark=Get<bool>(stick,"isMarked");
            if(belly)mask|=1<<i;if(mark){marked=i;marks++;}
            Require(!stick.IsAnimating && Get<SpriteRenderer>(stick,"faceRenderer").sprite==Get<Sprite>(stick,mark?(belly?"markedBellySprite":"markedBackSprite"):(belly?"bellySprite":"backSprite")),"Final real stick sprite agrees with face and X mark");
        }
        Require(marks==1 && Classify(mask,marked)==expected,"Four final faces and exactly one marked stick agree with result");
        Require(Get<Image>(thrower,"resultImage").sprite==Get<Sprite>(thrower,expected.ToString().ToLowerInvariant()+"Sprite"),"Actual result image agrees with adjusted faces");
    }
    static IEnumerator Check()
    {
        yield return null;
        var method=typeof(DokkaebiYutEffect).GetMethod("ResultForTicket",BindingFlags.Static|BindingFlags.NonPublic);
        var counts=Enum.GetValues(typeof(YutThrowResult)).Cast<YutThrowResult>().ToDictionary(r=>r,r=>0);
        for(int ticket=0;ticket<28;ticket++)counts[(YutThrowResult)method.Invoke(null,new object[]{ticket})]++;
        Require(counts[YutThrowResult.Backdo]==1 && counts[YutThrowResult.Do]==3 && counts[YutThrowResult.Gae]==6 && counts[YutThrowResult.Geol]==4 && counts[YutThrowResult.Yut]==7 && counts[YutThrowResult.Mo]==7,"All28 equiprobable tickets give exact1/3/6/4/7/7 weights");
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        int[] tickets={0,1,4,10,14,21};
        for(int round=0;round<tickets.Length;round++)
        {
            if(round>0){var reload=Reload();while(reload.MoveNext())yield return reload.Current;}
            Time.timeScale=10;
            var turn=Find<TurnController>();var w=Find<StageCurrency>();var inv=Find<StageInventory>();var use=Find<StageItemUseController>();var effect=Find<DokkaebiYutEffect>();var thrower=Find<YutThrowController>();var purchase=Find<ShopPurchaseController>();
            var shop=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var ui=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            Require(!effect.HasPending && !use.TryUse(ShopItemId.DokkaebiYut) && !((IStageItemEffect)effect).TryApply(),"Fresh effect cannot be bypassed or used unowned");
            Funds(w,300);turn.OpenShop();if(round%2==0)Require(purchase.TryPurchase(shop.Items[1]),"Real talisman fixture purchase");Require(purchase.TryPurchase(shop.Items[3]),"Real dokkaebi yut costs50");
            int before=w.Balance;Call(turn,"ReceiveYutResult",YutThrowResult.Gae);turn.SetInventoryOpen(true);Require(ui.SelectSlot(round%2==0?1:0),"Select actual owned dokkaebi yut");
            var button=Get<Button>(ui,"useButton");int consumed=0;Action changed=()=>{consumed++;Require(effect.HasPending && !inv.Contains(ShopItemId.DokkaebiYut) && use.IsUsing,"Consumption observers see pending effect committed");Require(!use.TryUse(ShopItemId.DokkaebiYut),"Consumption callback cannot use twice");};inv.Changed+=changed;yield return null;button.OnPointerClick(pointer);inv.Changed-=changed;
            Require(consumed==1 && effect.HasPending && !inv.Contains(ShopItemId.DokkaebiYut) && w.Balance==before && turn.CurrentYutResult==YutThrowResult.Gae,"Use consumes once, reserves next throw and preserves existing result/currency");
            while(Mathf.Abs(((RectTransform)ui.transform).anchoredPosition.y)>.1f)yield return null;
            yield return null;Require(Get<Text>(ui,"pendingEffectStatus").text.Contains("대기 중"),"Pending status remains visible after item is consumed");
            if(round==0){Time.timeScale=1;Capture("DokkaebiYutPending");Time.timeScale=10;}
            Set(turn,"<CurrentYutResult>k__BackingField",null);turn.SetInventoryOpen(false);
            var expected=(YutThrowResult)method.Invoke(null,new object[]{tickets[round]});int cost=turn.CurrentYutThrowCost;int debit=0,completed=0;
            Action<int> reenter=balance=>{debit++;turn.ThrowYut();thrower.Throw();Require(effect.HasPending && !use.TryUse(ShopItemId.DokkaebiYut),"Payment callback cannot reenter throw or use; pending survives until paid start commits");};
            Action<YutThrowResult> done=result=>{completed++;Require(result==expected && !effect.HasPending,"One completion reports expected ticket and effect is cleared");};
            w.Changed+=reenter;thrower.ThrowCompleted+=done;UnityEngine.Random.state=TicketState(tickets[round]);Get<GameObject>(turn,"throwButton").GetComponent<Button>().OnPointerClick(pointer);w.Changed-=reenter;
            Require(thrower.IsThrowing && !effect.HasPending && w.Balance==before-cost && debit==1,"Valid throw pays current20/16 once and consumes pending once");
            if(round==0){Time.timeScale=0;yield return null;yield return null;Require(thrower.IsThrowing && completed==0,"Paused animation cannot complete early");Time.timeScale=10;}
            while(thrower.IsThrowing)yield return null;thrower.ThrowCompleted-=done;
            Require(completed==1 && thrower.LastResult==expected && w.Balance==before-cost,"Actual animation completes once without extra payment");Faces(thrower,expected);
            if(expected==YutThrowResult.Yut){Time.timeScale=1;Capture("DokkaebiYutResult");Time.timeScale=10;}
            Set(turn,"<CurrentYutResult>k__BackingField",null);UnityEngine.Random.InitState(100+round);var normal=PredictNormal();int balance=w.Balance;turn.ThrowYut();while(thrower.IsThrowing)yield return null;
            Require(thrower.LastResult==normal && w.Balance==balance-cost && !effect.HasPending,"Immediately following throw returns to original independent face RNG");Faces(thrower,normal);
        }
        var reset=Reload();while(reset.MoveNext())yield return reset.Current;
        var t=Find<TurnController>();var wallet=Find<StageCurrency>();var inventory=Find<StageInventory>();var executor=Find<StageItemUseController>();var real=Find<DokkaebiYutEffect>();var tc=Find<YutThrowController>();var purchases=Find<ShopPurchaseController>();
        var catalog=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var view=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        t.OpenShop();Require(purchases.TryPurchase(catalog.Items[3]),"Guard fixture owns real item");t.SetInventoryOpen(true);view.SelectSlot(0);var useButton=Get<Button>(view,"useButton");
        Time.timeScale=0;useButton.onClick.Invoke();Require(inventory.Contains(ShopItemId.DokkaebiYut) && !real.HasPending,"Pause blocks consumption");Time.timeScale=1;
        t.SetInventoryOpen(false);useButton.onClick.Invoke();Require(inventory.Contains(ShopItemId.DokkaebiYut) && !real.HasPending,"Closed window stale use rejected");
        real.enabled=false;Require(!executor.TryUse(ShopItemId.DokkaebiYut),"Disabled effect unregisters");real.enabled=true;
        Set(tc,"<IsThrowing>k__BackingField",true);Require(!executor.TryUse(ShopItemId.DokkaebiYut),"Throwing blocks use");Set(tc,"<IsThrowing>k__BackingField",false);
        foreach(string state in new[]{"<IsBattleWon>k__BackingField","<IsBattleLost>k__BackingField"}){Set(t,state,true);Require(!executor.TryUse(ShopItemId.DokkaebiYut) && inventory.Contains(ShopItemId.DokkaebiYut),"Terminal states preserve item");Set(t,state,false);}
        t.SetInventoryOpen(true);view.SelectSlot(0);yield return null;useButton.OnPointerClick(pointer);Require(real.HasPending && !inventory.Contains(ShopItemId.DokkaebiYut),"Valid use creates pending before battle");
        Call(inventory,"TryAdd",ShopItemId.DokkaebiYut);Call(inventory,"NotifyChanged");view.SelectSlot(0);yield return null;useButton.onClick.Invoke();Require(!executor.TryUse(ShopItemId.DokkaebiYut) && inventory.Contains(ShopItemId.DokkaebiYut) && !useButton.interactable,"Injected duplicate ownership is retained without consuming/stacking while pending");Call(inventory,"TryRemove",ShopItemId.DokkaebiYut);
        t.SetInventoryOpen(false);Funds(wallet,19);t.ThrowYut();Require(real.HasPending && !tc.IsThrowing && wallet.Balance==19,"Insufficient throw retains pending and balance");
        Funds(wallet,100);var sticks=Get<YutStick[]>(tc,"sticks");sticks[0].enabled=false;t.ThrowYut();Require(real.HasPending && !tc.IsThrowing && wallet.Balance==100,"Unready stick prevents payment/pending consumption");sticks[0].enabled=true;
        t.SetInventoryOpen(true);t.ThrowYut();Require(real.HasPending && wallet.Balance==100,"Open inventory prevents pending consumption");t.SetInventoryOpen(false);
        Time.timeScale=0;t.ThrowYut();Require(real.HasPending && wallet.Balance==100,"Pause prevents pending consumption");Time.timeScale=1;
        var manager=Find<WaveManager>();var waveData=ScriptableObject.CreateInstance<WaveData>();Set(waveData,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});Get<WaveData[]>(manager,"waves")[0]=waveData;
        t.StartBattle();Require(manager.IsRunning && real.HasPending && !executor.TryUse(ShopItemId.DokkaebiYut),"Combat entered without throwing retains pending and rejects use");t.ThrowYut();Require(real.HasPending && wallet.Balance==100,"Combat throw never consumes pending");
        var wave=Find<Wave>();var enemy=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();Require(t.CurrentTurnNumber==1 && real.HasPending,"Actual next preparation retains pending effect");
        Time.timeScale=10;UnityEngine.Random.state=TicketState(21);int beforeWaveThrow=wallet.Balance;t.ThrowYut();while(tc.IsThrowing)yield return null;Require(tc.LastResult==YutThrowResult.Mo && !real.HasPending && wallet.Balance==beforeWaveThrow-20,"First next-wave throw applies waiting effect exactly once");Faces(tc,YutThrowResult.Mo);
        var reloadNew=Reload();while(reloadNew.MoveNext())yield return reloadNew.Current;
        Require(!Find<DokkaebiYutEffect>().HasPending && Find<StageInventory>().OwnedItems.Count==0 && Find<StageCurrency>().Balance==120,"Fresh scene resets pending, inventory and balance");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/DokkaebiYut");
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
            File.WriteAllBytes("Verification/DokkaebiYut/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

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
public static class InventoryVerification
{
    const string Key = "Yut.InventoryVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static InventoryVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Inventory verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Nine slots, owned-only selection, shared descriptions, passive status, safe effect dispatch, consumption/reentry guards, panel lifetime and preparation restrictions passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/InventoryVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
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
    sealed class Effect : IStageItemEffect
    {
        public bool allowed = true, applied, succeeds = true;
        public int calls;
        public Action callback;
        public bool CanApply => allowed;
        public bool IsApplied => applied;
        public bool TryApply() { calls++; callback?.Invoke(); if (!succeeds) return false; applied = true; return true; }
    }
    static IEnumerator Check()
    {
        yield return null;
        // Isolate the generic dispatch fixture. Real soul-crystal effects are covered separately.
        Find<SoulCrystalEffect>().enabled=false;
        Find<HeavenlyLuckTalismanEffect>().enabled=false;
        var turn=Find<TurnController>(); var inv=Find<StageInventory>(); var use=Find<StageItemUseController>(); var wallet=Find<StageCurrency>();
        var view=UnityEngine.Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        var shop=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var purchases=Find<ShopPurchaseController>();
        var slots=Get<Button[]>(view,"slots");var icons=Get<Image[]>(view,"icons");var highlights=Get<Image[]>(view,"highlights");
        var button=Get<Button>(view,"useButton");var detail=Get<GameObject>(view,"detail");
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        Require(slots.Length==9 && icons.Length==9 && highlights.Length==9,"Nine saved clickable slots/icons/highlights");
        turn.SetInventoryOpen(true);Require(view.gameObject.activeInHierarchy && view.SelectedItem==null && !detail.activeSelf,"Open empty inventory without fabricated selection");
        Require(slots.All(s=>!s.interactable) && icons.All(s=>!s.enabled) && Get<Text>(view,"emptyHint").gameObject.activeSelf,"All empty slots visibly empty and disabled");
        for(int i=0;i<9;i++){slots[i].onClick.Invoke();Require(!view.SelectSlot(i) && view.SelectedItem==null,"Empty slot cannot select even forced callback");}
        Require(!view.SelectSlot(-1) && !view.SelectSlot(9),"Out-of-range selection rejected");
        button.onClick.Invoke();Require(inv.OwnedItems.Count==0,"Empty forced use has no effect");
        yield return new WaitForSecondsRealtime(.6f);Capture("InventoryEmpty");
        turn.SetInventoryOpen(false);Funds(wallet,1000);turn.OpenShop();
        foreach(var item in shop.Items) Require(purchases.TryPurchase(item),"Purchase real canonical item for inventory");
        turn.SetInventoryOpen(true);Require(!shop.gameObject.activeSelf && inv.OwnedItems.Count==5,"Inventory opening closes shop and retains five purchases");
        Require(slots.Take(5).All(s=>s.interactable) && slots.Skip(5).All(s=>!s.interactable),"Five owned slots and four expansion blanks");
        Require(!Get<Text>(view,"emptyHint").gameObject.activeSelf,"No empty hint when owned");
        for(int i=0;i<5;i++)
        {
            slots[i].OnPointerClick(pointer);var item=shop.Items[i];
            Require(view.SelectedItem==item && detail.activeSelf && Get<Image>(view,"selectedIcon").sprite==item.Icon,"Real pointer selects owned canonical definition");
            Require(Get<Text>(view,"selectedName").text==item.DisplayName && Get<Text>(view,"description").text=="배경: "+item.Background+"\n\n효과: "+item.Effect,"Shared latest name/background/effect description");
            Canvas.ForceUpdateCanvases();Require(Get<Text>(view,"description").preferredHeight<=Get<Text>(view,"description").rectTransform.rect.height,"Full description fits assigned area");
            Require(highlights.Count(h=>h.enabled)==1 && highlights[i].enabled,"Exactly one selection marker");
            bool passive=item.Id==ShopItemId.HeavenlyLuckTalisman;
            Require(button.gameObject.activeSelf==!passive && !button.interactable,"Use visible only for consumables, no premature effect execution");
            int balance=wallet.Balance;button.onClick.Invoke();Require(inv.OwnedItems.Count==5 && wallet.Balance==balance,"Unregistered effect never consumes or grants");
        }
        Require(!view.SelectSlot(5) && view.SelectedItem==shop.Items[4],"Blank click preserves actual selection");
        view.SelectSlot(1);Require(Get<Text>(view,"status").text=="보유 중","Passive never falsely claims unimplemented discount");
        var passiveEffect=new Effect{applied=true};Require(use.Register(ShopItemId.HeavenlyLuckTalisman,passiveEffect),"Explicit passive state provider can register");
        yield return null;Require(Get<Text>(view,"status").text=="적용 중" && !button.gameObject.activeSelf && !use.TryUse(ShopItemId.HeavenlyLuckTalisman),"Applied-state path shows without passive use");
        yield return new WaitForSecondsRealtime(.6f);Capture("InventoryPassive");
        use.Unregister(ShopItemId.HeavenlyLuckTalisman,passiveEffect);yield return null;
        Require(Get<Text>(view,"status").text=="보유 중","Removing provider refreshes state");
        view.SelectSlot(0);yield return null;Capture("InventoryConsumable");
        Require(detail.GetComponent<Image>().material.shader.isSupported && detail.GetComponent<Image>().material.shader.name=="UI/InventoryFrame" && button.image.color.r<.6f,"Frame cutout shader supported and unavailable use visibly dimmed");
        var effect=new Effect{succeeds=false};Require(use.Register(ShopItemId.SoulCrystal,effect) && !use.Register(ShopItemId.SoulCrystal,new Effect()),"One explicit effect per item");
        yield return null;Require(button.interactable,"Registered eligible effect enables real use button");
        button.OnPointerClick(pointer);Require(effect.calls==1 && inv.Contains(ShopItemId.SoulCrystal) && !use.IsUsing,"Failed effect retains item and releases lock");
        effect.allowed=false;yield return null;button.onClick.Invoke();Require(!button.interactable && effect.calls==1,"Effect readiness rechecked even forced click");
        effect.allowed=true;effect.succeeds=true;int notifications=0;
        effect.callback=()=>{
            Require(use.IsUsing && !turn.CanUsePreparationInput && !use.TryUse(ShopItemId.SoulCrystal),"Effect callback rejects reentrant use");
            Require(!purchases.TryPurchase(shop.Items[0]) && !Find<SpecialSummonController>().TrySummon() && !Find<YutActionController>().DeployAlly(YutThrowResult.Do),"Other preparation actions blocked during use");
            turn.StartBattle();turn.ThrowYut();Require(!Find<WaveManager>().IsRunning && !turn.IsThrowing,"Battle and throw reentry blocked");
        };
        Action changed=()=>{notifications++;Require(use.IsUsing && !inv.Contains(ShopItemId.SoulCrystal) && effect.applied,"Inventory observers see successful effect and removed item under lock");};
        inv.Changed+=changed;yield return null;button.OnPointerClick(pointer);inv.Changed-=changed;
        Require(effect.calls==2 && notifications==1 && inv.OwnedItems.Count==4 && !use.IsUsing,"Successful UI dispatch consumes once");
        Require(view.SelectedItem==null && !detail.activeSelf && icons[0].sprite==shop.Items[1].Icon,"Consumed selection clears and remaining slots compact");
        Require(purchases.IsPurchased(ShopItemId.SoulCrystal) && !purchases.TryPurchase(shop.Items[0]),"Use does not restock shop");
        Require(!use.TryUse(ShopItemId.SoulCrystal) && effect.calls==2,"Used item cannot execute twice");
        view.SelectSlot(0);Get<Button>(view,"backButton").OnPointerClick(pointer);Require(view.SelectedItem==null && !detail.activeSelf && view.gameObject.activeSelf,"Back clears detail while retaining inventory");
        var pouch=new Effect();use.Register(ShopItemId.DokkaebiPouch,pouch);view.SelectSlot(1);
        turn.SetInventoryOpen(false);button.onClick.Invoke();Require(pouch.calls==0 && !detail.activeSelf,"Late click after closing has no effect");
        turn.SetInventoryOpen(true);Require(view.SelectedItem==null && slots.Take(4).All(s=>s.interactable),"Reopen retains owned data and clears selection");
        view.SelectSlot(1);Time.timeScale=0;button.onClick.Invoke();Require(!view.SelectSlot(0) && !use.TryUse(ShopItemId.DokkaebiPouch) && pouch.calls==0,"Pause blocks selection and use synchronously");
        yield return null;Require(!view.gameObject.activeSelf,"Paused inventory closes and toggle synchronizes");Time.timeScale=1;
        foreach(string state in new[]{"<IsBattleWon>k__BackingField","<IsBattleLost>k__BackingField"})
        { Set(turn,state,true);turn.SetInventoryOpen(true);Require(!view.gameObject.activeSelf && !use.TryUse(ShopItemId.DokkaebiPouch),"Terminal state blocks inventory and use");Set(turn,state,false); }
        var thrower=Get<YutThrowController>(turn,"throwController");Set(thrower,"<IsThrowing>k__BackingField",true);turn.SetInventoryOpen(true);
        Require(!view.gameObject.activeSelf && !use.TryUse(ShopItemId.DokkaebiPouch),"Throwing blocks open/use");Set(thrower,"<IsThrowing>k__BackingField",false);
        inv.enabled=false;Require(!use.TryUse(ShopItemId.DokkaebiPouch),"Disabled inventory rejects consumption");inv.enabled=true;
        use.enabled=false;Require(!use.TryUse(ShopItemId.DokkaebiPouch),"Disabled executor rejects use");use.enabled=true;
        turn.SetInventoryOpen(true);view.SelectSlot(1);turn.OpenShop();Require(!view.gameObject.activeSelf && !detail.activeSelf,"Shop opening closes inventory detail");turn.CloseShop();
        turn.SetInventoryOpen(true);view.SelectSlot(1);
        var manager=Find<WaveManager>();var waveData=ScriptableObject.CreateInstance<WaveData>();Set(waveData,"enemies",new[]{new WaveData.EnemyGroup{enemyId=EnemyId.NormalEnemy,count=1,health=125}});Get<WaveData[]>(manager,"waves")[0]=waveData;
        turn.StartBattle();Require(manager.IsRunning && !view.gameObject.activeSelf && !use.TryUse(ShopItemId.DokkaebiPouch),"Actual combat closes inventory and rejects use");
        var wave=Find<Wave>();var enemy=Get<HashSet<Enemy>>(wave,"spawnedEnemies").Single();enemy.TakeDamage(int.MaxValue);enemy.CompleteDeathAnimation();
        turn.SetInventoryOpen(true);Require(turn.CurrentTurnNumber==1 && view.gameObject.activeSelf && inv.OwnedItems.Count==4,"Actual next preparation retains ownership and restores access");
        Require(pouch.calls==0,"Rejected states never called effect");
        var reload=Reload();while(reload.MoveNext())yield return reload.Current;
        Require(Find<StageInventory>().OwnedItems.Count==0,"Fresh scene starts empty");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/Inventory");
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
            File.WriteAllBytes("Verification/Inventory/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

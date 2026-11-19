// Verification template only. Copy into the isolated project; never part of the source game's Assets.
#if !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainEntryPlayerProbe : MonoBehaviour
{
    [Serializable] sealed class Result
    {
        public bool passed, quitConfirmed;public int assertions,runtimeErrors,bootIndex;public string bootScene,unity,detail;public string[] scenes;
    }
    string directory,bootScene;int bootIndex,assertions,errors;bool confirmingQuit,failed;IEnumerator checks;double started;readonly List<string> scenes=new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-verify-main-entry");if(index<0 || index+1>=args.Length)return;
        var go=new GameObject("Main Entry Verification Probe");DontDestroyOnLoad(go);var probe=go.AddComponent<MainEntryPlayerProbe>();
        probe.directory=args[index+1];Directory.CreateDirectory(probe.directory);probe.bootScene=SceneManager.GetActiveScene().name;probe.bootIndex=SceneManager.GetActiveScene().buildIndex;probe.scenes.Add(probe.bootScene);
        probe.started=Time.realtimeSinceStartupAsDouble;Application.runInBackground=true;Application.targetFrameRate=60;
        Application.logMessageReceived+=probe.Error;SceneManager.sceneLoaded+=probe.Loaded;Application.quitting+=probe.Quitting;probe.checks=probe.Check();
    }
    void Loaded(Scene scene,LoadSceneMode mode)=>scenes.Add(scene.name);
    void Error(string message,string trace,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors++;}
    void Update()
    {
        if(failed)return;
        try
        {
            if(Time.realtimeSinceStartupAsDouble-started>180)throw new Exception("Player entry verification timeout");
            if(checks!=null && !confirmingQuit)checks.MoveNext();
        }
        catch(Exception exception){failed=true;Write(false,exception.ToString());Application.Quit(1);}
    }
    void Quitting()
    {
        if(failed)return;
        try
        {
            var exit=Find<StageExitConfirmation>();Require(confirmingQuit && exit!=null && exit.IsCommitting && !exit.IsOpen && Time.timeScale==1,"Actual player quit follows explicit confirmation");
            Require(errors==0,"No player runtime errors");Write(true,"Windows player booted Title0; start, cancel, confirmed home, restart, paid throw and actual Application.Quit passed.");
        }
        catch(Exception exception){Write(false,exception.ToString());}
    }
    void Write(bool passed,string detail)=>File.WriteAllText(Path.Combine(directory,"BuildMainEntryPlayer.json"),JsonUtility.ToJson(new Result{passed=passed,quitConfirmed=confirmingQuit && passed,assertions=assertions,runtimeErrors=errors,bootScene=bootScene,bootIndex=bootIndex,unity=Application.unityVersion,scenes=scenes.ToArray(),detail=detail},true));
    static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindFirstObjectByType<T>();
    static T Get<T>(object obj,string name)=> (T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    void Require(bool condition,string message){assertions++;if(!condition)throw new Exception("Check "+assertions+": "+message);}
    static void Click(Button button)=>button.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});
    void Fresh()
    {
        var t=Find<TurnController>();var w=Find<StageCurrency>();
        Require(SceneManager.GetActiveScene().name=="ThrowYut" && SceneManager.GetActiveScene().buildIndex==1,"Game is actual build scene1");
        Require(t.CurrentTurnNumber==0 && t.RemainingLives==10 && !t.IsBattleWon && !t.IsBattleLost && t.CanUsePreparationInput,"New game wave1/heart10/preparation");
        Require(w.Balance==120 && w.SpecialBalance==0 && Time.timeScale==1 && Find<GameSpeedButton>().CurrentSpeed==1,"New game wallet120/0 and1x");
        var catalog=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);Require(catalog.Items.All(item=>!Find<ShopPurchaseController>().IsPurchased(item.Id)) && Find<StageInventory>().OwnedItems.Count==0,"No previous purchases or inventory");
        Require(!Find<DokkaebiYutEffect>().HasPending && !Find<HeavenlyMandateEffect>().IsApplied && t.CurrentYutThrowCost==20 && t.BattleRewardMultiplier==1,"No previous item effects");
        Require(UnityEngine.Object.FindObjectsByType<Ally>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0 && !Find<StageExitConfirmation>().IsOpen,"No previous units or exit modal");
    }
    IEnumerator Check()
    {
        for(int i=0;i<12;i++)yield return null;
        Require(bootScene=="Title" && bootIndex==0,"Actual cold boot starts Title at index0");
        Require(SceneUtility.GetScenePathByBuildIndex(0)=="Assets/Scenes/Title.unity" && SceneUtility.GetScenePathByBuildIndex(1)=="Assets/Scenes/ThrowYut.unity","Built scene table follows source settings");
        var title=Find<TitleScreenController>();Require(title!=null && !title.IsMenuOpen,"Cold boot opens actual title prompt");Capture("BuildMainEntryTitle");title.ShowMenu();Time.timeScale=4;var start=Get<Button[]>(title,"menuButtons")[0];var late=start.onClick;Click(start);late.Invoke();
        while(title!=null)yield return null;for(int i=0;i<12;i++)yield return null;Fresh();Require(scenes.SequenceEqual(new[]{"Title","ThrowYut"}),"Title starts game once");Capture("BuildMainEntryGame");
        var t=Find<TurnController>();var w=Find<StageCurrency>();w.Grant(880);w.GrantSpecial(45);Require(Find<YutActionController>().DeployAlly(YutThrowResult.Do),"Actual unit before home return");
        t.OpenShop();var catalog=UnityEngine.Object.FindFirstObjectByType<ShopCatalogUI>(FindObjectsInactive.Include);var buying=Find<ShopPurchaseController>();foreach(int index in new[]{1,3,4})Require(buying.TryPurchase(catalog.Items[index]),"Actual purchases before home return");t.CloseShop();
        Require(Find<StageItemUseController>().TryUse(ShopItemId.DokkaebiYut) && Find<StageItemUseController>().TryUse(ShopItemId.HeavenlyMandateCoin),"Actual reserved item effects before home return");Find<GameSpeedButton>().GetComponent<Button>().onClick.Invoke();
        var exit=Find<StageExitConfirmation>();int balance=w.Balance;Click(Get<Button>(exit,"homeButton"));Require(exit.IsOpen && Time.timeScale==0,"Live-player home requires confirmation");for(int i=0;i<3;i++)yield return null;Click(Get<Button>(exit,"cancelButton"));
        Require(!exit.IsOpen && Time.timeScale==2 && w.Balance==balance && w.SpecialBalance==45 && Find<DokkaebiYutEffect>().HasPending && Find<HeavenlyMandateEffect>().HasPending,"Actual player cancel preserves progress and speed");yield return null;
        Click(Get<Button>(exit,"homeButton"));Click(Get<Button>(exit,"confirmButton"));while(t!=null)yield return null;for(int i=0;i<12;i++)yield return null;
        Require(SceneManager.GetActiveScene().name=="Title" && Time.timeScale==1 && Find<TurnController>()==null,"Confirmed return reaches actual title and discards game");title=Find<TitleScreenController>();Require(title!=null && !title.IsMenuOpen,"Returned title resets prompt");title.ShowMenu();Click(Get<Button[]>(title,"menuButtons")[0]);while(title!=null)yield return null;for(int i=0;i<12;i++)yield return null;Fresh();
        Require(scenes.SequenceEqual(new[]{"Title","ThrowYut","Title","ThrowYut"}),"Actual scene trace is title/game/title/game");t=Find<TurnController>();t.ThrowYut();Require(t.IsThrowing && Find<StageCurrency>().Balance==100,"Restarted game first real throw pays20");while(t.IsThrowing)yield return null;
        exit=Find<StageExitConfirmation>();Click(Get<Button>(exit,"quitButton"));Require(exit.IsOpen && !exit.IsCommitting,"Actual player does not quit before confirmation");Capture("BuildMainEntryQuit");confirmingQuit=true;Click(Get<Button>(exit,"confirmButton"));yield return null;
    }
    void Capture(string name)
    {
        var camera=Camera.main;var canvas=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c=>c.isRootCanvas && (c.name=="Canvas" || c.name=="Title Canvas"));
        var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float plane=canvas.planeDistance;bool sorting=canvas.overrideSorting;int order=canvas.sortingOrder;var target=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.overrideSorting=true;canvas.sortingOrder=32767;
            for(int i=0;i<2;i++){Canvas.ForceUpdateCanvases();camera.Render();}RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=plane;canvas.overrideSorting=sorting;canvas.sortingOrder=order;camera.targetTexture=target;RenderTexture.active=active;UnityEngine.Object.Destroy(image);rt.Release();UnityEngine.Object.Destroy(rt);
        }
    }
}
#endif

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
public static class CharacterInfoContentVerification
{
    const string Key = "Yut.CharacterInfoContentVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static int readyFrames;
    static CharacterInfoContentVerification() { EditorApplication.update += Tick; }
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
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Character info content verification timeout");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Eleven actual prefabs, direct board hit-testing, individual info/group movement, fixed background, content restoration, names/portraits/stars/live stats, prepaid state, rapid selection, invalid targets and pause/combat guards passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors++; }
    [Serializable] sealed class Result { public bool passed; public int assertions, runtimeErrors; public string unity, scene, detail; }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1; SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CharacterInfoContentVerification.json", JsonUtility.ToJson(new Result { passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static FieldInfo Field(object o, string name)
    { for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); if (f != null) return f; } throw new Exception(name); }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static Ally[] At(YutActionController a,int cell)=>( (IReadOnlyList<Ally>)Call(Get<YutBoardOccupancy>(a,"occupancy"),"GetAllies",cell)).ToArray();
    static Ally Register(YutActionController a,Ally prefab,int cell)
    {var unit=Find<AllyPool>().Request(prefab,Get<Transform[]>(a,"cells")[cell]);Require((bool)Call(a,"RegisterAlly",cell,unit),"Registered actual prefab");return unit;}
    static void Clear(YutActionController a,int cell)
    {var units=At(a,cell);if(units.Length==0)return;Call(a,"ClearSelection");Require((bool)Call(Get<YutBoardOccupancy>(a,"occupancy"),"TryRemoveAllies",(object)units),"Clear fixture occupancy");foreach(var u in units)Find<AllyPool>().Return(u);}
    static void Click(TurnController turn,Vector2 point)=>Call(turn,"HandleBoardClick",point);
    static Vector2 Point(Ally ally)=>ally.VisualRenderer.bounds.center;
    static IEnumerator Settle(CharacterInfoPanel panel)
    {
        double end = EditorApplication.timeSinceStartup + 2;
        while (panel.IsTransitioning)
        {
            if (EditorApplication.timeSinceStartup > end) throw new Exception("Info transition did not settle");
            yield return null;
        }
    }
    static IEnumerator Check()
    {
        yield return null;yield return null;
        var t=Find<TurnController>();var a=Find<YutActionController>();var range=Find<AllyRangeDisplay>();var wallet=Find<StageCurrency>();
        var panel=UnityEngine.Object.FindFirstObjectByType<CharacterInfoPanel>(FindObjectsInactive.Include);
        var groups=Get<CanvasGroup[]>(panel,"defaultContent");var sticks=Get<SpriteRenderer[]>(panel,"defaultWorldContent");
        Require(groups.Length==7 && sticks.Length==4,"Default UI groups and four stick renderers connected");
        Require(panel.GetComponent<Image>().color.a==0,"Info surface cannot redraw the fixed background/frame");
        var background=GameObject.Find("Background").GetComponent<SpriteRenderer>();var frame=background.sprite;Vector3 bgPosition=background.transform.position,bgScale=background.transform.localScale;
        Require(!panel.gameObject.activeSelf && groups.All(g=>g.alpha==1) && sticks.All(r=>r.enabled),"Initial default content visible");
        var infoButton=Resources.FindObjectsOfTypeAll<GameObject>().Single(g=>g.scene.IsValid()&&g.name=="Character_Info");Require(!infoButton.activeSelf,"Extra info button removed from selection flow");
        var names=new[]{"Do_Taoist","Gae_Warrior","Geol_Spearman","Yut_Maceman","Mo_Archer","WhiteTiger"};
        Sprite previousName=null;string previousStory=null;
        foreach(string name in names)foreach(int stage in name=="WhiteTiger"?new[]{1}:new[]{1,2})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/"+name+(stage==2?"_2nd_Evolution":"")+".prefab");var unit=Register(a,prefab,1);
            Click(t,Point(unit));
            var settle = Settle(panel); while (settle.MoveNext()) yield return settle.Current;
            Require(panel.gameObject.activeSelf && panel.CurrentAlly==unit,"Direct board click opens individual info "+name+stage);
            Require(Get<Ally>(range,"rangeAlly")==unit,"Single clicked unit owns range circle");
            Require(Get<Image>(panel,"characterName").sprite!=null && Get<Image>(panel,"characterPortrait").sprite!=null && Get<Image>(panel,"characterPortrait").preserveAspect,"Name and proportion-preserving portrait");
            Require(Get<GameObject>(panel,"firstStar").activeSelf && Get<GameObject>(panel,"secondStar").activeSelf==(stage==2),"Actual evolution stage stars");
            Require(Get<Text>(panel,"attackValue").text==unit.AttackDamage.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture) &&
                Get<Text>(panel,"attackSpeedValue").text==unit.AttacksPerSecond.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"회/초","Actual individual ATK and AS");
            Require(groups.All(g=>g.alpha==0&&!g.interactable&&!g.blocksRaycasts) && sticks.All(r=>!r.enabled),"Only default content hidden");
            string story=string.Join("\n",Get<Text[]>(panel,"storyLines").Select(line=>line.text));
            if(stage==1){previousName=Get<Image>(panel,"characterName").sprite;previousStory=story;}
            else Require(previousName==Get<Image>(panel,"characterName").sprite && story==previousStory,"Same kind shares approved name/story across stages");
            if(name=="WhiteTiger")Require(Get<GameObject[]>(panel,"storyDecorations").All(g=>!g.activeSelf),"Unknown tiger lore not fabricated");
            Require(background.sprite==frame && background.transform.position==bgPosition && background.transform.localScale==bgScale,"Fixed background/frame unchanged");
            Require(wallet.Balance==120 && wallet.SpecialBalance==0 && t.CurrentYutResult==null && At(a,1).Single()==unit,"Info alone never mutates game state");
            if((name=="Gae_Warrior" && stage==2)||name=="WhiteTiger"){yield return null;Capture(name=="WhiteTiger"?"InfoWhiteTiger":"InfoWarriorTwoStar");}
            Click(t,Get<Transform[]>(a,"cells")[0].position);
            settle = Settle(panel); while (settle.MoveNext()) yield return settle.Current;
            Require(!panel.gameObject.activeSelf && groups.All(g=>g.alpha==1&&g.interactable&&g.blocksRaycasts) && sticks.All(r=>r.enabled),"Empty board restores default content");
            Clear(a,1);
        }
        var gae=AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/Gae_Warrior.prefab");
        var group=new[]{Register(a,gae,5),Register(a,gae,5),Register(a,gae,5)};
        wallet.GrantSpecial(45);Call(t,"ReceiveYutResult",YutThrowResult.Do);var result=t.CurrentYutResult;
        Click(t,Point(group[1]));Require(panel.CurrentAlly==group[1] && At(a,5).Length==3 && t.CurrentYutResult==result,"Grouped unit info remains individual and preserves result");
        t.ChooseMove();Click(t,Point(group[1]));
        Require(panel.CurrentAlly==group[1] && Get<List<Ally>>(a,"selectedAllies").Count==3,"Individual info alongside three-unit movement selection");
        Click(t,Get<Transform[]>(a,"cells")[0].position);
        var returning = Settle(panel); while (returning.MoveNext()) yield return returning.Current;
        Require(!panel.gameObject.activeSelf && t.CurrentYutResult==result && At(a,5).Length==3 && wallet.SpecialBalance==45,"Empty click hides info without cancelling result or moving stack");
        Click(t,Point(group[0]));Click(t,Get<Transform[]>(a,"cells")[6].position);
        Require(!t.CurrentYutResult.HasValue && At(a,6).Length==3 && At(a,5).Length==0 && wallet.SpecialBalance==45,"Destination handling retains priority and normal group movement");
        Click(t,Point(group[2]));group[2].gameObject.SetActive(false);yield return null;yield return null;
        Require(!panel.gameObject.activeSelf && groups.All(g=>g.alpha==1),"Inactive selected unit removes stale info");Clear(a,6);
        var tiger=Register(a,AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/WhiteTiger.prefab"),1);
        var taoist=Register(a,AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/Do_Taoist.prefab"),2);
        for(int i=0;i<10;i++){Click(t,Point(i%2==0?tiger:taoist));Require(panel.CurrentAlly==(i%2==0?tiger:taoist),"Rapid reselection uses latest target");}
        t.OpenShop();Click(t,Point(tiger));Require(!Get<GameObject>(t,"shopPanel").activeSelf && panel.CurrentAlly==tiger,"Info replaces open shop content");
        Click(t,Get<Transform[]>(a,"cells")[0].position);
        returning = Settle(panel); while (returning.MoveNext()) yield return returning.Current;
        Capture("InfoReturnedToYut");
        Require(!panel.gameObject.activeSelf && !Get<GameObject>(t,"shopPanel").activeSelf,"Empty click returns to Yut mode");
        Click(t,Point(tiger));Time.timeScale=0;yield return null;yield return null;
        Require(!panel.gameObject.activeSelf,"Paused preparation hides info");Click(t,Point(tiger));Require(!panel.gameObject.activeSelf,"Paused board click cannot reopen");Time.timeScale=1;
        Click(t,Point(tiger));t.StartBattle();yield return null;
        Require(Find<WaveManager>().IsRunning && !panel.gameObject.activeSelf,"Battle clears info");Click(t,Point(tiger));panel.Show(tiger);Require(!panel.gameObject.activeSelf,"Combat click and direct Show blocked");
        Find<WaveManager>().StopWave();Set(t,"preparing",true);Click(t,Point(tiger));Require(panel.CurrentAlly==tiger,"Next preparation info works");
        panel.Show(null);Require(!panel.gameObject.activeSelf && sticks.All(r=>r.enabled),"Null selection safely restores default content");
        Require(wallet.SpecialBalance==45 && wallet.Balance==120,"Inspection, rapid changes, pause and restoration preserve wallets");
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Verification/CharacterInfoContent");
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
            File.WriteAllBytes("Verification/CharacterInfoContent/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; canvas.overrideSorting = sorting; canvas.sortingOrder = order;
            camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CharacterInfoSelectionVerification
{
    const string Trigger = "Logs/RunCharacterInfoSelectionVerification";
    const string Key = "CharacterInfoSelectionVerification.Running";
    static CharacterInfoSelectionVerification() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Trigger) && !EditorApplication.isPlaying)
        {
            File.Delete(Trigger);
            SessionState.SetInt(Key, 1);
            EditorApplication.EnterPlaymode();
            return;
        }
        int state = SessionState.GetInt(Key, 0);
        if (!EditorApplication.isPlaying || state == 0) return;
        if (state < 30) { SessionState.SetInt(Key, state + 1); return; }
        SessionState.SetInt(Key, 0);
        int checks = 0;
        try
        {
            var panel = UnityEngine.Object.FindFirstObjectByType<CharacterInfoPanel>(FindObjectsInactive.Include);
            var turn = UnityEngine.Object.FindFirstObjectByType<TurnController>();
            typeof(TurnController).GetMethod("BeginPreparation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(turn,null);
            Time.timeScale = 1f;
            var buttons = UnityEngine.Object.FindFirstObjectByType<AllyActionButtons>(FindObjectsInactive.Include);
            var story = panel.GetComponentsInChildren<Text>(true).Where(t => t.name.StartsWith("Story Line ")).OrderBy(t => t.name).ToArray();
            string[] units = { "Do_Taoist", "Gae_Warrior", "Geol_Spearman", "Yut_Maceman", "Mo_Archer" };
            string[] beginnings = { "작은 불씨로 밥을 짓고", "마지막 주민이 돌아올 때까지", "겁 많은 양이 창을 들었다.", "솥과 농기구를 고치던 대장장이.", "화살에 편지를 묶어서라도" };
            for (int i = 0; i < units.Length; i++)
            {
                string baseStory = null; Sprite baseName = null;
                for (int stage = 1; stage <= 2; stage++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + units[i] + (stage == 2 ? "_2nd_Evolution" : "") + ".prefab");
                    var go = UnityEngine.Object.Instantiate(prefab);
                    go.SetActive(true);
                    try
                    {
                        var ally = go.GetComponent<Ally>();
                        buttons.gameObject.SetActive(true); buttons.SetTarget(ally);
                        Require(!panel.gameObject.activeSelf, "Selection alone does not open info", ref checks);
                        buttons.RequestInfo();
                        Require(panel.gameObject.activeSelf, "Info button opens panel; prep="+turn.CanUsePreparationInput+", ally="+ally.isActiveAndEnabled+", controller="+(Get<YutActionController>(buttons,"actionController")!=null)+", panel="+(Get<CharacterInfoPanel>(buttons,"characterInfoPanel")==panel)+", target="+(Get<Ally>(buttons,"target")==ally), ref checks);
                        Require(story.Length == 3 && story[0].text == beginnings[i], units[i] + " story", ref checks);
                        var name = Get<Image>(panel,"characterName").sprite;
                        string fullStory = string.Join("\n", story.Select(t => t.text));
                        if(stage == 1) { baseStory=fullStory; baseName=name; }
                        else Require(baseStory==fullStory && baseName==name, "Shared name and story", ref checks);
                        Require(Get<GameObject>(panel,"firstStar").activeSelf && Get<GameObject>(panel,"secondStar").activeSelf == (stage == 2), "Stars", ref checks);
                        Require(Get<Text>(panel,"attackValue").text == ally.AttackDamage.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture), "Attack", ref checks);
                        Require(Get<Text>(panel,"attackSpeedValue").text == ally.AttacksPerSecond.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"회/초", "Speed", ref checks);
                        Require(Get<Image>(panel,"characterPortrait").sprite != null, "Portrait", ref checks);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(go); buttons.gameObject.SetActive(false); }
                }
            }
            File.WriteAllText("Logs/CharacterInfoSelectionVerification.json", "{\"passed\":true,\"checks\":"+checks+",\"mode\":\"Unity Play Mode, info-button path, 10 prefabs\"}");
            Debug.Log("Character info selection verification PASS: "+checks);
        }
        catch(Exception e)
        {
            File.WriteAllText("Logs/CharacterInfoSelectionVerification.json", JsonUtility.ToJson(new Failure{passed=false,error=e.ToString(),checks=checks},true));
            Debug.Log("Character info selection verification FAIL: "+e.Message);
        }
        finally { EditorApplication.ExitPlaymode(); }
    }
    [Serializable] class Failure { public bool passed; public string error; public int checks; }
    static T Get<T>(object obj,string name) => (T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    static void Require(bool condition,string message,ref int checks) { checks++; if(!condition)throw new Exception(message); }
}

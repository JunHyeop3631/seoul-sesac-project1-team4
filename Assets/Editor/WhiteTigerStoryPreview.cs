using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class WhiteTigerStoryPreview
{
 const string Key="Yut.WhiteTigerStoryPreview";
 const string Story="도깨비를 봉인한 전설의 장군.\n수호자들의 간절한 부름에\n잠들었던 창을 다시 들어 올린다.";
 static Ally tiger;
 static WhiteTigerStoryPreview(){EditorApplication.update+=Tick;}
 static void Tick()
 {
  if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  if(File.Exists("Logs/RunWhiteTigerStoryPreview")&&!EditorApplication.isPlaying)
  {
   File.Delete("Logs/RunWhiteTigerStoryPreview");
   var panel=UnityEngine.Object.FindFirstObjectByType<CharacterInfoPanel>(FindObjectsInactive.Include);
   var so=new SerializedObject(panel);var profiles=so.FindProperty("profiles");
   for(int i=0;i<profiles.arraySize;i++){var p=profiles.GetArrayElementAtIndex(i);if(p.FindPropertyRelative("unitKind").enumValueIndex==5)p.FindPropertyRelative("story").stringValue=Story;}
   so.ApplyModifiedProperties();EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
   SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();return;
  }
  int state=SessionState.GetInt(Key,0);
  if(!EditorApplication.isPlaying||state==0)return;
  try
  {
   if(state<30){SessionState.SetInt(Key,state+1);return;}
   var panel=UnityEngine.Object.FindFirstObjectByType<CharacterInfoPanel>(FindObjectsInactive.Include);
   if(state==30)
   {
    var action=UnityEngine.Object.FindFirstObjectByType<YutActionController>();
    var prefab=AssetDatabase.LoadAssetAtPath<Ally>("Assets/Prefabs/WhiteTiger.prefab");
    tiger=UnityEngine.Object.FindFirstObjectByType<AllyPool>().Request(prefab,Get<Transform[]>(action,"cells")[1]);
    bool registered=(bool)action.GetType().GetMethod("RegisterAlly",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(action,new object[]{1,tiger});
    if(!registered)throw new Exception("Unable to register test tiger");
    SessionState.SetInt(Key,31);Debug.Log("WhiteTiger story preview ready: click tiger on board.");return;
   }
   if(panel.CurrentAlly!=tiger||panel.IsTransitioning)return;
   var lines=Get<Text[]>(panel,"storyLines");
   string actual=string.Join("\n",lines.Select(t=>t.text));
   bool visible=lines.All(t=>t.isActiveAndEnabled)&&Get<GameObject[]>(panel,"storyDecorations").All(g=>g.activeInHierarchy);
   if(actual!=Story||!visible)throw new Exception("Story or decorations mismatch: "+actual);
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WhiteTigerStoryPreview.json",JsonUtility.ToJson(new Result{passed=true,story=actual,visible=true},true));
   Debug.Log("WhiteTiger story PASS: three approved lines and decorations visible.");SessionState.SetInt(Key,0);
  }
  catch(Exception e){SessionState.SetInt(Key,0);File.WriteAllText("Logs/WhiteTigerStoryPreview.json",JsonUtility.ToJson(new Result{passed=false,story=e.ToString()},true));Debug.LogError(e);}
 }
 [Serializable]class Result{public bool passed,visible;public string story;}
 static T Get<T>(object o,string f)=>(T)o.GetType().GetField(f,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in a disposable copy of the project; does not save play-mode changes.
[InitializeOnLoad]
public static class WaveCompleteRegression
{
    private const string Key = "WaveCompleteRegression";
    static WaveCompleteRegression() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetInt(Key, 1);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        int frame = SessionState.GetInt(Key, 0);
        if (frame == 0 || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (frame < 8) { SessionState.SetInt(Key, frame + 1); return; }
        SessionState.SetInt(Key, 0);
        try
        {
            var turn = UnityEngine.Object.FindFirstObjectByType<TurnController>();
            turn.StartBattle();
            var hud = UnityEngine.Object.FindFirstObjectByType<BattleStateHUD>();
            var panel = hud.transform.Find("Start Wave Panel");
            if (panel != null && panel.gameObject.activeInHierarchy)
                throw new Exception("REGRESSION: Start Battle still opens the blocking start-wave scroll.");
            Debug.Log("REGRESSION PASSED: no start-wave scroll.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}

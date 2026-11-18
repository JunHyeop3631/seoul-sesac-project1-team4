using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch-only checks. Data, timings and random state are never saved.
[InitializeOnLoad]
public static class WaveOrderVerification
{
    const string Key = "Yut.WaveOrderVerification";
    static IEnumerator checks;
    static int assertions, errors, spawned;
    static readonly string[] firstPass = new string[10], secondPass = new string[10];
    static WaveOrderVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        foreach (string path in new[] { "Assets/Scripts", "Assets/Prefabs", "Assets/Scenes/ThrowYut.unity" })
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        for (int i = 1; i <= 10; i++) AssetDatabase.ImportAsset($"Assets/Wave{i}.asset", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Run();
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetBool(Key, true); SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Wave order verification timed out");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Stored per-enemy order, balanced ratios, counts/HP, immediate first spawn, one-second cadence and two deterministic passes passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    [Serializable] sealed class Result
    {
        public bool passed; public int assertions, runtimeErrors, spawnedEnemies;
        public string unity, scene, detail; public string[] firstPass, secondPass;
    }
    static void Finish(bool passed, string detail)
    {
        SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/WaveOrderVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, assertions = assertions, runtimeErrors = errors, spawnedEnemies = spawned,
            unity = Application.unityVersion, scene = "ThrowYut (Play Mode)", detail = detail,
            firstPass = firstPass, secondPass = secondPass }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static IEnumerator Check()
    {
        yield return null; yield return null;
        var manager = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        var waves = Get<WaveData[]>(manager, "waves"); var wave = Get<Wave>(manager, "wave");
        var pool = Get<EnemyPool>(manager, "enemyPool");
        // Exercise Wave directly without final victory UI pausing the second pass.
        // Turn progression and rewards are separate checks; do not save this fixture.
        UnityEngine.Object.FindFirstObjectByType<TurnController>().enabled = false;
        Require(waves.Length == 10 && Get<float>(manager, "spawnInterval") == 1f, "Saved scene has ten waves and one-second cadence");
        int[] normals = {12,10,12,14,14,16,18,18,20,0}, fasts = {0,6,8,8,10,8,8,10,10,0};
        int[] normalHp = {125,240,290,270,290,355,335,360,375,0}, fastHp = {0,105,130,135,145,175,180,195,200,0};
        for (int w = 0; w < 10; w++)
        {
            var entries = waves[w].Enemies; int total = normals[w] + fasts[w] + (w == 9 ? 1 : 0);
            Require(entries.Length == total && entries.All(e => e.count == 1), "Each serialized entry represents one enemy, wave " + (w+1));
            Require(entries.Count(e => e.enemyId == EnemyId.NormalEnemy) == normals[w] &&
                entries.Count(e => e.enemyId == EnemyId.FastEnemy) == fasts[w] &&
                entries.Count(e => e.enemyId == EnemyId.BossEnemy) == (w == 9 ? 1 : 0), "Exact composition, wave " + (w+1));
            if (w == 9) continue;
            int seenFast = 0;
            for (int i = 0; i < total; i++)
            {
                if (entries[i].enemyId == EnemyId.FastEnemy) seenFast++;
                Require(Math.Abs(seenFast - (i+1) * (double)fasts[w] / total) < 1,
                    "Every prefix stays within one fast enemy of ideal ratio, wave " + (w+1));
            }
        }
        int completions = 0; wave.Completed += () => completions++;
        var randomState = UnityEngine.Random.state;
        for (int pass = 0; pass < 2; pass++)
        for (int w = 0; w < 10; w++)
        {
            UnityEngine.Random.InitState(137 + pass * 991 + w);
            var data = waves[w]; var actual = new List<EnemyId>();
            // Wave1's first pass uses the actual one-second interval; other passes accelerate only test timing.
            float interval = pass == 0 && w == 0 ? 1f : .01f;
            float lastSpawn = Time.time, startedAt = Time.time;
            wave.StartWave(data, pool, interval);
            Require(wave.IsRunning && wave.AliveEnemyCount == 1 && Time.time == startedAt, "First enemy spawned synchronously, pass/wave " + pass + "/" + (w+1));
            while (wave.IsRunning)
            {
                foreach (var enemy in Get<HashSet<Enemy>>(wave, "spawnedEnemies").ToArray())
                {
                    int index = actual.Count;
                    var id = enemy is BossEnemy ? EnemyId.BossEnemy : enemy is FastEnemy ? EnemyId.FastEnemy : EnemyId.NormalEnemy;
                    Require(index < data.Enemies.Length && id == data.Enemies[index].enemyId, "Actual spawn matches stored entry " + index);
                    int hp = id == EnemyId.BossEnemy ? 6000 : id == EnemyId.FastEnemy ? fastHp[w] : normalHp[w];
                    Require(enemy.CurrentHealth == hp, "Mixed entry preserves per-type wave HP");
                    if (index > 0) Require(Time.time - lastSpawn >= interval - .002f, "No early next spawn");
                    lastSpawn = Time.time; actual.Add(id); spawned++;
                    enemy.TakeDamage(int.MaxValue); enemy.CompleteDeathAnimation();
                    if (actual.Count < data.Enemies.Length) Require(wave.IsRunning, "Empty field between spawns does not complete wave");
                }
                yield return null;
            }
            Require(actual.Count == data.Enemies.Length && completions == pass * 10 + w + 1, "Complete exactly once after full stored list");
            string order = string.Concat(actual.Select(id => id == EnemyId.NormalEnemy ? "N" : id == EnemyId.FastEnemy ? "F" : "B"));
            if (pass == 0) firstPass[w] = order;
            else { secondPass[w] = order; Require(order == firstPass[w], "Replay order unchanged with different random seed"); }
        }
        UnityEngine.Random.state = randomState;
        Require(spawned == 406, "All203 enemies spawned twice");
    }
}

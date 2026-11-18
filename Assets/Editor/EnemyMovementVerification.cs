using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch-only measured movement on real Unity frames; never saves fixtures.
[InitializeOnLoad]
public static class EnemyMovementVerification
{
    const string Key = "Yut.EnemyMovementVerification";
    static IEnumerator checks;
    static int assertions, errors;
    static readonly List<Measurement> measurements = new();
    static EnemyMovementVerification() { EditorApplication.update += Tick; }
    public static void ReimportAndRun()
    {
        foreach (string path in new[] {"Assets/Scripts", "Assets/Prefabs", "Assets/Scenes/ThrowYut.unity"})
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetBool(Key, true); SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120) throw new Exception("Movement verification timed out");
            if (checks == null) { Application.logMessageReceived += CountError; checks = Check(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks"); Finish(true, "Measured lap times, speed ratios, enlarged path, pooled reuse, pause, 2x/4x and completion/reset passed.");
        }
        catch (Exception e) { Debug.LogException(e); Finish(false, e.ToString()); }
    }
    static void CountError(string m, string s, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    [Serializable] sealed class Measurement
    {
        public string scenario, enemy;
        public float pathLength, speed, timeScale;
        public double gameSeconds, unscaledSeconds;
    }
    [Serializable] sealed class Result
    {
        public bool passed; public int assertions, runtimeErrors;
        public string unity, scene, detail; public Measurement[] measurements;
    }
    static void Finish(bool passed, string detail)
    {
        Time.captureDeltaTime = 0; Time.timeScale = 1;
        SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/EnemyMovementVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, assertions = assertions, runtimeErrors = errors, unity = Application.unityVersion,
            scene = "ThrowYut (Play Mode)", detail = detail, measurements = measurements.ToArray() }, true));
        EditorApplication.Exit(passed ? 0 : 1);
    }
    static T Get<T>(object obj, string field)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            var member = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (member != null) return (T)member.GetValue(obj);
        }
        throw new Exception("Missing field: " + field);
    }
    static void Require(bool ok, string message) { assertions++; if (!ok) throw new Exception($"Check {assertions}: {message}"); }
    static IEnumerator Check()
    {
        yield return null; yield return null;
        var wave = UnityEngine.Object.FindFirstObjectByType<Wave>();
        var pool = UnityEngine.Object.FindFirstObjectByType<EnemyPool>();
        var points = Get<Transform[]>(wave, "waypoints");
        var ids = new[] {EnemyId.NormalEnemy, EnemyId.FastEnemy, EnemyId.BossEnemy};
        float[] expected = {30, 20, 60};
        Require(points.Length >= 2 && points.All(p => p != null), "Actual scene has valid movement path");
        // Fixed simulated frame interval keeps a full 60-game-second lap inexpensive.
        // Enemy.Update still runs through normal Unity frames, never called by reflection.
        Time.captureDeltaTime = .1f; Time.timeScale = 1;
        yield return null;
        var paused = pool.RequestEnemy(EnemyId.NormalEnemy, points[0].position, Quaternion.identity);
        paused.StartMoving(points, unit => pool.ReturnEnemy(unit));
        yield return null; yield return null;
        Time.timeScale = 0; yield return null;
        Vector3 beforePause = paused.transform.position; float health = paused.CurrentHealth;
        for (int i = 0; i < 8; i++) yield return null;
        Require(paused.transform.position == beforePause && paused.IsMoving && paused.CurrentHealth == health, "Pause freezes position without arrival or damage");
        Time.timeScale = 1; yield return null; yield return null;
        Require(paused.transform.position != beforePause, "Movement resumes after pause");
        pool.ReturnEnemy(paused);
        Require(!paused.IsMoving && Get<Vector3[]>(paused,"pathPoints") == null && Get<Action<Enemy>>(paused,"onArrived") == null, "Pool return clears path and callback");
        var expanded = new GameObject("Movement verification expanded path");
        Transform[] larger = points.Select(p => {
            var child = new GameObject("Point").transform; child.SetParent(expanded.transform);
            child.position = points[0].position + (p.position - points[0].position) * 1.5f; return child;
        }).ToArray();
        Enemy[] prior = null;
        for (int scenario = 0; scenario < 4; scenario++)
        {
            var route = scenario == 1 ? larger : points;
            float scale = scenario == 2 ? 2f : scenario == 3 ? 4f : 1f;
            // captureDeltaTime fixes game time, not unscaled wall time. Measure
            // native speed scaling separately with ordinary frame timing.
            Time.captureDeltaTime = scenario < 2 ? .1f : 0f;
            Time.timeScale = scale; yield return null;
            float length = 0;
            for (int i = 0; i < route.Length; i++) length += Vector3.Distance(route[i].position, route[(i+1)%route.Length].position);
            var units = new Enemy[3]; var arrivals = new int[3];
            var elapsed = new double[3]; var realElapsed = new double[3];
            double startedGame = Time.timeAsDouble, startedUnscaled = Time.unscaledTimeAsDouble;
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                units[i] = pool.RequestEnemy(ids[i], route[0].position, Quaternion.identity);
                if (prior != null) Require(units[i] == prior[i], "Same instance reused on next route/speed scenario");
                Require(Get<float>(units[i],"lapDurationSeconds") == expected[i], "Prefab duration matches source: " + ids[i]);
                units[i].StartMoving(route, unit => {
                    arrivals[index]++; elapsed[index] = Time.timeAsDouble - startedGame;
                    realElapsed[index] = Time.unscaledTimeAsDouble - startedUnscaled; pool.ReturnEnemy(unit);
                });
                Require(Vector3.Distance(units[i].transform.position, route[0].position) < .0001f && units[i].IsMoving, "Pooled start resets to route entrance");
                Require(Mathf.Abs(Get<float>(units[i],"moveSpeed") - length/expected[i]) < .0001f, "Speed accounts for closing path segment");
            }
            Require(Mathf.Abs(Get<float>(units[1],"moveSpeed") / Get<float>(units[0],"moveSpeed") - 1.5f) < .0001f &&
                Mathf.Abs(Get<float>(units[2],"moveSpeed") / Get<float>(units[0],"moveSpeed") - .5f) < .0001f, "Fast1.5x and boss0.5x relative speed");
            while (arrivals.Any(a => a == 0))
            {
                if (Time.timeAsDouble - startedGame > 63) throw new Exception("An enemy failed to finish its lap");
                yield return null;
            }
            for (int i = 0; i < 3; i++)
            {
                double tolerance = .1 * scale + .015;
                Require(Math.Abs(elapsed[i] - expected[i]) <= tolerance, "Measured game-time lap: " + ids[i]);
                if (scenario >= 2)
                    Require(Math.Abs(realElapsed[i] - expected[i]/scale) <= .25, "Measured native unscaled lap follows 2x/4x");
                Require(arrivals[i] == 1 && !units[i].gameObject.activeSelf && !units[i].IsMoving, "Arrival once then pool return");
                Require(Vector3.Distance(units[i].transform.position, route[0].position) < .0001f, "Lap ends at shared entrance/exit");
                measurements.Add(new Measurement {scenario = scenario == 1 ? "expanded1.5" : "scene", enemy = ids[i].ToString(),
                    pathLength = length, speed = Get<float>(units[i],"moveSpeed"), timeScale = scale, gameSeconds = elapsed[i], unscaledSeconds = realElapsed[i]});
            }
            yield return null; yield return null;
            Require(arrivals.All(a => a == 1), "Returned instances never repeat arrival"); prior = units;
        }
        UnityEngine.Object.Destroy(expanded); Time.timeScale = 1; Time.captureDeltaTime = 0;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in an isolated project with -batchmode -executeMethod MoAttackValidation.Run.
// Never enters Play Mode or replaces a scene in the user's interactive editor.
[InitializeOnLoad]
public static class MoAttackValidation
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Pending = "MoAttackValidation.Pending";
    private static readonly Stack<IEnumerator> routines = new();
    private static readonly List<GameObject> objects = new();
    private static int lastFrame = -1;
    private static int passed;
    private static double deadline;
    private static string loggedError;
    private static bool validatingTaoist;
    private static Wave wave;
    private static HashSet<Enemy> enemies;

    static MoAttackValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false))
                return;
            SessionState.SetBool(Pending, false);
            deadline = EditorApplication.timeSinceStartup + 100;
            routines.Push(ValidateBoth());
            EditorApplication.update += Step;
            Application.logMessageReceived += OnLog;
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run this validation in an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        // Unity 6000.3 can throw here while indexing a newly created batch project.
        // It is unrelated to gameplay; retain the original exception in the log.
        if (trace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
            return;
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            loggedError = message + "\n" + trace;
    }

    private static void Step()
    {
        if (EditorApplication.timeSinceStartup > deadline)
        {
            Debug.LogError("MO VALIDATION TIMEOUT");
            EditorApplication.Exit(1);
            return;
        }
        if (Time.frameCount == lastFrame)
            return;
        lastFrame = Time.frameCount;
        try
        {
            if (loggedError != null) throw new Exception(loggedError);
            while (routines.Count > 0)
            {
                IEnumerator routine = routines.Peek();
                if (!routine.MoveNext())
                {
                    routines.Pop();
                    continue;
                }
                if (routine.Current is IEnumerator nested)
                {
                    routines.Push(nested);
                    continue;
                }
                return;
            }
            Debug.Log($"ALLY VALIDATION PASSED: {passed} checks (Mo + Do + Gae + Geol + Yut)");
            System.IO.File.WriteAllText("validation-result.txt", $"PASSED: {passed} checks");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            System.IO.File.WriteAllText("validation-result.txt", exception.ToString());
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static IEnumerator ValidateBoth()
    {
        yield return ValidateEffects();
        yield return ValidateAnimatorCompletion();
        validatingTaoist = false;
        yield return Validate();
        validatingTaoist = true;
        yield return Validate();
        foreach (string unit in new[] { "Gae_Warrior", "Geol_Spearman", "Yut_Maceman" })
            yield return ValidateMelee(unit);
    }

    private static IEnumerator ValidateEffects()
    {
        foreach (string name in new[] { "Do_Magic", "Mo_Arrow", "Enemy_Hit" })
        {
            Setup();
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Effects/{name}.anim");
            int count = name == "Do_Magic" ? 54 : name == "Mo_Arrow" ? 18 : 11;
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Check(clip.frameRate == 60 && Math.Abs(clip.length - count / 60f) < 0.001 &&
                  keys.Length == count, name + $" has all frames at 60fps with complete final frame (fps={clip.frameRate}, length={clip.length}, keys={keys.Length})");
            bool ordered = true;
            for (int frame = 0; frame < count; frame++)
            {
                ordered &= keys[frame].value is Sprite sprite && sprite.name == $"{name}_{frame}" &&
                           Math.Abs(keys[frame].time - frame / 60f) < 0.0001;
            }
            Check(ordered, name + " binds every sprite in numeric order at 1/60-second intervals");
            if (name == "Enemy_Hit")
            {
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
                Check(events.Length == 1 && events[0].functionName == "OnHitAnimationFinished" &&
                      Math.Abs(events[0].time - clip.length) < 0.001,
                    "hit completion event is at the end after the final sprite interval");
            }

            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{name}.prefab"));
            objects.Add(instance);
            Animator animator = instance.GetComponent<Animator>();
            int state = Animator.StringToHash(name == "Enemy_Hit" ? "Hit" : "Flight");
            Check(animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, state) &&
                  Array.IndexOf(animator.runtimeAnimatorController.animationClips, clip) >= 0,
                name + " Animator state references the Effects clip without a duplicate component clip");
            yield return Wait(0.04f);
            SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
            Sprite beforePause = renderer.sprite;
            Check(beforePause != null && beforePause.name != $"{name}_0", name + " Animator advances sprites");
            Time.timeScale = 0;
            double until = Time.realtimeSinceStartupAsDouble + 0.1;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
            Check(instance != null && renderer.sprite == beforePause, name + " pauses with game time");
            Time.timeScale = 1;
            yield return Wait(count / 60f + 0.04f);
            Check(name == "Enemy_Hit" ? instance == null : instance != null &&
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 1,
                name + " completes once and cleans up or loops while flying");
        }

        Setup();
        Enemy enemy = SpawnEnemy(Vector3.zero);
        HitEffect prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Hit.prefab").GetComponent<HitEffect>();
        Set(enemy, "hitEffectPrefab", prefab);
        enemy.TakeDamage(2);
        HitEffect[] effects = Object.FindObjectsByType<HitEffect>(FindObjectsSortMode.None);
        Check(enemy.CurrentHealth == 98 && effects.Length == 1 && effects[0].transform.position == enemy.transform.position,
            "actual damage creates one hit effect at enemy center");
        enemy.TakeDamage(0);
        Check(Object.FindObjectsByType<HitEffect>(FindObjectsSortMode.None).Length == 1,
            "ignored damage does not create hit effect");
        yield return Wait(0.25f);
        Check(Object.FindObjectsByType<HitEffect>(FindObjectsSortMode.None).Length == 0,
            "hit effect destroys itself after its animation");
        Cleanup();
    }

    private static IEnumerator ValidateAnimatorCompletion()
    {
        Setup();
        GameObject effect = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Hit.prefab"));
        objects.Add(effect);
        yield return Wait(0.04f);
        Animator effectAnimator = effect.GetComponent<Animator>();
        effectAnimator.speed = 0.1f;
        yield return Wait(0.25f);
        Check(effect != null, "hit effect waits for actual slow Animator state instead of clip length");
        effectAnimator.speed = 4f;
        yield return Until(() => effect == null);
        Check(effect == null, "hit effect finishes after sped-up Animator state completes");

        foreach (string name in new[] { "NormalEnemy", "FastEnemy", "BossEnemy" })
        {
            foreach (bool left in new[] { true, false })
            {
                Setup();
                GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{name}.prefab"));
                objects.Add(instance);
                Enemy enemy = instance.GetComponent<Enemy>();
                Animator animator = Get<Animator>(enemy, "enemyAnimator");
                Set(enemy, "facingLeft", left);
                enemy.TakeDamage(int.MaxValue);
                Check(enemy.CurrentHealth == 0 && instance.activeSelf, name + " death begins before pool return");
                yield return Wait(0.08f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName(left ? "DeathLeft" : "DeathRight"),
                    name + " uses actual left/right death state and controller override");
                AnimationClip deathClip = animator.GetCurrentAnimatorClipInfo(0)[0].clip;
                AnimationEvent[] deathEvents = AnimationUtility.GetAnimationEvents(deathClip);
                Check(deathEvents.Length == 1 && deathEvents[0].functionName == "OnDeathAnimationFinished" &&
                      Math.Abs(deathEvents[0].time - deathClip.length) < 0.001 &&
                      animator.GetComponent<EnemyAnimationEvents>() != null,
                    name + " death event and child-to-parent relay are connected at clip end");
                animator.speed = 0.1f;
                Time.timeScale = 0;
                float paused = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                double until = Time.realtimeSinceStartupAsDouble + 0.08;
                while (Time.realtimeSinceStartupAsDouble < until) yield return null;
                Check(instance.activeSelf && Math.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - paused) < 0.001,
                    name + " pause cannot complete death state");
                Time.timeScale = 1;
                yield return Wait(0.6f);
                Check(instance.activeSelf, name + " slow death is not returned at original clip duration");
                animator.speed = 4f;
                yield return Until(() => !instance.activeSelf);
                Check(!instance.activeSelf, name + " returns after actual Animator completion");
                instance.SetActive(true);
                yield return Wait(0.04f);
                Check(enemy.CurrentHealth > 0 && animator.GetCurrentAnimatorStateInfo(0).IsName("WalkLeft"),
                    name + " pooled reuse resets death state and health");
                animator.GetComponent<EnemyAnimationEvents>().OnDeathAnimationFinished();
                Check(instance.activeSelf && enemy.CurrentHealth > 0,
                    name + " stale death event cannot return a live reused enemy");
            }
        }
        Cleanup();
    }

    private static IEnumerator Validate()
    {
        // Real Animator, prefab and Animation Events; no manual ReleaseArrow calls.
        Setup();
        Enemy a = SpawnEnemy(new Vector3(1, 0));
        Enemy b = SpawnEnemy(new Vector3(-2, 0));
        Ally mo = SpawnArcher();
        Check(mo.GetComponent<ProjectileAttack>() != null && mo.GetComponent<ProjectileAttack>().CanFire,
            "projectile ability is a configured component on the Ally");
        yield return Wait(0.25f);
        Check(a.CurrentHealth == 100 && Arrows().Length == 0, "no damage or arrow before release");
        float progress = mo.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime;
        a.gameObject.SetActive(false);
        yield return Wait(0.12f);
        AnimatorStateInfo state = mo.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
        Check(state.IsName("AttackLeft") && state.normalizedTime >= progress,
            "opposite target preserves animation progress");
        yield return Until(() => Arrows().Length == 1);
        Check(mo.CurrentTarget == b && b.CurrentHealth == 100, "event selects replacement; no immediate damage");
        HomingProjectile arrow = Arrows()[0];
        Check(Vector3.Distance(arrow.transform.position, mo.transform.position) < 0.3f,
            "arrow starts at archer center");
        b.transform.position = new Vector3(-10, 2);
        yield return Wait(0.12f);
        Check(!Get<bool>(mo, "attacking"), "no targets cancels motion immediately");
        Check(arrow != null && FacesTarget(arrow, b) &&
              arrow.transform.localScale == Vector3.one, "arrow faces moving target and keeps original scale");
        mo.gameObject.SetActive(false);
        yield return Wait(0.25f);
        Check(b.CurrentHealth == 98 && Arrows().Length == 0,
            "arrow survives source cancellation, follows out of range and damages once");
        yield return Wait(0.15f);
        Check(b.CurrentHealth == 98, "no duplicate damage");

        // Keep switching before and after the release boundary.
        Setup();
        a = SpawnEnemy(new Vector3(1, 0));
        mo = SpawnArcher();
        Set(mo.GetComponent<ProjectileAttack>(), "flightDuration", 5f);
        Set(mo, "attackInterval", 0.1f);
        for (int i = 0; i < 12; i++)
        {
            a.transform.position = new Vector3(i % 2 == 0 ? -1 : 1, 0);
            yield return Wait(0.1f);
        }
        Check(Arrows().Length == 1, "repeated direction changes never skip or duplicate release");
        Check(Get<bool>(mo, "attacking"), "short interval does not interrupt unfinished motion");
        yield return Until(() => Arrows().Length == 2);
        Check(Get<bool>(mo, "attacking"), "next attack starts after prior motion ends");

        // Long interval is counted from release rather than animation completion.
        Setup();
        a = SpawnEnemy(new Vector3(1, 0));
        mo = SpawnArcher();
        Set(mo, "attackInterval", 1f);
        Set(mo.GetComponent<ProjectileAttack>(), "flightDuration", 5f);
        yield return Until(() => Arrows().Length == 1);
        double ready = Get<double>(mo, "nextAttackTime");
        yield return Until(() => !Get<bool>(mo, "attacking"));
        Check(Time.timeAsDouble < ready, "completed motion waits for remaining cooldown");
        yield return Until(() => Get<bool>(mo, "attacking"));
        Check(Time.timeAsDouble >= ready && Time.timeAsDouble < ready + 0.2,
            "next motion starts at release-based deadline, without extra wait");

        // Cancellation before release doesn't consume cooldown.
        Setup();
        a = SpawnEnemy(new Vector3(1, 0));
        mo = SpawnArcher();
        yield return Wait(0.2f);
        a.gameObject.SetActive(false);
        yield return Wait(0.08f);
        Check(!Get<bool>(mo, "attacking") && Arrows().Length == 0 &&
              Get<double>(mo, "nextAttackTime") == 0, "pre-release cancellation has no cooldown");
        a.gameObject.SetActive(true);
        yield return Wait(0.08f);
        Check(Get<bool>(mo, "attacking"), "target reacquisition starts fresh motion");
        yield return Until(() => Arrows().Length == 1);
        ready = Get<double>(mo, "nextAttackTime");
        a.gameObject.SetActive(false);
        yield return Wait(0.08f);
        a.gameObject.SetActive(true);
        yield return Wait(0.08f);
        Check(!Get<bool>(mo, "attacking") && Get<double>(mo, "nextAttackTime") == ready,
            "post-release cancellation retains cooldown");

        // Direct projectile cases isolate flight timing and pooled target identity.
        Setup();
        a = SpawnEnemy(new Vector3(2, 0));
        arrow = Launch(a, 0.3f);
        Check(FacesTarget(arrow, a), "arrow faces target at launch");
        a.transform.position = new Vector3(-2, 3);
        yield return Wait(0.1f);
        Check(FacesTarget(arrow, a), "arrow rotates toward changed target position during flight");
        Vector3 position = arrow.transform.position;
        Quaternion rotation = arrow.transform.rotation;
        Time.timeScale = 0f;
        double realEnd = Time.realtimeSinceStartupAsDouble + 0.2;
        while (Time.realtimeSinceStartupAsDouble < realEnd) yield return null;
        Check(arrow.transform.position == position && arrow.transform.rotation == rotation &&
              a.CurrentHealth == 100, "pause freezes flight, rotation and damage");
        Time.timeScale = 2f;
        double realStart = Time.realtimeSinceStartupAsDouble;
        yield return Until(() => arrow == null);
        Check(a.CurrentHealth == 98 && Time.realtimeSinceStartupAsDouble - realStart < 0.2,
            "double speed advances flight in game time");
        Time.timeScale = 1f;

        arrow = Launch(a, 0.2f);
        a.gameObject.SetActive(false);
        a.gameObject.SetActive(true);
        yield return Wait(0.3f);
        Check(arrow == null && a.CurrentHealth == 100, "returned and reused target never receives old damage");
        arrow = Launch(a, 0.2f);
        a.TakeDamage(100);
        yield return Wait(0.08f);
        Check(arrow == null, "target death terminates arrow");

        Setup();
        a = SpawnEnemy(new Vector3(1, 0));
        mo = SpawnArcher();
        ProjectileAttack ability = mo.GetComponent<ProjectileAttack>();
        ability.enabled = false;
        yield return Wait(0.1f);
        Check(!Get<bool>(mo, "attacking") && Arrows().Length == 0,
            "disabled projectile component prevents attack startup");
        ability.enabled = true;
        yield return Wait(0.1f);
        Check(Get<bool>(mo, "attacking"), "enabling projectile component restores attack startup");
        ability.enabled = false;
        yield return Wait(0.1f);
        Check(!Get<bool>(mo, "attacking") && Get<double>(mo, "nextAttackTime") == 0 &&
              Arrows().Length == 0 && a.CurrentHealth == 100,
            "disabling ability before release cancels without projectile, damage or cooldown");

        Cleanup();
    }

    private static IEnumerator ValidateMelee(string unit)
    {
        Setup();
        Enemy a = SpawnEnemy(new Vector3(1, 0));
        Enemy b = SpawnEnemy(new Vector3(-2, 0));
        var obj = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{unit}.prefab"));
        objects.Add(obj);
        Ally ally = obj.GetComponent<Ally>();
        Bind(ally);
        double started = Time.timeAsDouble;
        yield return Wait(0.4f);
        float progress = ally.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).normalizedTime;
        a.gameObject.SetActive(false);
        yield return Wait(0.15f);
        AnimatorStateInfo state = ally.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
        Check(state.IsName("AttackLeft") && state.normalizedTime >= progress, unit + " preserves progress on target change");
        yield return Wait(0.3f);
        Check(b.CurrentHealth == 100, unit + " has no damage before frame 60");
        yield return Until(() => b.CurrentHealth < 100);
        Check(b.CurrentHealth == 98 && Time.timeAsDouble - started >= 0.99 &&
              Time.timeAsDouble - started < 1.2, unit + " applies damage at frame 60 event");
        Check(Arrows().Length == 0, unit + " direct hit creates no projectile");
        double ready = Get<double>(ally, "nextAttackTime");
        Check(Math.Abs(ready - Time.timeAsDouble - 1) < 0.1, unit + " cooldown begins at hit event");
        b.transform.position = new Vector3(1, 0);
        yield return Wait(0.15f);
        Check(b.CurrentHealth == 98, unit + " direction change after hit cannot duplicate damage");
        yield return Until(() => !Get<bool>(ally, "attacking"));
        Check(Time.timeAsDouble < ready, unit + " motion finishes before remaining cooldown");
        yield return Until(() => Get<bool>(ally, "attacking"));
        Check(Time.timeAsDouble >= ready, unit + " next attack respects event cooldown");
        b.gameObject.SetActive(false);
        yield return Wait(0.08f);
        Check(!Get<bool>(ally, "attacking"), unit + " no target cancels motion");

        Setup();
        a = SpawnEnemy(new Vector3(1, 0));
        obj = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{unit}.prefab"));
        objects.Add(obj);
        ally = obj.GetComponent<Ally>();
        Bind(ally);
        yield return Wait(0.2f);
        a.gameObject.SetActive(false);
        yield return Wait(0.08f);
        Check(!Get<bool>(ally, "attacking") && Get<double>(ally, "nextAttackTime") == 0 &&
              a.CurrentHealth == 100, unit + " cancellation before event has no damage or cooldown");
        Cleanup();
    }

    private static void Setup()
    {
        Cleanup();
        wave = NewObject("Validation wave").AddComponent<Wave>();
        Set(wave, "<IsRunning>k__BackingField", true);
        Set(wave, "enemyTargetLayers", (LayerMask)1);
        enemies = Get<HashSet<Enemy>>(wave, "spawnedEnemies");
    }

    private static void Cleanup()
    {
        Time.timeScale = 1f;
        foreach (HomingProjectile arrow in Arrows()) Object.DestroyImmediate(arrow.gameObject);
        foreach (HitEffect effect in Object.FindObjectsByType<HitEffect>(FindObjectsSortMode.None))
            Object.DestroyImmediate(effect.gameObject);
        foreach (GameObject obj in objects) if (obj != null) Object.DestroyImmediate(obj);
        objects.Clear();
    }

    private static GameObject NewObject(string name)
    {
        var obj = new GameObject(name);
        objects.Add(obj);
        return obj;
    }

    private static Enemy SpawnEnemy(Vector3 position)
    {
        GameObject obj = NewObject("Validation enemy");
        obj.SetActive(false);
        obj.transform.position = position;
        obj.AddComponent<CircleCollider2D>().radius = 0.1f;
        Enemy enemy = obj.AddComponent<Enemy>();
        Set(enemy, "maxHealth", 100);
        obj.SetActive(true);
        enemies.Add(enemy);
        return enemy;
    }

    private static Ally SpawnArcher()
    {
        string path = validatingTaoist ? "Assets/Prefabs/Do_Taoist.prefab" : "Assets/Prefabs/Mo_Archer.prefab";
        var obj = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        objects.Add(obj);
        Ally mo = obj.GetComponent<Ally>();
        Bind(mo);
        return mo;
    }

    private static void Bind(Ally ally) => typeof(Ally).GetMethod("SetTargetingWave", Fields).Invoke(ally, new object[] { wave });

    private static HomingProjectile Launch(Enemy target, float duration)
    {
        string path = validatingTaoist ? "Assets/Prefabs/Do_Magic.prefab" : "Assets/Prefabs/Mo_Arrow.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        HomingProjectile arrow = Object.Instantiate(prefab).GetComponent<HomingProjectile>();
        arrow.Launch(target, 2, duration);
        return arrow;
    }

    private static HomingProjectile[] Arrows() => Object.FindObjectsByType<HomingProjectile>(FindObjectsSortMode.None);

    private static bool FacesTarget(HomingProjectile arrow, Enemy target)
    {
        Vector2 direction = target.transform.position - arrow.transform.position;
        return direction.sqrMagnitude < 0.000001f ||
               Vector2.Dot(arrow.transform.right, direction.normalized) > 0.999f;
    }

    private static IEnumerator Wait(float seconds)
    {
        double end = Time.timeAsDouble + seconds;
        while (Time.timeAsDouble < end) yield return null;
    }

    private static IEnumerator Until(Func<bool> condition)
    {
        double end = Time.realtimeSinceStartupAsDouble + 8;
        while (!condition())
        {
            if (Time.realtimeSinceStartupAsDouble > end)
                throw new Exception("Condition timed out");
            yield return null;
        }
    }

    private static FieldInfo Field(object obj, string name)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Fields);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }

    private static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
        passed++;
        Debug.Log($"PASS [{(validatingTaoist ? "Do" : "Mo")}]: " + description);
    }
}

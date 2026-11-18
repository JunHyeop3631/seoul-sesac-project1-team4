using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Batch Play Mode verification of task03 against the saved ThrowYut scene.</summary>
[InitializeOnLoad]
public static class CurrencyVerification
{
    private const string Key = "Yut.CurrencyVerification";
    private static IEnumerator checks;
    private static int assertions, errors;
    static CurrencyVerification() { EditorApplication.update += Tick; }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/ThrowYut.unity");
        SessionState.SetInt(Key, 1);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        int state = SessionState.GetInt(Key, 0);
        if (state == 0) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0f) > 120)
                throw new Exception("Currency verification timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (state < 8) { SessionState.SetInt(Key, state + 1); return; }
            if (checks == null) { Application.logMessageReceived += CountError; checks = CheckScene(); }
            if (checks.MoveNext()) return;
            Require(errors == 0, "No runtime errors during checks");
            Finish(true, "Two balances, paid throws, HUD, rejected inputs, carryover, and scene restart passed.");
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false, exception.ToString()); }
    }

    [Serializable]
    private sealed class Result
    {
        public bool passed;
        public string unity, scene, detail;
        public int assertions, runtimeErrors;
    }

    private static void Finish(bool passed, string detail)
    {
        SessionState.SetInt(Key, 0);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CurrencyVerification.json", JsonUtility.ToJson(new Result {
            passed = passed, unity = Application.unityVersion, scene = "ThrowYut (Play Mode)",
            detail = detail, assertions = assertions, runtimeErrors = errors
        }, true));
        Debug.Log("Currency verification: " + (passed ? "PASS" : "FAIL") + " (" + assertions + " checks)");
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void CountError(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    private static void Require(bool condition, string message)
    { assertions++; if (!condition) throw new Exception("Check " + assertions + ": " + message); }
    private static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);

    private static IEnumerator CheckScene()
    {
        var turn = Find<TurnController>();
        var wallet = Find<StageCurrency>();
        var thrower = Find<YutThrowController>();
        var waves = Find<WaveManager>();
        var display = GameObject.Find("Coin HUD").GetComponent<GoldNumberDisplay>();
        var throwButton = Get<GameObject>(turn, "throwButton").GetComponent<Button>();
        var actions = Find<YutActionController>();
        Require(wallet.Balance == 120 && wallet.SpecialBalance == 0, "Fresh scene starts at 120 / 0");
        Require(Get<int>(display, "value") == 120 && turn.CanThrowYut && throwButton.interactable,
            "Initial real HUD and throw button reflect balance");
        Require(wallet.GrantSpecial(45) == 45 && wallet.TrySpendSpecial(30) && wallet.SpecialBalance == 15 &&
            wallet.Balance == 120, "Special acquisition and spending are independent from normal currency");

        int debits = 0;
        Action<int> reentrantClick = amount => { debits++; turn.ThrowYut(); thrower.Throw(); };
        wallet.Changed += reentrantClick;
        throwButton.onClick.Invoke();
        wallet.Changed -= reentrantClick;
        Require(thrower.IsThrowing && wallet.Balance == 100 && debits == 1,
            "Real button charges 20 exactly once even with reentrant balance callbacks");
        Require(Get<int>(display, "value") == 100, "HUD immediately displays paid balance");
        for (int i = 0; i < 5; i++) { turn.ThrowYut(); thrower.Throw(); }
        Require(wallet.Balance == 100 && wallet.SpecialBalance == 15, "Repeated inputs during animation cannot double charge");
        while (thrower.IsThrowing) yield return null;
        Require(wallet.Balance == 100, "Throw completion never charges a second time");
        if (turn.CurrentYutResult.HasValue)
        {
            turn.ThrowYut(); thrower.Throw();
            Require(wallet.Balance == 100 && !thrower.IsThrowing, "Unused result blocks another payment");
            turn.ChooseSummon();
            Require(!turn.CurrentYutResult.HasValue && wallet.Balance == 100, "Summon resolves prepaid result without extra payment");
        }
        else Require(wallet.Balance == 100, "Automatic empty-board Backdo consumption does not refund");

        // An explicit empty-board Backdo also covers no-refund independently of RNG.
        SceneManager.LoadScene("ThrowYut");
        for (int i = 0; i < 8; i++) yield return null;
        turn = Find<TurnController>(); wallet = Find<StageCurrency>(); thrower = Find<YutThrowController>();
        waves = Find<WaveManager>(); actions = Find<YutActionController>();
        display = GameObject.Find("Coin HUD").GetComponent<GoldNumberDisplay>();
        throwButton = Get<GameObject>(turn, "throwButton").GetComponent<Button>();
        turn.ThrowYut();
        while (thrower.IsThrowing) yield return null;
        int paidBalance = wallet.Balance;
        Call(turn, "ReceiveYutResult", YutThrowResult.Backdo);
        Require(!turn.CurrentYutResult.HasValue && wallet.Balance == paidBalance && paidBalance == 100,
            "Empty-board Backdo auto-consumes without refund");

        var sticks = Get<YutStick[]>(thrower, "sticks");
        var invalid = (YutStick[])sticks.Clone(); invalid[1] = invalid[0];
        Set(thrower, "sticks", invalid);
        turn.ThrowYut();
        Require(!thrower.IsThrowing && wallet.Balance == 100, "Invalid stick setup rejects before payment");
        Set(thrower, "sticks", sticks);
        turn.OpenShop(); turn.ThrowYut(); thrower.Throw();
        Require(!thrower.IsThrowing && wallet.Balance == 100, "Open shop rejects without payment");
        turn.CloseShop();

        Require(wallet.TrySpend(81) && wallet.Balance == 19, "Set real balance to one below throw cost");
        Require(!turn.CanThrowYut && !throwButton.interactable && Get<int>(display, "value") == 19,
            "19 disables button and updates HUD immediately");
        throwButton.onClick.Invoke(); thrower.Throw();
        Require(!thrower.IsThrowing && wallet.Balance == 19, "Direct callbacks cannot throw below cost");
        Require(wallet.Grant(1) == 1 && throwButton.interactable && turn.CanThrowYut, "Grant to exact cost enables input immediately");
        thrower.Throw();
        Require(thrower.IsThrowing && wallet.Balance == 0 && Get<int>(display, "value") == 0,
            "Direct valid throw pays once and accepts exact-cost balance");
        while (thrower.IsThrowing) yield return null;
        if (turn.CurrentYutResult.HasValue) turn.ChooseSummon();
        Require(!turn.CurrentYutResult.HasValue && !turn.CanThrowYut && wallet.Balance == 0,
            "Prepaid action still resolves at zero balance");
        Require(wallet.GrantSpecial(17) == 17, "Acquire special currency before battle");
        int specialBefore = wallet.SpecialBalance;
        Set(waves, "spawnInterval", 0f);
        int reward = waves.GetCompletionReward(turn.CurrentTurnNumber);
        int killIncome = 0; foreach(var g in Get<WaveData[]>(waves,"waves")[turn.CurrentTurnNumber].Enemies) killIncome += g.count * (g.enemyId==EnemyId.NormalEnemy?2:g.enemyId==EnemyId.FastEnemy?1:0);
        turn.StartBattle();
        Require(waves.IsRunning, "Zero balance does not block battle start");
        turn.ThrowYut(); thrower.Throw();
        Require(wallet.Balance == 0 && !thrower.IsThrowing, "Combat does not accept a paid throw");
        Require(wallet.Grant(7) == 7, "Credit a nonzero normal balance through the wallet API during battle");
        while (waves.IsRunning)
        {
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            { enemy.TakeDamage(int.MaxValue); enemy.CompleteDeathAnimation(); }
            yield return null;
        }
        Require(turn.CurrentTurnNumber == 1 && turn.CanUsePreparationInput && wallet.Balance == 7 + reward + killIncome &&
            wallet.SpecialBalance == specialBefore, "Next wave retains both balances plus existing completion reward");
        Require(Get<int>(display, "value") == 7 + reward + killIncome && turn.CanThrowYut && throwButton.interactable,
            "Completion credit restores HUD and throw availability");
        turn.ThrowYut();
        Require(wallet.Balance == 7 + reward + killIncome - 20 && wallet.SpecialBalance == specialBefore,
            "Next-wave throw spends carried normal balance only");

        // Wallet API boundaries are isolated from the stage progression fixture.
        var probeObject = new GameObject("Currency boundary probe");
        var probe = probeObject.AddComponent<StageCurrency>();
        int normalEvents = 0, specialEvents = 0;
        probe.Changed += _ => normalEvents++; probe.SpecialChanged += _ => specialEvents++;
        Require(probe.Grant(-1) == 0 && probe.GrantSpecial(-1) == 0 && !probe.TrySpend(0) &&
            !probe.TrySpend(-1) && !probe.TrySpendSpecial(0) && !probe.TrySpendSpecial(-1), "Reject nonpositive spending and negative grants");
        Require(!probe.TrySpend(121) && !probe.TrySpendSpecial(1) && normalEvents == 0 && specialEvents == 0,
            "Failed operations neither mutate balances nor emit events");
        Require(probe.Grant(int.MaxValue) == int.MaxValue - 120 && probe.Balance == int.MaxValue &&
            probe.Grant(1) == 0 && normalEvents == 1, "Normal credit saturates safely at integer maximum");
        Require(probe.GrantSpecial(int.MaxValue) == int.MaxValue && probe.GrantSpecial(1) == 0 &&
            probe.SpecialBalance == int.MaxValue && specialEvents == 1, "Special credit saturates safely at integer maximum");
        Require(probe.TrySpend(int.MaxValue) && probe.TrySpendSpecial(int.MaxValue) &&
            probe.Balance == 0 && probe.SpecialBalance == 0 && normalEvents == 2 && specialEvents == 2,
            "Exact spending reaches zero and notifies each balance once");
        UnityEngine.Object.Destroy(probeObject);
        SceneManager.LoadScene("ThrowYut");
        for (int i = 0; i < 8; i++) yield return null;
        wallet = Find<StageCurrency>();
        Require(wallet.Balance == 120 && wallet.SpecialBalance == 0 && Find<TurnController>().CurrentTurnNumber == 0,
            "A fresh scene resets to 120 / 0 instead of carrying the previous stage");
    }
}

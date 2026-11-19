using System;
using UnityEngine;

// Scene-scoped balance: restarting the stage creates a fresh wallet.
public sealed class StageCurrency : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingBalance = 120;
    [SerializeField, Min(0)] private int startingSpecialBalance = 0;
    public int Balance { get; private set; }
    public int SpecialBalance { get; private set; }
    public event Action<int> Changed;
    public event Action<int> SpecialChanged;
    public bool IsNotifying { get; private set; }
    private void Notify(Action<int> listeners, int balance)
    {
        bool previous = IsNotifying; IsNotifying = true;
        try { listeners?.Invoke(balance); }
        finally { IsNotifying = previous; }
    }

    private void Awake()
    {
        Balance = Mathf.Max(0, startingBalance);
        SpecialBalance = Mathf.Max(0, startingSpecialBalance);
    }

    // Returns the amount actually credited, including the integer-cap boundary.
    public int Grant(int amount)
    {
        int credited = Math.Min(Math.Max(0, amount), int.MaxValue - Balance);
        if (credited == 0) return 0;
        Balance += credited;
        Notify(Changed, Balance);
        return credited;
    }

    public bool CanAfford(int amount) => amount > 0 && Balance >= amount;

    public bool TrySpend(int amount)
    {
        return TrySpend(amount, () => true);
    }

    internal bool TrySpend(int amount, Func<bool> commit)
    {
        if (!CanAfford(amount) || commit == null) return false;
        Balance -= amount;
        bool committed = false;
        try { committed = commit(); }
        finally { if (!committed) Balance += amount; }
        if (!committed) return false;
        Notify(Changed, Balance);
        return true;
    }

    public int GrantSpecial(int amount)
    {
        int credited = Math.Min(Math.Max(0, amount), int.MaxValue - SpecialBalance);
        if (credited == 0) return 0;
        SpecialBalance += credited;
        Notify(SpecialChanged, SpecialBalance);
        return credited;
    }

    public bool TrySpendSpecial(int amount)
    {
        return TrySpendSpecial(amount, () => true);
    }

    // Commit placement before notifying observers; failure restores the balance silently.
    internal bool TrySpendSpecial(int amount, Func<bool> commit)
    {
        if (amount <= 0 || SpecialBalance < amount || commit == null) return false;
        SpecialBalance -= amount;
        bool committed = false;
        try { committed = commit(); }
        finally { if (!committed) SpecialBalance += amount; }
        if (!committed) return false;
        Notify(SpecialChanged, SpecialBalance);
        return true;
    }
}

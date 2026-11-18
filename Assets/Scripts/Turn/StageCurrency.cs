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
        Changed?.Invoke(Balance);
        return credited;
    }

    public bool CanAfford(int amount) => amount > 0 && Balance >= amount;

    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount)) return false;
        Balance -= amount;
        Changed?.Invoke(Balance);
        return true;
    }

    public int GrantSpecial(int amount)
    {
        int credited = Math.Min(Math.Max(0, amount), int.MaxValue - SpecialBalance);
        if (credited == 0) return 0;
        SpecialBalance += credited;
        SpecialChanged?.Invoke(SpecialBalance);
        return credited;
    }

    public bool TrySpendSpecial(int amount)
    {
        if (amount <= 0 || SpecialBalance < amount) return false;
        SpecialBalance -= amount;
        SpecialChanged?.Invoke(SpecialBalance);
        return true;
    }
}

using System;
using UnityEngine;

// Scene-scoped balance: restarting the stage creates a fresh wallet.
public sealed class StageCurrency : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingBalance = 120;
    public int Balance { get; private set; }
    public event Action<int> Changed;

    private void Awake()
    {
        Balance = Mathf.Max(0, startingBalance);
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
}

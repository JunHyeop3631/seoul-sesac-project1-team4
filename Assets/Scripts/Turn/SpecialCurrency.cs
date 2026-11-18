using System;
using UnityEngine;

// Separate stage-scoped balance; completion/item rewards can call Grant later.
public sealed class SpecialCurrency : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingBalance;
    public int Balance { get; private set; }
    public event Action<int> Changed;

    private void Awake() { Balance = Mathf.Max(0, startingBalance); }

    public int Grant(int amount)
    {
        int credited = Math.Min(Math.Max(0, amount), int.MaxValue - Balance);
        if (credited == 0) return 0;
        Balance += credited;
        Changed?.Invoke(Balance);
        return credited;
    }
}

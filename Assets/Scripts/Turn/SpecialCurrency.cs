using System;
using UnityEngine;

// Compatibility view for the special-summon HUD. StageCurrency owns the balance.
public sealed class SpecialCurrency : MonoBehaviour
{
    [SerializeField, Min(0)] private int startingBalance;
    private StageCurrency stageCurrency;
    private int standaloneBalance;
    public int Balance => stageCurrency != null ? stageCurrency.SpecialBalance : standaloneBalance;
    public event Action<int> Changed;

    private void Awake()
    {
        stageCurrency = GetComponent<StageCurrency>();
        standaloneBalance = Mathf.Max(0, startingBalance);
    }

    private void OnEnable()
    {
        if (stageCurrency != null) stageCurrency.SpecialChanged += ForwardChange;
    }
    private void OnDisable()
    {
        if (stageCurrency != null) stageCurrency.SpecialChanged -= ForwardChange;
    }
    private void ForwardChange(int balance) => Changed?.Invoke(balance);

    public int Grant(int amount)
    {
        if (stageCurrency != null) return stageCurrency.GrantSpecial(amount);
        int credited = Math.Min(Math.Max(0, amount), int.MaxValue - Balance);
        if (credited == 0) return 0;
        standaloneBalance += credited;
        Changed?.Invoke(Balance);
        return credited;
    }
}

using UnityEngine;

public sealed class CurrencyHUD : MonoBehaviour
{
    [SerializeField] private StageCurrency currency;
    [SerializeField] private GoldNumberDisplay display;

    private void OnEnable()
    {
        if (currency == null) return;
        currency.Changed += Refresh;
        Refresh(currency.Balance);
    }

    // All Awake calls have completed, regardless of scene object ordering.
    private void Start()
    {
        if (currency != null) Refresh(currency.Balance);
    }

    private void OnDisable()
    {
        if (currency != null) currency.Changed -= Refresh;
    }

    private void Refresh(int balance)
    {
        if (display != null) display.SetValue(balance);
    }
}

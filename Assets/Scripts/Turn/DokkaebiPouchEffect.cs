using UnityEngine;

public sealed class DokkaebiPouchEffect : MonoBehaviour, IStageItemEffect
{
    public const int MaximumReward = 300;
    [SerializeField] private StageItemUseController itemUse;
    [SerializeField] private StageCurrency currency;
    private bool applying;
    public bool CanApply => isActiveAndEnabled && currency != null && currency.isActiveAndEnabled &&
        currency.Balance <= int.MaxValue - MaximumReward;
    public bool IsApplied => false;
    private void OnEnable() { if (itemUse != null) itemUse.Register(ShopItemId.DokkaebiPouch, this); }
    private void OnDisable() { if (itemUse != null) itemUse.Unregister(ShopItemId.DokkaebiPouch, this); }
    bool IStageItemEffect.TryApply()
    {
        if (applying || itemUse == null || !itemUse.IsUsing || !CanApply) return false;
        applying = true;
        try
        {
            // Integer Range excludes5: five equally likely outcomes, each20 percent.
            int reward = 100 + Random.Range(0, 5) * 50;
            return currency.Grant(reward) == reward;
        }
        finally { applying = false; }
    }
}

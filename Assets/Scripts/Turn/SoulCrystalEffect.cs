using UnityEngine;

public sealed class SoulCrystalEffect : MonoBehaviour, IStageItemEffect
{
    public const int Reward = 5;
    [SerializeField] private StageItemUseController itemUse;
    [SerializeField] private StageCurrency currency;
    private bool applying;
    public bool CanApply => isActiveAndEnabled && currency != null && currency.isActiveAndEnabled &&
        currency.SpecialBalance <= int.MaxValue - Reward;
    public bool IsApplied => false;
    private void OnEnable() { if (itemUse != null) itemUse.Register(ShopItemId.SoulCrystal, this); }
    private void OnDisable() { if (itemUse != null) itemUse.Unregister(ShopItemId.SoulCrystal, this); }
    bool IStageItemEffect.TryApply()
    {
        if (applying || itemUse == null || !itemUse.IsUsing || !CanApply) return false;
        applying = true;
        try { return currency.GrantSpecial(Reward) == Reward; }
        finally { applying = false; }
    }
}

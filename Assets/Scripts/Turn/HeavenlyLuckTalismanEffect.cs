using UnityEngine;

// Purchase history is the stage-lifetime source of truth. No second activation flag or use action.
public sealed class HeavenlyLuckTalismanEffect : MonoBehaviour, IStageItemEffect
{
    public const int DiscountedThrowCost = 16;
    [SerializeField] private ShopPurchaseController purchases;
    [SerializeField] private StageItemUseController itemUse;
    public bool IsApplied => purchases != null && purchases.IsPurchased(ShopItemId.HeavenlyLuckTalisman);
    public bool CanApply => false;
    public bool TryApply() => false;
    private void OnEnable() { if (itemUse != null) itemUse.Register(ShopItemId.HeavenlyLuckTalisman, this); }
    private void OnDisable() { if (itemUse != null) itemUse.Unregister(ShopItemId.HeavenlyLuckTalisman, this); }
}

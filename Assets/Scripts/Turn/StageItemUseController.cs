using System.Collections.Generic;
using UnityEngine;

// Effects own their rule/state. The inventory owns consumption and the preparation input lock.
public interface IStageItemEffect
{
    bool CanApply { get; }
    bool IsApplied { get; }
    bool TryApply();
}

public sealed class StageItemUseController : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private StageInventory inventory;
    private readonly Dictionary<ShopItemId, IStageItemEffect> effects = new();
    public bool IsUsing { get; private set; }
    private void OnEnable() { if (turnController != null) turnController.SetItemUseController(this); }
    private void OnDisable() { if (turnController != null) turnController.SetItemUseController(null); }
    public bool Register(ShopItemId id, IStageItemEffect effect)
    {
        if (effect == null || effects.ContainsKey(id)) return false;
        effects.Add(id, effect); return true;
    }
    public void Unregister(ShopItemId id, IStageItemEffect effect)
    {
        if (effects.TryGetValue(id, out var current) && ReferenceEquals(current, effect)) effects.Remove(id);
    }
    public bool IsApplied(ShopItemId id) => effects.TryGetValue(id, out var effect) && effect.IsApplied;
    public bool CanUse(ShopItemId id) => isActiveAndEnabled && !IsUsing &&
        turnController != null && turnController.CanOpenPreparationPanel &&
        inventory != null && inventory.isActiveAndEnabled && inventory.Contains(id) &&
        id != ShopItemId.HeavenlyLuckTalisman && effects.TryGetValue(id, out var effect) && effect.CanApply;

    public bool TryUse(ShopItemId id)
    {
        if (!CanUse(id)) return false;
        IsUsing = true;
        bool taken = false, applied = false;
        try
        {
            // Currency/effect observers must already see the item removed. Failure restores silently.
            taken = inventory.TryTake(id);
            if (!taken) return false;
            applied = effects[id].TryApply();
            if (!applied) return false;
            inventory.NotifyChanged();
            return true;
        }
        finally
        {
            if (taken && !applied) inventory.TryAdd(id);
            IsUsing = false;
        }
    }
}

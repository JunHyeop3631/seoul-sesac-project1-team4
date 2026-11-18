using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShopPurchaseController : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private StageCurrency currency;
    [SerializeField] private StageInventory inventory;
    [SerializeField] private ShopItemDefinition[] products;
    private readonly HashSet<ShopItemId> purchased = new();
    public bool IsPurchasing { get; private set; }
    public StageCurrency Currency => currency;
    public bool IsPurchased(ShopItemId id) => purchased.Contains(id);
    public event Action<ShopItemDefinition> Purchased;
    private void OnEnable() { if (turnController != null) turnController.SetShopPurchaseController(this); }
    private void OnDisable() { if (turnController != null) turnController.SetShopPurchaseController(null); }

    public bool CanPurchase(ShopItemDefinition product)
    {
        return isActiveAndEnabled && !IsPurchasing && turnController != null && turnController.CanOpenPreparationPanel &&
            currency != null && currency.isActiveAndEnabled && inventory != null && inventory.isActiveAndEnabled &&
            product != null && products != null && Array.Exists(products, entry => entry == product) &&
            product.Price > 0 && !purchased.Contains(product.Id) && !inventory.Contains(product.Id) && currency.CanAfford(product.Price);
    }

    public bool TryPurchase(ShopItemDefinition product)
    {
        if (!CanPurchase(product)) return false;
        IsPurchasing = true;
        try
        {
            if (!currency.TrySpend(product.Price, () =>
                {
                    if (!inventory.TryAdd(product.Id)) return false;
                    purchased.Add(product.Id); return true;
                })) return false;
            inventory.NotifyChanged();
            Purchased?.Invoke(product);
            return true;
        }
        finally { IsPurchasing = false; }
    }
}

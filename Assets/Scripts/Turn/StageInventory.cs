using System;
using System.Collections.Generic;
using UnityEngine;

// Scene lifetime. Consuming an owned item never clears the separate shop purchase history.
public sealed class StageInventory : MonoBehaviour
{
    private readonly HashSet<ShopItemId> owned = new();
    public IReadOnlyCollection<ShopItemId> OwnedItems => owned;
    public bool Contains(ShopItemId id) => owned.Contains(id);
    public event Action Changed;
    internal bool TryAdd(ShopItemId id) => Enum.IsDefined(typeof(ShopItemId), id) && owned.Add(id);
    internal void NotifyChanged() => Changed?.Invoke();
    internal bool TryTake(ShopItemId id) => owned.Remove(id);
    // Explicit removal with notification; item transactions use TryTake until their effect commits.
    internal bool TryRemove(ShopItemId id)
    {
        if (!TryTake(id)) return false;
        Changed?.Invoke(); return true;
    }
}

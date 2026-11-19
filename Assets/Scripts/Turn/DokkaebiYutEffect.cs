using System;
using UnityEngine;

public sealed class DokkaebiYutEffect : MonoBehaviour, IStageItemEffect
{
    [SerializeField] private StageItemUseController itemUse;
    [SerializeField] private YutThrowController throwController;
    public bool HasPending { get; private set; }
    public bool IsApplied => HasPending;
    public bool CanApply => isActiveAndEnabled && !HasPending;
    private void OnEnable() { if (itemUse != null) itemUse.Register(ShopItemId.DokkaebiYut, this); }
    private void OnDisable() { if (itemUse != null) itemUse.Unregister(ShopItemId.DokkaebiYut, this); }
    bool IStageItemEffect.TryApply()
    {
        if (itemUse == null || !itemUse.IsUsing || !CanApply) return false;
        HasPending = true; return true;
    }
    internal bool TryConsumeForThrow(YutThrowController requester, out YutThrowResult result)
    {
        result = default;
        if (!isActiveAndEnabled || !HasPending || requester != throwController || !requester.IsThrowing) return false;
        result = ResultForTicket(UnityEngine.Random.Range(0, 28));
        HasPending = false; return true;
    }
    internal static YutThrowResult ResultForTicket(int ticket)
    {
        if (ticket < 0 || ticket >= 28) throw new ArgumentOutOfRangeException(nameof(ticket));
        if (ticket < 1) return YutThrowResult.Backdo;
        if (ticket < 4) return YutThrowResult.Do;
        if (ticket < 10) return YutThrowResult.Gae;
        if (ticket < 14) return YutThrowResult.Geol;
        return ticket < 21 ? YutThrowResult.Yut : YutThrowResult.Mo;
    }
}

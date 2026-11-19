using UnityEngine;

public sealed class HeavenlyMandateEffect : MonoBehaviour, IStageItemEffect
{
    [SerializeField] private StageItemUseController itemUse;
    public bool HasPending { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsApplied => HasPending || IsActive;
    public bool CanApply => isActiveAndEnabled && !HasPending && !IsActive;
    private void OnEnable() { if (itemUse != null) itemUse.Register(ShopItemId.HeavenlyMandateCoin, this); }
    private void OnDisable() { if (itemUse != null) itemUse.Unregister(ShopItemId.HeavenlyMandateCoin, this); }
    bool IStageItemEffect.TryApply()
    {
        if (itemUse == null || !itemUse.IsUsing || !CanApply) return false;
        HasPending = true; return true;
    }
    // Called before spawning: even an enemy defeated inside BattleStarted gets the correct multiplier.
    internal void BeginBattle() { IsActive = HasPending; HasPending = false; }
    internal void CancelBattleStart()
    {
        if (IsActive) { HasPending = true; IsActive = false; }
    }
    internal void FinishBattle() { IsActive = false; HasPending = false; }
}

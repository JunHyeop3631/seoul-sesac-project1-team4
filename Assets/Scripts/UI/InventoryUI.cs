using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryUI : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private StageInventory inventory;
    [SerializeField] private StageItemUseController itemUse;
    [SerializeField] private ShopItemDefinition[] items;
    [SerializeField] private Button[] slots;
    [SerializeField] private Image[] icons, highlights;
    [SerializeField] private GameObject detail;
    [SerializeField] private Image selectedIcon;
    [SerializeField] private Text selectedName, description, status, emptyHint;
    [SerializeField] private Button useButton, backButton;
    [SerializeField] private Text pendingEffectStatus;
    private readonly ShopItemDefinition[] contents = new ShopItemDefinition[9];
    public ShopItemDefinition SelectedItem { get; private set; }
    private bool CanSelect => isActiveAndEnabled && turnController != null && turnController.CanOpenPreparationPanel;

    private void Awake()
    {
        for (int i = 0; i < slots.Length; i++) { int index = i; slots[i].onClick.AddListener(() => SelectSlot(index)); }
        useButton.onClick.AddListener(UseSelected);
        backButton.onClick.AddListener(ClearSelection);
    }
    private void OnEnable()
    {
        if (!CanSelect) { turnController.SetInventoryOpen(false); return; }
        inventory.Changed += Refresh;
        ClearSelection(); Refresh();
    }
    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= Refresh;
        ClearSelection();
    }
    private void LateUpdate()
    {
        if (turnController != null && turnController.IsExitConfirmationOpen) return;
        if (!CanSelect) { turnController.SetInventoryOpen(false); return; }
        if (pendingEffectStatus != null)
        {
            bool yut = itemUse != null && itemUse.IsApplied(ShopItemId.DokkaebiYut);
            bool mandate = itemUse != null && itemUse.IsApplied(ShopItemId.HeavenlyMandateCoin);
            pendingEffectStatus.text = (yut ? "도깨비 윷: 다음 던지기 대기 중" : "") +
                (yut && mandate ? "\n" : "") + (mandate ? "천명전: 다음 전투 보상 2배 대기 중" : "");
        }
        RefreshSelection();
    }
    private void Refresh()
    {
        int count = 0;
        foreach (var item in items) if (item != null && inventory.Contains(item.Id)) contents[count++] = item;
        for (int i = count; i < contents.Length; i++) contents[i] = null;
        for (int i = 0; i < slots.Length; i++)
        {
            var item = contents[i]; slots[i].interactable = item != null;
            icons[i].enabled = item != null; icons[i].sprite = item != null ? item.Icon : null;
            highlights[i].enabled = item != null && item == SelectedItem;
        }
        emptyHint.gameObject.SetActive(count == 0);
        if (SelectedItem != null && !inventory.Contains(SelectedItem.Id)) ClearSelection();
        RefreshSelection();
    }
    public bool SelectSlot(int index)
    {
        if (!CanSelect || index < 0 || index >= contents.Length || contents[index] == null) return false;
        SelectedItem = contents[index]; Refresh(); return true;
    }
    public void ClearSelection()
    {
        SelectedItem = null;
        if (detail != null) detail.SetActive(false);
        if (highlights != null) foreach (var highlight in highlights) if (highlight != null) highlight.enabled = false;
    }
    private void RefreshSelection()
    {
        if (SelectedItem == null) return;
        detail.SetActive(true);
        selectedIcon.sprite = SelectedItem.Icon;
        selectedName.text = SelectedItem.DisplayName;
        description.text = $"배경: {SelectedItem.Background}\n\n효과: {SelectedItem.Effect}";
        bool passive = SelectedItem.Id == ShopItemId.HeavenlyLuckTalisman;
        useButton.gameObject.SetActive(!passive);
        useButton.interactable = CanSelect && itemUse != null && itemUse.CanUse(SelectedItem.Id);
        useButton.image.color = useButton.interactable ? Color.white : new Color(.55f,.55f,.55f,1);
        status.text = passive ? (itemUse != null && itemUse.IsApplied(SelectedItem.Id) ? "적용 중" : "보유 중") : "준비 중 사용";
    }
    public void UseSelected()
    {
        if (!CanSelect || SelectedItem == null || itemUse == null) return;
        itemUse.TryUse(SelectedItem.Id); Refresh();
    }
}

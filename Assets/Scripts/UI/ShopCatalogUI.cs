using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopCatalogUI : MonoBehaviour
{
    [SerializeField] private TurnController turnController;
    [SerializeField] private ShopItemDefinition[] items;
    [SerializeField] private Button[] itemButtons;
    [SerializeField] private Image[] rowIcons, selectionHighlights;
    [SerializeField] private GoldNumberDisplay[] rowPrices;
    [SerializeField] private Image selectedIcon, selectedName;
    [SerializeField] private Text description;
    [SerializeField] private GoldNumberDisplay selectedPrice;
    [SerializeField] private Button buyButton;
    [SerializeField] private ShopPurchaseController purchases;
    [SerializeField] private Text purchaseStatus;
    [SerializeField] private Text[] rowStatus;
    [SerializeField] private CanvasGroup[] backgroundControls;
    private float[] previousAlpha;
    private bool[] previousInteractable, previousRaycasts;
    private int selectedIndex;
    public IReadOnlyList<ShopItemDefinition> Items => items;
    public ShopItemDefinition SelectedItem { get; private set; }
    public event Action<ShopItemDefinition> SelectionChanged;
    private bool CanSelect => isActiveAndEnabled && turnController != null && turnController.CanOpenPreparationPanel;

    private void Awake()
    {
        for (int i = 0; i < itemButtons.Length; i++)
        {
            int index = i; itemButtons[i].onClick.AddListener(() => SelectItem(index));
        }
        if (buyButton != null) buyButton.onClick.AddListener(BuySelected);
    }

    private void OnEnable()
    {
        if (!CanSelect) { gameObject.SetActive(false); return; }
        if (purchases != null)
        {
            purchases.Purchased += HandlePurchased;
            if (purchases.Currency != null) purchases.Currency.Changed += HandleBalance;
        }
        previousAlpha = new float[backgroundControls.Length];
        previousInteractable = new bool[backgroundControls.Length]; previousRaycasts = new bool[backgroundControls.Length];
        for (int i = 0; i < backgroundControls.Length; i++)
        {
            var group = backgroundControls[i]; if (group == null) continue;
            previousAlpha[i] = group.alpha; previousInteractable[i] = group.interactable; previousRaycasts[i] = group.blocksRaycasts;
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
        }
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            rowIcons[i].sprite = items[i].Icon; rowIcons[i].preserveAspect = true;
            rowPrices[i].SetValue(items[i].Price);
        }
        SelectItem(Mathf.Clamp(selectedIndex, 0, items.Length - 1));
    }

    private void LateUpdate()
    {
        if (!CanSelect) gameObject.SetActive(false);
        else RefreshPurchaseState();
    }

    private void OnDisable()
    {
        if (purchases != null)
        {
            purchases.Purchased -= HandlePurchased;
            if (purchases.Currency != null) purchases.Currency.Changed -= HandleBalance;
        }
        if (previousAlpha == null) return;
        for (int i = 0; i < backgroundControls.Length; i++)
        {
            var group = backgroundControls[i]; if (group == null) continue;
            group.alpha = previousAlpha[i]; group.interactable = previousInteractable[i]; group.blocksRaycasts = previousRaycasts[i];
        }
        previousAlpha = null;
    }

    public bool SelectItem(int index)
    {
        if (!CanSelect || items == null || index < 0 || index >= items.Length || items[index] == null) return false;
        var item = items[index]; bool changed = SelectedItem != item;
        selectedIndex = index; SelectedItem = item;
        selectedIcon.sprite = item.Icon; selectedIcon.preserveAspect = true;
        selectedName.sprite = item.NameSprite; selectedName.preserveAspect = true;
        selectedPrice.SetValue(item.Price);
        description.text = $"배경: {item.Background}\n\n효과: {item.Effect}";
        for (int i = 0; i < selectionHighlights.Length; i++) selectionHighlights[i].enabled = i == index;
        RefreshPurchaseState();
        if (changed) SelectionChanged?.Invoke(item);
        return true;
    }

    public void BuySelected()
    {
        if (!CanSelect || purchases == null || SelectedItem == null) return;
        purchases.TryPurchase(SelectedItem);
        RefreshPurchaseState();
    }

    private void HandleBalance(int balance) => RefreshPurchaseState();
    private void HandlePurchased(ShopItemDefinition item) => RefreshPurchaseState();

    private void RefreshPurchaseState()
    {
        if (purchases == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i]; if (item == null) continue;
            bool sold = purchases.IsPurchased(item.Id);
            bool affordable = purchases.Currency != null && purchases.Currency.CanAfford(item.Price);
            string status = sold ? "구매 완료" : affordable ? "" : "재화 부족";
            if (i < rowStatus.Length && rowStatus[i] != null && rowStatus[i].text != status) rowStatus[i].text = status;
            Color color = sold ? new Color(.55f, .55f, .55f, 1f) : Color.white;
            rowIcons[i].color = color;
            foreach (var digit in rowPrices[i].GetComponentsInChildren<Image>()) digit.color = color;
        }
        bool available = CanSelect && purchases.CanPurchase(SelectedItem);
        if (buyButton != null)
        {
            buyButton.interactable = available;
            buyButton.image.color = available ? Color.white : new Color(.55f, .55f, .55f, 1f);
        }
        string reason = SelectedItem != null && purchases.IsPurchased(SelectedItem.Id)
            ? "구매 완료" : SelectedItem != null && purchases.Currency != null && purchases.Currency.CanAfford(SelectedItem.Price)
                ? "구매 가능" : "재화 부족";
        if (purchaseStatus != null && purchaseStatus.text != reason) purchaseStatus.text = reason;
    }
}

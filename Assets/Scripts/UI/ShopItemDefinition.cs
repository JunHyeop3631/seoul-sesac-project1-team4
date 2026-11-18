using UnityEngine;

public enum ShopItemId { SoulCrystal, HeavenlyLuckTalisman, DokkaebiPouch, DokkaebiYut, HeavenlyMandateCoin }

[CreateAssetMenu(menuName = "Yut/Shop Item")]
public sealed class ShopItemDefinition : ScriptableObject
{
    [SerializeField] private ShopItemId id;
    [SerializeField] private string displayName;
    [SerializeField, Min(0)] private int price;
    [SerializeField] private Sprite icon, nameSprite;
    [SerializeField, TextArea(2, 4)] private string background;
    [SerializeField, TextArea(2, 4)] private string effect;
    public ShopItemId Id => id;
    public string DisplayName => displayName;
    public int Price => price;
    public Sprite Icon => icon;
    public Sprite NameSprite => nameSprite;
    public string Background => background;
    public string Effect => effect;
}

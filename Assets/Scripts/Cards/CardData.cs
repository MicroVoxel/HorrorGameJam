using UnityEngine;

/// <summary>
/// ข้อมูลการ์ดสิ่งก่อสร้างสไตล์ Loop Hero
/// อ้างอิง TowerBase Prefab โดยตรง
/// </summary>
[CreateAssetMenu(fileName = "New_CardData", menuName = "GameJam/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Card Identity")]
    [SerializeField] private string cardId = "card_tower";
    [SerializeField] private string cardName = "cardName";
    [TextArea(2, 4)]
    [SerializeField] private string description = "cardDescription";

    [Header("Visuals")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Color frameThemeColor = Color.white;

    [Header("Placement Spawn Target")]
    [Tooltip("Prefab ของสิ่งก่อสร้างที่มีคอมโพเนนต์ TowerBase")]
    [SerializeField] private TowerBase towerPrefab;

    public string CardId => cardId;
    public string CardName => cardName;
    public string Description => description;
    public Sprite Icon => icon;
    public Color FrameThemeColor => frameThemeColor;
    public TowerBase TowerPrefab => towerPrefab;

    public bool CanPlaceOn(HexGroundType groundType)
    {
        if (towerPrefab == null) return false;
        return towerPrefab.CanPlaceOn(groundType);
    }
}
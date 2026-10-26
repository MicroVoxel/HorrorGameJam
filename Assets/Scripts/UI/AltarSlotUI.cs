using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI ช่องวางของถวายบนโต๊ะหมู่บูชา (1 ช่องต่อ 1 หมวดหมู่ OfferingType)
/// ตรวจสอบประเภทไอเทมที่ลากมาวาง และเปลี่ยนสี/ไอคอนเมื่อของแปรสภาพจากของสดเป็นของเน่าเสีย (Rotten)
/// </summary>
[DisallowMultipleComponent]
public class AltarSlotUI : MonoBehaviour, IDropHandler
{
    [Header("Slot Category Lock")]
    [SerializeField] private OfferingType slotCategory = OfferingType.Drink;

    [Header("UI References")]
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image itemIconImage;
    [SerializeField] private TextMeshProUGUI categoryHeaderText;
    [SerializeField] private TextMeshProUGUI decayCountdownText;

    [Header("State Colors")]
    [SerializeField] private Color emptySlotColor = new Color(0.18f, 0.18f, 0.22f, 0.85f);
    [SerializeField] private Color freshOfferingColor = new Color(0.75f, 0.58f, 0.18f, 0.9f);
    [SerializeField] private Color rottenOfferingColor = new Color(0.48f, 0.12f, 0.62f, 0.95f);

    public OfferingType SlotCategory => slotCategory;

    public void RefreshSlotVisual(ActiveAltarSlotState state)
    {
        if (categoryHeaderText != null)
        {
            categoryHeaderText.text = GetCategoryDisplayName(slotCategory);
        }

        if (state == null || !state.HasOffering)
        {
            if (slotBackground != null) slotBackground.color = emptySlotColor;
            if (itemIconImage != null)
            {
                itemIconImage.enabled = false;
                itemIconImage.sprite = null;
            }
            if (decayCountdownText != null)
            {
                decayCountdownText.text = "ว่าง";
            }
            return;
        }

        OfferingItemData item = state.currentItem;

        if (slotBackground != null)
        {
            slotBackground.color = state.isRotten ? rottenOfferingColor : freshOfferingColor;
        }

        if (itemIconImage != null)
        {
            Sprite targetSprite = state.isRotten ? item.RottenIcon : item.FreshIcon;
            itemIconImage.enabled = targetSprite != null;
            itemIconImage.sprite = targetSprite;
        }

        if (decayCountdownText != null)
        {
            decayCountdownText.text = state.isRotten
                ? "☠️ เน่าแล้ว! (ผีคลั่ง)"
                : $"สด ({state.remainingDecayLoops} ธูป)";
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        InventorySlotUI sourceSlot = eventData.pointerDrag.GetComponent<InventorySlotUI>();
        if (sourceSlot == null || sourceSlot.CurrentItem == null) return;

        // ตรวจสอบว่าหมวดหมู่ของไอเทมตรงกับช่องบนโต๊ะบูชานี้หรือไม่
        if (sourceSlot.CurrentItem.Category != slotCategory)
        {
            Debug.LogWarning($"<color=orange>[AltarSlotUI]</color> วางไม่ตรงช่อง! '{sourceSlot.CurrentItem.ItemName}' เป็นหมวด {sourceSlot.CurrentItem.Category} แต่ช่องนี้รับเฉพาะ {slotCategory}");
            return;
        }

        if (InventorySlotManager.Instance != null)
        {
            InventorySlotManager.Instance.TryEquipSlotToAltar(sourceSlot.SlotIndex);
        }
    }

    private string GetCategoryDisplayName(OfferingType type)
    {
        switch (type)
        {
            case OfferingType.Drink: return "🧃 เครื่องดื่ม";
            case OfferingType.Savory: return "🍱 อาหารคาว";
            case OfferingType.Sweet: return "🍙 ขนมหวาน";
            case OfferingType.Fragrance: return "🧴 เครื่องหอม";
            case OfferingType.Utility: return "🕯️ ของใช้";
            default: return type.ToString();
        }
    }
}
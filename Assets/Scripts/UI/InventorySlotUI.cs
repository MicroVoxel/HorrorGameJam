using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI ช่องเก็บของเดี่ยว (4x2 Backpack) v2 (GDD 6.2 & 6.5):
/// - ไม่มี emoji ใช้ข้อความจาก UITextProvider.Text
/// - ช่องซ้ายสุด (SlotIndex == 0) แสดงเครื่องหมายเตือน "ชิ้นถัดไปที่จะถูกดันทิ้งเมื่อกระเป๋าเต็ม"
/// - Hover แสดงรายละเอียดของสด/เน่าใน TooltipPanel และล็อกคำอธิบายระหว่างลาก
/// - แจ้งเตือน OnDraggingItemChanged เพื่อให้ AltarSlotUI เรืองแสงเฉพาะช่องที่ตรงหมวดหมู่
/// </summary>
[DisallowMultipleComponent]
public class InventorySlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    ITooltipSource
{
    public static event Action<OfferingItemData> OnDraggingItemChanged;

    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI categoryBadgeText;
    [Tooltip("(ทางเลือก) เครื่องหมายเตือนที่ช่องแรกสุดว่าจะถูกดันทิ้งเป็นชิ้นถัดไปหากกระเป๋าเต็ม")]
    [SerializeField] private GameObject nextDiscardWarningBadge;

    private int slotIndex;
    private OfferingItemData currentItem;
    private Canvas parentCanvas;
    private GameObject dragIconObj;
    private RectTransform dragIconRect;

    public int SlotIndex => slotIndex;
    public OfferingItemData CurrentItem => currentItem;

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
    }

    public void BindSlot(int index, OfferingItemData item)
    {
        slotIndex = index;
        currentItem = item;

        bool hasItem = currentItem != null;

        if (iconImage != null)
        {
            iconImage.enabled = hasItem && currentItem.FreshIcon != null;
            iconImage.sprite = hasItem ? currentItem.FreshIcon : null;
        }

        if (itemNameText != null)
        {
            itemNameText.text = hasItem ? currentItem.ItemName : string.Empty;
        }

        if (categoryBadgeText != null)
        {
            categoryBadgeText.text = hasItem
                ? UITextProvider.Text.GetOfferingCategoryName(currentItem.Category)
                : string.Empty;
        }

        if (nextDiscardWarningBadge != null)
        {
            bool showWarning = slotIndex == 0 && hasItem;
            if (nextDiscardWarningBadge.activeSelf != showWarning)
            {
                nextDiscardWarningBadge.SetActive(showWarning);
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentItem == null) return;

        if (eventData.button == PointerEventData.InputButton.Right || eventData.clickCount >= 2)
        {
            if (InventorySlotManager.Instance != null)
            {
                InventorySlotManager.Instance.TryEquipSlotToAltar(slotIndex);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipPanel.Instance != null && TryGetTooltipData(out TooltipData data))
        {
            TooltipPanel.Instance.Show(this, data);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipPanel.Instance != null)
        {
            TooltipPanel.Instance.Clear(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentItem == null || parentCanvas == null || !GameStateManager.IsPlayerActionPhase) return;

        dragIconObj = new GameObject($"Dragging_{currentItem.ItemName}");
        dragIconObj.transform.SetParent(parentCanvas.transform, false);
        dragIconObj.transform.SetAsLastSibling();

        Image img = dragIconObj.AddComponent<Image>();
        img.sprite = currentItem.FreshIcon;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.85f);

        dragIconRect = dragIconObj.GetComponent<RectTransform>();
        dragIconRect.sizeDelta = new Vector2(64f, 64f);
        dragIconRect.position = eventData.position;

        if (TooltipPanel.Instance != null && TryGetTooltipData(out TooltipData data))
        {
            TooltipPanel.Instance.Lock(this, data);
        }

        OnDraggingItemChanged?.Invoke(currentItem);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIconRect != null)
        {
            dragIconRect.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIconObj != null)
        {
            Destroy(dragIconObj);
            dragIconObj = null;
            dragIconRect = null;
        }

        if (TooltipPanel.Instance != null)
        {
            TooltipPanel.Instance.Unlock(this);
        }

        OnDraggingItemChanged?.Invoke(null);
    }

    public bool TryGetTooltipData(out TooltipData data)
    {
        if (currentItem == null)
        {
            data = default;
            return false;
        }

        UITextData t = UITextProvider.Text;

        // FIX บัก 7: แสดงอายุที่เหลือจริงของชิ้นนี้ (ของที่ถอดจากแท่นอาจเหลือน้อยกว่าเต็มอายุ)
        int remaining = InventorySlotManager.Instance != null
            ? InventorySlotManager.Instance.GetRemainingAt(slotIndex)
            : currentItem.DecayLoops;
        if (remaining <= 0) remaining = currentItem.DecayLoops;

        string subtitle = $"{t.GetOfferingCategoryName(currentItem.Category)}  |  Decay: {remaining} round(s)";
        string detail = BuildOfferingEffectsDetail(currentItem, slotIndex == 0 ? t.inventoryNextDiscardHint : null);

        data = TooltipData.Create(currentItem.ItemName, currentItem.Description, detail, subtitle);
        return true;
    }

    public static string BuildOfferingEffectsDetail(OfferingItemData item, string extraNote = null)
    {
        if (item == null) return string.Empty;

        StringBuilder sb = new StringBuilder();
        sb.Append("Fresh: ");
        switch (item.Category)
        {
            case OfferingType.Drink:
                sb.Append("All towers gain Double Strike.");
                break;
            case OfferingType.Savory:
                sb.Append($"Shrine damage taken x{1f - item.FreshShrineDamageReductionPercent:0.##}.");
                break;
            case OfferingType.Sweet:
                sb.Append($"Pushes lead spirit back {item.FreshPushLeadSpiritTiles} tile(s) every {item.FreshPushEveryNTurns} turns.");
                break;
            case OfferingType.Fragrance:
                sb.Append($"Spirit attack damage x{item.FreshEntityAttackMultiplier:0.##}.");
                break;
            case OfferingType.Utility:
                sb.Append("Tower patterns ignore unit blocking.");
                break;
        }

        sb.Append("\nRotten: ");
        switch (item.Category)
        {
            case OfferingType.Drink:
                sb.Append($"All spirits gain Lifedrain and +{item.RottenEntityMoveBonus} Move.");
                break;
            case OfferingType.Savory:
                sb.Append($"Shrine DEF becomes 0 and takes {item.RottenShrinePoisonDamagePerTurn:0} poison damage per turn.");
                break;
            case OfferingType.Sweet:
                sb.Append("Lead mutatable spirit mutates immediately.");
                break;
            case OfferingType.Fragrance:
                sb.Append($"All tower pattern sizes {item.RottenTowerPatternSizeDelta}.");
                break;
            case OfferingType.Utility:
                sb.Append($"All towers skip actions for {item.RottenTowersSkipDurationRounds} round(s).");
                break;
        }

        if (!string.IsNullOrEmpty(extraNote))
        {
            sb.Append('\n').Append(extraNote);
        }

        return sb.ToString();
    }
}
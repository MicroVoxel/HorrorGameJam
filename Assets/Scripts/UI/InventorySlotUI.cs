using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI ช่องเก็บของเดี่ยว (1 ช่องใน Backpack 8 ช่อง)
/// รองรับทั้งการคลิกขวาเพื่อส่งของขึ้นโต๊ะบูชาทันที (Quick-Equip) และการลากวาง (Drag & Drop) ไปยัง AltarSlotUI
/// </summary>
[DisallowMultipleComponent]
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI categoryBadgeText;

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
            itemNameText.text = hasItem ? currentItem.ItemName : "";
        }

        if (categoryBadgeText != null)
        {
            categoryBadgeText.text = hasItem ? GetShortCategoryLabel(currentItem.Category) : "";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentItem == null) return;

        // คลิกขวาหรือดับเบิลคลิกซ้ายเพื่อส่งของขึ้นโต๊ะหมู่บูชาทันที
        if (eventData.button == PointerEventData.InputButton.Right || eventData.clickCount >= 2)
        {
            if (InventorySlotManager.Instance != null)
            {
                InventorySlotManager.Instance.TryEquipSlotToAltar(slotIndex);
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentItem == null || parentCanvas == null) return;

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
    }

    private string GetShortCategoryLabel(OfferingType type)
    {
        switch (type)
        {
            case OfferingType.Drink: return "น้ำ";
            case OfferingType.Savory: return "คาว";
            case OfferingType.Sweet: return "ขนม";
            case OfferingType.Fragrance: return "หอม";
            case OfferingType.Utility: return "ของใช้";
            default: return "";
        }
    }
}
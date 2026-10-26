using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI ช่องวางของถวายบนโต๊ะหมู่บูชา (1 ช่องต่อ 1 หมวดหมู่ OfferingType) v2 (GDD 6.1, 6.2, 6.5):
/// - ใช้ข้อความภาษาอังกฤษจาก UITextProvider.Text ไม่มี emoji
/// - ขณะลากไอเทมจากกระเป๋า: ช่องที่ตรงหมวดหมู่จะเรืองแสง ช่องที่ไม่ตรงจะหรี่ลง
/// - Hover แสดงข้อมูลของถวายและสถานะปัจจุบันใน TooltipPanel
/// </summary>
[DisallowMultipleComponent]
public class AltarSlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, ITooltipSource
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
    [SerializeField] private Color matchingDragHighlightColor = new Color(0.95f, 0.85f, 0.35f, 1.0f);

    private ActiveAltarSlotState cachedState;
    private CanvasGroup canvasGroup;

    public OfferingType SlotCategory => slotCategory;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        InventorySlotUI.OnDraggingItemChanged += HandleDraggingItemChanged;
    }

    private void OnDisable()
    {
        InventorySlotUI.OnDraggingItemChanged -= HandleDraggingItemChanged;
    }

    public void RefreshSlotVisual(ActiveAltarSlotState state)
    {
        cachedState = state;
        UITextData t = UITextProvider.Text;

        if (categoryHeaderText != null)
        {
            categoryHeaderText.text = t.GetOfferingCategoryName(slotCategory);
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
                decayCountdownText.text = t.altarEmpty;
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
                ? t.altarRotten
                : UITextData.Format(t.altarFresh, "rounds", state.remainingDecayLoops);
        }
    }

    private void HandleDraggingItemChanged(OfferingItemData draggedItem)
    {
        if (draggedItem == null)
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            RefreshSlotVisual(cachedState);
            return;
        }

        bool matches = draggedItem.Category == slotCategory;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = matches ? 1f : 0.4f;
        }

        if (matches && slotBackground != null)
        {
            slotBackground.color = matchingDragHighlightColor;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        InventorySlotUI sourceSlot = eventData.pointerDrag.GetComponent<InventorySlotUI>();
        if (sourceSlot == null || sourceSlot.CurrentItem == null) return;

        if (sourceSlot.CurrentItem.Category != slotCategory)
        {
            return;
        }

        if (InventorySlotManager.Instance != null)
        {
            InventorySlotManager.Instance.TryEquipSlotToAltar(sourceSlot.SlotIndex);
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

    public bool TryGetTooltipData(out TooltipData data)
    {
        UITextData t = UITextProvider.Text;
        string categoryName = t.GetOfferingCategoryName(slotCategory);

        if (cachedState == null || !cachedState.HasOffering)
        {
            data = TooltipData.Create($"{categoryName} Slot", t.altarEmpty, null, "Altar Slot");
            return true;
        }

        OfferingItemData item = cachedState.currentItem;
        string stateText = cachedState.isRotten
            ? t.altarRotten
            : UITextData.Format(t.altarFresh, "rounds", cachedState.remainingDecayLoops);

        string detail = InventorySlotUI.BuildOfferingEffectsDetail(item);
        data = TooltipData.Create(item.ItemName, $"{stateText}\n{item.Description}", detail, $"{categoryName} Slot");
        return true;
    }
}
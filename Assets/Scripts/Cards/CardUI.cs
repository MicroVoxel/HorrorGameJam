using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// การ์ดบนมือ: คลิกซ้าย = เลือก/ยกเลิกการ์ดเพื่อวางลงกระดาน, คลิกขวา = ดูรายละเอียด
/// การวางจริงอยู่ที่ TilePlacementManager
/// </summary>
public class CardUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Image iconImage;

    [Header("Selected Look")]
    [Tooltip("ลูกของการ์ดที่เก็บภาพทั้งหมด จะถูกยกขึ้นเมื่อเลือก (LayoutGroup คุมตำแหน่ง root จึงต้องยกลูกแทน)")]
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private float selectedRaise = 40f;
    [SerializeField] private float selectedScale = 1.12f;

    private CardData cardData;
    private CardDeckController deckController;
    private static TilePlacementManager PlacementManager => TilePlacementManager.Instance;

    public CardData CardData => cardData;

    public void Setup(CardData data, CardDeckController controller)
    {
        cardData = data;
        deckController = controller;

        if (cardData != null)
        {
            if (titleText != null) titleText.text = cardData.CardName;
            if (iconImage != null)
            {
                iconImage.sprite = cardData.Icon;
                iconImage.enabled = cardData.Icon != null;
            }
        }

        SetSelected(false);
    }

    /// <summary>เรียกโดย TilePlacementManager เพื่อเปลี่ยนหน้าตาการ์ดตอนถูกเลือก</summary>
    public void SetSelected(bool selected)
    {
        transform.localScale = selected ? Vector3.one * selectedScale : Vector3.one;

        if (visualRoot != null)
        {
            visualRoot.anchoredPosition = selected ? new Vector2(0f, selectedRaise) : Vector2.zero;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // หน้าต่างรายละเอียดเปิดอยู่ คลิกที่ไหนก็ปิด
        if (CardDetailPopup.Instance != null && CardDetailPopup.Instance.IsOpen)
        {
            CardDetailPopup.Instance.Hide();
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (CardDetailPopup.Instance != null && !CardDetailPopup.Instance.WasClosedThisFrame)
            {
                CardDetailPopup.Instance.Show(cardData);
            }
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left && PlacementManager != null)
        {
            PlacementManager.ToggleCard(this);
        }
    }

    private void OnDestroy()
    {
        // การ์ดถูกลบขณะถูกเลือกอยู่ (เช่น มือเต็มแล้วถูกดันทิ้ง) ต้องยกเลิกการเลือกด้วย
        if (PlacementManager != null && PlacementManager.SelectedCard == this)
        {
            PlacementManager.CancelSelection();
        }
    }
}
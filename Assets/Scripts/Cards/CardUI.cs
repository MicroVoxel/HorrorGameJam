using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// การ์ดบนมือ v2 (GDD 6.5):
/// - คลิกซ้าย = เลือก/ยกเลิกการ์ดเพื่อวางลงกระดาน
/// - คลิกขวา = ยกเลิกการเลือกการ์ด
/// - ชี้เมาส์ (Hover) = แสดงรายละเอียดการ์ดใน TooltipPanel (แทน CardDetailPopup เดิม)
/// </summary>
public class CardUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ITooltipSource
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
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (PlacementManager != null && PlacementManager.HasSelection)
            {
                PlacementManager.CancelSelection();
            }
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left && PlacementManager != null)
        {
            PlacementManager.ToggleCard(this);
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
        if (cardData == null)
        {
            data = default;
            return false;
        }

        TowerBase prefab = cardData.TowerPrefab;
        string subtitle = "Card";
        StringBuilder detail = new StringBuilder();
        IReadOnlyList<AbilityData> keywords = null;

        if (prefab != null)
        {
            subtitle = ThreatPlacementValidator.IsThreatPrefab(prefab)
                ? "Threat Tower Card"
                : (prefab.GetComponent<TowerObjective>() != null ? "Shrine Card" : "Spawner Card");

            detail.Append($"HP: {prefab.MaxHealth:0}   DEF: {prefab.BaseDefense:0}");
            if (prefab.RequireAdjacentPath)
            {
                detail.Append("\nPlacement: Adjacent to Path");
            }
            if (prefab.RotateModelWithFacing)
            {
                detail.Append($"\n{UITextProvider.Text.placeRotateHint}");
            }

            if (prefab.TryGetComponent(out AbilityHolder holder))
            {
                keywords = holder.All;
            }
        }

        data = TooltipData.Create(cardData.CardName, cardData.Description, detail.ToString(), subtitle, keywords);
        return true;
    }

    private void OnDestroy()
    {
        if (TooltipPanel.Instance != null)
        {
            TooltipPanel.Instance.Clear(this);
        }

        if (PlacementManager != null && PlacementManager.SelectedCard == this)
        {
            PlacementManager.CancelSelection();
        }
    }
}
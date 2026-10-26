using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ควบคุมการอัปเดตแถบกระเป๋าเป้ช่องเดี่ยว (Single-Slot Backpack Bar) ด้านล่างซ้ายของจอ
/// </summary>
[DisallowMultipleComponent]
public class InventoryUIController : MonoBehaviour
{
    [Header("Backpack Slot UI Elements (8 ช่องเรียงแนวนอน)")]
    [SerializeField] private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();

    private void Start()
    {
        if (InventorySlotManager.Instance != null)
        {
            InventorySlotManager.Instance.OnInventoryChanged += RefreshBackpackUI;
        }

        RefreshBackpackUI();
    }

    private void OnDestroy()
    {
        if (InventorySlotManager.Instance != null)
        {
            InventorySlotManager.Instance.OnInventoryChanged -= RefreshBackpackUI;
        }
    }

    public void RefreshBackpackUI()
    {
        if (InventorySlotManager.Instance == null) return;

        for (int i = 0; i < slotUIs.Count; i++)
        {
            if (slotUIs[i] == null) continue;

            OfferingItemData item = InventorySlotManager.Instance.GetItemAt(i);
            slotUIs[i].BindSlot(i, item);
        }
    }
}
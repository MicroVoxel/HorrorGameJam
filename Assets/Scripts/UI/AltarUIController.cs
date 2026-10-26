using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ควบคุมการแสดงผลหน้าต่างโต๊ะหมู่บูชา 5 หมวดหมู่ฝั่งซ้ายของจอ
/// </summary>
[DisallowMultipleComponent]
public class AltarUIController : MonoBehaviour
{
    [Header("Categorized Altar Slot UIs (5 ช่อง)")]
    [SerializeField] private List<AltarSlotUI> altarSlotUIs = new List<AltarSlotUI>();

    private void Start()
    {
        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.OnAltarStateChanged += RefreshAltarUI;
        }

        RefreshAltarUI();
    }

    private void OnDestroy()
    {
        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.OnAltarStateChanged -= RefreshAltarUI;
        }
    }

    public void RefreshAltarUI()
    {
        if (AltarManager.Instance == null) return;

        for (int i = 0; i < altarSlotUIs.Count; i++)
        {
            AltarSlotUI slotUI = altarSlotUIs[i];
            if (slotUI == null) continue;

            ActiveAltarSlotState state = AltarManager.Instance.GetSlotByCategory(slotUI.SlotCategory);
            slotUI.RefreshSlotVisual(state);
        }
    }
}
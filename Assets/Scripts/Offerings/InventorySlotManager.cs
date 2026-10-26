using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบกระเป๋าเก็บของช่องเดี่ยวสไตล์ Loop Hero (1 ช่อง = 1 ชิ้น ไม่ต้องหมุนจัดขนาด)
/// หากกระเป๋าเต็ม (ค่าเริ่มต้น 8 ช่อง) แล้วมีของดรอปเข้ามาใหม่ ของชิ้นเก่าสุดในช่องที่ 0 จะถูกดันสลายไปอัตโนมัติ
/// </summary>
[DisallowMultipleComponent]
public class InventorySlotManager : SingletonBehaviour<InventorySlotManager>
{

    [Header("Backpack Capacity (Loop Hero Single-Slot)")]
    [SerializeField] private int maxSlots = 8;
    [SerializeField] private List<OfferingItemData> items = new List<OfferingItemData>();

    [Header("Starting Test Items (Optional)")]
    [SerializeField] private List<OfferingItemData> initialTestItems = new List<OfferingItemData>();

    public IReadOnlyList<OfferingItemData> Items => items;
    public int MaxSlots => maxSlots;
    public int CurrentItemCount => items.Count;

    public event Action OnInventoryChanged;
    public event Action<OfferingItemData> OnItemOverflowDisintegrated;


    private void Start()
    {
        for (int i = 0; i < initialTestItems.Count; i++)
        {
            if (initialTestItems[i] != null)
            {
                AddItem(initialTestItems[i]);
            }
        }
    }

    /// <summary>
    /// เพิ่มไอเทมของถวายลงในกระเป๋าช่องเดี่ยว หากเกินความจุจะดันชิ้นซ้ายสุดออก
    /// </summary>
    public void AddItem(OfferingItemData newItem)
    {
        if (newItem == null) return;

        if (items.Count >= maxSlots && items.Count > 0)
        {
            OfferingItemData overflow = items[0];
            items.RemoveAt(0);
            Debug.Log($"<color=orange>[Backpack Overflow]</color> กระเป๋าเต็ม! '{overflow.ItemName}' ถูกดันตกกระดานและสลายเป็นไอวิญญาณ");
            OnItemOverflowDisintegrated?.Invoke(overflow);
        }

        items.Add(newItem);
        OnInventoryChanged?.Invoke();
    }

    public OfferingItemData GetItemAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return null;
        return items[slotIndex];
    }

    public bool RemoveItemAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return false;

        items.RemoveAt(slotIndex);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(OfferingItemData item)
    {
        if (item == null) return false;

        bool removed = items.Remove(item);
        if (removed)
        {
            OnInventoryChanged?.Invoke();
        }
        return removed;
    }

    /// <summary>
    /// ส่งไอเทมจากช่องกระเป๋าที่ระบุขึ้นไปวางบนโต๊ะหมู่บูชาหน้าศาลเจ้า (หากหมวดหมู่ตรงกัน)
    /// ถอดของออกจากกระเป๋า "ก่อน" แล้วค่อยวางบนแท่น เพราะแท่นอาจคืนของเก่าเข้ากระเป๋า
    /// (ถ้าเต็มจะดันช่องซ้ายสุดออก ทำให้ index เดิมเลื่อน) หากวางไม่สำเร็จจะใส่คืนที่ช่องเดิม
    /// </summary>
    public bool TryEquipSlotToAltar(int slotIndex)
    {
        OfferingItemData item = GetItemAt(slotIndex);
        if (item == null || AltarManager.Instance == null) return false;

        items.RemoveAt(slotIndex);

        bool placed = AltarManager.Instance.EquipOffering(item);
        if (!placed)
        {
            items.Insert(Mathf.Clamp(slotIndex, 0, items.Count), item);
        }

        OnInventoryChanged?.Invoke();
        return placed;
    }
}
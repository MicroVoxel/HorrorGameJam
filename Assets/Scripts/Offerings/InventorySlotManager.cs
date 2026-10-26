using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบกระเป๋าเก็บของช่องเดี่ยวสไตล์ Loop Hero (1 ช่อง = 1 ชิ้น ไม่ต้องหมุนจัดขนาด)
/// หากกระเป๋าเต็ม (ค่าเริ่มต้น 8 ช่อง) แล้วมีของดรอปเข้ามาใหม่ ของชิ้นเก่าสุดในช่องที่ 0 จะถูกดันสลายไปอัตโนมัติ
///
/// FIX บัก 7: กระเป๋าจำ "อายุที่เหลือ (Round)" ของแต่ละชิ้นไว้ด้วย (ลิสต์ขนานกับ items)
/// ของที่ถอดจากแท่นตอนยังสดจะกลับมาพร้อมอายุเดิม ไม่ได้รีเซ็ตเป็นเต็มอายุ
/// ของที่ได้ใหม่ (ดรอป / เริ่มเกม) ใช้อายุเต็มตาม DecayLoops
/// </summary>
[DisallowMultipleComponent]
public class InventorySlotManager : SingletonBehaviour<InventorySlotManager>
{

    [Header("Backpack Capacity (Loop Hero Single-Slot)")]
    [SerializeField] private int maxSlots = 8;
    [SerializeField] private List<OfferingItemData> items = new List<OfferingItemData>();

    [Header("Starting Test Items (Optional)")]
    [SerializeField] private List<OfferingItemData> initialTestItems = new List<OfferingItemData>();

    // FIX บัก 7: อายุที่เหลือของแต่ละชิ้น index ตรงกับ items (ไม่ Serialize ปรับให้ตรงกันอัตโนมัติด้วย EnsureRemainingSynced)
    private readonly List<int> remainingRounds = new List<int>();

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

    // FIX บัก 7: กันกรณีแก้ items ใน Inspector แล้วลิสต์อายุไม่ตรง (เติมด้วยอายุเต็ม / ตัดส่วนเกิน)
    private void EnsureRemainingSynced()
    {
        while (remainingRounds.Count < items.Count)
        {
            OfferingItemData item = items[remainingRounds.Count];
            remainingRounds.Add(item != null ? item.DecayLoops : 1);
        }

        while (remainingRounds.Count > items.Count)
        {
            remainingRounds.RemoveAt(remainingRounds.Count - 1);
        }
    }

    /// <summary>
    /// เพิ่มไอเทมของถวายลงในกระเป๋าช่องเดี่ยว หากเกินความจุจะดันชิ้นซ้ายสุดออก
    /// remaining: อายุที่เหลือ (Round) ถ้าไม่ระบุ (&lt;= 0) ใช้อายุเต็มของไอเทม
    /// </summary>
    public void AddItem(OfferingItemData newItem, int remaining = -1)
    {
        if (newItem == null) return;

        EnsureRemainingSynced();

        if (items.Count >= maxSlots && items.Count > 0)
        {
            OfferingItemData overflow = items[0];
            items.RemoveAt(0);
            remainingRounds.RemoveAt(0);
            Debug.Log($"<color=orange>[Backpack Overflow]</color> กระเป๋าเต็ม! '{overflow.ItemName}' ถูกดันตกกระดานและสลายเป็นไอวิญญาณ");
            OnItemOverflowDisintegrated?.Invoke(overflow);
        }

        int life = remaining > 0 ? Mathf.Min(remaining, newItem.DecayLoops) : newItem.DecayLoops;

        items.Add(newItem);
        remainingRounds.Add(life);
        OnInventoryChanged?.Invoke();
    }

    public OfferingItemData GetItemAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return null;
        return items[slotIndex];
    }

    /// <summary>อายุที่เหลือ (Round) ของชิ้นในช่องนี้ (ไม่มีของ = 0)</summary>
    public int GetRemainingAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return 0;

        EnsureRemainingSynced();
        return remainingRounds[slotIndex];
    }

    public bool RemoveItemAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return false;

        EnsureRemainingSynced();
        items.RemoveAt(slotIndex);
        remainingRounds.RemoveAt(slotIndex);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(OfferingItemData item)
    {
        if (item == null) return false;

        int index = items.IndexOf(item);
        if (index < 0) return false;

        return RemoveItemAt(index);
    }

    /// <summary>
    /// ส่งไอเทมจากช่องกระเป๋าที่ระบุขึ้นไปวางบนโต๊ะหมู่บูชาหน้าศาลเจ้า (หากหมวดหมู่ตรงกัน)
    /// ถอดของออกจากกระเป๋า "ก่อน" แล้วค่อยวางบนแท่น เพราะแท่นอาจคืนของเก่าเข้ากระเป๋า
    /// (ถ้าเต็มจะดันช่องซ้ายสุดออก ทำให้ index เดิมเลื่อน) หากวางไม่สำเร็จจะใส่คืนที่ช่องเดิม
    /// FIX บัก 7: ส่งอายุที่เหลือของชิ้นนี้ไปกับการวาง และใส่คืนพร้อมอายุเดิมถ้าวางไม่สำเร็จ
    /// </summary>
    public bool TryEquipSlotToAltar(int slotIndex)
    {
        OfferingItemData item = GetItemAt(slotIndex);
        if (item == null || AltarManager.Instance == null) return false;

        int remaining = GetRemainingAt(slotIndex);

        items.RemoveAt(slotIndex);
        remainingRounds.RemoveAt(slotIndex);

        bool placed = AltarManager.Instance.EquipOffering(item, remaining);
        if (!placed)
        {
            EnsureRemainingSynced();
            int at = Mathf.Clamp(slotIndex, 0, items.Count);
            items.Insert(at, item);
            remainingRounds.Insert(at, remaining);
        }

        OnInventoryChanged?.Invoke();
        return placed;
    }
}
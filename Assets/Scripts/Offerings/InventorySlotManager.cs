using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ระบบกระเป๋าเก็บของช่องเดี่ยวสไตล์ Loop Hero (1 ช่อง = 1 ชิ้น)
/// เก็บเป็น List<OfferingInstance> ลิสต์เดียว (รวมไอเทมและอายุที่เหลือไว้ด้วยกัน ไม่ใช้ลิสต์คู่ขนาน)
/// ความจุอ่านจาก GameRulesData.MaxBackpackSlots
/// </summary>
[DisallowMultipleComponent]
public class InventorySlotManager : SingletonBehaviour<InventorySlotManager>
{
    [Header("Backpack Capacity (Fallback when GameStateManager is absent)")]
    [SerializeField, Min(1)] private int fallbackMaxSlots = 8;
    [SerializeField] private List<OfferingInstance> entries = new List<OfferingInstance>();

    [Header("Starting Test Items (Optional)")]
    [SerializeField] private List<OfferingItemData> initialTestItems = new List<OfferingItemData>();

    public IReadOnlyList<OfferingInstance> Entries => entries;
    public int MaxSlots => GameStateManager.Instance != null
        ? GameStateManager.Instance.Rules.MaxBackpackSlots
        : Mathf.Max(1, fallbackMaxSlots);
    public int CurrentItemCount => entries.Count;

    public event Action OnInventoryChanged;
    public event Action<OfferingItemData> OnItemOverflowDisintegrated;

    private void Start()
    {
        EnsureEntriesInitialized();

        for (int i = 0; i < initialTestItems.Count; i++)
        {
            if (initialTestItems[i] != null)
            {
                AddItem(initialTestItems[i]);
            }
        }
    }

    private void EnsureEntriesInitialized()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            OfferingInstance inst = entries[i];
            inst.EnsureInitialized();
            entries[i] = inst;
        }
    }

    public void AddItem(OfferingInstance instance)
    {
        if (!instance.IsValid) return;
        AddItem(instance.data, instance.remainingDecayLoops);
    }

    /// <summary>
    /// เพิ่มไอเทมของถวายลงในกระเป๋าช่องเดี่ยว หากเกินความจุจะดันชิ้นซ้ายสุดออก
    /// remaining: อายุที่เหลือ (Round) ถ้าไม่ระบุ (&lt;= 0) ใช้อายุเต็มของไอเทม
    /// </summary>
    public void AddItem(OfferingItemData newItem, int remaining = -1)
    {
        if (newItem == null) return;

        int cap = MaxSlots;
        if (entries.Count >= cap && entries.Count > 0)
        {
            OfferingItemData overflow = entries[0].data;
            entries.RemoveAt(0);
            if (overflow != null)
            {
                Debug.Log($"<color=orange>[Backpack Overflow]</color> กระเป๋าเต็ม! '{overflow.ItemName}' ถูกดันตกกระดานและสลายเป็นไอวิญญาณ");
                OnItemOverflowDisintegrated?.Invoke(overflow);
            }
        }

        entries.Add(OfferingInstance.CreateFresh(newItem, remaining));
        OnInventoryChanged?.Invoke();
    }

    public OfferingInstance GetInstanceAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= entries.Count) return default;
        OfferingInstance inst = entries[slotIndex];
        inst.EnsureInitialized();
        return inst;
    }

    public OfferingItemData GetItemAt(int slotIndex)
    {
        return GetInstanceAt(slotIndex).data;
    }

    /// <summary>อายุที่เหลือ (Round) ของชิ้นในช่องนี้ (ไม่มีของ = 0)</summary>
    public int GetRemainingAt(int slotIndex)
    {
        return GetInstanceAt(slotIndex).remainingDecayLoops;
    }

    public bool RemoveItemAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= entries.Count) return false;

        entries.RemoveAt(slotIndex);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(OfferingItemData item)
    {
        if (item == null) return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].data == item)
            {
                return RemoveItemAt(i);
            }
        }

        return false;
    }

    /// <summary>
    /// ส่งไอเทมจากช่องกระเป๋าที่ระบุขึ้นไปวางบนโต๊ะหมู่บูชาหน้าศาลเจ้า
    /// ถอดออกจากกระเป๋าก่อนวาง และใส่คืนช่องเดิมพร้อมอายุเดิมหากวางไม่สำเร็จ
    /// </summary>
    public bool TryEquipSlotToAltar(int slotIndex)
    {
        OfferingInstance inst = GetInstanceAt(slotIndex);
        if (!inst.IsValid || AltarManager.Instance == null) return false;

        entries.RemoveAt(slotIndex);

        bool placed = AltarManager.Instance.EquipOffering(inst.data, inst.remainingDecayLoops);
        if (!placed)
        {
            int at = Mathf.Clamp(slotIndex, 0, entries.Count);
            entries.Insert(at, inst);
        }

        OnInventoryChanged?.Invoke();
        return placed;
    }
}
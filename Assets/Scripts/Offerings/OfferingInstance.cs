using System;
using UnityEngine;

/// <summary>
/// โครงสร้างข้อมูลกลางของของถวาย 1 ชิ้นพร้อมสถานะอายุการใช้งาน
/// ใช้ร่วมกันทั้งในกระเป๋า (InventorySlotManager), บนโต๊ะหมู่บูชา (ActiveAltarSlotState) และใน RoundSnapshot
/// </summary>
[Serializable]
public struct OfferingInstance
{
    public OfferingItemData data;
    public int remainingDecayLoops;
    public bool isRotten;
    public int rottenRoundsElapsed;

    public bool IsValid => data != null;

    public static OfferingInstance CreateFresh(OfferingItemData item, int remainingRounds = -1)
    {
        if (item == null) return default;

        int life = remainingRounds > 0
            ? Mathf.Min(remainingRounds, item.DecayLoops)
            : item.DecayLoops;

        return new OfferingInstance
        {
            data = item,
            remainingDecayLoops = life,
            isRotten = false,
            rottenRoundsElapsed = 0
        };
    }

    /// <summary>ซ่อมค่าอายุกรณีเพิ่มไอเทมผ่าน Inspector โดยที่ remainingDecayLoops ยังเป็น 0</summary>
    public void EnsureInitialized()
    {
        if (data != null && !isRotten && remainingDecayLoops <= 0)
        {
            remainingDecayLoops = data.DecayLoops;
        }
    }
}
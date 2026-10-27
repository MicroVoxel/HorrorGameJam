using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static Registry สำหรับเก็บรายการยูนิต (EntityBase) ทั้งหมดที่มีชีวิตอยู่ในฉาก
/// ผีที่ตายจะถูกถอดออกทันทีใน EntityBase.Die() ไม่รอ Destroy ท้ายเฟรม
/// </summary>
public static class EntityRegistry
{
    private static readonly List<EntityBase> activeEntities = new List<EntityBase>(64);

    public static IReadOnlyList<EntityBase> ActiveEntities => activeEntities;
    public static int ActiveCount => activeEntities.Count;

    /// <summary>ส่งเมื่อมีผีใหม่ลงทะเบียน (ผู้ฟังต้องถอด event เองตอน OnDestroy)</summary>
    public static event Action<EntityBase> OnEntityRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeEntities.Clear();
    }

    public static void Register(EntityBase entity)
    {
        if (entity != null && !activeEntities.Contains(entity))
        {
            activeEntities.Add(entity);
            OnEntityRegistered?.Invoke(entity);
        }
    }

    public static void Unregister(EntityBase entity)
    {
        if (entity != null)
        {
            activeEntities.Remove(entity);
        }
    }

    public static void ClearAll()
    {
        activeEntities.Clear();
    }
}
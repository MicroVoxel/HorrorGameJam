using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static Registry สำหรับเก็บรายการยูนิต (EntityBase) ทั้งหมดที่มีชีวิตอยู่ในฉากแบบ Real-time
/// ช่วยให้ป้อม (TowerAttackable) และระบบเช็กการสูญพันธุ์ (Extinction) ค้นหายูนิตได้ทันทีโดยไม่ต้องเรียก FindObjectsByType
/// </summary>
public static class EntityRegistry
{
    private static readonly List<EntityBase> activeEntities = new List<EntityBase>(64);

    public static IReadOnlyList<EntityBase> ActiveEntities => activeEntities;
    public static int ActiveCount => activeEntities.Count;

    public static void Register(EntityBase entity)
    {
        if (entity != null && !activeEntities.Contains(entity))
        {
            activeEntities.Add(entity);
        }
    }

    public static void Unregister(EntityBase entity)
    {
        if (entity != null)
        {
            activeEntities.Remove(entity);
        }
    }

    /// <summary>
    /// ค้นหายูนิตที่อยู่ใกล้ตำแหน่งที่กำหนดมากที่สุดภายในระยะยิง
    /// </summary>
    public static EntityBase GetClosestEntity(Vector3 fromPosition, float maxRange)
    {
        EntityBase closest = null;
        float maxRangeSqr = maxRange * maxRange;
        float minSqrDist = maxRangeSqr;

        for (int i = activeEntities.Count - 1; i >= 0; i--)
        {
            EntityBase candidate = activeEntities[i];
            if (candidate == null || candidate.IsDead)
            {
                continue;
            }

            float sqrDist = (candidate.Position - fromPosition).sqrMagnitude;
            if (sqrDist <= minSqrDist)
            {
                minSqrDist = sqrDist;
                closest = candidate;
            }
        }

        return closest;
    }

    public static void ClearAll()
    {
        activeEntities.Clear();
    }
}
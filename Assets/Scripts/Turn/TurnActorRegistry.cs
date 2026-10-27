using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ทะเบียนผู้ลงมือทั้งหมดที่ TurnManager ต้องเรียกในแต่ละเทิร์น (ผี, ป้อม, รังผี)
/// ทำงานคู่กับ EntityRegistry ได้ แต่ตัวนี้เก็บเฉพาะที่ implement ITurnActor
/// </summary>
public static class TurnActorRegistry
{
    private static readonly List<ITurnActor> actors = new List<ITurnActor>(128);

    public static int Count => actors.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        actors.Clear();
    }

    public static void Register(ITurnActor actor)
    {
        if (actor != null && !actors.Contains(actor))
        {
            actors.Add(actor);
        }
    }

    public static void Unregister(ITurnActor actor)
    {
        if (actor != null)
        {
            actors.Remove(actor);
        }
    }

    /// <summary>
    /// เก็บสำเนาของผู้ลงมือที่ยัง Active ในกลุ่มที่ระบุ เรียงตาม SortKey น้อยไปมาก
    /// ตัวที่ SortKey เท่ากันคงลำดับการลงทะเบียนเดิม (Stable) ใช้ Snapshot จึงไม่พังเมื่อมีตัวใหม่เกิดระหว่างเทิร์น
    /// </summary>
    public static void CollectSorted(TurnActorGroup group, List<ITurnActor> buffer)
    {
        buffer.Clear();

        for (int i = 0; i < actors.Count; i++)
        {
            ITurnActor actor = actors[i];
            if (!IsUsable(actor) || actor.Group != group) continue;

            int insertAt = buffer.Count;
            while (insertAt > 0 && buffer[insertAt - 1].SortKey > actor.SortKey)
            {
                insertAt--;
            }

            buffer.Insert(insertAt, actor);
        }
    }

    /// <summary>เก็บผู้ลงมือที่ยัง Active ทุกกลุ่ม (ไม่เรียงลำดับ ใช้กับ OnTurnStart / OnTurnEnd)</summary>
    public static void CollectAll(List<ITurnActor> buffer)
    {
        buffer.Clear();

        for (int i = 0; i < actors.Count; i++)
        {
            if (IsUsable(actors[i])) buffer.Add(actors[i]);
        }
    }

    public static bool AnyActive(TurnActorGroup group)
    {
        for (int i = 0; i < actors.Count; i++)
        {
            if (IsUsable(actors[i]) && actors[i].Group == group) return true;
        }

        return false;
    }

    /// <summary>
    /// นิยามกลางของทั้งเกมว่า "บนกระดานยังมีผีเดินอยู่ หรือยังมีรังผีที่เสกผีต่อได้หรือไม่"
    /// ใช้ร่วมกันระหว่าง TurnManager (เช็กจบ Resolution เร็ว) และ ParadeExtinctionChecker (เช็กแพ้สูญพันธุ์)
    /// </summary>
    public static bool HasActiveParadeOrSpawner()
    {
        return EntityRegistry.ActiveCount > 0 || AnyActive(TurnActorGroup.Spawner);
    }

    /// <summary>กันกรณี MonoBehaviour ถูก Destroy ไปแล้วแต่ยังค้างในทะเบียน</summary>
    public static bool IsUsable(ITurnActor actor)
    {
        if (actor == null) return false;
        if (actor is Object unityObject && unityObject == null) return false;
        return actor.IsActorActive;
    }
}
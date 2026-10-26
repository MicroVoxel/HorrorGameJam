using System.Collections;

/// <summary>กลุ่มของผู้ลงมือในเทิร์น เรียงขั้นตอน: Spawner -> (Quick) -> Entity -> Tower</summary>
public enum TurnActorGroup
{
    Spawner,
    Entity,
    Tower
}

/// <summary>
/// ทุกสิ่งที่ทำ action ในเฟส Resolution ต้อง implement ตัวนี้ (ผี, ป้อม, รังผี)
/// เลิกใช้ Update() ในการเดิน/ยิง แล้วรอ TurnManager เรียก ExecuteTurn แทน
/// ต้อง Register กับ TurnActorRegistry ตอน OnEnable และ Unregister ตอน OnDisable
/// </summary>
public interface ITurnActor
{
    TurnActorGroup Group { get; }

    /// <summary>ค่าน้อยทำก่อน ผี = หัวขบวนก่อน (เช่น คืนค่าติดลบของความคืบหน้าบนทางเดิน) ป้อม = ลำดับการวาง</summary>
    int SortKey { get; }

    /// <summary>ยังมีชีวิต/ยังตั้งอยู่บนกระดาน (ต้องป้องกัน this == null เองเมื่อถูก Destroy)</summary>
    bool IsActorActive { get; }

    void OnTurnStart(TurnContext ctx);

    /// <summary>
    /// ทำ action ของเทิร์นนี้ ตัดสินผลทาง Logic ก่อนแล้วค่อยเล่นภาพ
    /// เรียก ctx.MarkAction() เมื่อทำอะไรจริง (เพื่อให้เกิดจังหวะหน่วง) และใช้ ctx.BeginAnimation()/EndAnimation()
    /// กับภาพที่ปล่อยเล่นพร้อมกัน เช่น ผีทุกตัวกระโดดพร้อมกัน
    /// </summary>
    IEnumerator ExecuteTurn(TurnContext ctx);

    void OnTurnEnd(TurnContext ctx);
}

/// <summary>ผู้ที่มี Keyword Quick Attack: ได้ทำ action ใน Quick Step ก่อน Entity Step และ Tower Step</summary>
public interface IQuickActor : ITurnActor
{
    IEnumerator ExecuteQuickAction(TurnContext ctx);
}

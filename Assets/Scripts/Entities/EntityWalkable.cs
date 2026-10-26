using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ผีบนทางเดินลูปแบบเทิร์น (ITurnActor, กลุ่ม Entity) ไม่มี Update() อีกแล้ว
///
/// ใน 1 เทิร์น ผีมี AP (ค่าตั้งต้น 2) วนทำจนหมด AP:
///   - ถ้ามีศาลอยู่ใน Range และยังโจมตีได้ -> โจมตี (1 AP)
///   - ไม่เช่นนั้นเดินหน้า Move Speed ช่อง (1 AP)
///   - ช่องหน้าตัน = เสีย AP ที่เหลือเปล่า (IsBlocked)
/// ภาพ: ตรรกะตัดสินก่อน แล้วผีทุกตัวกระโดดพร้อมกัน (ctx.BeginAnimation) การโจมตีเล่นทีละตัว
/// ลำดับการทำงาน: หัวขบวนก่อน (SortKey) ตัวหลังจึงเดินตามได้ในเทิร์นเดียวกัน
///
/// 4B: Quick Attack (IQuickActor) โจมตีใน Quick Step ก่อน Entity Step
///     Phase: ข้ามช่องที่มีหน่วยยืนอยู่โดยไม่นับเป็นก้าว แล้วลงที่ช่องว่างช่องแรกข้างหน้า (ห้ามหยุดบนช่องที่มีหน่วย)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EntityBase))]
public class EntityWalkable : MonoBehaviour, IQuickActor
{
    [Header("Movement Visuals (display only)")]
    [SerializeField] private float hopDuration = 0.22f;
    [SerializeField] private float hopHeight = 0.35f;

    [Header("AI")]
    [Tooltip("On = once in range of a target, stop moving after the attack is used (siege). Off = attack then keep moving.")]
    [SerializeField] private bool holdPositionWhenInRange = false;

    [Header("Loop Tracking")]
    [SerializeField] private int loopCount = 0;
    [SerializeField] private int stepsTakenSinceLastLoop = 0;

    private EntityBase entityBase;
    private EntityAttackable attackable;
    private AbilityHolder abilities;
    private LoopPathManager pathManager;

    private int currentPathIndex = 0;
    private int pathProgress = 0;
    private bool isInitialized = false;

    private readonly List<Vector3> pendingHops = new List<Vector3>(4);
    private bool isHopping;
    private TurnContext hopContext;

    // ---------- Public info ----------
    public EntityBase Entity => entityBase;
    public int CurrentPathIndex => currentPathIndex;
    public int PathProgress => pathProgress;
    public int LoopCount => loopCount;
    public int StepsTakenSinceLastLoop => stepsTakenSinceLastLoop;
    public bool IsInitialized => isInitialized;

    public event Action<EntityWalkable, int> OnLoopCompleted;

    /// <summary>ผีขยับไม่ได้เพราะช่องหน้าตัน (UI โชว์ไอคอน Blocked)</summary>
    public event Action<EntityWalkable> OnMoveBlocked;

    // ---------- ITurnActor ----------
    public TurnActorGroup Group => TurnActorGroup.Entity;
    public bool IsActorActive => isInitialized && entityBase != null && !entityBase.IsDead;

    /// <summary>
    /// น้อย = ทำก่อน: จัดตามความลึกของแถวผีที่ยืนต่อกันอยู่ข้างหน้า (หัวขบวนลึก 0) แล้วตามความคืบหน้าบนทาง
    /// ตัวหน้าเดินก่อน ตัวหลังจึงเดินตามเข้าช่องที่เพิ่งว่างได้ในเทิร์นเดียวกัน
    /// </summary>
    public int SortKey => isInitialized ? ComputeSortKey() : int.MaxValue;

    private void Awake()
    {
        entityBase = GetComponent<EntityBase>();
        attackable = GetComponent<EntityAttackable>();
        abilities = GetComponent<AbilityHolder>();
    }

    private void OnEnable()
    {
        TurnActorRegistry.Register(this);
    }

    private void OnDisable()
    {
        TurnActorRegistry.Unregister(this);
        ReleaseHopAnimation();
    }

    private void Start()
    {
        if (!isInitialized)
        {
            LoopPathManager foundPath = LoopPathManager.Instance;

            if (foundPath != null && foundPath.PathLength > 0)
            {
                int closest = foundPath.GetClosestPathIndex(transform.position);
                InitializeOnPath(foundPath, closest);
            }
        }
    }

    /// <summary>วางยูนิตลงบนเส้นทางลูป (เรียกโดย TowerSpawnable)</summary>
    public void InitializeOnPath(LoopPathManager path, int startIndex = 0)
    {
        if (entityBase == null) entityBase = GetComponent<EntityBase>();
        pathManager = path;
        if (pathManager == null || pathManager.PathLength == 0) return;

        currentPathIndex = pathManager.WrapIndex(startIndex);
        pathProgress = currentPathIndex;
        HexTile startTile = pathManager.GetTile(currentPathIndex);

        if (startTile != null)
        {
            transform.position = startTile.transform.position;
            entityBase.SetOccupiedTile(startTile);
        }
        else
        {
            transform.position = pathManager.GetWaypoint(currentPathIndex);
        }

        stepsTakenSinceLastLoop = 0;
        loopCount = 0;
        isInitialized = true;

        FaceAlongPath();
    }

    // ---------- Turn flow ----------

    public void OnTurnStart(TurnContext ctx)
    {
        if (!IsActorActive) return;

        entityBase.BeginTurn();
        if (attackable != null) attackable.BeginTurn();
    }

    /// <summary>
    /// Quick Step: ผีที่มี Keyword Quick Attack โจมตี 1 ครั้งก่อนทุกตัว (ใช้ 1 AP และ 1 ครั้งของโควตาโจมตี)
    /// ไม่มีเป้าหมายใน Range = ไม่ทำอะไร ไม่เสีย AP แล้วค่อยทำตามปกติใน Entity Step
    /// </summary>
    public IEnumerator ExecuteQuickAction(TurnContext ctx)
    {
        if (!IsActorActive || attackable == null) yield break;
        if (abilities == null || !abilities.Has(AbilityKeyword.QuickAttack)) yield break;
        if (entityBase.CurrentActionPoints < 1 || !attackable.CanAttack) yield break;
        if (!attackable.TryAcquireTarget()) yield break;

        entityBase.TrySpendActionPoints(1);
        yield return attackable.PerformAttack(ctx);
    }

    public IEnumerator ExecuteTurn(TurnContext ctx)
    {
        if (!IsActorActive) yield break;

        pendingHops.Clear();

        while (IsActorActive && entityBase.CurrentActionPoints > 0)
        {
            bool hasTarget = attackable != null && attackable.TryAcquireTarget();

            if (hasTarget && attackable.CanAttack)
            {
                entityBase.TrySpendActionPoints(1);

                // ถ้าเดินมาก่อนโจมตี ให้ท่ากระโดดเล่นจบก่อนแล้วค่อยโจมตี
                FlushHops(ctx);
                while (isHopping && !ctx.IsFastForward) yield return null;

                yield return attackable.PerformAttack(ctx);
                continue;
            }

            if (hasTarget && holdPositionWhenInRange) break;

            entityBase.TrySpendActionPoints(1);
            if (!TryMoveForward(ctx))
            {
                entityBase.DrainActionPoints(true);
                OnMoveBlocked?.Invoke(this);
                break;
            }
        }

        FlushHops(ctx);
    }

    public void OnTurnEnd(TurnContext ctx) { }

    // ---------- Movement logic ----------

    private bool TryMoveForward(TurnContext ctx)
    {
        int length = pathManager.PathLength;
        if (length < 2) return false;

        bool canPhase = abilities != null && abilities.Has(AbilityKeyword.Phase);
        bool movedAny = false;
        int steps = entityBase.MoveSpeed;

        for (int s = 0; s < steps; s++)
        {
            int offset = FindLandingOffset(length, canPhase);
            if (offset <= 0) break;

            HexTile fromTile = entityBase.OccupiedTile;
            HexTile landTile = pathManager.GetTile(currentPathIndex + offset);

            currentPathIndex = pathManager.WrapIndex(currentPathIndex + offset);
            entityBase.SetOccupiedTile(landTile);

            if (fromTile != null)
            {
                entityBase.SetFacing(fromTile.Coordinates.FacingTo(landTile.Coordinates));
            }

            pendingHops.Add(landTile.transform.position);
            ctx.MarkAction();
            movedAny = true;

            AdvanceProgress(offset, length);
        }

        return movedAny;
    }

    /// <summary>
    /// ระยะ (ช่องบนทาง) ถึงช่องว่างช่องแรกข้างหน้า 0 = ไปไม่ได้
    /// ปกติดูแค่ช่องถัดไป ถ้ามี Phase ข้ามช่องที่มีหน่วยยืนอยู่ได้ (แต่ต้องลงบนช่องว่างเท่านั้น)
    /// </summary>
    private int FindLandingOffset(int length, bool canPhase)
    {
        int maxOffset = canPhase ? length - 1 : 1;

        for (int offset = 1; offset <= maxOffset; offset++)
        {
            HexTile tile = pathManager.GetTile(currentPathIndex + offset);
            if (tile == null) return 0;

            if (!tile.HasEntity || tile.CurrentEntity == entityBase) return offset;
            if (!canPhase) return 0;
        }

        return 0;
    }

    /// <summary>นับความคืบหน้าบนทางตามจำนวนช่องที่ผ่านจริง (รวมช่องที่ Phase ข้าม) เพื่อให้นับรอบลูปถูก</summary>
    private void AdvanceProgress(int tiles, int length)
    {
        for (int i = 0; i < tiles; i++)
        {
            pathProgress++;
            stepsTakenSinceLastLoop++;

            if (stepsTakenSinceLastLoop >= length)
            {
                CompleteCircuitLoop();
            }
        }
    }

    private void CompleteCircuitLoop()
    {
        stepsTakenSinceLastLoop = 0;
        loopCount++;
        OnLoopCompleted?.Invoke(this, loopCount);
    }

    private int ComputeSortKey()
    {
        int depth = 0;
        EntityWalkable cursor = this;
        int length = pathManager.PathLength;

        for (int guard = 0; guard < length; guard++)
        {
            HexTile next = pathManager.GetTile(cursor.currentPathIndex + 1);
            if (next == null || !next.HasEntity) break;

            if (!next.CurrentEntity.TryGetComponent(out EntityWalkable ahead) || ahead == this) break;

            depth++;
            cursor = ahead;
        }

        return depth * 100000 - Mathf.Clamp(pathProgress, 0, 99999);
    }

    private void FaceAlongPath()
    {
        HexTile current = pathManager.GetTile(currentPathIndex);
        HexTile next = pathManager.GetTile(currentPathIndex + 1);
        if (current == null || next == null || current == next) return;

        entityBase.SetFacing(current.Coordinates.FacingTo(next.Coordinates));
    }

    /// <summary>
    /// ช่องบนทางเดินที่ผีจะยืนเมื่อจบแต่ละเทิร์น (คำนวณจากการใช้ AP เดินเต็มบนทางโล่ง) ใช้กับ Planning Preview
    /// results[0] = เมื่อจบเทิร์น 1 ...
    /// </summary>
    public void GetProjectedPathIndices(int turns, List<int> results)
    {
        if (results == null) return;
        results.Clear();
        if (!isInitialized || pathManager == null) return;

        int perTurn = entityBase.MaxActionPoints * entityBase.MoveSpeed;
        for (int t = 1; t <= turns; t++)
        {
            results.Add(pathManager.WrapIndex(currentPathIndex + perTurn * t));
        }
    }

    /// <summary>
    /// ผลักถอยหลังบนทางเดิน (ของถวาย Sweet ที่ยังสด) หยุดเมื่อช่องหลังมีหน่วยยืนอยู่ คืน true ถ้าขยับได้อย่างน้อย 1 ช่อง
    /// ความคืบหน้ารอบลูปถอยตามจริง จึงไม่ได้ผลเสียต่อการนับรอบ
    /// </summary>
    public bool TryPushBack(int tiles)
    {
        if (!IsActorActive || pathManager == null || pathManager.PathLength < 2) return false;

        bool moved = false;
        int count = Mathf.Max(1, tiles);

        for (int i = 0; i < count; i++)
        {
            HexTile back = pathManager.GetTile(currentPathIndex - 1);
            if (back == null || (back.HasEntity && back.CurrentEntity != entityBase)) break;

            currentPathIndex = pathManager.WrapIndex(currentPathIndex - 1);
            pathProgress = Mathf.Max(0, pathProgress - 1);
            stepsTakenSinceLastLoop = Mathf.Max(0, stepsTakenSinceLastLoop - 1);
            entityBase.SetOccupiedTile(back);
            moved = true;
        }

        if (moved)
        {
            SnapBackToCurrentTile();
            FaceAlongPath();
        }

        return moved;
    }

    // ---------- Visual hop ----------

    private void FlushHops(TurnContext ctx)
    {
        if (pendingHops.Count == 0) return;

        if (ctx.IsFastForward)
        {
            transform.position = pendingHops[pendingHops.Count - 1];
            pendingHops.Clear();
            return;
        }

        Vector3[] points = pendingHops.ToArray();
        pendingHops.Clear();
        StartCoroutine(HopRoutine(ctx, points));
    }

    private IEnumerator HopRoutine(TurnContext ctx, Vector3[] points)
    {
        isHopping = true;
        hopContext = ctx;
        ctx.BeginAnimation();

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 start = transform.position;
            Vector3 end = points[i];
            float duration = ctx.ScaleDuration(hopDuration);
            float elapsed = 0f;

            while (duration > 0f && elapsed < duration && !ctx.IsFastForward)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 pos = Vector3.Lerp(start, end, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * hopHeight;
                transform.position = pos;
                yield return null;
            }

            transform.position = end;
        }

        isHopping = false;
        hopContext = null;
        ctx.EndAnimation();
    }

    /// <summary>ถ้าถูกปิด/ทำลายกลางท่ากระโดด ต้องคืนตัวนับแอนิเมชัน ไม่งั้น TurnManager จะรอจนหมดเวลา</summary>
    private void ReleaseHopAnimation()
    {
        if (isHopping && hopContext != null)
        {
            hopContext.EndAnimation();
        }

        isHopping = false;
        hopContext = null;
    }

    // ---------- Planning: formation swap ----------

    /// <summary>
    /// สลับตำแหน่งช่องยืนของผี 2 ตัว (เรียกโดย FormationSwapManager ในช่วง Planning)
    /// จำนวนก้าวสะสมของแต่ละตัวไม่เปลี่ยน การสลับจึงไม่ให้โปรเกรสรอบลูปฟรี
    /// </summary>
    public void SwapPositionWith(EntityWalkable other)
    {
        if (other == null || other == this || pathManager == null) return;

        int myPathIndex = currentPathIndex;
        int otherPathIndex = other.currentPathIndex;
        int myProgress = pathProgress;

        HexTile myTile = pathManager.GetTile(myPathIndex);
        HexTile otherTile = pathManager.GetTile(otherPathIndex);

        if (myTile != null) myTile.ClearEntity(entityBase);
        if (otherTile != null) otherTile.ClearEntity(other.entityBase);

        currentPathIndex = otherPathIndex;
        pathProgress = other.pathProgress;
        entityBase.SetOccupiedTile(otherTile);
        SnapBackToCurrentTile();
        FaceAlongPath();

        other.currentPathIndex = myPathIndex;
        other.pathProgress = myProgress;
        other.entityBase.SetOccupiedTile(myTile);
        other.SnapBackToCurrentTile();
        other.FaceAlongPath();
    }

    /// <summary>ดีดตัวหมากกลับลงตำแหน่งช่อง Hex ปัจจุบัน (เมื่อปล่อยเมาส์หรือสลับเสร็จ)</summary>
    public void SnapBackToCurrentTile()
    {
        if (entityBase != null && entityBase.OccupiedTile != null)
        {
            transform.position = entityBase.OccupiedTile.transform.position;
        }
        else if (pathManager != null && pathManager.PathLength > 0)
        {
            transform.position = pathManager.GetWaypoint(currentPathIndex);
        }
    }
}
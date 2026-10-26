using System;
using System.Collections.Generic;
using UnityEngine;

public enum HexPatternType
{
    Ring,       // ทุกช่องห่างไม่เกิน N (ไม่ขึ้นกับทิศ)
    Line,       // N ช่องเป็นเส้นตรงไปด้านหน้า
    Fan,        // ลิ่ม 120 องศาด้านหน้า กว้างขึ้นตามระยะ
    Custom,     // รายการ offset ที่ทิศหน้า 0 หมุนตาม Facing
    King,       // 6 ช่องรอบตัว (King+ = เพิ่ม 6 ช่องมุมห่าง 2 ช่อง)
    Rook,       // 6 เส้นตรงตามขอบ ยาวเส้นละ N
    Bishop,     // 6 เส้นตรงตามมุม ยาวเส้นละ N ก้าว
    Queen,      // Rook + Bishop
    Knight,     // 12 ช่องรูปตัว L ห่าง 3 ช่อง ข้ามหน่วยเสมอ
    Pawn,       // 3 ช่องด้านหน้า (= Fan 1)
    Compound    // รวมหลาย Pattern (Union)
}

/// <summary>
/// ตัวช่วยบอก Pattern ว่าช่องไหนอยู่บนกระดาน และช่องไหนมีหน่วยยืนขวาง
/// ForBoard() สร้างจาก LoopPathManager ส่วน sizeDelta / ignoreBlocking ใช้รับผลของถวาย
/// </summary>
public struct HexPatternContext
{
    /// <summary>null = ถือว่าทุกช่องอยู่บนกระดาน</summary>
    public Func<HexCoordinates, bool> isOnBoard;

    /// <summary>ช่องนี้มีหน่วยยืนขวางหรือไม่ (ใช้เมื่อ Pattern เปิด blockedByUnits)</summary>
    public Func<HexCoordinates, bool> isBlocking;

    /// <summary>บวกลบขนาด N ของทุก Pattern (เช่น -1 จาก Fragrance ที่เน่า) N ต่ำสุดคือ 1</summary>
    public int sizeDelta;

    /// <summary>true = ข้ามการบล็อกของหน่วย (เช่น Fortune Candle ที่ยังสด)</summary>
    public bool ignoreBlocking;

    public static HexPatternContext ForBoard(LoopPathManager board, int sizeDelta = 0, bool ignoreBlocking = false)
    {
        return new HexPatternContext
        {
            isOnBoard = c => board != null && board.TryGetTileAt(c, out _),
            isBlocking = c => board != null && board.TryGetTileAt(c, out HexTile t) && (t.HasEntity || t.HasTower),
            sizeDelta = sizeDelta,
            ignoreBlocking = ignoreBlocking
        };
    }
}

/// <summary>
/// ชุดช่อง Hex ที่ผูกกับทิศหน้า (Facing 0-5) ใช้เป็นได้ทั้ง Range Pattern และ Hit Pattern
/// ลากใส่ Prefab ผ่าน AttackPatternData ผลคือรายการพิกัดสัมบูรณ์ (ช่องนอกกระดานถูกข้าม ช่องซ้ำนับครั้งเดียว)
/// </summary>
[CreateAssetMenu(fileName = "NewRangePattern", menuName = "GameJam/Hex Range Pattern")]
public class HexRangePattern : ScriptableObject
{
    private const int MaxCompoundDepth = 4;

    private static readonly HexCoordinates[] KnightOffsets = BuildKnightOffsets();

    [Header("Shape")]
    [SerializeField] private HexPatternType type = HexPatternType.Ring;
    [Tooltip("N: Ring/Line/Fan = ระยะ, Rook/Bishop/Queen = ความยาวต่อแฉก King/Knight/Pawn ไม่ใช้")]
    [SerializeField, Min(1)] private int size = 1;
    [Tooltip("รวมช่องของผู้ถือ Pattern เองด้วย")]
    [SerializeField] private bool includeCenter = false;

    [Header("King")]
    [Tooltip("King+: เพิ่ม 6 ช่องมุมที่ห่าง 2 ช่อง รวม 12 ช่อง")]
    [SerializeField] private bool kingPlus = false;

    [Header("Blocking")]
    [Tooltip("เส้นแบบ Line/Rook/Bishop/Queen หยุดที่ช่องแรกที่มีหน่วยยืนอยู่ (ช่องนั้นยังนับ) Knight ไม่ถูกบล็อกเสมอ")]
    [SerializeField] private bool blockedByUnits = false;

    [Header("Custom (type = Custom)")]
    [Tooltip("Axial offset (x = q, y = r) ที่ทิศหน้า 0 ระบบหมุนตาม Facing ให้เอง")]
    [SerializeField] private List<Vector2Int> customOffsets = new List<Vector2Int>();

    [Header("Compound (type = Compound)")]
    [SerializeField] private List<HexRangePattern> compoundParts = new List<HexRangePattern>();

    public HexPatternType Type => type;
    public int Size => Mathf.Max(1, size);
    public bool IncludeCenter => includeCenter;
    public bool BlockedByUnits => blockedByUnits;

    /// <summary>
    /// รวบรวมช่องของ Pattern นี้เมื่อผู้ถือยืนที่ origin และหัน facing (ล้าง results ก่อน)
    /// </summary>
    public void CollectTiles(HexCoordinates origin, int facing, List<HexCoordinates> results, in HexPatternContext ctx)
    {
        if (results == null) return;

        results.Clear();
        AppendTiles(origin, facing, results, ctx, 0);
    }

    public void CollectTiles(HexCoordinates origin, int facing, List<HexCoordinates> results)
    {
        CollectTiles(origin, facing, results, default);
    }

    private void AppendTiles(HexCoordinates origin, int facing, List<HexCoordinates> results, in HexPatternContext ctx, int depth)
    {
        if (depth > MaxCompoundDepth) return;

        int f = HexCoordinates.NormalizeFacing(facing);
        int n = Mathf.Max(1, size + ctx.sizeDelta);
        bool blocking = blockedByUnits && !ctx.ignoreBlocking && ctx.isBlocking != null;

        if (includeCenter) AddCell(origin, results, ctx);

        switch (type)
        {
            case HexPatternType.Ring:
                AddDisc(origin, n, results, ctx);
                break;

            case HexPatternType.Line:
                AddRay(origin, HexCoordinates.Directions[f], n, blocking, results, ctx);
                break;

            case HexPatternType.Fan:
                AddFan(origin, f, n, results, ctx);
                break;

            case HexPatternType.Custom:
                for (int i = 0; i < customOffsets.Count; i++)
                {
                    HexCoordinates offset = new HexCoordinates(customOffsets[i].x, customOffsets[i].y).Rotate(f);
                    AddCell(origin + offset, results, ctx);
                }
                break;

            case HexPatternType.King:
                for (int d = 0; d < 6; d++) AddCell(origin + HexCoordinates.Directions[d], results, ctx);
                if (kingPlus)
                {
                    for (int d = 0; d < 6; d++) AddCell(origin + HexCoordinates.DiagonalDirections[d], results, ctx);
                }
                break;

            case HexPatternType.Rook:
                AddStar(origin, HexCoordinates.Directions, n, blocking, results, ctx);
                break;

            case HexPatternType.Bishop:
                AddStar(origin, HexCoordinates.DiagonalDirections, n, blocking, results, ctx);
                break;

            case HexPatternType.Queen:
                AddStar(origin, HexCoordinates.Directions, n, blocking, results, ctx);
                AddStar(origin, HexCoordinates.DiagonalDirections, n, blocking, results, ctx);
                break;

            case HexPatternType.Knight:
                for (int i = 0; i < KnightOffsets.Length; i++) AddCell(origin + KnightOffsets[i], results, ctx);
                break;

            case HexPatternType.Pawn:
                AddFan(origin, f, 1, results, ctx);
                break;

            case HexPatternType.Compound:
                for (int i = 0; i < compoundParts.Count; i++)
                {
                    if (compoundParts[i] != null && compoundParts[i] != this)
                    {
                        compoundParts[i].AppendTiles(origin, f, results, ctx, depth + 1);
                    }
                }
                break;
        }
    }

    // ---------- Shape builders ----------

    private static void AddCell(HexCoordinates cell, List<HexCoordinates> results, in HexPatternContext ctx)
    {
        if (ctx.isOnBoard != null && !ctx.isOnBoard(cell)) return;
        if (!results.Contains(cell)) results.Add(cell);
    }

    private static void AddDisc(HexCoordinates origin, int radius, List<HexCoordinates> results, in HexPatternContext ctx)
    {
        for (int dq = -radius; dq <= radius; dq++)
        {
            int rMin = Mathf.Max(-radius, -dq - radius);
            int rMax = Mathf.Min(radius, -dq + radius);

            for (int dr = rMin; dr <= rMax; dr++)
            {
                if (dq == 0 && dr == 0) continue;
                AddCell(origin + new HexCoordinates(dq, dr), results, ctx);
            }
        }
    }

    private static void AddRay(HexCoordinates origin, HexCoordinates step, int length, bool blocking, List<HexCoordinates> results, in HexPatternContext ctx)
    {
        HexCoordinates current = origin;

        for (int i = 0; i < length; i++)
        {
            current = current + step;
            if (ctx.isOnBoard != null && !ctx.isOnBoard(current)) break;

            AddCell(current, results, ctx);
            if (blocking && ctx.isBlocking(current)) break;
        }
    }

    private static void AddStar(HexCoordinates origin, HexCoordinates[] steps, int length, bool blocking, List<HexCoordinates> results, in HexPatternContext ctx)
    {
        for (int i = 0; i < steps.Length; i++)
        {
            AddRay(origin, steps[i], length, blocking, results, ctx);
        }
    }

    /// <summary>
    /// ลิ่ม 120 องศาด้านหน้า: ที่ระยะ d มี 2d+1 ช่อง เดินจากมุม (f-1) ไปมุม f แล้วไปมุม (f+1)
    /// Fan 1 = 3 ช่อง (เท่า Pawn) Fan 2 = 8 ช่อง
    /// </summary>
    private static void AddFan(HexCoordinates origin, int facing, int radius, List<HexCoordinates> results, in HexPatternContext ctx)
    {
        HexCoordinates cornerLeft = HexCoordinates.Directions[HexCoordinates.NormalizeFacing(facing - 1)];
        HexCoordinates cornerCenter = HexCoordinates.Directions[facing];
        HexCoordinates stepFirst = HexCoordinates.Directions[HexCoordinates.NormalizeFacing(facing + 1)];
        HexCoordinates stepSecond = HexCoordinates.Directions[HexCoordinates.NormalizeFacing(facing + 2)];

        for (int d = 1; d <= radius; d++)
        {
            for (int j = 0; j <= 2 * d; j++)
            {
                HexCoordinates offset = j <= d
                    ? cornerLeft * d + stepFirst * j
                    : cornerCenter * d + stepSecond * (j - d);

                AddCell(origin + offset, results, ctx);
            }
        }
    }

    private static HexCoordinates[] BuildKnightOffsets()
    {
        // เรียงสลับค่า (3, -2, -1) และ (-3, 2, 1) ใน cube coordinates ได้ 12 ช่อง (q = x, r = z)
        int[][] bases = { new[] { 3, -2, -1 }, new[] { -3, 2, 1 } };
        int[][] perms =
        {
            new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
            new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
        };

        List<HexCoordinates> list = new List<HexCoordinates>(12);
        for (int b = 0; b < bases.Length; b++)
        {
            for (int p = 0; p < perms.Length; p++)
            {
                int x = bases[b][perms[p][0]];
                int z = bases[b][perms[p][2]];
                list.Add(new HexCoordinates(x, z));
            }
        }

        return list.ToArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        size = Mathf.Max(1, size);

        if (type == HexPatternType.Compound && compoundParts.Contains(this))
        {
            Debug.LogWarning("[HexRangePattern] Compound pattern contains itself. It will be ignored.", this);
        }
    }
#endif
}

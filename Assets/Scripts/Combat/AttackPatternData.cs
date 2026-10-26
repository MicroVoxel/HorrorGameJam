using System.Collections.Generic;
using UnityEngine;

public enum HitMode
{
    Single,         // โดนเป้าหมายเดียวที่เลือก
    AllInRange,     // โดนทุกช่องใน Range Pattern
    AreaAtTarget,   // วาง Area Pattern ที่ช่องเป้าหมาย
    Pierce          // เส้นตรงจากผู้โจมตีผ่านเป้า
}

public enum AreaOrientation
{
    AttackerFacing,     // ทิศ Area ตามทิศหน้าผู้โจมตี
    AttackerToTarget    // ทิศ Area ตามทิศจากผู้โจมตีไปหาเป้า
}

/// <summary>
/// รูปแบบการโจมตี = Range Pattern (ช่องที่เลือกเป้าหมายได้) + Hit Pattern (ช่องที่โดนดาเมจจริง)
/// ลากใส่ Prefab ของผี/ป้อม ผู้เรียก (Part 4) ส่งตำแหน่ง ทิศหน้า และ HexPatternContext เข้ามา
/// </summary>
[CreateAssetMenu(fileName = "NewAttackPattern", menuName = "GameJam/Attack Pattern Data")]
public class AttackPatternData : ScriptableObject
{
    [Header("Range (where targets can be chosen)")]
    [SerializeField] private HexRangePattern rangePattern;

    [Header("Hit (which tiles take damage)")]
    [SerializeField] private HitMode hitMode = HitMode.Single;

    [Tooltip("Area At Target: pattern placed on the target tile. The target tile is always hit.")]
    [SerializeField] private HexRangePattern areaPattern;
    [SerializeField] private AreaOrientation areaOrientation = AreaOrientation.AttackerFacing;

    [Tooltip("Pierce: minimum length of the line. It always extends at least to the target.")]
    [SerializeField, Min(1)] private int pierceLength = 4;

    [Header("Damage Shaping")]
    [Tooltip("Damage lost per tile away from the target (Area At Target and Pierce only). 0 = no falloff.")]
    [SerializeField, Range(0f, 100f)] private float falloffPercent = 0f;
    [Tooltip("Off = spirits do not hit spirits and shrines do not hit shrines.")]
    [SerializeField] private bool friendlyFire = false;

    public HexRangePattern RangePattern => rangePattern;
    public HitMode HitMode => hitMode;
    public HexRangePattern AreaPattern => areaPattern;
    public AreaOrientation AreaOrientation => areaOrientation;
    public float FalloffPercent => falloffPercent;
    public bool FriendlyFire => friendlyFire;

    /// <summary>ช่องที่เลือกเป้าหมายได้ (Range Pattern)</summary>
    public void CollectRangeTiles(HexCoordinates attacker, int facing, List<HexCoordinates> results, in HexPatternContext ctx)
    {
        if (results == null) return;

        if (rangePattern == null)
        {
            results.Clear();
            return;
        }

        rangePattern.CollectTiles(attacker, facing, results, ctx);
    }

    /// <summary>
    /// ช่องที่โดนจริงเมื่อเลือก target ผลเรียงจากใกล้ผู้โจมตีไปไกล (เสมอกันเรียงตาม q แล้ว r)
    /// เพื่อให้ลำดับการตายก่อนหลังคงที่ (Deterministic) ช่อง target ถูกรวมเสมอ
    /// rangeTiles ใส่ผลของ CollectRangeTiles ที่คำนวณไว้แล้วเพื่อประหยัดงาน (ไม่ใส่ = คำนวณเอง)
    /// </summary>
    public void CollectHitTiles(
        HexCoordinates attacker,
        int facing,
        HexCoordinates target,
        IReadOnlyList<HexCoordinates> rangeTiles,
        List<HexCoordinates> results,
        in HexPatternContext ctx)
    {
        if (results == null) return;
        results.Clear();

        switch (hitMode)
        {
            case HitMode.AllInRange:
                if (rangeTiles != null)
                {
                    for (int i = 0; i < rangeTiles.Count; i++) results.Add(rangeTiles[i]);
                }
                else if (rangePattern != null)
                {
                    rangePattern.CollectTiles(attacker, facing, results, ctx);
                }
                break;

            case HitMode.AreaAtTarget:
                if (areaPattern != null)
                {
                    int areaFacing = areaOrientation == AreaOrientation.AttackerFacing
                        ? facing
                        : attacker.FacingTo(target);

                    areaPattern.CollectTiles(target, areaFacing, results, ctx);
                }
                break;

            case HitMode.Pierce:
                CollectPierceLine(attacker, target, results, ctx);
                break;
        }

        if (!results.Contains(target)) results.Add(target);

        results.Sort((a, b) =>
        {
            int da = attacker.DistanceTo(a);
            int db = attacker.DistanceTo(b);
            if (da != db) return da.CompareTo(db);
            if (a.Q != b.Q) return a.Q.CompareTo(b.Q);
            return a.R.CompareTo(b.R);
        });
    }

    /// <summary>
    /// ตัวคูณดาเมจของช่อง cell เมื่อศูนย์กลางการโจมตีคือ target (1 = เต็ม) ใช้กับ Area At Target และ Pierce เท่านั้น
    /// </summary>
    public float GetDamageMultiplier(HexCoordinates target, HexCoordinates cell)
    {
        if (falloffPercent <= 0f) return 1f;
        if (hitMode != HitMode.AreaAtTarget && hitMode != HitMode.Pierce) return 1f;

        int distance = target.DistanceTo(cell);
        return Mathf.Max(0f, 1f - (falloffPercent / 100f) * distance);
    }

    private void CollectPierceLine(HexCoordinates attacker, HexCoordinates target, List<HexCoordinates> results, in HexPatternContext ctx)
    {
        int dir = HexCoordinates.NearestDirection(target - attacker);
        HexCoordinates step = HexCoordinates.Directions[dir];
        int length = Mathf.Max(pierceLength, attacker.DistanceTo(target));

        HexCoordinates current = attacker;
        for (int i = 0; i < length; i++)
        {
            current = current + step;
            if (ctx.isOnBoard != null && !ctx.isOnBoard(current)) break;

            results.Add(current);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>เหตุผลที่วางไม่ได้ (ใช้แสดงใน Tooltip ช่อง Hex และข้อความแจ้งเตือน)</summary>
public enum PlacementDenyReason
{
    None,
    WrongPhase,
    Occupied,
    GroundType,
    NotAdjacentPath,
    AdjacentShrine,
    NoThreatPath
}

/// <summary>
/// จุดเดียวที่ตรวจกฎการวางสิ่งก่อสร้างทั้งหมด:
/// - CheckTileRules: กฎระดับช่อง (ว่าง, ประเภทพื้น, ติดทางเดิน, ห้ามวางศาลติดกัน) ใช้ทั้งตอนไฮไลต์ช่องและตอนวางจริง
/// - Check: กฎเต็มรูปแบบสำหรับผู้เล่นวางการ์ด (รวมตรวจเฟสและ Valid Threat Check ตามทิศ Facing)
/// </summary>
public static class ThreatPlacementValidator
{
    private static readonly List<HexCoordinates> scratch = new List<HexCoordinates>(32);
    private static readonly List<int> bestFacings = new List<int>(6);

    /// <summary>Prefab นี้เป็นป้อมโจมตี (มี TowerAttackable + Range Pattern และไม่ใช่ศาลเป้าหมาย)</summary>
    public static bool IsThreatPrefab(TowerBase prefab)
    {
        if (prefab == null) return false;
        if (prefab.GetComponent<TowerObjective>() != null) return false;

        return prefab.TryGetComponent(out TowerAttackable attacker)
               && attacker.Pattern != null
               && attacker.Pattern.RangePattern != null;
    }

    /// <summary>ป้อมที่ตั้งอยู่นี้นับเป็น Threat Tower (ผู้เล่นวางเอง และเป็นป้อมโจมตี) ใช้คิดโบนัสตอน RoundEnd</summary>
    public static bool IsThreatTower(TowerBase tower)
    {
        return tower != null && tower.IsPlayerPlaced && IsThreatPrefab(tower);
    }

    /// <summary>จำนวนช่อง Path ที่อยู่ใน Range Pattern เมื่อป้อมยืนที่ origin และหัน facing</summary>
    public static int CountPathTilesInRange(TowerBase prefab, HexCoordinates origin, int facing, LoopPathManager board)
    {
        if (!IsThreatPrefab(prefab) || board == null) return 0;

        prefab.TryGetComponent(out TowerAttackable attacker);

        HexPatternContext pctx = BoardPatternContext.ForTower();
        attacker.Pattern.CollectRangeTiles(origin, facing, scratch, pctx);

        int count = 0;
        for (int i = 0; i < scratch.Count; i++)
        {
            if (board.TryGetTileAt(scratch[i], out HexTile tile) && tile.GroundType == HexGroundType.Path)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// ตรวจกฎระดับช่องล้วนๆ (ไม่ขึ้นกับเฟสและทิศการหมุน)
    /// TowerBase.CanPlaceOnTile เรียกตัวนี้เพื่อให้ชุดกฎอยู่ที่เดียว
    /// </summary>
    public static PlacementDenyReason CheckTileRules(TowerBase prefab, HexTile tile, LoopPathManager board)
    {
        if (prefab == null || tile == null) return PlacementDenyReason.Occupied;
        if (tile.HasTower) return PlacementDenyReason.Occupied;
        if (!prefab.CanPlaceOn(tile.GroundType)) return PlacementDenyReason.GroundType;

        if (board != null)
        {
            if (prefab.RequireAdjacentPath && !board.IsAdjacentToPath(tile.Coordinates))
            {
                return PlacementDenyReason.NotAdjacentPath;
            }

            if (prefab.TryGetComponent(out TowerObjective objective)
                && objective.PreventAdjacentDefenseBases
                && board.HasAdjacentDefenseBase(tile.Coordinates))
            {
                return PlacementDenyReason.AdjacentShrine;
            }
        }

        return PlacementDenyReason.None;
    }

    /// <summary>ตรวจทุกกฎการวางของการ์ดผู้เล่น คืน None ถ้าวางได้</summary>
    public static PlacementDenyReason Check(TowerBase prefab, HexTile tile, int facing, LoopPathManager board)
    {
        if (!GameStateManager.IsPlayerActionPhase) return PlacementDenyReason.WrongPhase;

        PlacementDenyReason tileReason = CheckTileRules(prefab, tile, board);
        if (tileReason != PlacementDenyReason.None) return tileReason;

        if (board != null && IsThreatPrefab(prefab) && CountPathTilesInRange(prefab, tile.Coordinates, facing, board) < GetMinPathTiles())
        {
            return PlacementDenyReason.NoThreatPath;
        }

        return PlacementDenyReason.None;
    }

    /// <summary>ทิศที่มีช่อง Path อยู่ใน Range มากที่สุด (เสมอกันสุ่มด้วย GameRandom) ไม่ใช่ป้อมโจมตี = 0</summary>
    public static int FindBestFacing(TowerBase prefab, HexTile tile)
    {
        LoopPathManager board = LoopPathManager.Instance;
        if (tile == null || board == null || !IsThreatPrefab(prefab)) return 0;

        int bestCount = -1;
        bestFacings.Clear();

        for (int f = 0; f < 6; f++)
        {
            int count = CountPathTilesInRange(prefab, tile.Coordinates, f, board);

            if (count > bestCount)
            {
                bestCount = count;
                bestFacings.Clear();
                bestFacings.Add(f);
            }
            else if (count == bestCount)
            {
                bestFacings.Add(f);
            }
        }

        return bestFacings.Count == 1 ? bestFacings[0] : GameRandom.Pick(bestFacings);
    }

    /// <summary>ข้อความเหตุผลจาก UITextData (แก้ใน Inspector) ว่างถ้า None</summary>
    public static string GetMessage(PlacementDenyReason reason)
    {
        UITextData t = UITextProvider.Text;

        switch (reason)
        {
            case PlacementDenyReason.WrongPhase: return t.cannotPlaceWrongPhase;
            case PlacementDenyReason.Occupied: return t.cannotPlaceOccupied;
            case PlacementDenyReason.GroundType: return t.cannotPlaceGroundType;
            case PlacementDenyReason.NotAdjacentPath: return t.cannotPlaceNotAdjacent;
            case PlacementDenyReason.AdjacentShrine: return t.cannotPlaceAdjacentShrine;
            case PlacementDenyReason.NoThreatPath: return UITextData.Format(t.cannotPlaceNoThreatPath, "count", GetMinPathTiles());
            default: return string.Empty;
        }
    }

    private static int GetMinPathTiles()
    {
        return GameStateManager.Instance != null ? GameStateManager.Instance.Rules.MinThreatPathTiles : 2;
    }
}
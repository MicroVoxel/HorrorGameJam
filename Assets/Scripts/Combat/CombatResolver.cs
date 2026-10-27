using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ศูนย์กลางการคำนวณการโจมตีและพรีวิวระยะของทั้งผี (EntityAttackable) และป้อม (TowerAttackable)
/// รวมตรรกะ ApplyHits, Overrun carry, Lifedrain, FriendlyFire และการหาช่อง Range/Hit ตอน Hover ไว้ที่เดียว
/// </summary>
public static class CombatResolver
{
    public struct AttackerSpec
    {
        public string attackerName;
        public HexCoordinates origin;
        public AttackPatternData pattern;
        public float scaledBaseDamage;
        public bool fromSpirit;
        public bool canTargetLanterns;
        public bool armorPiercing;
        public bool overrun;
        public bool lifedrain;
        public EntityBase selfEntity;
        public TowerBase selfTower;

        public void HealSelf(float amount)
        {
            if (amount <= 0f) return;
            if (selfEntity != null) selfEntity.Heal(amount);
            else if (selfTower != null) selfTower.Heal(amount);
        }
    }

    /// <summary>
    /// ทำดาเมจตามช่อง Hit Pattern ทั้งหมด พร้อมคำนวณ Falloff, Armor Piercing, Lifedrain, FriendlyFire และ Overrun
    /// </summary>
    public static void ApplyHits(
        TurnContext ctx,
        in AttackerSpec spec,
        HexCoordinates targetCoord,
        IReadOnlyList<HexCoordinates> rangeTiles,
        IReadOnlyList<HexCoordinates> hitTiles)
    {
        LoopPathManager board = LoopPathManager.Instance;
        if (board == null || spec.pattern == null || hitTiles == null) return;

        float carry = 0f;

        for (int i = 0; i < hitTiles.Count; i++)
        {
            if (!board.TryGetTileAt(hitTiles[i], out HexTile tile)) continue;

            float falloff = spec.pattern.GetDamageMultiplier(targetCoord, hitTiles[i]);
            float raw = Mathf.Floor(spec.scaledBaseDamage * falloff);

            if (HasValidPrimaryTarget(tile, spec))
            {
                DamagePrimaryTarget(ctx, tile, spec, raw, spec.armorPiercing, out float overkill);

                if (spec.overrun)
                {
                    float incoming = carry;
                    carry = overkill;

                    if (incoming > 0f)
                    {
                        if (HasValidPrimaryTarget(tile, spec))
                        {
                            // ดาเมจที่ส่งต่อมาจาก Overrun เป็นค่าสุทธิแล้ว ไม่หัก DEF ซ้ำ
                            DamagePrimaryTarget(ctx, tile, spec, incoming, true, out float overkillFromCarry);
                            carry = overkillFromCarry;
                        }
                        else
                        {
                            carry += incoming;
                        }
                    }
                }
            }

            if (spec.pattern.FriendlyFire)
            {
                ApplyFriendlyFire(ctx, tile, spec, raw);
            }
        }

        if (spec.overrun && carry > 0f)
        {
            ApplyOverrunToRange(ctx, board, spec, rangeTiles, hitTiles, carry);
        }
    }

    /// <summary>
    /// คำนวณช่อง Range และช่อง Hit ของเป้าหมายแรกที่เป็นไปได้สำหรับแสดงตอน Hover บนกระดาน
    /// </summary>
    public static void CollectPreviewRangeAndHit(
        HexCoordinates origin,
        int facing,
        AttackPatternData pattern,
        in HexPatternContext pctx,
        bool fromSpirit,
        bool canTargetLanterns,
        List<HexCoordinates> rangeBuffer,
        List<HexCoordinates> hitBuffer)
    {
        if (rangeBuffer == null || hitBuffer == null) return;
        rangeBuffer.Clear();
        hitBuffer.Clear();

        if (pattern == null) return;

        pattern.CollectRangeTiles(origin, facing, rangeBuffer, pctx);

        LoopPathManager board = LoopPathManager.Instance;
        if (board == null) return;

        for (int i = 0; i < rangeBuffer.Count; i++)
        {
            if (!board.TryGetTileAt(rangeBuffer[i], out HexTile tile)) continue;

            bool validTarget = fromSpirit
                ? IsValidTowerTarget(tile.CurrentTower, canTargetLanterns)
                : tile.HasEntity;

            if (validTarget)
            {
                pattern.CollectHitTiles(origin, facing, rangeBuffer[i], rangeBuffer, hitBuffer, pctx);
                break;
            }
        }
    }

    public static bool IsValidTowerTarget(TowerBase tower, bool canTargetLanterns)
    {
        if (tower == null || tower.IsDead || !tower.IsShrineSide) return false;
        return canTargetLanterns || tower.GetComponent<TowerObjective>() != null;
    }

    // ---------- Internals ----------

    private static bool HasValidPrimaryTarget(HexTile tile, in AttackerSpec spec)
    {
        if (tile == null) return false;

        if (spec.fromSpirit)
        {
            return IsValidTowerTarget(tile.CurrentTower, spec.canTargetLanterns);
        }

        return tile.CurrentEntity != null && !tile.CurrentEntity.IsDead;
    }

    private static float DamagePrimaryTarget(
        TurnContext ctx,
        HexTile tile,
        in AttackerSpec spec,
        float raw,
        bool ignoreDefense,
        out float overkill)
    {
        if (spec.fromSpirit)
        {
            TowerBase tower = tile.CurrentTower;
            float dealt = tower.ApplyDamage(raw, ignoreDefense, out overkill);

            if (dealt > 0f)
            {
                ctx?.Log($"{spec.attackerName} hits {tower.TowerName} for {dealt:0}");
                if (spec.lifedrain) spec.HealSelf(dealt);
            }
            else
            {
                ctx?.Log($"{tower.TowerName} blocks {spec.attackerName}");
            }

            return dealt;
        }
        else
        {
            EntityBase entity = tile.CurrentEntity;
            float dealt = entity.ApplyDamage(raw, ignoreDefense, out overkill);

            if (dealt > 0f)
            {
                ctx?.Log($"{spec.attackerName} hits {entity.EntityName} for {dealt:0}");
                if (spec.lifedrain) spec.HealSelf(dealt);
            }
            else
            {
                ctx?.Log($"{entity.EntityName} blocks {spec.attackerName}");
            }

            return dealt;
        }
    }

    private static void ApplyFriendlyFire(TurnContext ctx, HexTile tile, in AttackerSpec spec, float raw)
    {
        if (spec.fromSpirit)
        {
            EntityBase other = tile.CurrentEntity;
            if (other != null && other != spec.selfEntity && !other.IsDead)
            {
                float dealt = other.ApplyDamage(raw, false);
                ctx?.Log($"{spec.attackerName} hits {other.EntityName} for {dealt:0}");
            }
        }
        else
        {
            TowerBase other = tile.CurrentTower;
            if (other != null && other != spec.selfTower && !other.IsDead && other.IsShrineSide)
            {
                float dealt = other.ApplyDamage(raw, false);
                ctx?.Log($"{spec.attackerName} hits {other.TowerName} for {dealt:0}");
            }
        }
    }

    private static void ApplyOverrunToRange(
        TurnContext ctx,
        LoopPathManager board,
        in AttackerSpec spec,
        IReadOnlyList<HexCoordinates> rangeTiles,
        IReadOnlyList<HexCoordinates> hitTiles,
        float carry)
    {
        if (rangeTiles == null) return;

        HexTile bestTile = null;

        if (spec.fromSpirit)
        {
            int bestDistance = int.MaxValue;
            bool bestIsObjective = false;

            for (int i = 0; i < rangeTiles.Count; i++)
            {
                if (ContainsCoord(hitTiles, rangeTiles[i])) continue;
                if (!board.TryGetTileAt(rangeTiles[i], out HexTile tile)) continue;

                TowerBase tower = tile.CurrentTower;
                if (!IsValidTowerTarget(tower, spec.canTargetLanterns)) continue;

                int distance = spec.origin.DistanceTo(rangeTiles[i]);
                bool isObjective = tower.GetComponent<TowerObjective>() != null;

                if (distance < bestDistance || (distance == bestDistance && isObjective && !bestIsObjective))
                {
                    bestTile = tile;
                    bestDistance = distance;
                    bestIsObjective = isObjective;
                }
            }
        }
        else
        {
            int bestKey = int.MaxValue;

            for (int i = 0; i < rangeTiles.Count; i++)
            {
                if (ContainsCoord(hitTiles, rangeTiles[i])) continue;
                if (!board.TryGetTileAt(rangeTiles[i], out HexTile tile) || !tile.HasEntity) continue;

                EntityBase entity = tile.CurrentEntity;
                int key = entity.TryGetComponent(out EntityWalkable walkable) ? walkable.SortKey : int.MaxValue - 1;

                if (bestTile == null || key < bestKey)
                {
                    bestTile = tile;
                    bestKey = key;
                }
            }
        }

        if (bestTile != null)
        {
            DamagePrimaryTarget(ctx, bestTile, spec, carry, true, out _);
        }
    }

    private static bool ContainsCoord(IReadOnlyList<HexCoordinates> list, HexCoordinates coord)
    {
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == coord) return true;
        }
        return false;
    }
}
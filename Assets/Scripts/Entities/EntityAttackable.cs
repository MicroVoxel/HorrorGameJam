using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ส่วนโจมตีของผี (ไม่ใช่ ITurnActor เอง EntityWalkable เป็นคนเรียกตอนมี AP)
/// - เลือกเป้าหมายเฉพาะช่องใน Range Pattern: ศาลฝั่งป้อมที่ใกล้สุด เสมอกันให้ศาลเป้าหมายหลักก่อน แล้วค่อยสุ่มด้วย GameRandom
/// - ทำดาเมจทุกช่องใน Hit Pattern เรียงจากใกล้ไปไกล ดาเมจ = floor(base x AttackMultiplier x falloff) แล้วหัก DEF ของเป้า
/// - โจมตีได้ attacksPerTurn ครั้งต่อเทิร์น (Frenzy ปลดเพดานเป็นอย่างน้อย 2)
///
/// 4B Keyword: Double Strike (+1 ครั้งต่อเทิร์น), Armor Piercing (ข้าม DEF), Lifedrain (ฟื้น HP เท่าดาเมจ),
///             Overrun (ดาเมจที่เกินส่งต่อเป้าถัดไปในช่อง Hit ถ้าไม่มีส่งเข้าศาลที่ใกล้สุดใน Range)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EntityBase))]
public class EntityAttackable : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private AttackPatternData attackPattern;
    [SerializeField, Min(0f)] private float baseDamage = 6.0f;
    [SerializeField, Min(1)] private int attacksPerTurn = 1;

    [Header("Targeting")]
    [Tooltip("Off = spirits only attack objective shrines, not lanterns.")]
    [SerializeField] private bool canTargetLanterns = true;

    [Header("Visuals (display only)")]
    [SerializeField] private float lungeDistance = 0.3f;
    [SerializeField] private float lungeDuration = 0.12f;

    private EntityBase entityBase;
    private AbilityHolder abilities;
    private int attacksUsed;
    private TowerBase plannedTarget;

    private readonly List<HexCoordinates> rangeTiles = new List<HexCoordinates>(32);
    private readonly List<HexCoordinates> hitTiles = new List<HexCoordinates>(32);
    private readonly List<TowerBase> candidates = new List<TowerBase>(8);

    public AttackPatternData Pattern => attackPattern;
    public float BaseDamage => baseDamage;

    /// <summary>จำนวนครั้งที่โจมตีได้ต่อเทิร์น (Double Strike +1, Frenzy ปลดเพดานเป็นอย่างน้อย 2 ไม่ซ้อนกัน)</summary>
    public int MaxAttacksPerTurn
    {
        get
        {
            int count = Mathf.Max(1, attacksPerTurn) + (Has(AbilityKeyword.DoubleStrike) ? 1 : 0);
            return entityBase != null && entityBase.IsFrenzied ? Mathf.Max(2, count) : count;
        }
    }

    public bool CanAttack => attacksUsed < MaxAttacksPerTurn;

    private bool Has(AbilityKeyword keyword) => abilities != null && abilities.Has(keyword);

    private void Awake()
    {
        entityBase = GetComponent<EntityBase>();
        abilities = GetComponent<AbilityHolder>();
    }

    private void OnDisable()
    {
        RangeIndicator.Hide(this);
    }

    public void BeginTurn()
    {
        attacksUsed = 0;
        plannedTarget = null;
    }

    /// <summary>หาเป้าหมายใน Range จากช่องและทิศหน้าปัจจุบัน คืน true ถ้ามี (ผลเก็บไว้ให้ PerformAttack)</summary>
    public bool TryAcquireTarget()
    {
        plannedTarget = null;

        HexTile origin = entityBase.OccupiedTile;
        LoopPathManager board = LoopPathManager.Instance;
        if (attackPattern == null || origin == null || board == null) return false;

        HexPatternContext pctx = BoardPatternContext.Get();
        attackPattern.CollectRangeTiles(origin.Coordinates, entityBase.Facing, rangeTiles, pctx);

        candidates.Clear();
        int bestDistance = int.MaxValue;
        bool bestIsObjective = false;

        for (int i = 0; i < rangeTiles.Count; i++)
        {
            if (!board.TryGetTileAt(rangeTiles[i], out HexTile tile)) continue;

            TowerBase tower = tile.CurrentTower;
            if (!IsValidTarget(tower)) continue;

            int distance = origin.Coordinates.DistanceTo(rangeTiles[i]);
            bool isObjective = tower.GetComponent<TowerObjective>() != null;

            bool better = distance < bestDistance || (distance == bestDistance && isObjective && !bestIsObjective);
            bool tied = distance == bestDistance && isObjective == bestIsObjective;

            if (better)
            {
                candidates.Clear();
                candidates.Add(tower);
                bestDistance = distance;
                bestIsObjective = isObjective;
            }
            else if (tied)
            {
                candidates.Add(tower);
            }
        }

        if (candidates.Count == 0) return false;

        plannedTarget = candidates.Count == 1 ? candidates[0] : GameRandom.Pick(candidates);
        return true;
    }

    /// <summary>โจมตีเป้าหมายที่ TryAcquireTarget เลือกไว้ (ใช้ AP ไปแล้วโดยผู้เรียก)</summary>
    public IEnumerator PerformAttack(TurnContext ctx)
    {
        TowerBase target = plannedTarget;
        HexTile originTile = entityBase.OccupiedTile;
        if (target == null || target.IsDead || target.CurrentTile == null || originTile == null) yield break;

        HexPatternContext pctx = BoardPatternContext.Get();
        HexCoordinates targetCoord = target.CurrentTile.Coordinates;

        attackPattern.CollectHitTiles(originTile.Coordinates, entityBase.Facing, targetCoord, rangeTiles, hitTiles, pctx);

        attacksUsed++;
        ctx.MarkAction();

        bool animate = !ctx.IsFastForward;
        if (animate && RangeIndicator.Instance != null)
        {
            RangeIndicator.Instance.Show(this, rangeTiles, hitTiles, false);
        }

        Vector3 home = transform.position;
        Vector3 toward = target.transform.position - home;
        toward.y = 0f;
        Vector3 peak = home + (toward.sqrMagnitude > 0.0001f ? toward.normalized * lungeDistance : Vector3.zero);

        if (animate) yield return MoveVisual(ctx, home, peak, lungeDuration);

        ApplyHits(ctx, targetCoord);

        if (animate) yield return MoveVisual(ctx, peak, home, lungeDuration);

        transform.position = home;
        RangeIndicator.Hide(this);
    }

    private void ApplyHits(TurnContext ctx, HexCoordinates targetCoord)
    {
        LoopPathManager board = LoopPathManager.Instance;
        if (board == null) return;

        bool armorPiercing = Has(AbilityKeyword.ArmorPiercing);
        bool overrun = Has(AbilityKeyword.Overrun);
        float carry = 0f;

        for (int i = 0; i < hitTiles.Count; i++)
        {
            if (!board.TryGetTileAt(hitTiles[i], out HexTile tile)) continue;

            float falloff = attackPattern.GetDamageMultiplier(targetCoord, hitTiles[i]);
            float raw = Mathf.Floor(baseDamage * entityBase.AttackMultiplier * falloff);

            TowerBase tower = tile.CurrentTower;
            if (IsValidTarget(tower))
            {
                DamageTower(ctx, tower, raw, armorPiercing, out float overkill);

                if (overrun)
                {
                    float incoming = carry;
                    carry = overkill;

                    if (incoming > 0f)
                    {
                        if (!tower.IsDead)
                        {
                            // ดาเมจที่ส่งต่อมาเป็นค่าสุดท้ายแล้ว ไม่หัก DEF ซ้ำ
                            DamageTower(ctx, tower, incoming, true, out float overkillFromCarry);
                            carry = overkillFromCarry;
                        }
                        else
                        {
                            carry += incoming;
                        }
                    }
                }
            }

            if (attackPattern.FriendlyFire)
            {
                EntityBase other = tile.CurrentEntity;
                if (other != null && other != entityBase && !other.IsDead)
                {
                    float dealt = other.ApplyDamage(raw, false);
                    ctx.Log($"{entityBase.EntityName} hits {other.EntityName} for {dealt:0}");
                }
            }
        }

        if (overrun && carry > 0f) ApplyOverrunToRange(ctx, board, carry);
    }

    /// <summary>Overrun: ไม่มีเป้าถัดไปในช่อง Hit ส่งดาเมจที่เหลือเข้าศาลที่ใกล้สุดใน Range (ที่ยังไม่ถูกตีรอบนี้)</summary>
    private void ApplyOverrunToRange(TurnContext ctx, LoopPathManager board, float carry)
    {
        HexTile originTile = entityBase.OccupiedTile;
        if (originTile == null) return;

        TowerBase best = null;
        int bestDistance = int.MaxValue;
        bool bestIsObjective = false;

        for (int i = 0; i < rangeTiles.Count; i++)
        {
            if (hitTiles.Contains(rangeTiles[i])) continue;
            if (!board.TryGetTileAt(rangeTiles[i], out HexTile tile)) continue;

            TowerBase tower = tile.CurrentTower;
            if (!IsValidTarget(tower)) continue;

            int distance = originTile.Coordinates.DistanceTo(rangeTiles[i]);
            bool isObjective = tower.GetComponent<TowerObjective>() != null;

            if (distance < bestDistance || (distance == bestDistance && isObjective && !bestIsObjective))
            {
                best = tower;
                bestDistance = distance;
                bestIsObjective = isObjective;
            }
        }

        if (best != null) DamageTower(ctx, best, carry, true, out _);
    }

    private float DamageTower(TurnContext ctx, TowerBase tower, float raw, bool ignoreDefense, out float overkill)
    {
        float dealt = tower.ApplyDamage(raw, ignoreDefense, out overkill);

        if (dealt > 0f)
        {
            ctx.Log($"{entityBase.EntityName} hits {tower.TowerName} for {dealt:0}");
            if (Has(AbilityKeyword.Lifedrain)) entityBase.Heal(dealt);
        }
        else
        {
            ctx.Log($"{tower.TowerName} blocks {entityBase.EntityName}");
        }

        return dealt;
    }

    private bool IsValidTarget(TowerBase tower)
    {
        if (tower == null || tower.IsDead || !tower.IsShrineSide) return false;
        return canTargetLanterns || tower.GetComponent<TowerObjective>() != null;
    }

    private IEnumerator MoveVisual(TurnContext ctx, Vector3 from, Vector3 to, float seconds)
    {
        float duration = ctx.ScaleDuration(seconds);
        float elapsed = 0f;

        while (duration > 0f && elapsed < duration && !ctx.IsFastForward)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.position = to;
    }
}
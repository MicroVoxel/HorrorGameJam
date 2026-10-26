using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ป้อมฝั่งศาล (เช่น โคมไฟหิน) แบบเทิร์น (ITurnActor, กลุ่ม Tower) ไม่มี Update()
/// - 1 action ต่อเทิร์น (ของถวายสดที่ "ยิงถี่ขึ้น" เพิ่มเป็นเศษสะสม: ครบ 1 = +1 action)
/// - เลือกเป้าหมายจากช่องใน Range Pattern ที่หันตาม Facing: ผีหัวขบวนก่อน เสมอกันสุ่มด้วย GameRandom
/// - ทำดาเมจทุกผีในช่อง Hit Pattern เรียงจากใกล้ไปไกล
///
/// 4B Keyword: Quick Attack (IQuickActor), Double Strike (+1 action), Decoy (ผีที่มี Decoy ใน Range ต้องถูกเลือกก่อน),
///             Armor Piercing, Lifedrain, Overrun (ส่งดาเมจที่เกินให้ผีถัดไปในช่อง Hit หรือผีหัวขบวนที่เหลือใน Range)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TowerBase))]
public class TowerAttackable : MonoBehaviour, IQuickActor
{
    [Header("Attack")]
    [SerializeField] private AttackPatternData attackPattern;
    [SerializeField, Min(0f)] private float baseDamage = 10.0f;
    [SerializeField, Min(1)] private int actionsPerTurn = 1;

    [Header("Altar Buff Multipliers (บัฟศาลเจ้าเมื่อผู้เล่นถวายของสด)")]
    [SerializeField] private float damageMultiplier = 1.0f;
    [Tooltip("1.0 = ปกติ ค่าที่เกิน 1 สะสมเป็นเศษต่อเทิร์น (1.33 = ได้ action เพิ่มทุก 3 เทิร์น)")]
    [SerializeField] private float fireRateMultiplier = 1.0f;

    [Header("Visuals (display only)")]
    [SerializeField] private float flashSeconds = 0.18f;

    private TowerBase towerBase;
    private AbilityHolder abilities;
    private float actionCarry;
    private int actionsRemaining = 1;
    private EntityBase plannedTarget;

    private readonly List<HexCoordinates> rangeTiles = new List<HexCoordinates>(32);
    private readonly List<HexCoordinates> hitTiles = new List<HexCoordinates>(32);
    private readonly List<EntityBase> candidates = new List<EntityBase>(8);

    public AttackPatternData Pattern => attackPattern;
    public float BaseDamage => baseDamage;

    // ---------- ITurnActor ----------
    public TurnActorGroup Group => TurnActorGroup.Tower;
    public int SortKey => towerBase != null ? towerBase.PlacementOrder : int.MaxValue;
    public bool IsActorActive => towerBase != null && !towerBase.IsDead && towerBase.CurrentTile != null;

    private bool Has(AbilityKeyword keyword) => abilities != null && abilities.Has(keyword);

    private void Awake()
    {
        towerBase = GetComponent<TowerBase>();
        abilities = GetComponent<AbilityHolder>();
    }

    private void OnEnable()
    {
        // ลงทะเบียนเฉพาะตอนวางลงกระดานจริง (Ghost Preview จะไม่ถูกนับ)
        if (towerBase != null)
        {
            towerBase.OnPlacedOnBoard += HandlePlacedOnBoard;
            if (towerBase.CurrentTile != null) TurnActorRegistry.Register(this);
        }
    }

    private void OnDisable()
    {
        if (towerBase != null) towerBase.OnPlacedOnBoard -= HandlePlacedOnBoard;
        TurnActorRegistry.Unregister(this);
        RangeIndicator.Hide(this);
    }

    private void HandlePlacedOnBoard(TowerBase placed)
    {
        TurnActorRegistry.Register(this);
    }

    public void OnTurnStart(TurnContext ctx)
    {
        actionCarry += Mathf.Max(0f, fireRateMultiplier - 1f);
        int extra = Mathf.FloorToInt(actionCarry + 0.0001f);
        actionCarry -= extra;

        actionsRemaining = Mathf.Max(1, actionsPerTurn) + extra + (Has(AbilityKeyword.DoubleStrike) ? 1 : 0);

        // Fortune Candle ที่เน่า: ป้อมทุกหลังข้าม action ทั้ง Round (Quick Step ก็ข้ามด้วย)
        if (AltarManager.Instance != null && AltarManager.Instance.TowersSkipActive) actionsRemaining = 0;
    }

    /// <summary>Quick Step: ป้อมที่มี Quick Attack ใช้ 1 action ก่อน Entity Step (ไม่มีเป้าหมาย = ไม่เสีย action)</summary>
    public IEnumerator ExecuteQuickAction(TurnContext ctx)
    {
        if (!IsActorActive || actionsRemaining <= 0 || !Has(AbilityKeyword.QuickAttack)) yield break;
        if (!TryAcquireTarget()) yield break;

        actionsRemaining--;
        yield return PerformAttack(ctx);
    }

    public IEnumerator ExecuteTurn(TurnContext ctx)
    {
        while (actionsRemaining > 0)
        {
            if (!IsActorActive) yield break;
            if (!TryAcquireTarget()) break;

            actionsRemaining--;
            yield return PerformAttack(ctx);
        }
    }

    public void OnTurnEnd(TurnContext ctx) { }

    // ---------- Targeting ----------

    private bool TryAcquireTarget()
    {
        plannedTarget = null;

        LoopPathManager board = LoopPathManager.Instance;
        if (attackPattern == null || board == null || towerBase.CurrentTile == null) return false;

        HexPatternContext pctx = BoardPatternContext.ForTower();
        attackPattern.CollectRangeTiles(towerBase.CurrentTile.Coordinates, towerBase.Facing, rangeTiles, pctx);

        candidates.Clear();
        int bestKey = int.MaxValue;
        bool bestIsDecoy = false;

        for (int i = 0; i < rangeTiles.Count; i++)
        {
            if (!board.TryGetTileAt(rangeTiles[i], out HexTile tile) || !tile.HasEntity) continue;

            EntityBase entity = tile.CurrentEntity;
            bool decoy = AbilityResolver.HasKeyword(entity, AbilityKeyword.Decoy);

            // Decoy ใน Range ต้องถูกเลือกก่อนเสมอ
            if (bestIsDecoy && !decoy) continue;

            int key = entity.TryGetComponent(out EntityWalkable walkable) ? walkable.SortKey : int.MaxValue - 1;

            if (decoy && !bestIsDecoy)
            {
                candidates.Clear();
                candidates.Add(entity);
                bestKey = key;
                bestIsDecoy = true;
            }
            else if (key < bestKey)
            {
                candidates.Clear();
                candidates.Add(entity);
                bestKey = key;
            }
            else if (key == bestKey)
            {
                candidates.Add(entity);
            }
        }

        if (candidates.Count == 0) return false;

        plannedTarget = candidates.Count == 1 ? candidates[0] : GameRandom.Pick(candidates);
        return true;
    }

    private IEnumerator PerformAttack(TurnContext ctx)
    {
        EntityBase target = plannedTarget;
        if (target == null || target.IsDead || target.OccupiedTile == null) yield break;

        HexPatternContext pctx = BoardPatternContext.ForTower();
        HexCoordinates origin = towerBase.CurrentTile.Coordinates;
        HexCoordinates targetCoord = target.OccupiedTile.Coordinates;

        attackPattern.CollectHitTiles(origin, towerBase.Facing, targetCoord, rangeTiles, hitTiles, pctx);
        ctx.MarkAction();

        bool animate = !ctx.IsFastForward;
        if (animate && RangeIndicator.Instance != null)
        {
            RangeIndicator.Instance.Show(this, rangeTiles, hitTiles, true);
        }

        if (animate) yield return ctx.Pause(flashSeconds);

        ApplyHits(ctx, targetCoord);
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
            float raw = Mathf.Floor(baseDamage * damageMultiplier * falloff);

            EntityBase entity = tile.CurrentEntity;
            if (entity != null && !entity.IsDead)
            {
                DamageEntity(ctx, entity, raw, armorPiercing, out float overkill);

                if (overrun)
                {
                    float incoming = carry;
                    carry = overkill;

                    if (incoming > 0f)
                    {
                        if (!entity.IsDead)
                        {
                            DamageEntity(ctx, entity, incoming, true, out float overkillFromCarry);
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
                TowerBase other = tile.CurrentTower;
                if (other != null && other != towerBase && !other.IsDead && other.IsShrineSide)
                {
                    float dealt = other.ApplyDamage(raw, false);
                    ctx.Log($"{towerBase.TowerName} hits {other.TowerName} for {dealt:0}");
                }
            }
        }

        if (overrun && carry > 0f) ApplyOverrunToRange(ctx, board, carry);
    }

    /// <summary>Overrun: ไม่มีผีถัดไปในช่อง Hit ส่งดาเมจที่เหลือให้ผีหัวขบวนที่ยังไม่ถูกตีใน Range</summary>
    private void ApplyOverrunToRange(TurnContext ctx, LoopPathManager board, float carry)
    {
        EntityBase best = null;
        int bestKey = int.MaxValue;

        for (int i = 0; i < rangeTiles.Count; i++)
        {
            if (hitTiles.Contains(rangeTiles[i])) continue;
            if (!board.TryGetTileAt(rangeTiles[i], out HexTile tile) || !tile.HasEntity) continue;

            EntityBase entity = tile.CurrentEntity;
            int key = entity.TryGetComponent(out EntityWalkable walkable) ? walkable.SortKey : int.MaxValue - 1;

            if (best == null || key < bestKey)
            {
                best = entity;
                bestKey = key;
            }
        }

        if (best != null) DamageEntity(ctx, best, carry, true, out _);
    }

    private float DamageEntity(TurnContext ctx, EntityBase entity, float raw, bool ignoreDefense, out float overkill)
    {
        float dealt = entity.ApplyDamage(raw, ignoreDefense, out overkill);

        if (dealt > 0f)
        {
            ctx.Log($"{towerBase.TowerName} hits {entity.EntityName} for {dealt:0}");
            if (Has(AbilityKeyword.Lifedrain)) towerBase.Heal(dealt);
        }
        else
        {
            ctx.Log($"{entity.EntityName} blocks {towerBase.TowerName}");
        }

        return dealt;
    }

    // ---------- Altar hooks ----------

    /// <summary>ปรับตัวคูณความแรงและความถี่ในการยิง</summary>
    public void SetBuffMultipliers(float dmgMult, float rateMult)
    {
        damageMultiplier = Mathf.Max(0.1f, dmgMult);
        fireRateMultiplier = Mathf.Max(0.1f, rateMult);
    }

    /// <summary>ปรับเฉพาะความถี่ในการยิง (เรียกจาก AltarManager) เช่น 1.33 = ได้ action เพิ่มทุก 3 เทิร์น</summary>
    public void SetFireRateMultiplier(float rateMult)
    {
        fireRateMultiplier = Mathf.Max(0.1f, rateMult);
    }
}
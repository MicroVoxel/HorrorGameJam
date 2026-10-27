using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ป้อมฝั่งศาล (เช่น โคมไฟหิน) แบบเทิร์น (ITurnActor, กลุ่ม Tower) ไม่มี Update()
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

        if (AltarManager.Instance != null && AltarManager.Instance.TowersSkipActive) actionsRemaining = 0;
    }

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

        CombatResolver.AttackerSpec spec = new CombatResolver.AttackerSpec
        {
            attackerName = towerBase.TowerName,
            origin = origin,
            pattern = attackPattern,
            scaledBaseDamage = baseDamage * damageMultiplier,
            fromSpirit = false,
            canTargetLanterns = true,
            armorPiercing = Has(AbilityKeyword.ArmorPiercing),
            overrun = Has(AbilityKeyword.Overrun),
            lifedrain = Has(AbilityKeyword.Lifedrain),
            selfTower = towerBase
        };

        CombatResolver.ApplyHits(ctx, spec, targetCoord, rangeTiles, hitTiles);
        RangeIndicator.Hide(this);
    }

    // ---------- Altar hooks ----------

    /// <summary>ปรับความถี่ในการยิง (เรียกจาก AltarManager) เช่น 1.33 = ได้ action เพิ่มทุก 3 เทิร์น</summary>
    public void SetFireRateMultiplier(float rateMult)
    {
        fireRateMultiplier = Mathf.Max(0.1f, rateMult);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ส่วนโจมตีของผี (ไม่ใช่ ITurnActor เอง EntityWalkable เป็นคนเรียกตอนมี AP)
/// ใช้ CombatResolver.ApplyHits ร่วมกับ TowerAttackable เพื่อให้ตรรกะดาเมจและ Overrun อยู่ที่เดียว
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
    public bool CanTargetLanterns => canTargetLanterns;

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
            if (!CombatResolver.IsValidTowerTarget(tower, canTargetLanterns)) continue;

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
        HexCoordinates originCoord = originTile.Coordinates;
        HexCoordinates targetCoord = target.CurrentTile.Coordinates;

        attackPattern.CollectHitTiles(originCoord, entityBase.Facing, targetCoord, rangeTiles, hitTiles, pctx);

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

        CombatResolver.AttackerSpec spec = new CombatResolver.AttackerSpec
        {
            attackerName = entityBase.EntityName,
            origin = originCoord,
            pattern = attackPattern,
            scaledBaseDamage = baseDamage * entityBase.AttackMultiplier,
            fromSpirit = true,
            canTargetLanterns = canTargetLanterns,
            armorPiercing = Has(AbilityKeyword.ArmorPiercing),
            overrun = Has(AbilityKeyword.Overrun),
            lifedrain = Has(AbilityKeyword.Lifedrain),
            selfEntity = entityBase
        };

        CombatResolver.ApplyHits(ctx, spec, targetCoord, rangeTiles, hitTiles);

        if (animate) yield return MoveVisual(ctx, peak, home, lungeDuration);

        transform.position = home;
        RangeIndicator.Hide(this);
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
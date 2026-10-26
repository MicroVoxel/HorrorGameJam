using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ประมวลผล Keyword ที่ต้องทำในขั้น Cleanup: Last Breath
/// ผี/ป้อมที่ตายส่งคำขอเข้าคิว (AbilityHolder) แล้ว TurnManager เรียก ResolvePending ก่อนยิง OnTurnCleanup
/// เพื่อให้ Last Breath ระเบิดเสร็จก่อนที่ Victory / Extinction จะตัดสิน
/// ตัวที่ตายจาก Last Breath ก็เข้าคิวต่อเป็นลูกโซ่ (จำกัดไม่เกิน 64 ครั้งต่อ Cleanup)
/// ต้องมี 1 ตัวในซีนเกม (วางบน GameObject เดียวกับ TurnManager ได้)
/// </summary>
[DisallowMultipleComponent]
public class AbilityResolver : SingletonBehaviour<AbilityResolver>
{
    private struct LastBreathRequest
    {
        public AbilityData data;
        public HexCoordinates origin;
        public int facing;
        public bool fromSpirit;
        public string sourceName;
    }

    private const int MaxChainPerCleanup = 64;

    private readonly Queue<LastBreathRequest> pending = new Queue<LastBreathRequest>();
    private readonly List<HexCoordinates> tiles = new List<HexCoordinates>(32);
    private bool subscribedState;

    // ---------- Static helpers ----------

    /// <summary>true ถ้า component นี้อยู่บน GameObject ที่มี AbilityHolder และมี Keyword นั้น</summary>
    public static bool HasKeyword(Component component, AbilityKeyword keyword)
    {
        return component != null
               && component.TryGetComponent(out AbilityHolder holder)
               && holder.Has(keyword);
    }

    /// <summary>ส่งคำขอ Last Breath เข้าคิว (fromSpirit = true: ผีตาย ระเบิดใส่ฝั่งศาล / false: ป้อมตาย ระเบิดใส่ผี)</summary>
    public static void QueueLastBreath(AbilityData data, HexCoordinates origin, int facing, bool fromSpirit, string sourceName)
    {
        if (data == null) return;

        if (Instance == null)
        {
            Debug.LogWarning("[AbilityResolver] No AbilityResolver in the scene. Last Breath is ignored.");
            return;
        }

        Instance.pending.Enqueue(new LastBreathRequest
        {
            data = data,
            origin = origin,
            facing = facing,
            fromSpirit = fromSpirit,
            sourceName = sourceName
        });
    }

    // FIX บัก 4: TurnManager เรียกก่อนยิง OnTurnCleanup เพื่อให้ Last Breath จบก่อนตัดสินแพ้/ชนะ
    /// <summary>ประมวลผลคิว Last Breath ที่ค้างอยู่ทั้งหมด (รวมลูกโซ่) ปลอดภัยแม้ไม่มี AbilityResolver ในซีน</summary>
    public static void ResolvePending(TurnContext ctx)
    {
        if (Instance != null) Instance.ProcessPending(ctx);
    }

    // ---------- Lifecycle ----------

    private void Start()
    {
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (!subscribedState && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
            subscribedState = true;
        }
    }

    protected override void OnDestroy()
    {
        if (subscribedState && GameStateManager.Instance != null) GameStateManager.Instance.OnStateChanged -= HandleStateChanged;

        base.OnDestroy();
    }

    private void HandleStateChanged(GameState state)
    {
        // คำขอที่ค้างจากเฟสอื่น (เช่น เกมจบกลางเทิร์น) ไม่ต้องยกไป Round ถัดไป
        if (state != GameState.Resolution) pending.Clear();
    }

    // ---------- Resolve ----------

    private void ProcessPending(TurnContext ctx)
    {
        int guard = 0;
        while (pending.Count > 0 && guard < MaxChainPerCleanup)
        {
            guard++;
            Resolve(pending.Dequeue(), ctx);
        }

        pending.Clear();
    }

    private void Resolve(LastBreathRequest req, TurnContext ctx)
    {
        LoopPathManager board = LoopPathManager.Instance;
        if (board == null || req.data == null) return;

        HexCoordinates origin = req.origin;
        HexRangePattern pattern = req.data.LastBreathPattern;

        tiles.Clear();
        if (pattern != null)
        {
            HexPatternContext pctx = BoardPatternContext.Get();
            pattern.CollectTiles(origin, req.facing, tiles, pctx);
        }
        else
        {
            tiles.Add(origin);
        }

        // เรียงใกล้ไปไกล แล้ว q, r เพื่อให้ลำดับการตายคงที่
        tiles.Sort((a, b) =>
        {
            int da = origin.DistanceTo(a);
            int db = origin.DistanceTo(b);
            if (da != db) return da.CompareTo(db);
            if (a.Q != b.Q) return a.Q.CompareTo(b.Q);
            return a.R.CompareTo(b.R);
        });

        float raw = Mathf.Floor(req.data.LastBreathDamage);
        ctx?.Log($"{req.sourceName} bursts");
        ctx?.MarkAction();

        for (int i = 0; i < tiles.Count; i++)
        {
            if (!board.TryGetTileAt(tiles[i], out HexTile tile)) continue;

            if (req.fromSpirit)
            {
                TowerBase tower = tile.CurrentTower;
                if (tower == null || tower.IsDead || !tower.IsShrineSide) continue;

                float dealt = tower.ApplyDamage(raw, false);
                ctx?.Log($"{req.sourceName} burst hits {tower.TowerName} for {dealt:0}");
            }
            else
            {
                EntityBase entity = tile.CurrentEntity;
                if (entity == null || entity.IsDead) continue;

                float dealt = entity.ApplyDamage(raw, false);
                ctx?.Log($"{req.sourceName} burst hits {entity.EntityName} for {dealt:0}");
            }
        }
    }
}
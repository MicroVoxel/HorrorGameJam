using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ประมวลผล Keyword ที่ต้องทำในขั้น Cleanup: Last Breath
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

    private const int FallbackMaxChainPerCleanup = 64;

    private readonly Queue<LastBreathRequest> pending = new Queue<LastBreathRequest>();
    private readonly List<HexCoordinates> tiles = new List<HexCoordinates>(32);
    private bool subscribedState;

    // ---------- Static helpers ----------

    public static bool HasKeyword(Component component, AbilityKeyword keyword)
    {
        return component != null
               && component.TryGetComponent(out AbilityHolder holder)
               && holder.Has(keyword);
    }

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
        if (state != GameState.Resolution) pending.Clear();
    }

    // ---------- Resolve ----------

    private void ProcessPending(TurnContext ctx)
    {
        int maxChain = GameStateManager.Instance != null
            ? GameStateManager.Instance.Rules.MaxChainPerCleanup
            : FallbackMaxChainPerCleanup;

        int guard = 0;
        while (pending.Count > 0 && guard < maxChain)
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

        HexCoordinates.SortByDistanceFrom(tiles, origin);

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
using System.Collections;
using UnityEngine;

/// <summary>
/// รังผี (Graveyard / Torii Spawner) แบบเทิร์น (ITurnActor, กลุ่ม Spawner)
/// cooldown เป็นหน่วยเทิร์น: ลด 1 ต่อเทิร์นใน Spawner Step ครบ 0 แล้วเสกผีลงช่องทางเดินที่ว่างใกล้รังที่สุด
/// เสกผีทันที 1 ตัวเมื่อวางลงกระดาน (ผู้เล่นจะมีผีให้จัดขบวนใน Planning) แล้วนับ cooldown ใหม่
/// ผีที่เสกใน Spawner Step จะได้ทำ action ใน Entity Step ของเทิร์นเดียวกัน
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TowerBase))]
public class TowerSpawnable : MonoBehaviour, ITurnActor
{
    [Header("Spawn Unit Configuration")]
    [SerializeField] private EntityBase entityPrefab;
    [Tooltip("Turns between spawns (cooldown in turns).")]
    [SerializeField, Min(1)] private int spawnEveryTurns = 3;
    [SerializeField, Min(1)] private int maxConcurrentEntities = 3;

    [Header("Placement Trigger (เสกทันทีเมื่อวางลงกระดาน)")]
    [SerializeField] private bool triggerOnPlaced = true;
    [SerializeField, Min(0)] private int initialSpawnCountOnPlaced = 1;

    private TowerBase towerBase;
    private LoopPathManager pathManager;
    private int turnsUntilSpawn;
    private int activeSpawnedCount = 0;

    public int ActiveSpawnedCount => activeSpawnedCount;
    public int TurnsUntilSpawn => turnsUntilSpawn;

    // ---------- ITurnActor ----------
    public TurnActorGroup Group => TurnActorGroup.Spawner;
    public int SortKey => towerBase != null ? towerBase.PlacementOrder : int.MaxValue;
    public bool IsActorActive => towerBase != null && !towerBase.IsDead && towerBase.CurrentTile != null && entityPrefab != null;

    private void Awake()
    {
        towerBase = GetComponent<TowerBase>();
        turnsUntilSpawn = spawnEveryTurns;
    }

    private void OnEnable()
    {
        if (towerBase != null)
        {
            towerBase.OnPlacedOnBoard += HandleTowerPlaced;
            if (towerBase.CurrentTile != null) TurnActorRegistry.Register(this);
        }
    }

    private void OnDisable()
    {
        if (towerBase != null) towerBase.OnPlacedOnBoard -= HandleTowerPlaced;
        TurnActorRegistry.Unregister(this);
    }

    private void Start()
    {
        pathManager = LoopPathManager.Instance;
    }

    private void HandleTowerPlaced(TowerBase placedTower)
    {
        TurnActorRegistry.Register(this);

        if (!triggerOnPlaced || entityPrefab == null) return;

        for (int i = 0; i < initialSpawnCountOnPlaced; i++)
        {
            if (activeSpawnedCount < maxConcurrentEntities)
            {
                TrySpawnEntityOnNearestAvailablePath();
            }
        }
    }

    public void OnTurnStart(TurnContext ctx) { }

    public IEnumerator ExecuteTurn(TurnContext ctx)
    {
        if (!IsActorActive) yield break;

        if (turnsUntilSpawn > 0) turnsUntilSpawn--;
        if (turnsUntilSpawn > 0) yield break;
        if (activeSpawnedCount >= maxConcurrentEntities) yield break;

        EntityBase spawned = TrySpawnEntityOnNearestAvailablePath();
        if (spawned == null) yield break;

        turnsUntilSpawn = spawnEveryTurns;
        ctx.MarkAction();
        ctx.Log($"{towerBase.TowerName} summons {spawned.EntityName}");
    }

    public void OnTurnEnd(TurnContext ctx) { }

    /// <summary>
    /// ค้นหาช่องทางเดินที่ใกล้รังผีที่สุดและว่างอยู่ (กฎ 1 ช่อง = 1 ตัว) แล้วเสกผีลงช่องนั้น คืนผีที่เสก (null ถ้าไม่ได้)
    /// </summary>
    public EntityBase TrySpawnEntityOnNearestAvailablePath()
    {
        if (entityPrefab == null) return null;

        if (pathManager == null) pathManager = LoopPathManager.Instance;
        if (pathManager == null || pathManager.PathLength == 0) return null;

        int closestIndex = pathManager.GetClosestPathIndex(transform.position);
        int chosenIndex = -1;

        for (int offset = 0; offset < pathManager.PathLength; offset++)
        {
            int candidateIdx = (closestIndex + offset) % pathManager.PathLength;
            HexTile candidateTile = pathManager.GetTile(candidateIdx);

            if (candidateTile != null && !candidateTile.HasEntity)
            {
                chosenIndex = candidateIdx;
                break;
            }
        }

        // ทุกช่องบนถนนมีผียืนเต็ม ให้รอเทิร์นถัดไป
        if (chosenIndex < 0) return null;

        HexTile spawnTile = pathManager.GetTile(chosenIndex);
        EntityBase spawned = Instantiate(entityPrefab, spawnTile.transform.position, Quaternion.identity);
        activeSpawnedCount++;

        spawned.OnEntityDied += HandleSpawnedEntityDied;

        EntityWalkable walkable = spawned.GetComponent<EntityWalkable>();
        if (walkable != null)
        {
            walkable.InitializeOnPath(pathManager, chosenIndex);
            GiveLateSpawnItsTurn(walkable); // FIX บัก 3
        }
        else
        {
            spawned.SetOccupiedTile(spawnTile);
        }

        return spawned;
    }

    // FIX บัก 3: OnTurnStart (ตั้ง AP) ถูกเรียกก่อน Spawner Step ผีที่เสกใน Spawner Step จึงมี AP = 0
    // ถ้ากำลังรันเทิร์นอยู่ใน Resolution ให้เรียก OnTurnStart ให้ผีตัวนี้ทันที จะได้ทำ action ใน Entity Step เทิร์นเดียวกัน
    // (ตอนวางรังใน Planning IsRunning เป็น false จึงไม่เข้าเงื่อนไข ผีจะได้ AP ตามปกติตอน OnTurnStart ของเทิร์น 1)
    private static void GiveLateSpawnItsTurn(EntityWalkable walkable)
    {
        TurnManager tm = TurnManager.Instance;
        if (walkable == null || tm == null) return;
        if (!GameStateManager.IsResolution || !tm.IsRunning || tm.Context == null) return;

        walkable.OnTurnStart(tm.Context);
    }

    private void HandleSpawnedEntityDied(EntityBase deadEntity)
    {
        if (deadEntity != null)
        {
            deadEntity.OnEntityDied -= HandleSpawnedEntityDied;
        }
        activeSpawnedCount = Mathf.Max(0, activeSpawnedCount - 1);
    }
}
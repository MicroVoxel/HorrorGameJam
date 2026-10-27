using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สถานะของช่องวางของถวายแต่ละหมวดหมู่บนโต๊ะหมู่บูชา (เก็บผ่าน OfferingInstance ตัวเดียว)
/// </summary>
[Serializable]
public class ActiveAltarSlotState
{
    public OfferingType slotCategory;
    public OfferingInstance offering;

    public OfferingItemData currentItem
    {
        get => offering.data;
        set => offering.data = value;
    }

    public int remainingDecayLoops
    {
        get => offering.remainingDecayLoops;
        set => offering.remainingDecayLoops = value;
    }

    public bool isRotten
    {
        get => offering.isRotten;
        set => offering.isRotten = value;
    }

    public int rottenRoundsElapsed
    {
        get => offering.rottenRoundsElapsed;
        set => offering.rottenRoundsElapsed = value;
    }

    public bool HasOffering => offering.IsValid;

    /// <summary>วางของสดลงช่อง (รีเซ็ตอายุและสถานะเน่า หรือระบุอายุที่เหลือ)</summary>
    public void Place(OfferingItemData item, int remainingRounds = -1)
    {
        offering = OfferingInstance.CreateFresh(item, remainingRounds);
    }

    public void Place(OfferingInstance instance)
    {
        offering = OfferingInstance.CreateFresh(instance.data, instance.remainingDecayLoops);
    }
}

[DisallowMultipleComponent]
public class AltarManager : SingletonBehaviour<AltarManager>
{
    [Header("Altar Slots (5 Fixed Categories)")]
    [SerializeField] private List<ActiveAltarSlotState> altarSlots = new List<ActiveAltarSlotState>();

    [Header("Retaliation Lantern (เสกป้อมโคมไฟเมื่อถวายของสด)")]
    [Tooltip("Prefab ป้อมโคมไฟหิน (TowerBase + TowerAttackable) — ถ้าไม่ใส่จะไม่เสกป้อม")]
    [SerializeField] private TowerBase retaliationLanternPrefab;
    [SerializeField] private int maxRetaliationLanterns = 4;

    [Header("Global Entity Modifiers (Synchronized to Entities) — อ่านอย่างเดียว")]
    [SerializeField] private float globalEntityAttackMultiplier = 1.0f;
    [SerializeField] private int globalEntityMoveBonus = 0;
    [SerializeField] private bool entityGrantLifedrain = false;

    [Header("Global Shrine & Tower Modifiers (Synchronized to Towers) — อ่านอย่างเดียว")]
    [SerializeField] private float shrineDamageReduction = 0.0f;
    [SerializeField] private bool shrineArmorStripped = false;
    [SerializeField] private float shrinePoisonDamagePerTurn = 0.0f;
    [SerializeField] private bool towerGrantDoubleStrike = false;
    [SerializeField] private int towerPatternSizeDelta = 0;
    [SerializeField] private bool towerIgnoreBlocking = false;
    [SerializeField] private bool towersSkipActive = false;

    [Header("Sweet Fresh Pushback State — อ่านอย่างเดียว")]
    [SerializeField] private int freshPushTiles = 0;
    [SerializeField] private int freshPushIntervalTurns = 2;

    private readonly List<TowerBase> spawnedLanterns = new List<TowerBase>();
    private AbilityData runtimeDoubleStrikeAbility;
    private AbilityData runtimeLifedrainAbility;
    private bool subscribedToTurns;

    public float GlobalEntityAttackMultiplier => globalEntityAttackMultiplier;
    public int GlobalEntityMoveBonus => globalEntityMoveBonus;
    public bool EntityGrantLifedrain => entityGrantLifedrain;

    public float ShrineDamageReduction => shrineDamageReduction;
    public bool ShrineArmorStripped => shrineArmorStripped;
    public float ShrinePoisonDamagePerTurn => shrinePoisonDamagePerTurn;
    public bool TowerGrantDoubleStrike => towerGrantDoubleStrike;
    public int TowerPatternSizeDelta => towerPatternSizeDelta;
    public bool TowerIgnoreBlocking => towerIgnoreBlocking;
    public bool TowersSkipActive => towersSkipActive;

    public IReadOnlyList<ActiveAltarSlotState> AltarSlots => altarSlots;

    public event Action<int, ActiveAltarSlotState> OnSlotUpdated;
    public event Action<OfferingItemData> OnOfferingRotten;
    public event Action OnGlobalMultipliersChanged;
    public event Action OnAltarStateChanged;

    protected override void OnSingletonAwake()
    {
        InitializeDefaultSlots();
        EnsureRuntimeAbilities();
    }

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        }

        TrySubscribeTurnManager();
        RecalculateAllGlobalMultipliers();
    }

    protected override void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }

        if (subscribedToTurns && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStarted -= HandleTurnStarted;
        }

        base.OnDestroy();
    }

    private void TrySubscribeTurnManager()
    {
        if (subscribedToTurns || TurnManager.Instance == null) return;

        TurnManager.Instance.OnTurnStarted += HandleTurnStarted;
        subscribedToTurns = true;
    }

    private void EnsureRuntimeAbilities()
    {
        if (runtimeDoubleStrikeAbility == null)
        {
            runtimeDoubleStrikeAbility = AbilityData.CreateRuntime(
                AbilityKeyword.DoubleStrike,
                "Double Strike",
                "Attacks twice each turn."
            );
        }

        if (runtimeLifedrainAbility == null)
        {
            runtimeLifedrainAbility = AbilityData.CreateRuntime(
                AbilityKeyword.Lifedrain,
                "Lifedrain",
                "Heals HP equal to damage dealt."
            );
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        TrySubscribeTurnManager();

        if (newState == GameState.RoundEnd)
        {
            TickAltarDecay();
        }
    }

    private void HandleTurnStarted(int turn, int totalTurns)
    {
        if (!GameStateManager.IsResolution) return;

        TurnContext ctx = TurnManager.Instance != null ? TurnManager.Instance.Context : null;

        if (shrinePoisonDamagePerTurn > 0f)
        {
            ApplyPoisonToShrines(ctx);
        }

        if (freshPushTiles > 0 && freshPushIntervalTurns > 0 && turn % freshPushIntervalTurns == 0)
        {
            TryPushLeadSpiritBack(freshPushTiles, ctx);
        }
    }

    private void InitializeDefaultSlots()
    {
        if (altarSlots.Count > 0) return;

        foreach (OfferingType type in (OfferingType[])Enum.GetValues(typeof(OfferingType)))
        {
            altarSlots.Add(new ActiveAltarSlotState { slotCategory = type });
        }
    }

    public ActiveAltarSlotState GetSlotByCategory(OfferingType category)
    {
        for (int i = 0; i < altarSlots.Count; i++)
        {
            if (altarSlots[i].slotCategory == category)
            {
                return altarSlots[i];
            }
        }
        return null;
    }

    public bool EquipOffering(OfferingItemData item, int remainingRounds = -1)
    {
        if (item == null) return false;
        if (!GameStateManager.IsPlayerActionPhase) return false;

        ActiveAltarSlotState slot = GetSlotByCategory(item.Category);
        if (slot == null)
        {
            Debug.LogWarning($"[AltarManager] ไม่พบช่องประเภท {item.Category} บนแท่นบูชา!");
            return false;
        }

        OfferingInstance previous = slot.offering;

        slot.Place(item, remainingRounds);
        RecalculateAllGlobalMultipliers();

        if (previous.IsValid && !previous.isRotten && InventorySlotManager.Instance != null)
        {
            InventorySlotManager.Instance.AddItem(previous);
        }

        if (item.SpawnRetaliationLanternOnFresh)
        {
            TrySpawnRetaliationLantern();
        }

        OnSlotUpdated?.Invoke(altarSlots.IndexOf(slot), slot);
        OnAltarStateChanged?.Invoke();
        return true;
    }

    public void TickAltarDecay()
    {
        bool stateChanged = false;

        for (int i = 0; i < altarSlots.Count; i++)
        {
            ActiveAltarSlotState slot = altarSlots[i];
            if (slot.currentItem == null) continue;

            if (slot.isRotten)
            {
                slot.rottenRoundsElapsed++;
                stateChanged = true;
                OnSlotUpdated?.Invoke(i, slot);
                continue;
            }

            slot.remainingDecayLoops--;
            if (slot.remainingDecayLoops <= 0)
            {
                slot.isRotten = true;
                slot.remainingDecayLoops = 0;
                slot.rottenRoundsElapsed = 0;
                stateChanged = true;

                if (slot.currentItem.RottenMutateLeadSpiritImmediately)
                {
                    TriggerLeadSpiritMutation();
                }

                OnOfferingRotten?.Invoke(slot.currentItem);
                Debug.Log($"<color=purple>[FAUST PAYOFF]</color> ของถวาย {slot.currentItem.ItemName} บูดเน่า! ปลดปล่อยคำสาปและปลุกผีคลั่ง!");
            }

            OnSlotUpdated?.Invoke(i, slot);
        }

        if (stateChanged)
        {
            RecalculateAllGlobalMultipliers();
        }

        OnAltarStateChanged?.Invoke();
    }

    public void RecalculateAllGlobalMultipliers()
    {
        EnsureRuntimeAbilities();

        float totalAtkMult = 1.0f;
        int totalMoveBonus = 0;
        bool grantLifedrain = false;

        float damagePassThrough = 1.0f;
        bool stripArmor = false;
        float poisonPerTurn = 0.0f;
        bool grantTowerDouble = false;
        int patternDelta = 0;
        bool ignoreBlocking = false;
        bool skipTowers = false;

        int pushTiles = 0;
        int pushEveryTurns = int.MaxValue;

        for (int i = 0; i < altarSlots.Count; i++)
        {
            ActiveAltarSlotState slot = altarSlots[i];
            OfferingItemData item = slot.currentItem;
            if (item == null) continue;

            if (slot.isRotten)
            {
                totalMoveBonus += item.RottenEntityMoveBonus;

                if (item.RottenGrantEntityLifedrain) grantLifedrain = true;
                if (item.StripShrineArmor) stripArmor = true;

                poisonPerTurn += item.RottenShrinePoisonDamagePerTurn;
                patternDelta += item.RottenTowerPatternSizeDelta;

                if (item.RottenTowersSkipRound && slot.rottenRoundsElapsed < item.RottenTowersSkipDurationRounds)
                {
                    skipTowers = true;
                }
            }
            else
            {
                totalAtkMult *= item.FreshEntityAttackMultiplier;
                damagePassThrough *= (1.0f - Mathf.Clamp01(item.FreshShrineDamageReductionPercent));

                if (item.FreshGrantTowerDoubleStrike) grantTowerDouble = true;
                if (item.FreshTowerIgnoreBlocking) ignoreBlocking = true;

                if (item.FreshPushLeadSpiritTiles > 0)
                {
                    pushTiles += item.FreshPushLeadSpiritTiles;
                    pushEveryTurns = Mathf.Min(pushEveryTurns, item.FreshPushEveryNTurns);
                }
            }
        }

        globalEntityAttackMultiplier = totalAtkMult;
        globalEntityMoveBonus = totalMoveBonus;
        entityGrantLifedrain = grantLifedrain;

        shrineDamageReduction = Mathf.Clamp(1.0f - damagePassThrough, 0.0f, 0.8f);
        shrineArmorStripped = stripArmor;
        shrinePoisonDamagePerTurn = poisonPerTurn;
        towerGrantDoubleStrike = grantTowerDouble;
        towerPatternSizeDelta = patternDelta;
        towerIgnoreBlocking = ignoreBlocking;
        towersSkipActive = skipTowers;

        freshPushTiles = pushTiles;
        freshPushIntervalTurns = pushEveryTurns == int.MaxValue ? 2 : Mathf.Max(1, pushEveryTurns);

        ApplyEffectsToAllEntities();
        ApplyEffectsToAllTowers();

        OnGlobalMultipliersChanged?.Invoke();
    }

    private void ApplyEffectsToAllEntities()
    {
        IReadOnlyList<EntityBase> activeEntities = EntityRegistry.ActiveEntities;
        for (int i = 0; i < activeEntities.Count; i++)
        {
            if (activeEntities[i] != null && !activeEntities[i].IsDead)
            {
                ApplyEffectsToEntity(activeEntities[i]);
            }
        }
    }

    public void ApplyEffectsToEntity(EntityBase entity)
    {
        if (entity == null || entity.IsDead) return;

        EnsureRuntimeAbilities();

        entity.ApplyGlobalMultipliers(globalEntityAttackMultiplier, 1.0f, false);
        entity.SetAltarMoveBonus(globalEntityMoveBonus);

        AbilityHolder holder = entity.Abilities != null ? entity.Abilities : entity.GetComponent<AbilityHolder>();
        if (holder != null)
        {
            AbilityData lifedrainData = GetCustomOrRuntimeEntityAbility();
            holder.Revoke(runtimeLifedrainAbility);
            if (lifedrainData != runtimeLifedrainAbility) holder.Revoke(lifedrainData);

            if (entityGrantLifedrain && lifedrainData != null)
            {
                holder.Grant(lifedrainData, true);
            }
        }
    }

    private void ApplyEffectsToAllTowers()
    {
        IReadOnlyList<TowerBase> towers = TowerBase.ActiveTowers;
        for (int i = towers.Count - 1; i >= 0; i--)
        {
            if (towers[i] != null)
            {
                ApplyEffectsToTower(towers[i]);
            }
        }
    }

    public void ApplyEffectsToTower(TowerBase tower)
    {
        if (tower == null || tower.IsDead) return;

        EnsureRuntimeAbilities();

        if (tower.GetComponent<TowerObjective>() != null)
        {
            tower.SetAltarModifiers(0f, shrineDamageReduction, shrineArmorStripped);
        }

        if (tower.TryGetComponent(out TowerAttackable _))
        {
            AbilityHolder holder = tower.Abilities != null ? tower.Abilities : tower.GetComponent<AbilityHolder>();
            if (holder != null)
            {
                AbilityData doubleStrikeData = GetCustomOrRuntimeTowerAbility();
                holder.Revoke(runtimeDoubleStrikeAbility);
                if (doubleStrikeData != runtimeDoubleStrikeAbility) holder.Revoke(doubleStrikeData);

                if (towerGrantDoubleStrike && doubleStrikeData != null)
                {
                    holder.Grant(doubleStrikeData, true);
                }
            }
        }
    }

    // ---------- Table 4.6 Turn & Trigger Mechanics ----------

    private void ApplyPoisonToShrines(TurnContext ctx)
    {
        if (ObjectiveTracker.Instance == null) return;

        IReadOnlyList<TowerObjective> shrines = ObjectiveTracker.Instance.ActiveTargets;
        for (int i = shrines.Count - 1; i >= 0; i--)
        {
            TowerObjective shrine = shrines[i];
            if (shrine == null || !shrine.IsAlive) continue;

            float dealt = shrine.Tower.ApplyDamage(shrinePoisonDamagePerTurn, true);
            if (dealt > 0f)
            {
                ctx?.MarkAction();
                ctx?.Log($"Rotten curse poisons {shrine.Tower.TowerName} for {dealt:0}");
            }
        }
    }

    private void TryPushLeadSpiritBack(int tiles, TurnContext ctx)
    {
        EntityWalkable lead = FindLeadWalkable(requireMutatable: false);
        if (lead == null) return;

        if (lead.TryPushBack(tiles))
        {
            ctx?.MarkAction();
            ctx?.Log($"Shrine repels {lead.Entity.EntityName} back {tiles} tile(s)");
        }
    }

    private void TriggerLeadSpiritMutation()
    {
        EntityWalkable leadMutatable = FindLeadWalkable(requireMutatable: true);
        if (leadMutatable == null) return;

        if (leadMutatable.TryGetComponent(out EntityMutatable mutatable))
        {
            TurnContext ctx = TurnManager.Instance != null ? TurnManager.Instance.Context : null;
            mutatable.ForceMutate(ctx);
        }
    }

    private static EntityWalkable FindLeadWalkable(bool requireMutatable)
    {
        IReadOnlyList<EntityBase> entities = EntityRegistry.ActiveEntities;
        EntityWalkable best = null;
        int bestSortKey = int.MaxValue;

        for (int i = 0; i < entities.Count; i++)
        {
            EntityBase entity = entities[i];
            if (entity == null || entity.IsDead) continue;
            if (!entity.TryGetComponent(out EntityWalkable walkable) || !walkable.IsActorActive) continue;

            if (requireMutatable)
            {
                if (!entity.TryGetComponent(out EntityMutatable mutatable)
                    || mutatable.MutationCount >= mutatable.MaxMutations)
                {
                    continue;
                }
            }

            int key = walkable.SortKey;
            if (best == null || key < bestSortKey)
            {
                best = walkable;
                bestSortKey = key;
            }
        }

        return best;
    }

    private AbilityData GetCustomOrRuntimeTowerAbility()
    {
        for (int i = 0; i < altarSlots.Count; i++)
        {
            ActiveAltarSlotState slot = altarSlots[i];
            if (slot.currentItem != null && !slot.isRotten
                && slot.currentItem.FreshGrantTowerDoubleStrike
                && slot.currentItem.FreshTowerAbility != null)
            {
                return slot.currentItem.FreshTowerAbility;
            }
        }
        return runtimeDoubleStrikeAbility;
    }

    private AbilityData GetCustomOrRuntimeEntityAbility()
    {
        for (int i = 0; i < altarSlots.Count; i++)
        {
            ActiveAltarSlotState slot = altarSlots[i];
            if (slot.currentItem != null && slot.isRotten
                && slot.currentItem.RottenGrantEntityLifedrain
                && slot.currentItem.RottenEntityAbility != null)
            {
                return slot.currentItem.RottenEntityAbility;
            }
        }
        return runtimeLifedrainAbility;
    }

    private void TrySpawnRetaliationLantern()
    {
        if (retaliationLanternPrefab == null) return;

        spawnedLanterns.RemoveAll(t => t == null || t.IsDead);
        if (spawnedLanterns.Count >= maxRetaliationLanterns) return;

        LoopPathManager path = LoopPathManager.Instance;
        if (path == null || ObjectiveTracker.Instance == null) return;

        IReadOnlyList<TowerObjective> shrines = ObjectiveTracker.Instance.ActiveTargets;
        if (shrines.Count == 0) return;

        int startIdx = GameRandom.Range(0, shrines.Count);
        for (int s = 0; s < shrines.Count; s++)
        {
            TowerObjective shrine = shrines[(startIdx + s) % shrines.Count];
            if (shrine == null || !shrine.IsAlive || shrine.Tower.CurrentTile == null) continue;

            List<HexTile> neighbors = path.GetNeighbors(shrine.Tower.CurrentTile.Coordinates);
            List<HexTile> validTiles = new List<HexTile>(6);
            for (int n = 0; n < neighbors.Count; n++)
            {
                if (retaliationLanternPrefab.CanPlaceOnTile(neighbors[n], path))
                {
                    validTiles.Add(neighbors[n]);
                }
            }

            if (validTiles.Count == 0) continue;

            HexTile chosen = validTiles[GameRandom.Range(0, validTiles.Count)];
            int bestFacing = ThreatPlacementValidator.FindBestFacing(retaliationLanternPrefab, chosen);

            TowerBase lantern = Instantiate(retaliationLanternPrefab, chosen.transform.position, Quaternion.identity);
            lantern.SetFacing(bestFacing);

            if (!chosen.PlaceTower(lantern))
            {
                Destroy(lantern.gameObject);
                continue;
            }

            spawnedLanterns.Add(lantern);
            Debug.Log("<color=orange>[FAUST]</color> ศาลเจ้าตื่นตัว! เสกโคมไฟหินอาคมป้องกันศาล");
            return;
        }
    }
}
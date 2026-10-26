using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สถานะของช่องวางของถวายแต่ละหมวดหมู่บนโต๊ะหมู่บูชา
/// เก็บเฉพาะ "สถานะที่เปลี่ยนตามเวลา" (ไอเทม, อายุที่เหลือ, เน่าหรือยัง)
/// ค่าบัฟ/คำสาปทั้งหมดอ่านจาก OfferingItemData ตอนคำนวณ จึงไม่มีค่าซ้ำให้ซิงก์พลาด
/// หมายเหตุ: remainingDecayLoops นับเป็น Round (ชื่อฟิลด์คงไว้เพื่อไม่ให้ Scene/Asset เดิมพัง)
/// </summary>
[Serializable]
public class ActiveAltarSlotState
{
    public OfferingType slotCategory;
    public OfferingItemData currentItem;
    public int remainingDecayLoops;
    public bool isRotten;

    public bool HasOffering => currentItem != null;

    /// <summary>วางของสดลงช่อง (รีเซ็ตอายุและสถานะเน่า)</summary>
    public void Place(OfferingItemData item)
    {
        currentItem = item;
        remainingDecayLoops = item.DecayLoops;
        isRotten = false;
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
    [SerializeField] private float globalEntitySpeedMultiplier = 1.0f;
    [SerializeField] private bool isGlobalFrenzyActive = false;

    [Header("Global Shrine Modifiers (Synchronized to Towers) — อ่านอย่างเดียว")]
    [SerializeField] private float shrineDefenseBonus = 0.0f;
    [SerializeField] private float shrineDamageReduction = 0.0f;
    [SerializeField] private float towerAttackIntervalMultiplier = 1.0f;
    [SerializeField] private bool shrineArmorStripped = false;

    private readonly List<TowerBase> spawnedLanterns = new List<TowerBase>();

    public float GlobalEntityAttackMultiplier => globalEntityAttackMultiplier;
    public float GlobalEntitySpeedMultiplier => globalEntitySpeedMultiplier;
    public bool IsGlobalFrenzyActive => isGlobalFrenzyActive;
    public IReadOnlyList<ActiveAltarSlotState> AltarSlots => altarSlots;

    public event Action<int, ActiveAltarSlotState> OnSlotUpdated;
    public event Action<OfferingItemData> OnOfferingRotten;
    public event Action OnGlobalMultipliersChanged;
    public event Action OnAltarStateChanged;

    protected override void OnSingletonAwake()
    {
        InitializeDefaultSlots();
    }

    private void Start()
    {
        // v2: อายุของถวายลดตอน RoundEnd (ไม่ผูกกับธูปซึ่งเป็นแค่ตัวแสดงผลแล้ว)
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        }
    }

    protected override void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }

        base.OnDestroy();
    }

    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.RoundEnd)
        {
            TickAltarDecay();
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

    /// <summary>
    /// วางของถวายลงในช่องบูชา (ทำได้เฉพาะ Dawn / Planning)
    /// หากมีของเดิมอยู่: ของที่ยัง "สด" จะถูกคืนเข้ากระเป๋า, ของที่ "เน่าแล้ว" จะสลายทิ้ง
    /// (ไม่คืนของเน่า เพราะกระเป๋าแสดงเป็นของสด จะกลายเป็นช่องโหว่รีเซ็ตอายุของ)
    /// </summary>
    public bool EquipOffering(OfferingItemData item)
    {
        if (item == null) return false;
        if (!GameStateManager.IsPlayerActionPhase) return false;

        ActiveAltarSlotState slot = GetSlotByCategory(item.Category);
        if (slot == null)
        {
            Debug.LogWarning($"[AltarManager] ไม่พบช่องประเภท {item.Category} บนแท่นบูชา!");
            return false;
        }

        OfferingItemData previousItem = slot.currentItem;
        bool previousWasRotten = slot.isRotten;

        slot.Place(item);
        RecalculateAllGlobalMultipliers();

        // คืนของสดชิ้นเดิมเข้ากระเป๋า
        if (previousItem != null && !previousWasRotten && InventorySlotManager.Instance != null)
        {
            InventorySlotManager.Instance.AddItem(previousItem);
        }

        if (item.SpawnRetaliationLanternOnFresh)
        {
            TrySpawnRetaliationLantern();
        }

        OnSlotUpdated?.Invoke(altarSlots.IndexOf(slot), slot);
        OnAltarStateChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// ลดอายุของถวายสดทุกช่อง 1 Round (เรียกอัตโนมัติตอนเข้า RoundEnd) ช่องที่อายุหมดจะเน่า
    /// </summary>
    public void TickAltarDecay()
    {
        bool anyRotted = false;

        for (int i = 0; i < altarSlots.Count; i++)
        {
            ActiveAltarSlotState slot = altarSlots[i];
            if (slot.currentItem == null || slot.isRotten) continue;

            slot.remainingDecayLoops--;
            if (slot.remainingDecayLoops <= 0)
            {
                slot.isRotten = true;
                slot.remainingDecayLoops = 0;
                anyRotted = true;

                OnOfferingRotten?.Invoke(slot.currentItem);
                Debug.Log($"<color=purple>[FAUST PAYOFF]</color> ของถวาย {slot.currentItem.ItemName} บูดเน่า! ปลดปล่อยคำสาปและปลุกผีคลั่ง!");
            }

            OnSlotUpdated?.Invoke(i, slot);
        }

        if (anyRotted)
        {
            RecalculateAllGlobalMultipliers();
        }

        OnAltarStateChanged?.Invoke();
    }

    /// <summary>
    /// คำนวณผลรวมของทุกช่องใหม่จากไอเทมในช่อง แล้วกระจายให้
    /// - ผีทุกตัว (ฝั่งของเน่า: ตัวคูณโจมตี/ความเร็ว/คลั่ง)
    /// - ศาลเจ้าและป้อมทุกหลังบนกระดาน (ฝั่งของสด: เกราะ/ลดดาเมจ/ยิงถี่ และฝั่งของเน่า: เกราะถูกกัดกร่อน)
    /// </summary>
    public void RecalculateAllGlobalMultipliers()
    {
        float totalAtkMult = 1.0f;
        float totalSpdMult = 1.0f;
        bool hasFrenzy = false;

        float defenseBonus = 0.0f;
        float damagePassThrough = 1.0f;
        float intervalMult = 1.0f;
        bool stripArmor = false;

        for (int i = 0; i < altarSlots.Count; i++)
        {
            ActiveAltarSlotState slot = altarSlots[i];
            OfferingItemData item = slot.currentItem;
            if (item == null) continue;

            if (slot.isRotten)
            {
                totalAtkMult *= item.RottenEntityAttackMultiplier;
                totalSpdMult *= item.RottenEntitySpeedMultiplier;
                if (item.TriggersFrenzy) hasFrenzy = true;
                if (item.StripShrineArmor) stripArmor = true;
            }
            else
            {
                defenseBonus += item.FreshShrineDefenseBonus;
                damagePassThrough *= (1.0f - Mathf.Clamp01(item.FreshShrineDamageReductionPercent));
                intervalMult *= Mathf.Max(0.1f, item.FreshTowerAttackSpeedMultiplier);
            }
        }

        globalEntityAttackMultiplier = totalAtkMult;
        globalEntitySpeedMultiplier = totalSpdMult;
        isGlobalFrenzyActive = hasFrenzy;

        shrineDefenseBonus = defenseBonus;
        shrineDamageReduction = Mathf.Clamp(1.0f - damagePassThrough, 0.0f, 0.8f);
        towerAttackIntervalMultiplier = intervalMult;
        shrineArmorStripped = stripArmor;

        // กระจายค่าให้ผีทั้งหมดในฉาก
        IReadOnlyList<EntityBase> activeEntities = EntityRegistry.ActiveEntities;
        for (int i = 0; i < activeEntities.Count; i++)
        {
            if (activeEntities[i] != null && !activeEntities[i].IsDead)
            {
                activeEntities[i].ApplyGlobalMultipliers(globalEntityAttackMultiplier, globalEntitySpeedMultiplier, isGlobalFrenzyActive);
            }
        }

        // กระจายค่าให้ศาลเจ้าและป้อมทุกหลัง
        ApplyEffectsToAllTowers();

        OnGlobalMultipliersChanged?.Invoke();
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

    /// <summary>
    /// ใส่ผลของโต๊ะบูชาให้สิ่งก่อสร้าง 1 หลัง (เรียกอัตโนมัติจาก TowerBase.OnPlaced สำหรับป้อมที่วางทีหลัง)
    /// - ศาลเจ้า (TowerObjective): โบนัสเกราะ + ลดดาเมจ / เกราะถูกกัดกร่อนเมื่อของเน่า
    /// - ป้อมยิง (TowerAttackable): ความถี่ในการยิง
    /// </summary>
    public void ApplyEffectsToTower(TowerBase tower)
    {
        if (tower == null) return;

        if (tower.GetComponent<TowerObjective>() != null)
        {
            tower.SetAltarModifiers(shrineDefenseBonus, shrineDamageReduction, shrineArmorStripped);
        }

        TowerAttackable attacker = tower.GetComponent<TowerAttackable>();
        if (attacker != null)
        {
            attacker.SetFireRateMultiplier(1.0f / Mathf.Max(0.1f, towerAttackIntervalMultiplier));
        }
    }

    /// <summary>
    /// เสกป้อมโคมไฟหินบนช่อง Ground รอบศาลเจ้าที่ยังอยู่ (FAUST: ถวายของสด = ศาลตื่นตัว)
    /// ใช้ GameRandom (ไม่ใช่ UnityEngine.Random) ตามกติกา Seeded RNG ของ v2
    /// </summary>
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
            TowerBase lantern = Instantiate(retaliationLanternPrefab, chosen.transform.position, Quaternion.identity);

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
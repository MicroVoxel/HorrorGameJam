using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวถือ Keyword ของผีหรือสิ่งก่อสร้าง 1 ตัว (ติดบน Prefab ข้าง EntityBase / TowerBase)
/// - Base Abilities: ตั้งใน Prefab (ตัดที่ maxKeywordsPerUnit)
/// - Granted: ได้มาระหว่างเกม เช่น จากของถวายที่เน่า (Grant / Revoke)
/// ทำหน้าที่เองเรื่อง Barrier (รีเซ็ตทุก Round), Regeneration (ต้นเทิร์น) และส่ง Last Breath เข้าคิว AbilityResolver
/// </summary>
[DisallowMultipleComponent]
public class AbilityHolder : MonoBehaviour
{
    [SerializeField] private List<AbilityData> baseAbilities = new List<AbilityData>();

    private readonly List<AbilityData> granted = new List<AbilityData>(2);
    private readonly List<AbilityData> all = new List<AbilityData>(4);

    private EntityBase entity;
    private TowerBase tower;
    private bool dirty = true;
    private bool barrierReady = true;
    private bool subscribedRound;
    private bool subscribedTurns;

    public event Action OnAbilitiesChanged;

    private int Cap => Mathf.Max(1, GameStateManager.Instance != null ? GameStateManager.Instance.Rules.MaxKeywordsPerUnit : 2);

    public IReadOnlyList<AbilityData> All
    {
        get { EnsureBuilt(); return all; }
    }

    /// <summary>Barrier ยังพร้อมกันดาเมจครั้งแรกของ Round นี้</summary>
    public bool BarrierReady => barrierReady && Has(AbilityKeyword.Barrier);

    // ---------- Queries ----------

    public bool Has(AbilityKeyword keyword)
    {
        EnsureBuilt();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Keyword == keyword) return true;
        }
        return false;
    }

    public AbilityData Get(AbilityKeyword keyword)
    {
        EnsureBuilt();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Keyword == keyword) return all[i];
        }
        return null;
    }

    /// <summary>ค่า N ของ Keyword (ไม่มี = 0)</summary>
    public int GetValue(AbilityKeyword keyword)
    {
        AbilityData data = Get(keyword);
        return data != null ? data.Value : 0;
    }

    // ---------- Grant / Revoke ----------

    /// <summary>เพิ่ม Keyword ชั่วคราว (เช่น จากของถวาย) คืน false ถ้าซ้ำหรือเต็มโควตา (ignoreLimit = true ข้ามโควตา)</summary>
    public bool Grant(AbilityData data, bool ignoreLimit = false)
    {
        if (data == null) return false;

        EnsureBuilt();
        if (Has(data.Keyword)) return false;
        if (!ignoreLimit && all.Count >= Cap) return false;

        granted.Add(data);
        dirty = true;
        OnAbilitiesChanged?.Invoke();
        return true;
    }

    public bool Revoke(AbilityData data)
    {
        if (data == null || !granted.Remove(data)) return false;

        dirty = true;
        OnAbilitiesChanged?.Invoke();
        return true;
    }

    public void RevokeAllGranted()
    {
        if (granted.Count == 0) return;

        granted.Clear();
        dirty = true;
        OnAbilitiesChanged?.Invoke();
    }

    // ---------- Barrier ----------

    /// <summary>ใช้ Barrier กันดาเมจ 1 ครั้ง คืน true ถ้ากันได้ (เรียกโดย EntityBase / TowerBase)</summary>
    public bool TryConsumeBarrier()
    {
        if (!barrierReady || !Has(AbilityKeyword.Barrier)) return false;

        barrierReady = false;
        return true;
    }

    // ---------- Lifecycle ----------

    private void Awake()
    {
        entity = GetComponent<EntityBase>();
        tower = GetComponent<TowerBase>();
    }

    private void OnEnable()
    {
        if (entity != null) entity.OnEntityDied += HandleEntityDied;
        if (tower != null) tower.OnTowerDestroyed += HandleTowerDestroyed;
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (entity != null) entity.OnEntityDied -= HandleEntityDied;
        if (tower != null) tower.OnTowerDestroyed -= HandleTowerDestroyed;

        if (subscribedRound && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted -= HandleRoundStarted;
        }

        if (subscribedTurns && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStarted -= HandleTurnStarted;
        }

        subscribedRound = false;
        subscribedTurns = false;
    }

    private void TrySubscribe()
    {
        if (!subscribedRound && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted += HandleRoundStarted;
            subscribedRound = true;
        }

        if (!subscribedTurns && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStarted += HandleTurnStarted;
            subscribedTurns = true;
        }
    }

    private void HandleRoundStarted(int round, int turns)
    {
        barrierReady = true;
    }

    private void HandleTurnStarted(int turn, int totalTurns)
    {
        int n = GetValue(AbilityKeyword.Regeneration);
        if (n <= 0 || !Has(AbilityKeyword.Regeneration)) return;

        if (entity != null && !entity.IsDead) entity.Heal(n);
        else if (tower != null && !tower.IsDead) tower.Heal(n);
    }

    private void HandleEntityDied(EntityBase dead)
    {
        AbilityData data = Get(AbilityKeyword.LastBreath);
        if (data == null || dead == null) return;

        AbilityResolver.QueueLastBreath(data, dead.LastCoordinates, dead.Facing, true, dead.EntityName);
    }

    private void HandleTowerDestroyed(TowerBase dead)
    {
        AbilityData data = Get(AbilityKeyword.LastBreath);
        if (data == null || dead == null) return;

        AbilityResolver.QueueLastBreath(data, dead.LastCoordinates, dead.Facing, false, dead.TowerName);
    }

    // ---------- Internals ----------

    private void EnsureBuilt()
    {
        if (!dirty) return;

        all.Clear();
        int cap = Cap;

        for (int i = 0; i < baseAbilities.Count; i++)
        {
            AbilityData a = baseAbilities[i];
            if (a == null || ContainsKeyword(a.Keyword)) continue;
            if (all.Count >= cap) break;
            all.Add(a);
        }

        for (int i = 0; i < granted.Count; i++)
        {
            if (!ContainsKeyword(granted[i].Keyword)) all.Add(granted[i]);
        }

        dirty = false;
    }

    private bool ContainsKeyword(AbilityKeyword keyword)
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Keyword == keyword) return true;
        }
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        dirty = true;
    }
#endif
}

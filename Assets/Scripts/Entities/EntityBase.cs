using System;
using UnityEngine;

/// <summary>
/// โครงสร้างฐานของยูนิตขบวนร้อยอสูร (Entity Base)
/// v2: มี AP ต่อเทิร์น, Move Speed (ช่องต่อ 1 Move), ทิศหน้า (Facing 0-5) และสูตรดาเมจ
/// </summary>
[DisallowMultipleComponent]
[SelectionBase]
public class EntityBase : MonoBehaviour, IDamageable
{
    [Header("Entity Identity & Base Stats")]
    [SerializeField] private string entityName = "Skeleton";
    [SerializeField] private float maxHealth = 30.0f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float defense = 0.0f;

    [Header("Turn Stats")]
    [Tooltip("Action points per turn. Move costs 1, Attack costs 1, Skill costs 2.")]
    [SerializeField, Min(0)] private int baseActionPoints = 2;
    [Tooltip("Tiles travelled by one Move action.")]
    [SerializeField, Min(1)] private int baseMoveSpeed = 1;
    [Tooltip("Rotation offset (degrees) if the model does not face +Z at yaw 0.")]
    [SerializeField] private float facingYawOffset = 0f;

    [Header("Global & Frenzy Modifiers")]
    [SerializeField] private float attackMultiplier = 1.0f;
    [SerializeField] private float speedMultiplier = 1.0f;
    [SerializeField] private bool isFrenzied = false;

    [Header("Board Occupancy (1 Tile = 1 Entity)")]
    [SerializeField] private HexTile occupiedTile;

    [Header("Destruction Feedback")]
    [SerializeField] private GameObject deathVfxPrefab;

    private int apBonus;
    private int moveBonus;
    private int altarMoveBonus;
    private int currentAp;
    private int facing;
    private AbilityHolder abilities;

    public string EntityName => entityName;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    public float BaseDefense => defense;
    /// <summary>DEF จริง (รวม Tough N)</summary>
    public float EffectiveDefense => defense + (abilities != null && abilities.Has(AbilityKeyword.Tough) ? abilities.GetValue(AbilityKeyword.Tough) : 0);

    public bool IsDead => currentHealth <= 0.0f;
    public Vector3 Position => transform.position;

    public float AttackMultiplier => attackMultiplier;
    public float SpeedMultiplier => speedMultiplier;
    public bool IsFrenzied => isFrenzied;
    public HexTile OccupiedTile => occupiedTile;
    public AbilityHolder Abilities => abilities;
    public HexCoordinates LastCoordinates { get; private set; }

    // ---------- Turn stats ----------
    public int MaxActionPoints => baseActionPoints + apBonus + (isFrenzied ? 1 : 0);
    public int MoveSpeed => baseMoveSpeed + moveBonus + altarMoveBonus;
    public int CurrentActionPoints => currentAp;
    public int Facing => facing;
    public bool IsBlocked { get; private set; }

    public event Action<float, float> OnHealthChanged;
    public event Action<EntityBase> OnEntityDied;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        abilities = GetComponent<AbilityHolder>();
    }

    protected virtual void OnEnable()
    {
        EntityRegistry.Register(this);
        SyncWithAltarManager();
    }

    protected virtual void OnDisable()
    {
        EntityRegistry.Unregister(this);
    }

    public void SyncWithAltarManager()
    {
        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.ApplyEffectsToEntity(this);
        }
    }

    public void SetAltarMoveBonus(int amount)
    {
        altarMoveBonus = Mathf.Max(0, amount);
    }

    public void ApplyGlobalMultipliers(float atkMult, float spdMult, bool frenzy)
    {
        attackMultiplier = Mathf.Max(0.1f, atkMult);
        speedMultiplier = Mathf.Max(0.1f, spdMult);
        isFrenzied = frenzy;
    }

    // ---------- Turn API ----------

    public void BeginTurn()
    {
        currentAp = MaxActionPoints;
        IsBlocked = false;
    }

    public bool TrySpendActionPoints(int cost)
    {
        if (cost < 0 || currentAp < cost) return false;

        currentAp -= cost;
        return true;
    }

    public void DrainActionPoints(bool blocked)
    {
        currentAp = 0;
        IsBlocked = blocked;
    }

    public void AddMoveBonus(int amount) => moveBonus = Mathf.Max(0, moveBonus + amount);
    public void AddActionPointBonus(int amount) => apBonus = Mathf.Max(0, apBonus + amount);

    public void SetFacing(int newFacing, bool rotateVisual = true)
    {
        facing = HexCoordinates.NormalizeFacing(newFacing);

        if (rotateVisual)
        {
            transform.rotation = Quaternion.Euler(0f, HexCoordinates.FacingToYaw(facing) + facingYawOffset, 0f);
        }
    }

    // ---------- Board occupancy ----------

    public void SetOccupiedTile(HexTile newTile)
    {
        if (occupiedTile != null && occupiedTile.CurrentEntity == this)
        {
            occupiedTile.ClearEntity(this);
        }

        occupiedTile = newTile;

        if (occupiedTile != null)
        {
            occupiedTile.SetEntity(this);
            LastCoordinates = occupiedTile.Coordinates;
        }
    }

    // ---------- Health ----------

    public void HealFullAndScaleMaxHealth(float multiplier)
    {
        if (IsDead) return;
        float scaledMax = maxHealth * Mathf.Max(1.0f, multiplier);
        ModifyMaxHealth(scaledMax, true);
    }

    public void ModifyMaxHealth(float newMaxHealth, bool healToFull = false)
    {
        maxHealth = Mathf.Max(1.0f, newMaxHealth);
        if (healToFull)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public float ApplyDamage(float rawAmount, bool ignoreDefense = false)
    {
        return ApplyDamage(rawAmount, ignoreDefense, out _);
    }

    public float ApplyDamage(float rawAmount, bool ignoreDefense, out float overkill)
    {
        overkill = 0f;
        if (IsDead) return 0f;

        if (abilities != null && abilities.TryConsumeBarrier()) return 0f;

        float baseDamage = Mathf.Floor(rawAmount);
        float full = Mathf.Max(1.0f, ignoreDefense ? baseDamage : baseDamage - EffectiveDefense);
        float dealt = Mathf.Min(full, currentHealth);
        overkill = Mathf.Max(0f, full - dealt);

        currentHealth = Mathf.Max(0.0f, currentHealth - dealt);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0.0f)
        {
            Die();
        }

        return dealt;
    }

    public virtual void TakeDamage(float amount)
    {
        ApplyDamage(amount, false);
    }

    public virtual void Heal(float amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    protected virtual void Die()
    {
        if (occupiedTile != null && occupiedTile.CurrentEntity == this)
        {
            occupiedTile.ClearEntity(this);
            occupiedTile = null;
        }

        EntityRegistry.Unregister(this);

        OnEntityDied?.Invoke(this);

        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
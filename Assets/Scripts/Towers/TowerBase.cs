using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คลาสฐานของสิ่งก่อสร้างทุกชนิดบนกระดาน Hex (ศาลเจ้า, ป้อมโคมไฟ, รังผี)
/// จัดการ HP, DEF, กฎการวาง, ทิศหน้า (Facing 0-5) และรับผลจากโต๊ะบูชาผ่าน SetAltarModifiers
/// สูตรดาเมจ: max(1, floor(raw x (1 - ลดดาเมจ)) - DEF) ; ignoreDefense (Armor Piercing) ข้ามทั้งสองส่วน
/// 4B: Barrier / Tough N / Heal (Lifedrain, Regeneration) / PlayerPlaced (นับเป็น Threat Tower) / LastCoordinates
/// </summary>
[DisallowMultipleComponent]
public class TowerBase : MonoBehaviour, IDamageable
{
    // ทะเบียนสิ่งก่อสร้างที่ "วางลงบนกระดานจริงแล้ว" (ไม่รวม Ghost Preview)
    private static readonly List<TowerBase> activeTowers = new List<TowerBase>(32);
    private static int placementCounter;
    public static IReadOnlyList<TowerBase> ActiveTowers => activeTowers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeTowers.Clear();
        placementCounter = 0;
    }

    [Header("Tower Identity & Placement Rules")]
    [SerializeField] private string towerName = "Structure";
    [SerializeField] private List<HexGroundType> allowedGroundTypes = new List<HexGroundType> { HexGroundType.Ground };
    [Tooltip("บังคับว่าต้องวางติดกับช่องทางเดิน Path เท่านั้น (เช่น ศาลเจ้า, รังผีข้างทาง)")]
    [SerializeField] private bool requireAdjacentPath = true;

    [Header("Base Stats")]
    [SerializeField] private float maxHealth = 100.0f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float defense = 1.0f;

    [Header("Facing")]
    [Tooltip("ทิศหน้า 0-5 (ตรงกับ HexCoordinates.Directions) ตั้งก่อนวางลงกระดาน หลังวางทิศล็อก")]
    [SerializeField, Range(0, 5)] private int facing = 0;
    [Tooltip("On = หมุนโมเดลตามทิศหน้า (ป้อมยิงที่มี Range ขึ้นกับทิศ) Off = ศาลและรังผีไม่ต้องหมุน")]
    [SerializeField] private bool rotateModelWithFacing = false;
    [SerializeField] private float facingYawOffset = 0f;

    [Header("Runtime Altar Modifiers (ตั้งค่าโดย AltarManager — ไม่ต้องแก้เอง)")]
    [SerializeField] private float altarDefenseBonus = 0.0f;
    [Range(0f, 0.8f)]
    [SerializeField] private float altarDamageReduction = 0.0f;
    [SerializeField] private bool altarArmorStripped = false;

    private bool isShrineSide = true;
    private AbilityHolder abilities;

    public string TowerName => towerName;
    public HexTile CurrentTile { get; private set; }
    public bool RequireAdjacentPath => requireAdjacentPath;
    public bool RotateModelWithFacing => rotateModelWithFacing;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    public float BaseDefense => defense;
    /// <summary>เกราะจริงหลังรวม Tough N และบัฟโต๊ะบูชา (ถ้าถูกกัดกร่อนจะเป็น 0)</summary>
    public float EffectiveDefense => altarArmorStripped ? 0.0f : defense + altarDefenseBonus + ToughBonus;
    public float EffectiveDamageReduction => altarArmorStripped ? 0.0f : altarDamageReduction;
    public bool IsArmorStripped => altarArmorStripped;

    private float ToughBonus => abilities != null && abilities.Has(AbilityKeyword.Tough) ? abilities.GetValue(AbilityKeyword.Tough) : 0f;

    public Vector3 Position => transform.position;
    public bool IsDead => currentHealth <= 0.0f;

    public int Facing => facing;
    public AbilityHolder Abilities => abilities;

    /// <summary>ลำดับการวาง (เลขน้อย = วางก่อน) ใช้เป็น SortKey ของป้อมและรังผีใน TurnManager</summary>
    public int PlacementOrder { get; private set; }

    /// <summary>true = ฝั่งศาล (ศาล ป้อม) ผีโจมตีได้ / false = ฝั่งผี (รังผี) ป้อมและผีไม่โจมตี</summary>
    public bool IsShrineSide => isShrineSide;

    /// <summary>ผู้เล่นวางเองจากการ์ด (ป้อมโจมตีที่ผู้เล่นวางนับเป็น Threat Tower) ป้อมที่ศาลเสกเองเป็น false</summary>
    public bool IsPlayerPlaced { get; private set; }

    /// <summary>พิกัดช่องที่เคยวางอยู่ (ยังอ่านได้หลังถูกทำลาย ใช้กับ Last Breath)</summary>
    public HexCoordinates LastCoordinates { get; private set; }

    public event Action<TowerBase> OnPlacedOnBoard;
    public event Action<TowerBase, float> OnHealthChanged;
    public event Action<TowerBase> OnTowerDestroyed;

    private void Awake()
    {
        currentHealth = maxHealth;
        isShrineSide = GetComponent<TowerSpawnable>() == null;
        abilities = GetComponent<AbilityHolder>();

        if (allowedGroundTypes == null || allowedGroundTypes.Count == 0)
        {
            allowedGroundTypes = new List<HexGroundType> { HexGroundType.Ground };
        }
    }

    private void OnDestroy()
    {
        activeTowers.Remove(this);
    }

    /// <summary>เรียกก่อน PlaceTower เมื่อผู้เล่นเป็นคนวาง (นับเป็น Threat Tower ถ้าเป็นป้อมโจมตี)</summary>
    public void MarkPlayerPlaced()
    {
        IsPlayerPlaced = true;
    }

    /// <summary>ตั้งทิศหน้า (ใช้ตอนวาง: Ghost หมุนด้วย Q / E แล้วส่งค่ามาที่นี่ก่อน PlaceTower)</summary>
    public void SetFacing(int newFacing)
    {
        facing = HexCoordinates.NormalizeFacing(newFacing);
        ApplyFacingRotation();
    }

    private void ApplyFacingRotation()
    {
        if (!rotateModelWithFacing) return;

        transform.rotation = Quaternion.Euler(0f, HexCoordinates.FacingToYaw(facing) + facingYawOffset, 0f);
    }

    public virtual bool CanPlaceOn(HexGroundType groundType)
    {
        return allowedGroundTypes.Contains(groundType);
    }

    /// <summary>
    /// ตรวจสอบความถูกต้องของช่อง Hex ทั้งประเภทพื้นผิว, กฎติดทางเดิน และกฎห้ามวางศาลเจ้าติดกัน
    /// (เหตุผลละเอียดและ Valid Threat Check อยู่ที่ ThreatPlacementValidator)
    /// </summary>
    public bool CanPlaceOnTile(HexTile tile, LoopPathManager pathManager)
    {
        if (tile == null || tile.HasTower) return false;
        if (!CanPlaceOn(tile.GroundType)) return false;

        if (pathManager != null)
        {
            if (requireAdjacentPath && !pathManager.IsAdjacentToPath(tile.Coordinates))
            {
                return false;
            }

            TowerObjective defenseComp = GetComponent<TowerObjective>();
            if (defenseComp != null && defenseComp.PreventAdjacentDefenseBases)
            {
                if (pathManager.HasAdjacentDefenseBase(tile.Coordinates))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public virtual void OnPlaced(HexTile tile)
    {
        CurrentTile = tile;
        LastCoordinates = tile.Coordinates;
        transform.position = tile.transform.position;
        ApplyFacingRotation();

        if (PlacementOrder == 0)
        {
            PlacementOrder = ++placementCounter;
        }

        if (!activeTowers.Contains(this))
        {
            activeTowers.Add(this);
        }

        // สิ่งก่อสร้างที่วางทีหลัง (เช่น ป้อมที่ถูกเสก) ต้องได้รับบัฟ/คำสาปที่โต๊ะบูชามีอยู่ในตอนนี้ทันที
        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.ApplyEffectsToTower(this);
        }

        OnPlacedOnBoard?.Invoke(this);
    }

    public virtual void OnRemoved()
    {
        activeTowers.Remove(this);
        CurrentTile = null;
    }

    /// <summary>
    /// รับผลจากโต๊ะบูชา: โบนัสเกราะ, สัดส่วนลดดาเมจ (0-0.8) และสถานะเกราะถูกกัดกร่อนจากของเน่า
    /// </summary>
    public void SetAltarModifiers(float defenseBonus, float damageReduction, bool armorStripped)
    {
        altarDefenseBonus = Mathf.Max(0.0f, defenseBonus);
        altarDamageReduction = Mathf.Clamp(damageReduction, 0.0f, 0.8f);
        altarArmorStripped = armorStripped;
    }

    /// <summary>
    /// ทำดาเมจและคืนค่าที่เกิดขึ้นจริง ignoreDefense = true (Armor Piercing) ข้าม DEF และส่วนลดดาเมจ ขั้นต่ำ 1
    /// </summary>
    public float ApplyDamage(float rawAmount, bool ignoreDefense = false)
    {
        return ApplyDamage(rawAmount, ignoreDefense, out _);
    }

    /// <summary>เหมือนด้านบน แต่คืน overkill (ดาเมจที่เกินเลือดที่เหลือ) ให้ Overrun ส่งต่อ Barrier กันได้ 1 ครั้งต่อ Round</summary>
    public float ApplyDamage(float rawAmount, bool ignoreDefense, out float overkill)
    {
        overkill = 0f;
        if (IsDead) return 0f;

        if (abilities != null && abilities.TryConsumeBarrier()) return 0f;

        float full;
        if (ignoreDefense)
        {
            full = Mathf.Max(1.0f, Mathf.Floor(rawAmount));
        }
        else
        {
            float afterReduction = Mathf.Floor(rawAmount * (1.0f - EffectiveDamageReduction));
            full = Mathf.Max(1.0f, afterReduction - EffectiveDefense);
        }

        float dealt = Mathf.Min(full, currentHealth);
        overkill = Mathf.Max(0f, full - dealt);
        currentHealth = Mathf.Max(0.0f, currentHealth - dealt);

        OnHealthChanged?.Invoke(this, currentHealth);

        if (currentHealth <= 0.0f)
        {
            DestroyTower();
        }

        return dealt;
    }

    public virtual void TakeDamage(float rawAmount)
    {
        ApplyDamage(rawAmount, false);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(this, currentHealth);
    }

    public void ModifyDefense(float delta)
    {
        defense = Mathf.Max(0.0f, defense + delta);
    }

    private void DestroyTower()
    {
        if (CurrentTile != null)
        {
            CurrentTile.RemoveTower();
        }

        OnTowerDestroyed?.Invoke(this);
        Destroy(gameObject);
    }
}
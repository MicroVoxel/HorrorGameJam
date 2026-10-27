using UnityEngine;

/// <summary>
/// ข้อมูลไอเทมของถวายช่องเดี่ยว (Single-Slot Offering Item) v2 ตามตาราง 4.6:
/// - Drink (Red Soda, อายุ 2): สด = ป้อมทุกหลังได้ Double Strike | เน่า = ผีทุกตัวได้ Lifedrain และ +1 Move (Berserk)
/// - Savory (Pork Belly, อายุ 2): สด = ดาเมจที่ศาลรับ x0.5 (ก่อนหัก DEF) | เน่า = DEF ศาล = 0 และพิษลด HP ศาลทุกเทิร์น (2)
/// - Sweet (Sweet Onigiri, อายุ 3): สด = ศาลผลักผีหัวขบวนถอย 1 ช่องทุก 2 เทิร์น | เน่า = ผีหัวขบวนที่มี EntityMutatable กลายพันธุ์ทันที
/// - Fragrance (Traditional Scent, อายุ 1): สด = ดาเมจของผี x0.8 | เน่า = ขนาด N ของ Pattern ป้อมทุกหลังลด 1 (ต่ำสุด 1)
/// - Utility (Fortune Candle, อายุ 2): สด = Pattern ป้อมยิงข้ามหน่วยได้ | เน่า = ป้อมทุกหลังข้าม action 1 Round
/// </summary>
[CreateAssetMenu(fileName = "NewOfferingItem", menuName = "GameJam/Offering Item Data")]
public class OfferingItemData : ScriptableObject
{
    private const int CurrentSchemaVersion = 2;

    [HideInInspector]
    [SerializeField] private int schemaVersion = 0;

    [Header("General Info")]
    [SerializeField] private string itemId = "offering_item";
    [SerializeField] private string itemName = "Red Soda";
    [TextArea(2, 4)]
    [SerializeField] private string description = "Offer to the shrine and wait for it to rot.";
    [SerializeField] private OfferingType offeringType = OfferingType.Drink;

    [Header("Visuals (Fresh vs Rotten)")]
    [SerializeField] private Sprite freshIcon;
    [SerializeField] private Sprite rottenIcon;

    [Header("Faustian Decay Lifecycle")]
    [Tooltip("จำนวน Round ก่อนที่ของชิ้นนี้จะบูดเน่าคาแท่นบูชา")]
    [SerializeField, Min(1)] private int decayLoops = 2;

    [Header("1. Fresh State (Sacrifice — ศาลเจ้าได้เปรียบ)")]
    [Tooltip("Drink (Fresh): ป้อมทุกหลังได้ Keyword Double Strike")]
    [SerializeField] private bool freshGrantTowerDoubleStrike = true;
    [Tooltip("(ทางเลือก) AbilityData ของ Double Strike ถ้าเว้นว่างระบบจะสร้าง Runtime AbilityData ให้อัตโนมัติ")]
    [SerializeField] private AbilityData freshTowerAbility;

    [Tooltip("Savory (Fresh): ลดความเสียหายที่ศาลเจ้าได้รับตามสัดส่วน (0.5 = ดาเมจ x0.5 คิดก่อนหัก DEF)")]
    [Range(0f, 0.8f)]
    [SerializeField] private float freshShrineDamageReductionPercent = 0.0f;

    [Tooltip("Sweet (Fresh): จำนวนช่องที่ผลักผีหัวขบวนถอยหลัง (0 = ไม่ผลัก)")]
    [SerializeField, Min(0)] private int freshPushLeadSpiritTiles = 0;
    [Tooltip("Sweet (Fresh): ผลักผีหัวขบวนทุกๆ กี่เทิร์น (เช่น 2 = เทิร์นที่ 2, 4, 6...)")]
    [SerializeField, Min(1)] private int freshPushEveryNTurns = 2;

    [Tooltip("Fragrance (Fresh): ตัวคูณดาเมจของผีขณะของยังสด (เช่น 0.8 = ดาเมจผี x0.8)")]
    [SerializeField, Range(0.1f, 2f)] private float freshEntityAttackMultiplier = 1.0f;

    [Tooltip("Utility (Fresh): Pattern ป้อมยิงข้ามหน่วยได้ (ปิด blockedByUnits)")]
    [SerializeField] private bool freshTowerIgnoreBlocking = false;

    [Tooltip("เสกป้อมโคมไฟหินออกมาป้องกันศาลเจ้าทันทีเมื่อวางของสดหรือไม่")]
    [SerializeField] private bool spawnRetaliationLanternOnFresh = false;

    [Header("2. Rotten State (Payoff — ผีคลั่ง & ศาลเสียเปรียบ)")]
    [Tooltip("Drink (Rotten): ผีทุกตัวได้ Keyword Lifedrain")]
    [SerializeField] private bool rottenGrantEntityLifedrain = true;
    [Tooltip("(ทางเลือก) AbilityData ของ Lifedrain ถ้าเว้นว่างระบบจะสร้าง Runtime AbilityData ให้อัตโนมัติ")]
    [SerializeField] private AbilityData rottenEntityAbility;
    [Tooltip("Drink (Rotten): โบนัสระยะเดิน +Move ของผีทุกตัว (Berserk)")]
    [SerializeField, Min(0)] private int rottenEntityMoveBonus = 1;

    [Tooltip("Savory (Rotten): กัดกร่อนเกราะของศาลเจ้าทั้งหมดให้เหลือ 0")]
    [SerializeField] private bool stripShrineArmor = false;
    [Tooltip("Savory (Rotten): พิษลด HP ศาลเจ้าทุกต้นเทิร์น (0 = ไม่มีพิษ)")]
    [SerializeField, Min(0f)] private float rottenShrinePoisonDamagePerTurn = 0.0f;

    [Tooltip("Sweet (Rotten): ผีหัวขบวนที่มี EntityMutatable กลายพันธุ์ทันทีเมื่อของชิ้นนี้เน่า")]
    [SerializeField] private bool rottenMutateLeadSpiritImmediately = false;

    [Tooltip("Fragrance (Rotten): ค่าปรับขนาด N ของ Pattern ป้อมทุกหลัง (เช่น -1 = ลดขนาดลง 1 ต่ำสุด 1)")]
    [SerializeField] private int rottenTowerPatternSizeDelta = 0;

    [Tooltip("Utility (Rotten): ป้อมทุกหลังข้าม action เป็นจำนวน Round ที่กำหนด")]
    [SerializeField] private bool rottenTowersSkipRound = false;
    [SerializeField, Min(1)] private int rottenTowersSkipDurationRounds = 1;

    // ---------- General Properties ----------
    public string ItemId => itemId;
    public string ItemName => itemName;
    public string Description => description;
    public OfferingType Category => offeringType;

    public Sprite FreshIcon => freshIcon;
    public Sprite RottenIcon => rottenIcon != null ? rottenIcon : freshIcon;
    public int DecayLoops => Mathf.Max(1, decayLoops);

    // ---------- Fresh Properties ----------
    public bool FreshGrantTowerDoubleStrike => freshGrantTowerDoubleStrike;
    public AbilityData FreshTowerAbility => freshTowerAbility;
    public float FreshShrineDamageReductionPercent => freshShrineDamageReductionPercent;
    public int FreshPushLeadSpiritTiles => freshPushLeadSpiritTiles;
    public int FreshPushEveryNTurns => Mathf.Max(1, freshPushEveryNTurns);
    public float FreshEntityAttackMultiplier => freshEntityAttackMultiplier;
    public bool FreshTowerIgnoreBlocking => freshTowerIgnoreBlocking;
    public bool SpawnRetaliationLanternOnFresh => spawnRetaliationLanternOnFresh;

    // ---------- Rotten Properties ----------
    public bool RottenGrantEntityLifedrain => rottenGrantEntityLifedrain;
    public AbilityData RottenEntityAbility => rottenEntityAbility;
    public int RottenEntityMoveBonus => Mathf.Max(0, rottenEntityMoveBonus);
    public bool StripShrineArmor => stripShrineArmor;
    public float RottenShrinePoisonDamagePerTurn => Mathf.Max(0f, rottenShrinePoisonDamagePerTurn);
    public bool RottenMutateLeadSpiritImmediately => rottenMutateLeadSpiritImmediately;
    public int RottenTowerPatternSizeDelta => rottenTowerPatternSizeDelta;
    public bool RottenTowersSkipRound => rottenTowersSkipRound;
    public int RottenTowersSkipDurationRounds => Mathf.Max(1, rottenTowersSkipDurationRounds);

    private void OnEnable()
    {
        EnsureMigratedToV2();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureMigratedToV2();
        decayLoops = Mathf.Max(1, decayLoops);
        freshPushEveryNTurns = Mathf.Max(1, freshPushEveryNTurns);
        rottenTowersSkipDurationRounds = Mathf.Max(1, rottenTowersSkipDurationRounds);
    }
#endif

    [ContextMenu("Apply Table 4.6 Defaults For Category")]
    public void ApplyTable46DefaultsForCategory()
    {
        freshGrantTowerDoubleStrike = false;
        freshShrineDamageReductionPercent = 0f;
        freshPushLeadSpiritTiles = 0;
        freshPushEveryNTurns = 2;
        freshEntityAttackMultiplier = 1f;
        freshTowerIgnoreBlocking = false;

        rottenGrantEntityLifedrain = false;
        rottenEntityMoveBonus = 0;
        stripShrineArmor = false;
        rottenShrinePoisonDamagePerTurn = 0f;
        rottenMutateLeadSpiritImmediately = false;
        rottenTowerPatternSizeDelta = 0;
        rottenTowersSkipRound = false;
        rottenTowersSkipDurationRounds = 1;

        switch (offeringType)
        {
            case OfferingType.Drink:
                decayLoops = 2;
                freshGrantTowerDoubleStrike = true;
                rottenGrantEntityLifedrain = true;
                rottenEntityMoveBonus = 1;
                break;

            case OfferingType.Savory:
                decayLoops = 2;
                freshShrineDamageReductionPercent = 0.5f;
                stripShrineArmor = true;
                rottenShrinePoisonDamagePerTurn = 2f;
                break;

            case OfferingType.Sweet:
                decayLoops = 3;
                freshPushLeadSpiritTiles = 1;
                freshPushEveryNTurns = 2;
                rottenMutateLeadSpiritImmediately = true;
                break;

            case OfferingType.Fragrance:
                decayLoops = 1;
                freshEntityAttackMultiplier = 0.8f;
                rottenTowerPatternSizeDelta = -1;
                break;

            case OfferingType.Utility:
                decayLoops = 2;
                freshTowerIgnoreBlocking = true;
                rottenTowersSkipRound = true;
                rottenTowersSkipDurationRounds = 1;
                break;
        }

        schemaVersion = CurrentSchemaVersion;
    }

    private void EnsureMigratedToV2()
    {
        if (schemaVersion < CurrentSchemaVersion)
        {
            ApplyTable46DefaultsForCategory();
        }
    }
}
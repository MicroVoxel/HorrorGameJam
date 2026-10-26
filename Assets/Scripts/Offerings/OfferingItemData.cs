using UnityEngine;

/// <summary>
/// ข้อมูลไอเทมของถวายช่องเดี่ยว (Single-Slot Offering Item)
/// รองรับกลไก FAUST: ถวายตอนสดทำให้ศาลเจ้าเก่งขึ้น (Sacrifice) แต่เมื่อบูดเน่าจะปลดเกราะศาลและทำให้ผีคลั่ง (Payoff)
/// </summary>
[CreateAssetMenu(fileName = "NewOfferingItem", menuName = "GameJam/Offering Item Data")]
public class OfferingItemData : ScriptableObject
{
    [Header("General Info")]
    [SerializeField] private string itemId = "offering_item";
    [SerializeField] private string itemName = "น้ำแดงโซดา";
    [TextArea(2, 4)]
    [SerializeField] private string description = "ถวายศาลเจ้าเพื่อรอวันบูดเน่า";
    [SerializeField] private OfferingType offeringType = OfferingType.Drink;

    [Header("Visuals (Fresh vs Rotten)")]
    [SerializeField] private Sprite freshIcon;
    [SerializeField] private Sprite rottenIcon;

    [Header("Faustian Decay Lifecycle")]
    [Tooltip("จำนวนรอบก้านธูปก่อนที่ของชิ้นนี้จะบูดเน่าคาแท่นบูชา")]
    [SerializeField] private int decayLoops = 2;

    [Header("1. Fresh State (Sacrifice — ศาลเจ้าได้บัฟทันที)")]
    [Tooltip("เพิ่มค่าเกราะป้องกันให้ศาลเจ้าขณะของยังสด")]
    [SerializeField] private float freshShrineDefenseBonus = 2.0f;
    [Tooltip("ลดความเสียหายที่ศาลเจ้าได้รับลงตามสัดส่วน (เช่น 0.25 = ลดดาเมจ 25%)")]
    [Range(0f, 0.8f)]
    [SerializeField] private float freshShrineDamageReductionPercent = 0.2f;
    [Tooltip("ตัวคูณความถี่ในการยิงของป้อมศาลเจ้า (ค่ายิ่งน้อยป้อมยิ่งยิงถี่ เช่น 0.75 = ยิงไวขึ้น 25%)")]
    [SerializeField] private float freshTowerAttackSpeedMultiplier = 0.75f;
    [Tooltip("เสกป้อมโคมไฟหินออกมาป้องกันศาลเจ้าทันทีเมื่อวางของสดหรือไม่")]
    [SerializeField] private bool spawnRetaliationLanternOnFresh = true;

    [Header("2. Rotten State (Payoff — คำสาปของเน่า & ผีคลั่ง)")]
    [Tooltip("ตัวคูณพลังโจมตีของผีทุกตัวเมื่อของชิ้นนี้เน่าเสีย")]
    [SerializeField] private float rottenEntityAttackMultiplier = 1.5f;
    [Tooltip("ตัวคูณความเร็วการกระโดดของผีทุกตัวเมื่อของชิ้นนี้เน่าเสีย")]
    [SerializeField] private float rottenEntitySpeedMultiplier = 1.35f;
    [Tooltip("ปลุกผีทุกตัวในขบวนให้เข้าสู่สถานะคลั่ง (Frenzy) หรือไม่")]
    [SerializeField] private bool triggersFrenzy = true;
    [Tooltip("กัดกร่อนเกราะของศาลเจ้าทั้งหมดให้เหลือ 0 หรือไม่")]
    [SerializeField] private bool stripShrineArmor = true;

    // Properties
    public string ItemId => itemId;
    public string ItemName => itemName;
    public string Description => description;

    public OfferingType Category => offeringType;

    public Sprite FreshIcon => freshIcon;
    public Sprite RottenIcon => rottenIcon != null ? rottenIcon : freshIcon;

    public int DecayLoops => Mathf.Max(1, decayLoops);

    // Fresh Buff Properties
    public float FreshShrineDefenseBonus => freshShrineDefenseBonus;
    public float FreshShrineDamageReductionPercent => freshShrineDamageReductionPercent;
    public float FreshTowerAttackSpeedMultiplier => freshTowerAttackSpeedMultiplier;
    public bool SpawnRetaliationLanternOnFresh => spawnRetaliationLanternOnFresh;

    // Rotten Curse Properties
    public float RottenEntityAttackMultiplier => rottenEntityAttackMultiplier;
    public float RottenEntitySpeedMultiplier => rottenEntitySpeedMultiplier;
    public bool TriggersFrenzy => triggersFrenzy;
    public bool StripShrineArmor => stripShrineArmor;
}
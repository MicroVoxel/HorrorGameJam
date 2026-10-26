using UnityEngine;

public enum AbilityKeyword
{
    QuickAttack,
    DoubleStrike,
    ArmorPiercing,
    Overrun,
    Decoy,
    Phase,
    Barrier,
    Tough,
    Lifedrain,
    Regeneration,
    LastBreath
}

/// <summary>จุดที่ Keyword ทำงาน (ใช้แสดงใน Tooltip) กำหนดอัตโนมัติจากชนิด Keyword</summary>
public enum AbilityTrigger
{
    QuickStep,
    AttackRule,
    DamageCalc,
    Targeting,
    Movement,
    Defense,
    AfterAttack,
    StartOfTurn,
    OnDeath
}

/// <summary>
/// ข้อมูล Keyword 1 ตัว (กติกาสำเร็จรูป) ลากใส่ AbilityHolder ของ Prefab ผี/ป้อม
/// ข้อความทั้งหมดเป็นภาษาอังกฤษ ไม่มี emoji ใช้ {n} ในคำอธิบายเพื่อแทนค่า Value
/// </summary>
[CreateAssetMenu(fileName = "NewAbility", menuName = "GameJam/Ability Data")]
public class AbilityData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string keywordId = "quick_attack";
    [SerializeField] private string displayName = "Quick Attack";
    [TextArea(2, 4)]
    [SerializeField] private string description = "Attacks before everyone else each turn.";
    [SerializeField] private Sprite icon;

    [Header("Rule")]
    [SerializeField] private AbilityKeyword keyword = AbilityKeyword.QuickAttack;
    [Tooltip("N ของ Keyword เช่น Tough N, Regeneration N (ตัวอื่นไม่ใช้)")]
    [SerializeField, Min(0)] private int value = 1;

    [Header("Last Breath (keyword = LastBreath)")]
    [Tooltip("Pattern ที่ระเบิดรอบช่องที่ตาย ว่าง = โดนเฉพาะช่องที่ตาย")]
    [SerializeField] private HexRangePattern lastBreathPattern;
    [SerializeField, Min(0f)] private float lastBreathDamage = 6f;

    public string KeywordId => keywordId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public AbilityKeyword Keyword => keyword;
    public int Value => value;
    public AbilityTrigger Trigger => DefaultTrigger(keyword);
    public HexRangePattern LastBreathPattern => lastBreathPattern;
    public float LastBreathDamage => lastBreathDamage;

    /// <summary>คำอธิบายที่แทน {n} ด้วย Value แล้ว</summary>
    public string GetDescription()
    {
        return UITextData.Format(description, "n", value);
    }

    /// <summary>สร้าง AbilityData ชั่วคราวในหน่วยความจำ (ใช้เป็น Fallback เมื่อของถวายมอบ Keyword แต่ไม่ได้ลาก SO ใส่)</summary>
    public static AbilityData CreateRuntime(AbilityKeyword keyword, string displayName, string description, int value = 1)
    {
        AbilityData instance = CreateInstance<AbilityData>();
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.keywordId = keyword.ToString().ToLowerInvariant();
        instance.displayName = displayName;
        instance.description = description;
        instance.keyword = keyword;
        instance.value = value;
        return instance;
    }

    public static AbilityTrigger DefaultTrigger(AbilityKeyword k)
    {
        switch (k)
        {
            case AbilityKeyword.QuickAttack: return AbilityTrigger.QuickStep;
            case AbilityKeyword.DoubleStrike: return AbilityTrigger.AttackRule;
            case AbilityKeyword.ArmorPiercing: return AbilityTrigger.DamageCalc;
            case AbilityKeyword.Overrun: return AbilityTrigger.DamageCalc;
            case AbilityKeyword.Decoy: return AbilityTrigger.Targeting;
            case AbilityKeyword.Phase: return AbilityTrigger.Movement;
            case AbilityKeyword.Barrier: return AbilityTrigger.Defense;
            case AbilityKeyword.Tough: return AbilityTrigger.Defense;
            case AbilityKeyword.Lifedrain: return AbilityTrigger.AfterAttack;
            case AbilityKeyword.Regeneration: return AbilityTrigger.StartOfTurn;
            case AbilityKeyword.LastBreath: return AbilityTrigger.OnDeath;
            default: return AbilityTrigger.AttackRule;
        }
    }
}
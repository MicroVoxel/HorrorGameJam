using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คอมโพเนนต์สำหรับติดบนป้อมโคมไฟหินหรือศาลเจ้า (TowerBase)
/// เรียกใช้ LootStash.TryRollDrops ร่วมกับ EntityDroppable
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TowerBase))]
public class TowerDroppable : MonoBehaviour
{
    [Header("Loot On Tower Destroyed (เมื่อป้อมพัง)")]
    [Range(0f, 1f)]
    [SerializeField] private float cardDropChance = 0.75f;
    [SerializeField] private List<CardData> possibleCards = new List<CardData>();

    [Range(0f, 1f)]
    [SerializeField] private float offeringDropChance = 0.65f;
    [SerializeField] private List<OfferingItemData> possibleOfferings = new List<OfferingItemData>();

    [Header("Shrine Chip Damage Loot (ของร่วงจากโต๊ะบูชาเมื่อเลือดลดทุกๆ 25%)")]
    [SerializeField] private bool dropOfferingOnHealthThresholds = true;
    [SerializeField] private float healthThresholdStep = 0.25f;

    private TowerBase towerBase;
    private float nextThresholdNormalized = 0.75f;

    private void Awake()
    {
        towerBase = GetComponent<TowerBase>();
        nextThresholdNormalized = 1.0f - Mathf.Clamp(healthThresholdStep, 0.1f, 0.5f);
    }

    private void Start()
    {
        LootStash.RegisterPools(possibleOfferings, possibleCards);
    }

    private void OnEnable()
    {
        if (towerBase != null)
        {
            towerBase.OnHealthChanged += HandleHealthChanged;
            towerBase.OnTowerDestroyed += HandleTowerDestroyed;
        }
    }

    private void OnDisable()
    {
        if (towerBase != null)
        {
            towerBase.OnHealthChanged -= HandleHealthChanged;
            towerBase.OnTowerDestroyed -= HandleTowerDestroyed;
        }
    }

    private void HandleHealthChanged(TowerBase tower, float currentHp)
    {
        if (!dropOfferingOnHealthThresholds || tower == null || tower.MaxHealth <= 0f) return;

        float normalized = currentHp / tower.MaxHealth;
        while (normalized <= nextThresholdNormalized && nextThresholdNormalized > 0.01f)
        {
            nextThresholdNormalized -= healthThresholdStep;
            LootStash.TryRollOfferingDrop(possibleOfferings, 1f, "Shrine Chip Damage");
        }
    }

    private void HandleTowerDestroyed(TowerBase destroyedTower)
    {
        LootStash.TryRollDrops(
            possibleOfferings,
            offeringDropChance,
            possibleCards,
            cardDropChance,
            "Tower Destroyed");
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คอมโพเนนต์สำหรับติดบนป้อมโคมไฟหินหรือศาลเจ้า (TowerBase) 
/// เมื่อป้อมถูกทำลาย หรือเมื่อเลือดศาลเจ้าลดลงทุกๆ ช่วงที่กำหนด (Chip Damage Loot) จะดรอปของถวายและการ์ดให้ผู้เล่น
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
            GrantRandomOffering("Shrine Chip Damage");
        }
    }

    private void HandleTowerDestroyed(TowerBase destroyedTower)
    {
        if (Random.value <= offeringDropChance)
        {
            GrantRandomOffering("Tower Destroyed");
        }

        if (Random.value <= cardDropChance && possibleCards.Count > 0)
        {
            CardData card = possibleCards[Random.Range(0, possibleCards.Count)];
            CardDeckController deck = FindAnyObjectByType<CardDeckController>();
            if (card != null && deck != null)
            {
                deck.AddCardToHand(card);
            }
        }
    }

    private void GrantRandomOffering(string reason)
    {
        if (possibleOfferings == null || possibleOfferings.Count == 0) return;

        OfferingItemData item = possibleOfferings[Random.Range(0, possibleOfferings.Count)];
        if (item != null && InventorySlotManager.Instance != null)
        {
            InventorySlotManager.Instance.AddItem(item);
            Debug.Log($"<color=yellow>[{reason}]</color> ของเซ่นไหว้ '{item.ItemName}' ร่วงหล่นลงกระเป๋า!");
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คอมโพเนนต์จัดการการดรอปไอเทมของถวาย (OfferingItemData) และการ์ดแผ่นกระดาน (CardData)
/// รองรับทั้งการดรอปเมื่อยูนิตตาย (เช่น ตีคนเฝ้าศาลตาย) และการดรอปรางวัลเมื่อผีกระโดดวนครบรอบลูปสำเร็จ!
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EntityBase))]
public class EntityDroppable : MonoBehaviour
{
    [Header("Drop On Death (เมื่อยูนิตถูกกำจัด)")]
    [Range(0f, 1f)]
    [SerializeField] private float offeringDropChanceOnDeath = 0.25f;
    [SerializeField] private List<OfferingItemData> possibleOfferingDrops = new List<OfferingItemData>();

    [Range(0f, 1f)]
    [SerializeField] private float cardDropChanceOnDeath = 0.30f;
    [SerializeField] private List<CardData> possibleCardDrops = new List<CardData>();

    [Header("Drop On Loop Completed (รางวัลเมื่อผีรอดชีวิตเดินครบรอบลูป)")]
    [SerializeField] private bool rewardOnLoopCompleted = true;
    [Range(0f, 1f)]
    [SerializeField] private float loopOfferingDropChance = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float loopCardDropChance = 0.40f;

    private EntityBase entityBase;
    private EntityWalkable walkable;

    private void Awake()
    {
        entityBase = GetComponent<EntityBase>();
        walkable = GetComponent<EntityWalkable>();
    }

    private void OnEnable()
    {
        if (entityBase != null)
        {
            entityBase.OnEntityDied += HandleEntityDied;
        }

        if (walkable != null)
        {
            walkable.OnLoopCompleted += HandleLoopCompleted;
        }
    }

    private void OnDisable()
    {
        if (entityBase != null)
        {
            entityBase.OnEntityDied -= HandleEntityDied;
        }

        if (walkable != null)
        {
            walkable.OnLoopCompleted -= HandleLoopCompleted;
        }
    }

    private void HandleEntityDied(EntityBase deadEntity)
    {
        TryRollOfferingDrop(offeringDropChanceOnDeath, "OnDeath");
        TryRollCardDrop(cardDropChanceOnDeath, "OnDeath");
    }

    private void HandleLoopCompleted(EntityWalkable sender, int completedLoopCount)
    {
        if (!rewardOnLoopCompleted) return;

        TryRollOfferingDrop(loopOfferingDropChance, $"Loop #{completedLoopCount}");
        TryRollCardDrop(loopCardDropChance, $"Loop #{completedLoopCount}");
    }

    private void TryRollOfferingDrop(float chance, string sourceTag)
    {
        if (possibleOfferingDrops == null || possibleOfferingDrops.Count == 0) return;
        if (Random.value > chance) return;

        OfferingItemData chosenItem = possibleOfferingDrops[Random.Range(0, possibleOfferingDrops.Count)];
        if (chosenItem != null && InventorySlotManager.Instance != null)
        {
            InventorySlotManager.Instance.AddItem(chosenItem);
            Debug.Log($"<color=cyan>[Loot Drop - {sourceTag}]</color> ดรอปของเซ่นไหว้ '{chosenItem.ItemName}' เข้ากระเป๋า!");
        }
    }

    private void TryRollCardDrop(float chance, string sourceTag)
    {
        if (possibleCardDrops == null || possibleCardDrops.Count == 0) return;
        if (Random.value > chance) return;

        CardData chosenCard = possibleCardDrops[Random.Range(0, possibleCardDrops.Count)];
        if (chosenCard != null)
        {
            CardDeckController deck = FindAnyObjectByType<CardDeckController>();
            if (deck != null)
            {
                deck.AddCardToHand(chosenCard);
                Debug.Log($"<color=green>[Card Drop - {sourceTag}]</color> ดรอปการ์ด '{chosenCard.CardName}' เข้ามือ!");
            }
        }
    }
}
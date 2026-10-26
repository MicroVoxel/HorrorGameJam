using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คอมโพเนนต์จัดการการดรอปไอเทมของถวาย (OfferingItemData) และการ์ดแผ่นกระดาน (CardData)
/// v2 (4C): ใช้ GameRandom (Seeded RNG) และส่งของดรอปเข้า LootStash เพื่อแจกตอน RoundEnd
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

    private void Start()
    {
        LootStash.RegisterPools(possibleOfferingDrops, possibleCardDrops);
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
        if (possibleOfferingDrops == null || possibleOfferingDrops.Count == 0 || chance <= 0f) return;
        if (!GameRandom.Chance(chance)) return;

        OfferingItemData chosenItem = GameRandom.Pick(possibleOfferingDrops);
        if (chosenItem != null)
        {
            LootStash.StashOffering(chosenItem, sourceTag);
        }
    }

    private void TryRollCardDrop(float baseChance, string sourceTag)
    {
        if (possibleCardDrops == null || possibleCardDrops.Count == 0 || baseChance <= 0f) return;

        float effectiveChance = Mathf.Clamp01(baseChance + LootStash.GetThreatCardChanceBonus());
        if (!GameRandom.Chance(effectiveChance)) return;

        CardData chosenCard = GameRandom.Pick(possibleCardDrops);
        if (chosenCard != null)
        {
            LootStash.StashCard(chosenCard, sourceTag);
        }
    }
}
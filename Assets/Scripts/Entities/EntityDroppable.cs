using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คอมโพเนนต์จัดการการดรอปไอเทมของถวายและการ์ดเมื่อผีตายหรือเดินครบรอบลูป
/// เรียกใช้ LootStash.TryRollDrops ร่วมกับ TowerDroppable
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
        LootStash.TryRollDrops(
            possibleOfferingDrops,
            offeringDropChanceOnDeath,
            possibleCardDrops,
            cardDropChanceOnDeath,
            "OnDeath");
    }

    private void HandleLoopCompleted(EntityWalkable sender, int completedLoopCount)
    {
        if (!rewardOnLoopCompleted) return;

        LootStash.TryRollDrops(
            possibleOfferingDrops,
            loopOfferingDropChance,
            possibleCardDrops,
            loopCardDropChance,
            $"Loop #{completedLoopCount}");
    }
}
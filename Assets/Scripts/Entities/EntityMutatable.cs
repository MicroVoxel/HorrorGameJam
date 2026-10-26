using System;
using UnityEngine;

public enum MutationTrigger
{
    LoopCompleted,      // เดินครบรอบทางเดิน
    SurvivedTurns,      // รอดครบ N เทิร์น (ทุกๆ N เทิร์น)
    OfferingStimulus    // ถูกของถวายกระตุ้น (เรียก ForceMutate จากภายนอก)
}

public enum MutationReward
{
    MoveBonus,          // +1 Move Speed
    ActionPointBonus    // +1 AP
}

/// <summary>
/// การกลายพันธุ์ของผี (ฟีเจอร์ทางเลือก ติดเฉพาะยูนิตที่ต้องการ)
/// เงื่อนไขทริกเกอร์ตั้งต่อ Prefab ผลคือ HP x multiplier (ฟื้นเต็ม) + ขยายร่าง + โบนัส Move หรือ AP
/// การกลายพันธุ์เกิดในขั้น Cleanup ของเทิร์น (TurnManager.OnTurnCleanup) ยกเว้น ForceMutate ที่เกิดทันที
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EntityBase), typeof(EntityWalkable))]
public class EntityMutatable : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private MutationTrigger trigger = MutationTrigger.LoopCompleted;
    [Tooltip("SurvivedTurns: mutate every N turns survived.")]
    [SerializeField, Min(1)] private int survivedTurnsRequired = 3;

    [Header("Result")]
    [SerializeField] private float healthMultiplier = 1.35f;
    [SerializeField] private float scaleMultiplier = 1.18f;
    [SerializeField] private MutationReward reward = MutationReward.MoveBonus;
    [SerializeField, Min(1)] private int maxMutations = 3;

    [Header("Runtime State")]
    [SerializeField] private int mutationCount = 0;
    [SerializeField] private int turnsSurvived = 0;

    private EntityBase entityBase;
    private EntityWalkable walkable;
    private bool loopPending;
    private bool subscribedToTurns;

    public int MutationCount => mutationCount;
    public int MaxMutations => maxMutations;
    public event Action<EntityMutatable, int> OnEntityMutated;

    private void Awake()
    {
        entityBase = GetComponent<EntityBase>();
        walkable = GetComponent<EntityWalkable>();
    }

    private void OnEnable()
    {
        if (walkable != null) walkable.OnLoopCompleted += HandleLoopCompleted;
        TrySubscribeToTurns();
    }

    private void Start()
    {
        TrySubscribeToTurns();
    }

    private void OnDisable()
    {
        if (walkable != null) walkable.OnLoopCompleted -= HandleLoopCompleted;

        if (subscribedToTurns && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnCleanup -= HandleTurnCleanup;
        }

        subscribedToTurns = false;
    }

    private void TrySubscribeToTurns()
    {
        if (subscribedToTurns || TurnManager.Instance == null) return;

        TurnManager.Instance.OnTurnCleanup += HandleTurnCleanup;
        subscribedToTurns = true;
    }

    private void HandleLoopCompleted(EntityWalkable sender, int loopNumber)
    {
        if (trigger == MutationTrigger.LoopCompleted) loopPending = true;
    }

    private void HandleTurnCleanup(TurnContext ctx)
    {
        if (entityBase == null || entityBase.IsDead) return;

        turnsSurvived++;

        bool shouldMutate = false;
        if (trigger == MutationTrigger.LoopCompleted && loopPending) shouldMutate = true;
        if (trigger == MutationTrigger.SurvivedTurns && turnsSurvived % survivedTurnsRequired == 0) shouldMutate = true;

        loopPending = false;
        if (shouldMutate) Mutate(ctx);
    }

    /// <summary>กลายพันธุ์ทันที (เช่น ของถวาย Sweet ที่เน่ากระตุ้นผีหัวขบวน) คืน false ถ้าครบเพดานแล้ว</summary>
    public bool ForceMutate(TurnContext ctx = null)
    {
        return Mutate(ctx);
    }

    private bool Mutate(TurnContext ctx)
    {
        if (entityBase == null || entityBase.IsDead || mutationCount >= maxMutations) return false;

        mutationCount++;

        entityBase.HealFullAndScaleMaxHealth(healthMultiplier);
        transform.localScale *= scaleMultiplier;

        if (reward == MutationReward.MoveBonus) entityBase.AddMoveBonus(1);
        else entityBase.AddActionPointBonus(1);

        ctx?.Log($"{entityBase.EntityName} mutates (stage {mutationCount})");
        OnEntityMutated?.Invoke(this, mutationCount);
        return true;
    }
}
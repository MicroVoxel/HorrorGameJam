using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คลังพักของดรอปประจำ Round (GDD 4.2, 4.3, 4.8):
/// รวมฟังก์ชันทอยดรอปกลาง (TryRollOfferingDrop / TryRollCardDrop / TryRollDrops) ให้ EntityDroppable และ TowerDroppable ใช้ร่วมกัน
/// </summary>
[DisallowMultipleComponent]
public class LootStash : SingletonBehaviour<LootStash>
{
    [Header("Threat Tower Bonus Pools (เว้นว่างได้ ระบบจะรวบรวมจาก Droppable ในฉากอัตโนมัติ)")]
    [SerializeField] private List<OfferingItemData> bonusOfferingPool = new List<OfferingItemData>();
    [SerializeField] private List<CardData> bonusCardPool = new List<CardData>();

    private readonly List<OfferingItemData> pendingOfferings = new List<OfferingItemData>(16);
    private readonly List<CardData> pendingCards = new List<CardData>(16);

    private readonly List<OfferingItemData> lastRoundOfferings = new List<OfferingItemData>(16);
    private readonly List<CardData> lastRoundCards = new List<CardData>(16);

    private bool subscribed;

    public IReadOnlyList<OfferingItemData> PendingOfferings => pendingOfferings;
    public IReadOnlyList<CardData> PendingCards => pendingCards;
    public IReadOnlyList<OfferingItemData> LastRoundOfferings => lastRoundOfferings;
    public IReadOnlyList<CardData> LastRoundCards => lastRoundCards;

    public event Action<IReadOnlyList<OfferingItemData>, IReadOnlyList<CardData>> OnRoundLootGranted;

    public static LootStash EnsureInstance()
    {
        if (Instance != null) return Instance;

        LootStash found = FindAnyObjectByType<LootStash>();
        if (found != null) return found;

        if (GameStateManager.Instance != null)
        {
            return GameStateManager.Instance.gameObject.AddComponent<LootStash>();
        }

        return null;
    }

    private void Start()
    {
        TrySubscribe();
    }

    protected override void OnDestroy()
    {
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
            GameStateManager.Instance.OnRoundStarted -= HandleRoundStarted;
        }

        base.OnDestroy();
    }

    private void TrySubscribe()
    {
        if (subscribed || GameStateManager.Instance == null) return;

        GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        GameStateManager.Instance.OnRoundStarted += HandleRoundStarted;
        subscribed = true;
    }

    private void HandleRoundStarted(int round, int turns)
    {
        lastRoundOfferings.Clear();
        lastRoundCards.Clear();
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.RoundEnd)
        {
            FlushRoundEndLoot();
        }
    }

    // ---------- Shared Drop Roll Helpers ----------

    /// <summary>ทอยดรอปของถวายจากตารางที่ส่งเข้ามาด้วย GameRandom แล้วเก็บเข้า LootStash</summary>
    public static void TryRollOfferingDrop(IList<OfferingItemData> pool, float chance, string sourceTag)
    {
        if (pool == null || pool.Count == 0 || chance <= 0f) return;
        if (!GameRandom.Chance(chance)) return;

        OfferingItemData chosen = GameRandom.Pick(pool);
        if (chosen != null)
        {
            StashOffering(chosen, sourceTag);
        }
    }

    /// <summary>ทอยดรอปการ์ด (รวมโบนัสจาก Threat Tower อัตโนมัติ) ด้วย GameRandom แล้วเก็บเข้า LootStash</summary>
    public static void TryRollCardDrop(IList<CardData> pool, float baseChance, string sourceTag)
    {
        if (pool == null || pool.Count == 0 || baseChance <= 0f) return;

        float effectiveChance = Mathf.Clamp01(baseChance + GetThreatCardChanceBonus());
        if (!GameRandom.Chance(effectiveChance)) return;

        CardData chosen = GameRandom.Pick(pool);
        if (chosen != null)
        {
            StashCard(chosen, sourceTag);
        }
    }

    /// <summary>ทอยทั้งของถวายและการ์ดในคำสั่งเดียว</summary>
    public static void TryRollDrops(
        IList<OfferingItemData> offeringPool,
        float offeringChance,
        IList<CardData> cardPool,
        float cardBaseChance,
        string sourceTag)
    {
        TryRollOfferingDrop(offeringPool, offeringChance, sourceTag);
        TryRollCardDrop(cardPool, cardBaseChance, sourceTag);
    }

    // ---------- Pool Registration ----------

    public static void RegisterPools(IReadOnlyList<OfferingItemData> offerings, IReadOnlyList<CardData> cards)
    {
        LootStash stash = EnsureInstance();
        if (stash == null) return;

        stash.TrySubscribe();

        if (offerings != null)
        {
            for (int i = 0; i < offerings.Count; i++)
            {
                OfferingItemData item = offerings[i];
                if (item != null && !stash.bonusOfferingPool.Contains(item))
                {
                    stash.bonusOfferingPool.Add(item);
                }
            }
        }

        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                CardData card = cards[i];
                if (card != null && !stash.bonusCardPool.Contains(card))
                {
                    stash.bonusCardPool.Add(card);
                }
            }
        }
    }

    // ---------- Stash API ----------

    public static void StashOffering(OfferingItemData item, string sourceTag = null)
    {
        if (item == null) return;

        LootStash stash = EnsureInstance();
        if (stash != null)
        {
            stash.AddOffering(item, sourceTag);
        }
        else if (InventorySlotManager.Instance != null)
        {
            InventorySlotManager.Instance.AddItem(item);
        }
    }

    public static void StashCard(CardData card, string sourceTag = null)
    {
        if (card == null) return;

        LootStash stash = EnsureInstance();
        if (stash != null)
        {
            stash.AddCard(card, sourceTag);
        }
        else if (CardDeckController.Instance != null)
        {
            CardDeckController.Instance.AddCardToHand(card);
        }
    }

    public void AddOffering(OfferingItemData item, string sourceTag = null)
    {
        if (item == null) return;
        TrySubscribe();

        if (GameStateManager.IsResolution)
        {
            pendingOfferings.Add(item);
            TurnManager.Instance?.Context?.Log($"Loot stashed: {item.ItemName}");
            Debug.Log($"<color=cyan>[LootStash - {sourceTag}]</color> เก็บ '{item.ItemName}' เข้าคลังรอแจกตอนจบ Round");
        }
        else
        {
            lastRoundOfferings.Add(item);
            if (InventorySlotManager.Instance != null)
            {
                InventorySlotManager.Instance.AddItem(item);
            }
        }
    }

    public void AddCard(CardData card, string sourceTag = null)
    {
        if (card == null) return;
        TrySubscribe();

        if (GameStateManager.IsResolution)
        {
            pendingCards.Add(card);
            TurnManager.Instance?.Context?.Log($"Card stashed: {card.CardName}");
            Debug.Log($"<color=green>[LootStash - {sourceTag}]</color> เก็บการ์ด '{card.CardName}' เข้าคลังรอแจกตอนจบ Round");
        }
        else
        {
            lastRoundCards.Add(card);
            if (CardDeckController.Instance != null)
            {
                CardDeckController.Instance.AddCardToHand(card);
            }
        }
    }

    // ---------- Threat Tower Bonus & RoundEnd Flush ----------

    public static int CountSurvivingThreatTowers()
    {
        int count = 0;
        IReadOnlyList<TowerBase> towers = TowerBase.ActiveTowers;
        for (int i = 0; i < towers.Count; i++)
        {
            TowerBase tower = towers[i];
            if (tower != null && !tower.IsDead && ThreatPlacementValidator.IsThreatTower(tower))
            {
                count++;
            }
        }
        return count;
    }

    public static float GetThreatCardChanceBonus()
    {
        if (GameStateManager.Instance == null) return 0f;

        GameRulesData rules = GameStateManager.Instance.Rules;
        int threatCount = CountSurvivingThreatTowers();
        if (threatCount <= 0) return 0f;

        return Mathf.Min(rules.ThreatCardChanceCap, threatCount * rules.ThreatCardChanceBonus);
    }

    public void FlushRoundEndLoot()
    {
        RollThreatTowerBonuses();

        for (int i = 0; i < pendingOfferings.Count; i++)
        {
            OfferingItemData item = pendingOfferings[i];
            if (item == null) continue;

            lastRoundOfferings.Add(item);
            if (InventorySlotManager.Instance != null)
            {
                InventorySlotManager.Instance.AddItem(item);
            }
        }

        for (int i = 0; i < pendingCards.Count; i++)
        {
            CardData card = pendingCards[i];
            if (card == null) continue;

            lastRoundCards.Add(card);
            if (CardDeckController.Instance != null)
            {
                CardDeckController.Instance.AddCardToHand(card);
            }
        }

        pendingOfferings.Clear();
        pendingCards.Clear();

        OnRoundLootGranted?.Invoke(lastRoundOfferings, lastRoundCards);
    }

    private void RollThreatTowerBonuses()
    {
        if (GameStateManager.Instance == null) return;

        int threatCount = CountSurvivingThreatTowers();
        if (threatCount <= 0) return;

        GameRulesData rules = GameStateManager.Instance.Rules;
        int bonusRolls = threatCount * Mathf.Max(0, rules.ThreatLootBonusPerTower);
        float offeringRollChance = rules.ThreatOfferingRollChance;

        if (bonusRolls > 0 && bonusOfferingPool.Count > 0)
        {
            for (int r = 0; r < bonusRolls; r++)
            {
                if (GameRandom.Chance(offeringRollChance))
                {
                    OfferingItemData bonusItem = GameRandom.Pick(bonusOfferingPool);
                    if (bonusItem != null)
                    {
                        pendingOfferings.Add(bonusItem);
                        Debug.Log($"<color=yellow>[Threat Bonus]</color> ได้รับของเซ่นโบนัส '{bonusItem.ItemName}' จากป้อมอาคมที่ตั้งอยู่!");
                    }
                }
            }
        }

        float bonusCardChance = GetThreatCardChanceBonus();
        if (bonusCardChance > 0f && GameRandom.Chance(bonusCardChance))
        {
            CardData bonusCard = PickBonusCard();
            if (bonusCard != null)
            {
                pendingCards.Add(bonusCard);
                Debug.Log($"<color=yellow>[Threat Bonus]</color> ได้รับการ์ดโบนัส '{bonusCard.CardName}' (โอกาส +{bonusCardChance:P0})!");
            }
        }
    }

    private CardData PickBonusCard()
    {
        if (bonusCardPool.Count > 0)
        {
            return GameRandom.Pick(bonusCardPool);
        }

        if (CardDeckController.Instance != null)
        {
            return CardDeckController.Instance.PickRandomPoolCard();
        }

        return null;
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// แจกการ์ดเริ่มต้นตามกติกา GDD v2 (2.1 และ 5.1):
/// - ในเฟส Dawn: แจกเฉพาะการ์ดศาลเจ้าตามจำนวน ObjectiveTracker.RequiredShrinesToPlace เท่านั้น
/// - เมื่อเริ่ม Planning ของ Round 1: แจกการ์ดรังผีเริ่มต้นตามจำนวน GameRulesData.StartingSpawnerCards
/// </summary>
[DisallowMultipleComponent]
public class SetupPhaseDeckSeeder : MonoBehaviour
{
    [Header("References (เว้นว่างได้ ระบบจะใช้ CardDeckController.Instance)")]
    [SerializeField] private CardDeckController deckController;

    [Header("Dawn Shrine Cards (จำนวนอ่านจาก GameRulesData.RequiredShrines)")]
    [SerializeField] private CardData shrineCardData;
    [Tooltip("ค่าสำรองกรณีไม่มี GameStateManager และ ObjectiveTracker ในฉาก")]
    [SerializeField, Min(1)] private int fallbackShrineCardCount = 2;

    [Header("Round 1 Spawner Cards Pool (จำนวนที่แจกอ่านจาก GameRulesData.StartingSpawnerCards)")]
    [SerializeField] private List<CardData> initialSpawnerCards = new List<CardData>();

    private bool dealtDawnShrines;
    private bool dealtStartingSpawners;

    private void Start()
    {
        if (deckController == null)
        {
            deckController = CardDeckController.Instance;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted += HandleRoundStarted;
        }

        DealDawnShrineHand();

        if (GameStateManager.IsPlanning && GameStateManager.CurrentRound == 1)
        {
            DealRoundOneSpawnerCards();
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted -= HandleRoundStarted;
        }
    }

    private void HandleRoundStarted(int round, int turns)
    {
        if (round == 1)
        {
            DealRoundOneSpawnerCards();
        }
    }

    /// <summary>แจกเฉพาะการ์ดศาลเจ้าในเฟส Dawn ตามจำนวน RequiredShrines</summary>
    [ContextMenu("Deal Dawn Shrine Hand")]
    public void DealDawnShrineHand()
    {
        if (dealtDawnShrines) return;
        dealtDawnShrines = true;

        int requiredShrines = ObjectiveTracker.Instance != null
            ? ObjectiveTracker.Instance.RequiredShrinesToPlace
            : (GameStateManager.Instance != null ? GameStateManager.Instance.Rules.RequiredShrines : Mathf.Max(1, fallbackShrineCardCount));

        if (deckController == null) deckController = CardDeckController.Instance;
        if (deckController == null)
        {
            Debug.LogWarning("[SetupPhaseDeckSeeder] CardDeckController not found in scene!");
            return;
        }

        if (shrineCardData != null)
        {
            for (int i = 0; i < requiredShrines; i++)
            {
                deckController.AddCardToHand(shrineCardData);
            }
        }
    }

    /// <summary>แจกการ์ดรังผีเริ่มต้นเมื่อเข้าสู่ Planning ของ Round 1 ตามจำนวน StartingSpawnerCards ใน GameRulesData</summary>
    [ContextMenu("Deal Round 1 Spawner Cards")]
    public void DealRoundOneSpawnerCards()
    {
        if (dealtStartingSpawners) return;
        dealtStartingSpawners = true;

        if (deckController == null) deckController = CardDeckController.Instance;
        if (deckController == null || initialSpawnerCards == null || initialSpawnerCards.Count == 0) return;

        int spawnerCount = GameStateManager.Instance != null
            ? GameStateManager.Instance.Rules.StartingSpawnerCards
            : initialSpawnerCards.Count;

        int validCardsInPool = 0;
        for (int i = 0; i < initialSpawnerCards.Count; i++)
        {
            if (initialSpawnerCards[i] != null) validCardsInPool++;
        }

        if (validCardsInPool == 0) return;

        int dealt = 0;
        int cursor = 0;
        while (dealt < spawnerCount)
        {
            CardData data = initialSpawnerCards[cursor % initialSpawnerCards.Count];
            cursor++;
            if (data == null) continue;

            deckController.AddCardToHand(data);
            dealt++;
        }
    }
}
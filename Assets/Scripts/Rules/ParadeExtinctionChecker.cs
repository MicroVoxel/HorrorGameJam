using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตรวจจับสภาวะ "ขบวนผีสูญพันธุ์" (Extinction Defeat) ตาม GDD 2.3 และ 4.2:
/// ใช้ TurnActorRegistry.HasActiveParadeOrSpawner() ร่วมกับ TurnManager เพื่อเป็นนิยามเดียวกันทั้งเกม
/// หากศาลเจ้าทั้งหมดพังใน Cleanup เดียวกัน จะให้สิทธิ์ ObjectiveTracker ตัดสิน Victory ก่อนเสมอ
/// </summary>
[DisallowMultipleComponent]
public class ParadeExtinctionChecker : MonoBehaviour
{
    private bool subscribedToTurns;
    private bool subscribedToState;
    private bool deferredToNextRound;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (subscribedToTurns && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnCleanup -= HandleTurnCleanup;
        }

        if (subscribedToState && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
            GameStateManager.Instance.OnRoundEndProcessed -= HandleRoundEndProcessed;
            GameStateManager.Instance.OnRoundStarted -= HandleRoundStarted;
        }

        subscribedToTurns = false;
        subscribedToState = false;
    }

    private void TrySubscribe()
    {
        if (!subscribedToTurns && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnCleanup += HandleTurnCleanup;
            subscribedToTurns = true;
        }

        if (!subscribedToState && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
            GameStateManager.Instance.OnRoundEndProcessed += HandleRoundEndProcessed;
            GameStateManager.Instance.OnRoundStarted += HandleRoundStarted;
            subscribedToState = true;
        }
    }

    private void HandleTurnCleanup(TurnContext ctx)
    {
        EvaluateExtinctionCondition();
    }

    private void HandleGameStateChanged(GameState state)
    {
        TrySubscribe();
    }

    private void HandleRoundEndProcessed()
    {
        EvaluateExtinctionCondition();
    }

    private void HandleRoundStarted(int round, int turns)
    {
        if (!deferredToNextRound) return;
        StartCoroutine(EvaluateAfterDraw());
    }

    private IEnumerator EvaluateAfterDraw()
    {
        yield return null;

        deferredToNextRound = false;
        EvaluateInternal(allowDefer: false);
    }

    public void EvaluateExtinctionCondition()
    {
        EvaluateInternal(allowDefer: true);
    }

    private void EvaluateInternal(bool allowDefer)
    {
        if (GameStateManager.IsDawn || GameStateManager.IsEnded) return;

        // หากศาลเจ้าถูกทำลายครบหมดแล้ว ให้ ObjectiveTracker ตัดสิน Victory ก่อนเสมอ
        if (ObjectiveTracker.Instance != null
            && ObjectiveTracker.Instance.IsSetupComplete
            && ObjectiveTracker.Instance.TargetCount == 0)
        {
            ObjectiveTracker.Instance.EvaluateVictoryAfterCleanup();
            return;
        }

        // 1 & 2. ยังมีผีบนทางเดินหรือรังผีที่ยังเสกผีได้บนกระดานหรือไม่ (ฟังก์ชันกลางร่วมกับ TurnManager)
        if (TurnActorRegistry.HasActiveParadeOrSpawner()) return;

        // 3. ในมือ (หรือใน LootStash ที่กำลังจะแจกตอนจบ Round) ยังมีการ์ดรังผีให้วางแก้เกมหรือไม่
        if (HasSpawnerCardAvailable()) return;

        // 4. Planning ถัดไปอาจจั่วได้การ์ดรังผี ให้รอผลการจั่วก่อนตัดสิน
        if (allowDefer && CouldDrawSpawnerNextRound())
        {
            deferredToNextRound = true;
            return;
        }

        Debug.LogWarning("<color=red>[ParadeExtinctionChecker]</color> All spirits have fallen, no spawner remains on board or in hand — Parade Extinct!");
        if (GameOverController.Instance != null)
        {
            GameOverController.Instance.TriggerDefeatByExtinction();
        }
    }

    private static bool CouldDrawSpawnerNextRound()
    {
        GameStateManager gsm = GameStateManager.Instance;
        if (gsm == null || CardDeckController.Instance == null) return false;
        if (GameStateManager.CurrentRound >= gsm.Rules.TotalRounds) return false;
        if (gsm.Rules.CardsDrawnPerRound <= 0) return false;

        return CardDeckController.Instance.CanDrawSpawnerCard();
    }

    private static bool HasSpawnerCardAvailable()
    {
        CardDeckController deck = CardDeckController.Instance;
        if (deck != null && deck.CardCount > 0)
        {
            IReadOnlyList<CardUI> cards = deck.CardsInHand;
            for (int i = 0; i < cards.Count; i++)
            {
                CardData data = cards[i] != null ? cards[i].CardData : null;
                if (IsSpawnerCard(data)) return true;
            }
        }

        if (LootStash.Instance != null)
        {
            IReadOnlyList<CardData> pendingCards = LootStash.Instance.PendingCards;
            for (int i = 0; i < pendingCards.Count; i++)
            {
                if (IsSpawnerCard(pendingCards[i])) return true;
            }
        }

        return false;
    }

    private static bool IsSpawnerCard(CardData data)
    {
        return data != null
               && data.TowerPrefab != null
               && data.TowerPrefab.GetComponent<TowerSpawnable>() != null;
    }
}
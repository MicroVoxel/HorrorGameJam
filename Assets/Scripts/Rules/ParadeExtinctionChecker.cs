using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตรวจจับสภาวะ "ขบวนผีสูญพันธุ์" (Extinction Defeat) ตาม GDD 2.3 และ 4.2:
/// เช็กหลังทุกขั้น Cleanup ของเทิร์น (TurnManager.OnTurnCleanup) และหลัง RoundEnd ประมวลผลจบ:
/// 1. ไม่มีผีที่มีชีวิตอยู่บนทางเดินเลย (EntityRegistry.ActiveCount == 0)
/// 2. ไม่มีรังผี (TowerSpawnable) ตั้งอยู่บนกระดานที่จะผลิตผีออกมาได้อีก
/// 3. ไม่มีการ์ดรังผีในมือ (และไม่มีการ์ดรังผีรอแจกอยู่ใน LootStash)
/// หากศาลเจ้าทั้งหมดพังใน Cleanup เดียวกัน จะให้สิทธิ์ Victory ก่อนเสมอ
///
/// FIX บัก 9: ถ้า Planning ถัดไปจะจั่วการ์ดให้ (CardsDrawnPerRound) และกองจั่วมีการ์ดรังผีอยู่
/// จะยังไม่ตัดสินแพ้ทันที แต่รอให้จั่วเสร็จตอนเริ่ม Round ใหม่ แล้วค่อยตัดสินอีกครั้ง
/// </summary>
[DisallowMultipleComponent]
public class ParadeExtinctionChecker : MonoBehaviour
{
    private bool subscribedToTurns;
    private bool subscribedToState;
    private bool deferredToNextRound; // FIX บัก 9: เจอสภาพสูญพันธุ์แล้ว แต่รอผลการจั่วของ Round ถัดไป

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
        // แค่ลองสมัครรับ event ที่ยังไม่ได้ต่อ (ตัวเช็ก RoundEnd ย้ายไป HandleRoundEndProcessed แล้ว)
        TrySubscribe();
    }

    // FIX บัก 9: เช็กหลังของเน่าและ loot แจกเสร็จแล้ว (การ์ดรังที่เพิ่งดรอปอยู่ในมือแล้ว)
    private void HandleRoundEndProcessed()
    {
        EvaluateExtinctionCondition();
    }

    // FIX บัก 9: ตัดสินที่ค้างไว้ หลังการจั่วของ Round ใหม่เสร็จ
    private void HandleRoundStarted(int round, int turns)
    {
        if (!deferredToNextRound) return;
        StartCoroutine(EvaluateAfterDraw());
    }

    private IEnumerator EvaluateAfterDraw()
    {
        // รอ 1 เฟรม ให้ CardDeckController จั่วการ์ดของ Round นี้เสร็จทุกกรณี ไม่ขึ้นกับลำดับ subscribe
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

        // หากศาลเจ้าถูกทำลายครบหมดแล้ว ให้ตัดสินเป็น Victory ก่อนเสมอ (เช่น ผีตัวสุดท้ายระเบิด Last Breath พังศาลหลังสุดท้าย)
        if (ObjectiveTracker.Instance != null && ObjectiveTracker.Instance.IsSetupComplete)
        {
            if (ObjectiveTracker.Instance.TargetCount == 0)
            {
                ObjectiveTracker.Instance.EvaluateVictoryAfterCleanup();
                return;
            }
        }

        // 1. ยังมีผีเดินอยู่บนถนนหรือไม่
        if (EntityRegistry.ActiveCount > 0) return;

        // 2. ยังมีรังผี (TowerSpawnable) ที่วางอยู่บนกระดานและยังไม่พังหรือไม่
        IReadOnlyList<TowerBase> towers = TowerBase.ActiveTowers;
        for (int i = 0; i < towers.Count; i++)
        {
            TowerBase tower = towers[i];
            if (tower != null && !tower.IsDead && tower.GetComponent<TowerSpawnable>() != null)
            {
                return;
            }
        }

        // 3. ในมือ (หรือใน LootStash ที่กำลังจะแจกตอนจบ Round) ยังมีการ์ดรังผีให้วางแก้เกมหรือไม่
        if (HasSpawnerCardAvailable()) return;

        // FIX บัก 9: Planning ถัดไปอาจจั่วได้การ์ดรังผี ให้รอผลการจั่วก่อนตัดสิน
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

    /// <summary>Round ถัดไปยังมี และจะจั่วการ์ด และกองจั่วมีการ์ดรังผีอยู่ (ถ้าไม่มี การรอก็ไม่ช่วยอะไร)</summary>
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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตรวจจับสภาวะ "ขบวนผีสูญพันธุ์" (Extinction Defeat):
/// หลังเข้าสู่ช่วงเล่นจริง (GameState = Playing) และพ้น Grace Period แล้ว หาก:
/// 1. ไม่มีผีที่มีชีวิตอยู่บนถนนเลย (EntityRegistry.ActiveCount == 0)
/// 2. ไม่มีรังผี (TowerSpawnable) ตั้งอยู่บนกระดานที่จะผลิตผีออกมาได้อีก
/// 3. ไม่มีการ์ดบนมือที่สามารถนำมาวางผลิตผีได้
/// ระบบจะสั่งแจ้งเตือน Game Over ทันที
/// </summary>
[DisallowMultipleComponent]
public class ParadeExtinctionChecker : MonoBehaviour
{
    [Header("Extinction Check Timings")]
    [Tooltip("ระยะเวลาหน่วงก่อนเริ่มตรวจจับการสูญพันธุ์ (ป้องกันเช็กช่วงรอยต่อตอนเริ่มปล่อยตัว)")]
    [SerializeField] private float initialGracePeriod = 6.0f;
    [SerializeField] private float checkInterval = 1.5f;

    private IEnumerator Start()
    {
        // 1. รอจนเข้าช่วงเล่นจริง (วางศาลเจ้าครบ)
        yield return new WaitUntil(() => GameStateManager.IsPlaying);

        // 2. รอให้รังผีชุดแรกทำงานก่อน
        yield return new WaitForSeconds(initialGracePeriod);

        // 3. วนเช็กจนกว่าเกมจะจบ
        WaitForSeconds interval = new WaitForSeconds(checkInterval);
        while (GameStateManager.IsPlaying)
        {
            EvaluateExtinctionCondition();
            yield return interval;
        }
    }

    private void EvaluateExtinctionCondition()
    {
        // 1. ยังมีผีเดินอยู่บนถนนหรือไม่
        if (EntityRegistry.ActiveCount > 0)
        {
            return;
        }

        // 2. ยังมีรังผี (TowerSpawnable) ที่วางอยู่บนกระดานและยังไม่พังหรือไม่
        //    ใช้ TowerBase.ActiveTowers (เฉพาะที่วางจริงแล้ว) แทนการ FindObjectsByType ทั้งฉาก
        IReadOnlyList<TowerBase> towers = TowerBase.ActiveTowers;
        for (int i = 0; i < towers.Count; i++)
        {
            TowerBase tower = towers[i];
            if (tower != null && !tower.IsDead && tower.GetComponent<TowerSpawnable>() != null)
            {
                return;
            }
        }

        // 3. ในมือยังมีการ์ดรังผีให้วางแก้เกมหรือไม่
        if (HasSpawnerCardInHand())
        {
            return;
        }

        Debug.LogWarning("<color=red>[ParadeExtinctionChecker]</color> ผีตายหมดกระดาน ไร้รังผี และไม่มีการ์ดให้วางต่อ — ขบวนผีสูญพันธุ์!");
        if (GameOverController.Instance != null)
        {
            GameOverController.Instance.TriggerDefeatByExtinction();
        }
    }

    private bool HasSpawnerCardInHand()
    {
        CardDeckController deck = CardDeckController.Instance;
        if (deck == null || deck.CardCount == 0) return false;

        IReadOnlyList<CardUI> cards = deck.CardsInHand;
        for (int i = 0; i < cards.Count; i++)
        {
            CardData data = cards[i] != null ? cards[i].CardData : null;
            if (data != null && data.TowerPrefab != null && data.TowerPrefab.GetComponent<TowerSpawnable>() != null)
            {
                return true;
            }
        }

        return false;
    }
}

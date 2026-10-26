using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// แจกการ์ดเริ่มต้นในช่วง Phase 1 (Setup & Dawn):
/// - แจกการ์ดศาลเจ้า (TowerDefenseBase) ตามระดับความยาก (เช่น 2 ใบ)
/// - แจกการ์ดรังผีเริ่มต้น (TowerSpawnable) จำนวน 2 ใบ
///
/// ไม่ต้องล็อก Spacebar เองอีกแล้ว: GameState เริ่มที่ Setup อยู่แล้ว
/// และจะเปลี่ยนเป็น Playing เองเมื่อวางศาลเจ้าครบตามที่ตั้งไว้ที่นี่
/// </summary>
[DisallowMultipleComponent]
public class SetupPhaseDeckSeeder : MonoBehaviour
{
    [Header("References (เว้นว่างได้ ระบบจะใช้ CardDeckController.Instance)")]
    [SerializeField] private CardDeckController deckController;

    [Header("Initial Shrine Cards (บังคับวางให้ครบก่อนปลดล็อก Spacebar)")]
    [SerializeField] private CardData shrineCardData;
    [SerializeField] private int shrineCardCount = 2;

    [Header("Initial Spawner Cards (รังผีเริ่มต้น 2 ใบ)")]
    [SerializeField] private List<CardData> initialSpawnerCards = new List<CardData>();

    private void Start()
    {
        if (deckController == null)
        {
            deckController = CardDeckController.Instance;
        }

        DealInitialSetupHand();
    }

    [ContextMenu("Deal Initial Setup Hand")]
    public void DealInitialSetupHand()
    {
        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.SetRequiredShrinesCount(shrineCardCount);
        }

        if (deckController == null)
        {
            Debug.LogWarning("[SetupPhaseDeckSeeder] ไม่พบ CardDeckController ในฉาก!");
            return;
        }

        // 1. แจกการ์ดศาลเจ้าตามจำนวนที่กำหนด
        if (shrineCardData != null)
        {
            for (int i = 0; i < shrineCardCount; i++)
            {
                deckController.AddCardToHand(shrineCardData);
            }
        }

        // 2. แจกการ์ดรังผีเริ่มต้น
        for (int i = 0; i < initialSpawnerCards.Count; i++)
        {
            if (initialSpawnerCards[i] != null)
            {
                deckController.AddCardToHand(initialSpawnerCards[i]);
            }
        }
    }
}

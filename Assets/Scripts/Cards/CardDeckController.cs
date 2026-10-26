using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// พฤติกรรมเมื่อการ์ดในมือเต็มแล้วมีการ์ดใหม่เข้ามา
/// </summary>
public enum HandFullBehavior
{
    DiscardOldest,  // สลายการ์ดใบซ้ายสุดแล้วรับใบใหม่ (สไตล์ Loop Hero)
    RejectNew       // ไม่รับการ์ดใหม่ การ์ดที่เข้ามาจะหายไป
}

/// <summary>
/// ควบคุมแถบการ์ดในมือผู้เล่นแนวนอนสไตล์ Loop Hero
/// </summary>
[DisallowMultipleComponent]
public class CardDeckController : SingletonBehaviour<CardDeckController>
{
    [Header("Hand UI Container")]
    [SerializeField] private Transform handContainer;
    [SerializeField] private CardUI cardUIPrefab;

    [Header("Deck Configuration")]
    [SerializeField] private List<CardData> availableCards = new List<CardData>();
    [SerializeField] private int startingHandSize = 3;
    [SerializeField] private int maxHandSize = 7;

    [Header("Hand Full Rule")]
    [Tooltip("DiscardOldest = ลบการ์ดแรกสุดแล้วรับใบใหม่ / RejectNew = ไม่รับการ์ดใหม่")]
    [SerializeField] private HandFullBehavior handFullBehavior = HandFullBehavior.DiscardOldest;

    private readonly List<CardUI> cardsInHand = new List<CardUI>();

    // Property สำหรับ ParadeExtinctionChecker และระบบภายนอก
    public IReadOnlyList<CardUI> CardsInHand => cardsInHand;
    public int CardCount => cardsInHand.Count;
    public int MaxHandSize => maxHandSize;
    public bool IsHandFull => cardsInHand.Count >= maxHandSize;
    public HandFullBehavior FullBehavior => handFullBehavior;

    /// <summary>ส่งสัญญาณเมื่อการ์ดใหม่ถูกปฏิเสธเพราะมือเต็ม (ใช้ทำ UI แจ้งเตือนได้)</summary>
    public event System.Action<CardData> OnCardRejected;

    private void Start()
    {
        if (cardsInHand.Count == 0 && startingHandSize > 0)
        {
            DrawStartingHand();
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        // กดปุ่ม C เพื่อทดสอบจั่วการ์ดในโหมดพัฒนา (ไม่ทับปุ่ม D ที่ใช้หมุนกล้อง)
        if (IsDrawShortcutPressed())
        {
            DrawRandomCard();
        }
#endif
    }

    private bool IsDrawShortcutPressed()
    {
        return GameInput.DebugDrawCardPressed;
    }

    public void DrawStartingHand()
    {
        for (int i = 0; i < startingHandSize; i++)
        {
            DrawRandomCard();
        }
    }

    [ContextMenu("Draw Random Card (Test)")]
    public void DrawRandomCard()
    {
        if (availableCards == null || availableCards.Count == 0)
        {
            return;
        }

        CardData randomData = availableCards[Random.Range(0, availableCards.Count)];
        AddCardToHand(randomData);
    }

    /// <summary>
    /// เพิ่มการ์ดเข้ามือ คืน null หากไม่ได้เพิ่ม (ข้อมูลไม่ครบ หรือมือเต็มและตั้งเป็น RejectNew)
    /// </summary>
    public CardUI AddCardToHand(CardData data)
    {
        if (data == null || cardUIPrefab == null || handContainer == null) return null;

        if (IsHandFull)
        {
            if (handFullBehavior == HandFullBehavior.RejectNew)
            {
                Debug.Log($"<color=orange>[CardDeck]</color> มือเต็ม ({cardsInHand.Count}/{maxHandSize}) ไม่รับการ์ด '{data.CardName}'");
                OnCardRejected?.Invoke(data);
                return null;
            }

            // DiscardOldest: สลายการ์ดใบซ้ายสุดออกตามสไตล์ Loop Hero
            if (cardsInHand.Count > 0)
            {
                RemoveCardFromHand(cardsInHand[0]);
            }
        }

        CardUI newCard = Instantiate(cardUIPrefab, handContainer);
        newCard.name = $"Card_{data.CardName}";
        newCard.Setup(data, this);

        cardsInHand.Add(newCard);
        newCard.transform.SetSiblingIndex(cardsInHand.Count - 1);

        return newCard;
    }

    public void RemoveCardFromHand(CardUI card)
    {
        if (card == null) return;

        if (cardsInHand.Remove(card))
        {
            Destroy(card.gameObject);
        }
    }
}
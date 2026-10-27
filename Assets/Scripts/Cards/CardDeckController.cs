using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// พฤติกรรมเมื่อการ์ดในมือเต็มแล้วมีการ์ดใหม่เข้ามา
/// </summary>
public enum HandFullBehavior
{
    DiscardOldest,
    RejectNew
}

/// <summary>
/// ควบคุมแถบการ์ดในมือผู้เล่นแนวนอนสไตล์ Loop Hero
/// เพดานมือการ์ดอ่านจาก GameRulesData.MaxHandSize
/// </summary>
[DisallowMultipleComponent]
public class CardDeckController : SingletonBehaviour<CardDeckController>
{
    [Header("Hand UI Container")]
    [SerializeField] private Transform handContainer;
    [SerializeField] private CardUI cardUIPrefab;

    [Header("Deck Configuration")]
    [SerializeField] private List<CardData> availableCards = new List<CardData>();
    [SerializeField] private int startingHandSize = 0;
    [Tooltip("ค่าสำรองกรณีไม่มี GameStateManager ในฉาก")]
    [SerializeField, Min(1)] private int fallbackMaxHandSize = 7;
    [Tooltip("จั่วการ์ดประจำ Round (cardsDrawnPerRound) ตั้งแต่วันแรก (Round 1) ด้วยหรือไม่")]
    [SerializeField] private bool drawPerRoundOnRoundOne = true;

    [Header("Hand Full Rule")]
    [Tooltip("DiscardOldest = ลบการ์ดแรกสุดแล้วรับใบใหม่ / RejectNew = ไม่รับการ์ดใหม่")]
    [SerializeField] private HandFullBehavior handFullBehavior = HandFullBehavior.DiscardOldest;

    private readonly List<CardUI> cardsInHand = new List<CardUI>();
    private readonly List<CardData> nightDrawablePool = new List<CardData>(16);

    public IReadOnlyList<CardUI> CardsInHand => cardsInHand;
    public int CardCount => cardsInHand.Count;
    public int MaxHandSize => GameStateManager.Instance != null
        ? GameStateManager.Instance.Rules.MaxHandSize
        : Mathf.Max(1, fallbackMaxHandSize);
    public bool IsHandFull => cardsInHand.Count >= MaxHandSize;
    public HandFullBehavior FullBehavior => handFullBehavior;

    /// <summary>ส่งสัญญาณเมื่อการ์ดใหม่ถูกปฏิเสธเพราะมือเต็ม (ใช้ทำ UI แจ้งเตือนได้)</summary>
    public event Action<CardData> OnCardRejected;

    private void Start()
    {
        RebuildNightDrawablePool();
        LootStash.RegisterPools(null, nightDrawablePool);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted += HandleRoundStarted;
        }
        else if (cardsInHand.Count == 0 && startingHandSize > 0)
        {
            DrawStartingHand();
        }
    }

    protected override void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted -= HandleRoundStarted;
        }

        base.OnDestroy();
    }

    private void Update()
    {
#if UNITY_EDITOR
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

    private void HandleRoundStarted(int round, int turns)
    {
        if (GameStateManager.Instance == null) return;
        if (round == 1 && !drawPerRoundOnRoundOne) return;

        int drawCount = GameStateManager.Instance.Rules.CardsDrawnPerRound;
        DrawCards(drawCount);
    }

    public void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawRandomCard();
        }
    }

    public void DrawStartingHand()
    {
        DrawCards(startingHandSize);
    }

    [ContextMenu("Draw Random Card (Test)")]
    public void DrawRandomCard()
    {
        CardData randomData = PickRandomPoolCard();
        if (randomData != null)
        {
            AddCardToHand(randomData);
        }
    }

    /// <summary>สุ่มเลือกการ์ด 1 ใบจากกองกลางด้วย GameRandom (ตัดการ์ดศาลเจ้าออกในช่วงกลางคืน)</summary>
    public CardData PickRandomPoolCard()
    {
        RebuildNightDrawablePool();

        if (nightDrawablePool.Count > 0)
        {
            return GameRandom.Pick(nightDrawablePool);
        }

        if (availableCards != null && availableCards.Count > 0)
        {
            return GameRandom.Pick(availableCards);
        }

        return null;
    }

    /// <summary>กองที่ใช้จั่วจริง (ตัดการ์ดศาลเจ้าแล้ว) มีการ์ดรังผีอยู่หรือไม่</summary>
    public bool CanDrawSpawnerCard()
    {
        RebuildNightDrawablePool();

        IReadOnlyList<CardData> pool = nightDrawablePool.Count > 0 ? (IReadOnlyList<CardData>)nightDrawablePool : availableCards;
        if (pool == null) return false;

        for (int i = 0; i < pool.Count; i++)
        {
            CardData card = pool[i];
            if (card != null && card.TowerPrefab != null && card.TowerPrefab.GetComponent<TowerSpawnable>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildNightDrawablePool()
    {
        nightDrawablePool.Clear();
        if (availableCards == null) return;

        for (int i = 0; i < availableCards.Count; i++)
        {
            CardData card = availableCards[i];
            if (card == null) continue;

            if (card.TowerPrefab != null && card.TowerPrefab.GetComponent<TowerObjective>() != null)
            {
                continue;
            }

            nightDrawablePool.Add(card);
        }
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
                Debug.Log($"<color=orange>[CardDeck]</color> Hand full ({cardsInHand.Count}/{MaxHandSize}), rejected '{data.CardName}'");
                OnCardRejected?.Invoke(data);
                return null;
            }

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
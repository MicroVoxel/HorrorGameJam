using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// บันทึกสถานะก่อนเริ่ม Resolution ของแต่ละ Round (GDD 3 & 7.2)
/// ใช้ OfferingInstance เดียวกันทั้งสำหรับช่องโต๊ะบูชาและของในกระเป๋า
/// </summary>
[Serializable]
public class RoundSnapshot
{
    [Serializable]
    public struct EntityRecord
    {
        public string entityName;
        public int pathIndex;
        public int pathProgress;
        public float currentHp;
        public float maxHp;
        public int mutationStage;
    }

    [Serializable]
    public struct AltarRecord
    {
        public OfferingType category;
        public OfferingInstance offering;
    }

    public int roundNumber;
    public int roundSeed;
    public List<EntityRecord> entities = new List<EntityRecord>();
    public List<AltarRecord> altarSlots = new List<AltarRecord>();
    public List<OfferingInstance> backpackOfferings = new List<OfferingInstance>();
    public List<CardData> handCards = new List<CardData>();

    public static RoundSnapshot CaptureCurrent { get; private set; }

    public static RoundSnapshot Capture(int round, int seed)
    {
        RoundSnapshot snap = new RoundSnapshot
        {
            roundNumber = round,
            roundSeed = seed
        };

        IReadOnlyList<EntityBase> activeEntities = EntityRegistry.ActiveEntities;
        for (int i = 0; i < activeEntities.Count; i++)
        {
            EntityBase e = activeEntities[i];
            if (e == null || e.IsDead) continue;

            e.TryGetComponent(out EntityWalkable walkable);
            e.TryGetComponent(out EntityMutatable mutatable);

            snap.entities.Add(new EntityRecord
            {
                entityName = e.EntityName,
                pathIndex = walkable != null ? walkable.CurrentPathIndex : 0,
                pathProgress = walkable != null ? walkable.PathProgress : 0,
                currentHp = e.CurrentHealth,
                maxHp = e.MaxHealth,
                mutationStage = mutatable != null ? mutatable.MutationCount : 0
            });
        }

        if (AltarManager.Instance != null)
        {
            IReadOnlyList<ActiveAltarSlotState> slots = AltarManager.Instance.AltarSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                ActiveAltarSlotState s = slots[i];
                snap.altarSlots.Add(new AltarRecord
                {
                    category = s.slotCategory,
                    offering = s.offering
                });
            }
        }

        if (InventorySlotManager.Instance != null)
        {
            IReadOnlyList<OfferingInstance> bag = InventorySlotManager.Instance.Entries;
            for (int i = 0; i < bag.Count; i++)
            {
                snap.backpackOfferings.Add(bag[i]);
            }
        }

        if (CardDeckController.Instance != null)
        {
            IReadOnlyList<CardUI> hand = CardDeckController.Instance.CardsInHand;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] != null && hand[i].CardData != null)
                {
                    snap.handCards.Add(hand[i].CardData);
                }
            }
        }

        CaptureCurrent = snap;
        return snap;
    }
}
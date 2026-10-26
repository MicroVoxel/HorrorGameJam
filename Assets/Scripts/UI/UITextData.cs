using System.Text;
using UnityEngine;

/// <summary>
/// ข้อความทั้งหมดที่ขึ้นบน UI (ค่าตั้งต้นเป็นภาษาอังกฤษ ห้ามใส่ emoji) แก้ได้ใน Inspector ทุกข้อความ
/// ข้อความที่มีตัวแปรใช้ placeholder เช่น {current} แล้วเรียก UITextData.Format(template, "current", 3, "total", 5)
/// ชื่อ/คำอธิบายของการ์ดและไอเทมยังอยู่ใน CardData / OfferingItemData ตามเดิม
/// </summary>
[CreateAssetMenu(fileName = "UIText", menuName = "GameJam/UI Text Data")]
public class UITextData : ScriptableObject
{
    [Header("Phase Labels")]
    public string phaseDawn = "Dawn - Place Your Shrines";
    public string phasePlanning = "Moonless - Planning";
    public string phaseResolution = "Moonlit - Ritual in Progress";
    public string phaseRoundEnd = "Moonlit - Round Complete";
    public string phaseEnded = "The Night Is Over";

    [Header("Round and Turn")]
    [Tooltip("Placeholders: {current}, {total}")]
    public string roundLabel = "Round {current} / {total}";
    [Tooltip("Placeholders: {current}, {total}")]
    public string turnLabel = "Turn {current} / {total}";
    [Tooltip("Placeholders: {turns}")]
    public string nextRitualInfo = "Next ritual: {turns} turns";
    [Tooltip("Placeholders: {current}, {total}")]
    public string incenseLabel = "Incense {current} / {total}";

    [Header("Buttons")]
    public string btnStartNight = "Start Night";
    public string btnBeginRitual = "Begin Ritual";
    public string btnContinue = "Continue";
    public string btnSkip = "Skip";
    public string btnRetry = "Retry";
    public string btnMainMenu = "Main Menu";
    [Tooltip("Placeholders: {speed}")]
    public string speedLabel = "{speed}x";

    [Header("Hints")]
    [Tooltip("Placeholders: {placed}, {required}")]
    public string startNightDisabledHint = "Place all shrines to start ({placed} / {required})";
    public string tooltipEmptyTip = "Hover over anything to see details. Hold Shift for more.";
    public string tooltipDetailHint = "Hold Shift for details";
    public string placeRotateHint = "Q / E: Rotate";

    [Header("Placement Errors")]
    public string cannotPlaceNotAdjacent = "Must be placed next to the path";
    public string cannotPlaceAdjacentShrine = "Cannot be placed next to another shrine";
    public string cannotPlaceOccupied = "This tile is occupied";
    public string cannotPlaceGroundType = "Cannot be placed on this terrain";
    public string cannotPlaceWrongPhase = "Cannot be placed during the ritual";
    [Tooltip("Placeholders: {count}")]
    public string cannotPlaceNoThreatPath = "Needs at least {count} path tiles in range";

    [Header("Objective HUD")]
    [Tooltip("Placeholders: {placed}, {required}")]
    public string objectiveSetup = "Shrines placed: {placed} / {required}";
    [Tooltip("Placeholders: {current}, {total}")]
    public string objectiveRemaining = "Shrines remaining: {current} / {total}";

    [Header("Game Over")]
    public string victoryTitle = "The Parade Triumphs";
    [Tooltip("Placeholders: {round}")]
    public string victoryDetail = "Every shrine was destroyed on night {round}.";
    public string defeatTimeTitle = "Sunlight Prevails";
    public string defeatTimeReason = "All the incense burned out before the shrines fell.";
    public string defeatExtinctionTitle = "The Parade Is Extinct";
    public string defeatExtinctionReason = "Every spirit has fallen and no spawner remains.";

    [Header("Altar")]
    public string altarDrink = "Drink";
    public string altarSavory = "Savory";
    public string altarSweet = "Sweet";
    public string altarFragrance = "Fragrance";
    public string altarUtility = "Utility";
    public string altarEmpty = "Empty";
    [Tooltip("Placeholders: {rounds}")]
    public string altarFresh = "Fresh ({rounds} left)";
    public string altarRotten = "Rotten";

    [Header("Inventory & Swap")]
    public string inventoryNextDiscardHint = "Oldest slot: discarded next if backpack overflows.";
    [Tooltip("Placeholders: {remaining}, {total}")]
    public string swapQuotaLabel = "Swaps: {remaining} / {total}";
    public string swapDeniedNotPlanning = "Can only swap spirits during Planning";
    public string swapDeniedNoQuota = "No formation swaps remaining this round";
    [Tooltip("Placeholders: {max}")]
    public string swapDeniedTooFar = "Can only swap spirits within {max} path tiles";

    [Header("Round Summary")]
    [Tooltip("Placeholders: {round}")]
    public string summaryTitle = "Round {round} Complete";
    [Tooltip("Placeholders: {damage}, {remaining}, {total}")]
    public string summaryShrinesLine = "Shrine damage dealt: {damage}  |  Shrines remaining: {remaining} / {total}";
    [Tooltip("Placeholders: {died}, {mutated}")]
    public string summarySpiritsLine = "Spirits fallen: {died}  |  Mutated: {mutated}";
    [Tooltip("Placeholders: {items}, {cards}")]
    public string summaryLootLine = "Loot gained: {items} offering(s), {cards} card(s)";
    [Tooltip("Placeholders: {rotten}, {sticks}")]
    public string summaryAltarLine = "Rotten this round: {rotten}  |  Incense left: {sticks}";

    /// <summary>แทนที่ {key} ด้วยค่า โดยส่งเป็นคู่ key, value ต่อกัน เช่น Format(t, "current", 2, "total", 5)</summary>
    public static string Format(string template, params object[] keyValuePairs)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        if (keyValuePairs == null || keyValuePairs.Length < 2) return template;

        StringBuilder sb = new StringBuilder(template);
        for (int i = 0; i + 1 < keyValuePairs.Length; i += 2)
        {
            sb.Replace("{" + keyValuePairs[i] + "}", keyValuePairs[i + 1] != null ? keyValuePairs[i + 1].ToString() : string.Empty);
        }

        return sb.ToString();
    }

    public string GetPhaseLabel(GameState state)
    {
        switch (state)
        {
            case GameState.Dawn: return phaseDawn;
            case GameState.Planning: return phasePlanning;
            case GameState.Resolution: return phaseResolution;
            case GameState.RoundEnd: return phaseRoundEnd;
            case GameState.Ended: return phaseEnded;
            default: return string.Empty;
        }
    }

    public string GetOfferingCategoryName(OfferingType type)
    {
        switch (type)
        {
            case OfferingType.Drink: return altarDrink;
            case OfferingType.Savory: return altarSavory;
            case OfferingType.Sweet: return altarSweet;
            case OfferingType.Fragrance: return altarFragrance;
            case OfferingType.Utility: return altarUtility;
            default: return type.ToString();
        }
    }
}
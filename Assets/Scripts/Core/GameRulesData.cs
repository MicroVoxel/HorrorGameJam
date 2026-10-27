using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ค่าตั้งต้นของกติกาเกมทั้งหมด (จำนวน Round/Turn, โควตา, ความจุกระเป๋า/มือการ์ด, ค่าบาลานซ์กลาง)
/// ลากใส่ GameStateManager แล้วปรับค่าใน Inspector ได้โดยไม่ต้องแตะโค้ด
/// 1 ก้านธูป = 1 Round
/// </summary>
[CreateAssetMenu(fileName = "GameRules", menuName = "GameJam/Game Rules")]
public class GameRulesData : ScriptableObject
{
    [Header("Rounds (1 incense stick = 1 round)")]
    [SerializeField, Min(1)] private int totalRounds = 7;
    [Tooltip("If on, every round uses Uniform Turns and the list below is ignored.")]
    [SerializeField] private bool useUniformTurns = false;
    [SerializeField, Min(1)] private int uniformTurns = 5;
    [Tooltip("Turns per round. Index 0 = Round 1. If the list is shorter than Total Rounds, the last value is reused.")]
    [SerializeField] private List<int> turnsPerRound = new List<int> { 4, 4, 5, 5, 6, 6, 7 };

    [Header("Dawn (Setup)")]
    [Tooltip("Shrines the player must place during Dawn before the night can start.")]
    [SerializeField, Min(1)] private int requiredShrines = 2;

    [Header("Cards & Backpack Limits")]
    [Tooltip("Spawner cards dealt at the start of Round 1 Planning (not during Dawn).")]
    [SerializeField, Min(0)] private int startingSpawnerCards = 2;
    [Tooltip("Cards drawn at the start of every Planning phase (safety net so the player never runs dry).")]
    [SerializeField, Min(0)] private int cardsDrawnPerRound = 1;
    [Tooltip("Maximum cards the player can hold in hand at once.")]
    [SerializeField, Min(1)] private int maxHandSize = 7;
    [Tooltip("Maximum single-slot offerings in the 4x2 backpack.")]
    [SerializeField, Min(1)] private int maxBackpackSlots = 8;

    [Header("Resolution Pacing")]
    [Tooltip("Playback speed options shown in the speed control.")]
    [SerializeField] private float[] speedSteps = { 1f, 2f, 4f };
    [SerializeField, Min(0)] private int defaultSpeedIndex = 0;
    [Tooltip("Seconds between actions inside a turn at 1x speed.")]
    [SerializeField, Min(0f)] private float turnStepDelay = 0.35f;
    [Tooltip("Skip turns in which no unit has anything to do.")]
    [SerializeField] private bool autoSkipEmptyTurns = true;
    [Tooltip("End the Resolution early when no spirits are left and none can be spawned.")]
    [SerializeField] private bool endResolutionEarlyOnWipe = true;

    [Header("Randomness")]
    [Tooltip("0 = pick a random seed every run. Any other value = reproducible run.")]
    [SerializeField] private int baseSeed = 0;

    [Header("Planning Limits")]
    [Tooltip("Free formation swaps allowed per Planning phase.")]
    [SerializeField, Min(0)] private int swapQuotaPerRound = 2;
    [Tooltip("Max distance (in path tiles) between two spirits that can be swapped.")]
    [SerializeField, Min(1)] private int maxSwapPathDistance = 2;

    [Header("Keywords")]
    [SerializeField, Min(0)] private int maxKeywordsPerUnit = 2;
    [Tooltip("Maximum Last Breath chain explosions resolved per turn cleanup.")]
    [SerializeField, Min(1)] private int maxChainPerCleanup = 64;

    [Header("Threat Towers (placed by the player)")]
    [Tooltip("Minimum number of path tiles that must fall inside the tower's range pattern.")]
    [SerializeField, Min(0)] private int minThreatPathTiles = 2;
    [Tooltip("Extra loot rolls per surviving Threat Tower at Round End.")]
    [SerializeField, Min(0)] private int threatLootBonusPerTower = 1;
    [Tooltip("Chance for each Threat Tower bonus loot roll to grant an offering.")]
    [SerializeField, Range(0f, 1f)] private float threatOfferingRollChance = 0.65f;
    [Tooltip("Extra card drop chance per surviving Threat Tower.")]
    [SerializeField, Range(0f, 1f)] private float threatCardChanceBonus = 0.10f;
    [Tooltip("Upper limit of the total bonus card chance.")]
    [SerializeField, Range(0f, 1f)] private float threatCardChanceCap = 0.5f;

    // ---------- Rounds ----------
    public int TotalRounds => Mathf.Max(1, totalRounds);
    public int RequiredShrines => Mathf.Max(1, requiredShrines);

    /// <summary>จำนวนเทิร์นของ Round ที่ระบุ (นับจาก 1) ถ้ารายการสั้นกว่าจะใช้ค่าตัวสุดท้ายต่อ</summary>
    public int GetTurnsForRound(int roundNumber)
    {
        if (useUniformTurns || turnsPerRound == null || turnsPerRound.Count == 0)
        {
            return Mathf.Max(1, uniformTurns);
        }

        int index = Mathf.Clamp(roundNumber - 1, 0, turnsPerRound.Count - 1);
        return Mathf.Max(1, turnsPerRound[index]);
    }

    // ---------- Cards & Backpack ----------
    public int StartingSpawnerCards => startingSpawnerCards;
    public int CardsDrawnPerRound => cardsDrawnPerRound;
    public int MaxHandSize => Mathf.Max(1, maxHandSize);
    public int MaxBackpackSlots => Mathf.Max(1, maxBackpackSlots);

    // ---------- Pacing ----------
    public int SpeedStepCount => speedSteps != null ? speedSteps.Length : 0;
    public int DefaultSpeedIndex => Mathf.Clamp(defaultSpeedIndex, 0, Mathf.Max(0, SpeedStepCount - 1));
    public float TurnStepDelay => turnStepDelay;
    public bool AutoSkipEmptyTurns => autoSkipEmptyTurns;
    public bool EndResolutionEarlyOnWipe => endResolutionEarlyOnWipe;

    public float GetSpeedMultiplier(int index)
    {
        if (speedSteps == null || speedSteps.Length == 0) return 1f;
        return Mathf.Max(0.1f, speedSteps[Mathf.Clamp(index, 0, speedSteps.Length - 1)]);
    }

    // ---------- Randomness ----------
    public int BaseSeed => baseSeed;

    // ---------- Planning ----------
    public int SwapQuotaPerRound => swapQuotaPerRound;
    public int MaxSwapPathDistance => maxSwapPathDistance;

    // ---------- Keywords ----------
    public int MaxKeywordsPerUnit => maxKeywordsPerUnit;
    public int MaxChainPerCleanup => Mathf.Max(1, maxChainPerCleanup);

    // ---------- Threat Towers ----------
    public int MinThreatPathTiles => minThreatPathTiles;
    public int ThreatLootBonusPerTower => threatLootBonusPerTower;
    public float ThreatOfferingRollChance => threatOfferingRollChance;
    public float ThreatCardChanceBonus => threatCardChanceBonus;
    public float ThreatCardChanceCap => threatCardChanceCap;

#if UNITY_EDITOR
    private void OnValidate()
    {
        totalRounds = Mathf.Max(1, totalRounds);
        maxHandSize = Mathf.Max(1, maxHandSize);
        maxBackpackSlots = Mathf.Max(1, maxBackpackSlots);
        maxChainPerCleanup = Mathf.Max(1, maxChainPerCleanup);

        if (turnsPerRound != null)
        {
            for (int i = 0; i < turnsPerRound.Count; i++)
            {
                if (turnsPerRound[i] < 1) turnsPerRound[i] = 1;
            }

            if (!useUniformTurns && turnsPerRound.Count > 0 && turnsPerRound.Count < totalRounds)
            {
                Debug.LogWarning($"[GameRulesData] Turns Per Round has {turnsPerRound.Count} entries but Total Rounds is {totalRounds}. The last value will be reused.", this);
            }
        }

        defaultSpeedIndex = Mathf.Clamp(defaultSpeedIndex, 0, Mathf.Max(0, SpeedStepCount - 1));
    }
#endif
}
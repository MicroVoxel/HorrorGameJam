using System;
using UnityEngine;

/// <summary>
/// สถานะหลักของด่าน มีค่าเดียวเสมอ
///
/// Dawn       = ตอนเช้า ยังไม่เริ่มเกม ผู้เล่นวางศาลเจ้าเริ่มต้น (มีเฉพาะการ์ดศาลเจ้า)
/// Planning   = กลางคืนช่วงวางแผน (Moonless) วางป้อม/รังผี ถวายของ สลับขบวน
/// Resolution = แสงจันทร์สาดลงมา (Moonlit) เกมเล่นเองเป็นเทิร์น ผู้เล่นดูอย่างเดียว
/// RoundEnd   = จบ Round สรุปผล ธูปมอด ของถวายลดอายุ
/// Ended      = ชนะหรือแพ้แล้ว
/// </summary>
public enum GameState
{
    Dawn,
    Planning,
    Resolution,
    RoundEnd,
    Ended
}

/// <summary>
/// ตัวถือสถานะเกมและลำดับ Round เพียงตัวเดียว ต้องมี 1 ตัวในซีนเกมเพลย์
/// ระบบอื่นอ่านสถานะผ่าน static property เช่น GameStateManager.IsPlanning
/// </summary>
[DisallowMultipleComponent]
public class GameStateManager : SingletonBehaviour<GameStateManager>
{
    [Header("Rules")]
    [Tooltip("ลาก GameRulesData ใส่ที่นี่ ถ้าเว้นว่างจะใช้ค่าตั้งต้นและขึ้น Warning")]
    [SerializeField] private GameRulesData rules;

    [Header("Runtime (read-only)")]
    [SerializeField] private GameState state = GameState.Dawn;
    [SerializeField] private int currentRound = 0;
    [SerializeField] private int turnsThisRound = 0;

    [Header("Test Helpers")]
    [Tooltip("ทดสอบเท่านั้น: เริ่มกลางคืนอัตโนมัติเมื่อวางศาลครบ (ใช้ระหว่างที่ยังไม่มีปุ่ม Start Night)")]
    [SerializeField] private bool debugAutoStartNight = false;

    private bool startNightAvailable;
    private bool warnedMissingRules;

    // ---------- Static state queries ----------
    public static GameState Current => Instance != null ? Instance.state : GameState.Dawn;
    public static bool IsDawn => Current == GameState.Dawn;
    public static bool IsPlanning => Current == GameState.Planning;
    public static bool IsResolution => Current == GameState.Resolution;
    public static bool IsRoundEnd => Current == GameState.RoundEnd;
    public static bool IsEnded => Current == GameState.Ended;

    /// <summary>ช่วงที่ผู้เล่นวางการ์ด ถวายของ และจัดขบวนได้ (Dawn และ Planning)</summary>
    public static bool IsPlayerActionPhase => IsDawn || IsPlanning;

    // ---------- Round info ----------
    public static int CurrentRound => Instance != null ? Instance.currentRound : 0;
    public static int TotalRounds => Instance != null ? Instance.Rules.TotalRounds : 0;
    public static int TurnsThisRound => Instance != null ? Instance.turnsThisRound : 0;

    /// <summary>ผู้เล่นกด Start Night ได้หรือยัง (อยู่ใน Dawn และวางศาลครบ)</summary>
    public static bool CanStartNight =>
        IsDawn && ObjectiveTracker.Instance != null && ObjectiveTracker.Instance.IsSetupComplete;

    public GameRulesData Rules
    {
        get
        {
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<GameRulesData>();
                if (!warnedMissingRules)
                {
                    warnedMissingRules = true;
                    Debug.LogWarning("[GameStateManager] ยังไม่ได้ลาก GameRulesData จึงใช้ค่าตั้งต้นชั่วคราว", this);
                }
            }

            return rules;
        }
    }

    // ---------- Events ----------
    /// <summary>ส่งสัญญาณทุกครั้งที่สถานะเปลี่ยน (HUD, แสง, แผงข้างใช้ฟัง)</summary>
    public event Action<GameState> OnStateChanged;

    /// <summary>เริ่ม Planning ของ Round ใหม่ (พารามิเตอร์: เลข Round, จำนวนเทิร์นของ Round นี้)</summary>
    public event Action<int, int> OnRoundStarted;

    /// <summary>จบ Round สุดท้ายแล้วศาลยังเหลือ (ผู้ฟังตัดสินแพ้ด้วยหมดเวลา)</summary>
    public event Action OnAllRoundsCompleted;

    /// <summary>ปุ่ม Start Night เปิด/ปิดใช้งาน (true = กดได้)</summary>
    public event Action<bool> OnStartNightAvailabilityChanged;

    /// <summary>
    /// ยิงหลังผู้ฟัง OnStateChanged(RoundEnd) ทุกตัวทำงานจบแล้ว (ของถวายเน่า, แจก loot)
    /// ระบบที่ต้องอ่านผลของ RoundEnd (เช่น RoundSummaryUI) ให้ฟังตัวนี้แทน เพื่อไม่ขึ้นกับลำดับ subscribe
    /// </summary>
    public event Action OnRoundEndProcessed;

    private void Start()
    {
        GameRandom.Initialize(Rules.BaseSeed);

        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.OnSetupProgressChanged += HandleSetupProgressChanged;
        }

        RefreshStartNightAvailability();
    }

    protected override void OnDestroy()
    {
        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.OnSetupProgressChanged -= HandleSetupProgressChanged;
        }

        base.OnDestroy();
    }

    // ---------- Flow control ----------

    /// <summary>Dawn -> Planning (Round 1) คืน false หากยังวางศาลไม่ครบหรือไม่ได้อยู่ใน Dawn</summary>
    public bool TryStartNight()
    {
        if (!CanStartNight) return false;

        currentRound = 1;
        BeginPlanningForCurrentRound();
        return true;
    }

    /// <summary>Planning -> Resolution (กดปุ่ม Begin Ritual) รีเซ็ตตัวสุ่มเป็น seed ของ Round นี้</summary>
    public bool TryBeginRitual()
    {
        if (state != GameState.Planning) return false;

        GameRandom.BeginRound(currentRound);
        RoundSnapshot.Capture(currentRound, GameRandom.CurrentSeed);
        SetState(GameState.Resolution);
        return true;
    }

    /// <summary>Resolution -> RoundEnd (เรียกโดย TurnManager เมื่อรันเทิร์นครบ)</summary>
    public void CompleteResolution()
    {
        if (state != GameState.Resolution) return;
        SetState(GameState.RoundEnd);
    }

    /// <summary>RoundEnd -> Planning ของ Round ถัดไป หากเป็น Round สุดท้ายจะประกาศ OnAllRoundsCompleted แทน</summary>
    public void FinishRoundEnd()
    {
        if (state != GameState.RoundEnd) return;

        if (currentRound >= Rules.TotalRounds)
        {
            OnAllRoundsCompleted?.Invoke();
            return;
        }

        currentRound++;
        BeginPlanningForCurrentRound();
    }

    /// <summary>
    /// จบเกม (ชนะ/แพ้) คืน false หากเกมจบไปแล้ว ใช้เป็นตัวกันเรียกซ้ำ
    /// เช่น ธูปหมดพร้อมศาลพังในเฟรมเดียวกัน จะมีผลแค่ครั้งแรก
    /// </summary>
    public static bool TryEndGame()
    {
        if (Instance == null)
        {
            Debug.LogError("[GameStateManager] ไม่พบ GameStateManager ในซีน กรุณาเพิ่มคอมโพเนนต์นี้");
            return false;
        }

        if (Instance.state == GameState.Ended) return false;

        Instance.SetState(GameState.Ended);
        return true;
    }

    [ContextMenu("Force Start Night (Test)")]
    public void ForceStartNight()
    {
        if (state != GameState.Dawn) return;

        currentRound = 1;
        BeginPlanningForCurrentRound();
    }

    [ContextMenu("Force Begin Ritual (Test)")]
    public void ForceBeginRitual()
    {
        TryBeginRitual();
    }

    // ---------- Internals ----------

    private void BeginPlanningForCurrentRound()
    {
        turnsThisRound = Rules.GetTurnsForRound(currentRound);
        SetState(GameState.Planning);
        OnRoundStarted?.Invoke(currentRound, turnsThisRound);
    }

    private void HandleSetupProgressChanged(int placedCount, int requiredCount)
    {
        RefreshStartNightAvailability();

        if (debugAutoStartNight && CanStartNight)
        {
            TryStartNight();
        }
    }

    private void RefreshStartNightAvailability()
    {
        bool available = CanStartNight;
        if (available == startNightAvailable) return;

        startNightAvailable = available;
        OnStartNightAvailabilityChanged?.Invoke(available);
    }

    private void SetState(GameState next)
    {
        if (state == next) return;

        state = next;
        OnStateChanged?.Invoke(next);

        if (next == GameState.RoundEnd && state == GameState.RoundEnd)
        {
            OnRoundEndProcessed?.Invoke();
        }

        RefreshStartNightAvailability();
    }
}
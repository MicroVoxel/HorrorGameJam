using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ธูป 7 ก้าน = 7 Round (ตัวแสดงผลล้วน ไม่ใช่ Timer)
/// - Planning: ก้านเต็ม
/// - Resolution: ก้านลดตามสัดส่วน turn / turnsThisRound เมื่อเริ่มแต่ละเทิร์น และมอดพอดีเมื่อจบเทิร์นสุดท้าย
/// - RoundEnd: ก้านดับ (ถ้าจบ Resolution เร็วก็ดับทันที) ส่ง OnIncenseStickBurnedOut
/// - จบ Round สุดท้ายแล้วศาลยังเหลือ: ส่งต่อ OnAllIncenseExhausted (GameOverController เดิมฟังตัวนี้อยู่)
/// การนับ Decay ของของถวายไม่ผูกกับคลาสนี้อีก (AltarManager ฟัง GameState.RoundEnd เอง)
/// </summary>
[DisallowMultipleComponent]
public class IncenseGaugeController : SingletonBehaviour<IncenseGaugeController>
{
    [Header("UI References (Optional)")]
    [SerializeField] private Image incenseFillBar;
    [SerializeField] private TextMeshProUGUI loopCounterText;
    [Tooltip("Optional: one icon per round. Spent sticks use the Spent Color.")]
    [SerializeField] private List<Image> stickIcons = new List<Image>();
    [SerializeField] private Color activeStickColor = Color.white;
    [SerializeField] private Color spentStickColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);

    [Header("Animation (display only)")]
    [Tooltip("Fill units per second at 1x speed. Skip snaps instantly.")]
    [SerializeField, Min(0.05f)] private float burnSpeed = 0.6f;

    private int currentLoop = 1;
    private int maxLoops = 7;
    private float displayedFill = 1f;
    private float targetFill = 1f;

    public int CurrentLoop => currentLoop;
    public int MaxLoops => maxLoops;
    public float NormalizedIncense => displayedFill;
    public bool IsBurningActive => GameStateManager.IsResolution;

    /// <summary>สัดส่วนธูปที่เหลือ (0-1) ส่งเมื่อค่าที่แสดงเปลี่ยน</summary>
    public event Action<float> OnIncenseValueChanged;

    /// <summary>ก้านของ Round นี้มอดแล้ว (พารามิเตอร์: เลข Round)</summary>
    public event Action<int> OnIncenseStickBurnedOut;

    /// <summary>จบ Round สุดท้ายแล้วศาลยังเหลือ</summary>
    public event Action OnAllIncenseExhausted;

    private void Start()
    {
        GameStateManager gsm = GameStateManager.Instance;
        if (gsm != null)
        {
            maxLoops = gsm.Rules.TotalRounds;
            currentLoop = Mathf.Max(1, GameStateManager.CurrentRound);

            gsm.OnRoundStarted += HandleRoundStarted;
            gsm.OnStateChanged += HandleStateChanged;
            gsm.OnAllRoundsCompleted += HandleAllRoundsCompleted;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStarted += HandleTurnStarted;
        }

        displayedFill = targetFill = 1f;
        UpdateUI();
    }

    protected override void OnDestroy()
    {
        GameStateManager gsm = GameStateManager.Instance;
        if (gsm != null)
        {
            gsm.OnRoundStarted -= HandleRoundStarted;
            gsm.OnStateChanged -= HandleStateChanged;
            gsm.OnAllRoundsCompleted -= HandleAllRoundsCompleted;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStarted -= HandleTurnStarted;
        }

        base.OnDestroy();
    }

    private void Update()
    {
        if (Mathf.Approximately(displayedFill, targetFill)) return;

        TurnManager tm = TurnManager.Instance;
        float next;

        if (tm != null && tm.IsFastForward)
        {
            next = targetFill;
        }
        else
        {
            float speed = burnSpeed * (tm != null ? tm.SpeedMultiplier : 1f);
            next = Mathf.MoveTowards(displayedFill, targetFill, speed * Time.unscaledDeltaTime);
        }

        SetDisplayedFill(next);
    }

    // ---------- Handlers ----------

    private void HandleRoundStarted(int round, int turns)
    {
        currentLoop = round;
        maxLoops = GameStateManager.Instance.Rules.TotalRounds;
        targetFill = 1f;
        SetDisplayedFill(1f);
    }

    private void HandleTurnStarted(int turn, int totalTurns)
    {
        // ก้านลดระหว่างเทิร์น และถึงเป้าหมายพอดีตอนจบเทิร์น (เทิร์นสุดท้าย = 0)
        targetFill = Mathf.Clamp01(1f - (float)turn / Mathf.Max(1, totalTurns));
    }

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.RoundEnd) return;

        targetFill = 0f;
        AudioManager.Bell();
        UpdateUI();
        OnIncenseStickBurnedOut?.Invoke(currentLoop);
    }

    private void HandleAllRoundsCompleted()
    {
        OnAllIncenseExhausted?.Invoke();
    }

    // ---------- Display ----------

    private void SetDisplayedFill(float value)
    {
        if (Mathf.Approximately(displayedFill, value) && incenseFillBar != null && Mathf.Approximately(incenseFillBar.fillAmount, value)) return;

        displayedFill = value;
        UpdateUI();
        OnIncenseValueChanged?.Invoke(displayedFill);
    }

    private void UpdateUI()
    {
        if (incenseFillBar != null)
        {
            incenseFillBar.fillAmount = displayedFill;
        }

        if (loopCounterText != null)
        {
            loopCounterText.text = UITextData.Format(UITextProvider.Text.incenseLabel, "current", currentLoop, "total", maxLoops);
        }

        for (int i = 0; i < stickIcons.Count; i++)
        {
            if (stickIcons[i] == null) continue;

            bool spent = i < currentLoop - 1 || (i == currentLoop - 1 && displayedFill <= 0f);
            stickIcons[i].color = spent ? spentStickColor : activeStickColor;
        }
    }

    /// <summary>เลิกใช้: v2 ไม่มีค่าธูปจากการสลับขบวน (FormationSwapManager จะแก้ใน Part 4) เก็บไว้ให้คอมไพล์ผ่านเท่านั้น</summary>
    [Obsolete("Swapping no longer costs incense in v2.")]
    public void ConsumeIncenseInstant(float amount) { }
}
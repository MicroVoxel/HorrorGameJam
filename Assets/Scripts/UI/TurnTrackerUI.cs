using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ศูนย์กลาง HUD แถบบน (รวม TurnTrackerUI + IncenseGaugeController ตามตัวเลือก 1C, 2A, 3B):
/// - แสดงชื่อเฟสปัจจุบัน (phaseStatusText)
/// - แสดงตัวนับรอบ/ก้านธูป (roundText: Round X / 7)
/// - แสดงหลอดไฟธูป (incenseFillBar) และไอคอนก้านธูป 7 ก้าน (stickIcons)
/// - แสดง Turn X / N (ใน Resolution) หรือ Next ritual: N turns (ใน Planning) พร้อมเม็ด Pip ของเทิร์น
/// </summary>
[DisallowMultipleComponent]
public class TurnTrackerUI : SingletonBehaviour<TurnTrackerUI>
{
    [Header("Phase & Round Labels")]
    [Tooltip("(ตัวเลือก 3B) ป้ายแสดงชื่อเฟสปัจจุบันจุดเดียวบน HUD บน")]
    [SerializeField] private TextMeshProUGUI phaseStatusText;
    [Tooltip("ป้ายแสดงตัวนับรอบ (Round X / 7) จุดเดียว")]
    [SerializeField] private TextMeshProUGUI roundText;
    [Tooltip("แสดง 'Next ritual: N turns' ใน Planning และ 'Turn X / N' ใน Resolution")]
    [SerializeField] private TextMeshProUGUI turnOrNextRitualText;

    [Header("Turn Pips (Optional)")]
    [SerializeField] private Transform pipContainer;
    [SerializeField] private Image pipPrefab;
    [SerializeField] private List<Image> pipImages = new List<Image>();

    [Header("Pip Colors")]
    [SerializeField] private Color passedPipColor = new Color(0.85f, 0.78f, 0.55f, 1.0f);
    [SerializeField] private Color currentPipColor = new Color(1.0f, 0.95f, 0.35f, 1.0f);
    [SerializeField] private Color upcomingPipColor = new Color(0.35f, 0.35f, 0.42f, 0.45f);

    [Header("Incense Gauge Visuals (Merged from IncenseGaugeController - 1C)")]
    [SerializeField] private Image incenseFillBar;
    [Tooltip("Optional: one icon per round. Spent sticks use the Spent Color.")]
    [SerializeField] private List<Image> stickIcons = new List<Image>();
    [SerializeField] private Color activeStickColor = Color.white;
    [SerializeField] private Color spentStickColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);
    [Tooltip("Fill units per second at 1x speed. Skip snaps instantly.")]
    [SerializeField, Min(0.05f)] private float incenseBurnSpeed = 0.6f;

    private int displayedTurn;
    private int displayedTotalTurns;
    private float displayedFill = 1f;
    private float targetFill = 1f;
    private bool subscribedTurns;

    public int CurrentLoop => Mathf.Max(1, GameStateManager.CurrentRound);
    public int MaxLoops => GameStateManager.TotalRounds > 0 ? GameStateManager.TotalRounds : 7;
    public float NormalizedIncense => displayedFill;
    public bool IsBurningActive => GameStateManager.IsResolution;

    /// <summary>สัดส่วนธูปที่เหลือ (0-1) ส่งเมื่อค่าที่แสดงเปลี่ยน</summary>
    public event Action<float> OnIncenseValueChanged;

    /// <summary>ก้านธูปของ Round นี้มอดแล้ว (พารามิเตอร์: เลข Round)</summary>
    public event Action<int> OnIncenseStickBurnedOut;

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
            GameStateManager.Instance.OnRoundStarted += HandleRoundStarted;
        }

        TrySubscribeTurnManager();
        displayedFill = targetFill = 1f;
        RefreshAll();
    }

    protected override void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
            GameStateManager.Instance.OnRoundStarted -= HandleRoundStarted;
        }

        if (subscribedTurns && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStarted -= HandleTurnStarted;
        }

        base.OnDestroy();
    }

    private void Update()
    {
        UpdateActivePipPulse();
        UpdateIncenseBurnAnimation();
    }

    private void UpdateActivePipPulse()
    {
        if (!GameStateManager.IsResolution) return;
        if (displayedTurn <= 0 || displayedTurn > pipImages.Count) return;

        Image activePip = pipImages[displayedTurn - 1];
        if (activePip != null)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
            Color c = currentPipColor;
            c.a = pulse;
            activePip.color = c;
        }
    }

    private void UpdateIncenseBurnAnimation()
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
            float speed = incenseBurnSpeed * (tm != null ? tm.SpeedMultiplier : 1f);
            next = Mathf.MoveTowards(displayedFill, targetFill, speed * Time.unscaledDeltaTime);
        }

        SetDisplayedFill(next);
    }

    private void TrySubscribeTurnManager()
    {
        if (subscribedTurns || TurnManager.Instance == null) return;

        TurnManager.Instance.OnTurnStarted += HandleTurnStarted;
        subscribedTurns = true;
    }

    private void HandleStateChanged(GameState state)
    {
        TrySubscribeTurnManager();

        if (state == GameState.RoundEnd)
        {
            targetFill = 0f;
            SetDisplayedFill(0f);
            AudioManager.Bell();
            OnIncenseStickBurnedOut?.Invoke(CurrentLoop);
        }

        RefreshAll();
    }

    private void HandleRoundStarted(int round, int turns)
    {
        displayedTurn = 0;
        displayedTotalTurns = turns;
        targetFill = 1f;
        SetDisplayedFill(1f);
        RefreshAll();
    }

    private void HandleTurnStarted(int turn, int totalTurns)
    {
        displayedTurn = turn;
        displayedTotalTurns = totalTurns;
        targetFill = Mathf.Clamp01(1f - (float)turn / Mathf.Max(1, totalTurns));
        RefreshAll();
    }

    private void SetDisplayedFill(float value)
    {
        if (Mathf.Approximately(displayedFill, value) && incenseFillBar != null && Mathf.Approximately(incenseFillBar.fillAmount, value)) return;

        displayedFill = value;
        RefreshIncenseVisuals();
        OnIncenseValueChanged?.Invoke(displayedFill);
    }

    public void RefreshAll()
    {
        UITextData t = UITextProvider.Text;
        GameState state = GameStateManager.Current;
        int currentRound = CurrentLoop;
        int totalRounds = MaxLoops;
        int turnsThisRound = GameStateManager.TurnsThisRound > 0
            ? GameStateManager.TurnsThisRound
            : (GameStateManager.Instance != null ? GameStateManager.Instance.Rules.GetTurnsForRound(currentRound) : 4);

        // ตัวเลือก 3B: แสดงชื่อเฟสที่ TurnTrackerUI จุดเดียว
        if (phaseStatusText != null)
        {
            phaseStatusText.text = t.GetPhaseLabel(state);
        }

        // ตัวเลือก 1C: ตัวนับรอบจุดเดียว (Round X / 7)
        if (roundText != null)
        {
            roundText.text = UITextData.Format(t.roundLabel, "current", currentRound, "total", totalRounds);
        }

        // ตัวเลือก 2A & 3B:
        // - Resolution: Turn X / N
        // - Planning: Next ritual: N turns
        // - Dawn / RoundEnd / Ended: หากไม่ได้แยกป้าย phaseStatusText ให้แสดงชื่อเฟสที่นี่ แต่ถ้ามี phaseStatusText แล้วให้เว้นว่างเพื่อไม่ให้ซ้ำ
        if (turnOrNextRitualText != null)
        {
            if (state == GameState.Resolution)
            {
                int turn = Mathf.Max(1, displayedTurn);
                turnOrNextRitualText.text = UITextData.Format(t.turnLabel, "current", turn, "total", turnsThisRound);
            }
            else if (state == GameState.Planning)
            {
                turnOrNextRitualText.text = UITextData.Format(t.nextRitualInfo, "turns", turnsThisRound);
            }
            else
            {
                turnOrNextRitualText.text = phaseStatusText == null ? t.GetPhaseLabel(state) : string.Empty;
            }
        }

        bool showTurnPips = state == GameState.Planning || state == GameState.Resolution;
        RefreshPips(showTurnPips ? turnsThisRound : 0, state == GameState.Resolution ? displayedTurn : 0);
        RefreshIncenseVisuals();
    }

    private void RefreshIncenseVisuals()
    {
        int currentLoop = CurrentLoop;

        if (incenseFillBar != null)
        {
            incenseFillBar.fillAmount = displayedFill;
        }

        for (int i = 0; i < stickIcons.Count; i++)
        {
            if (stickIcons[i] == null) continue;

            bool spent = !GameStateManager.IsDawn && (i < currentLoop - 1 || (i == currentLoop - 1 && displayedFill <= 0f));
            stickIcons[i].color = spent ? spentStickColor : activeStickColor;
        }
    }

    private void RefreshPips(int totalTurns, int activeTurn)
    {
        EnsurePipCount(totalTurns);

        for (int i = 0; i < pipImages.Count; i++)
        {
            Image pip = pipImages[i];
            if (pip == null) continue;

            bool visible = i < totalTurns;
            if (pip.gameObject.activeSelf != visible) pip.gameObject.SetActive(visible);
            if (!visible) continue;

            int turnNumber = i + 1;
            if (turnNumber < activeTurn) pip.color = passedPipColor;
            else if (turnNumber == activeTurn) pip.color = currentPipColor;
            else pip.color = upcomingPipColor;
        }
    }

    private void EnsurePipCount(int needed)
    {
        if (pipContainer == null || pipPrefab == null) return;

        while (pipImages.Count < needed)
        {
            Image created = Instantiate(pipPrefab, pipContainer);
            pipImages.Add(created);
        }
    }
}
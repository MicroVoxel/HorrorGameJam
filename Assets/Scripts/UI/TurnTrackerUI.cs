using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// แสดง Round X / 7, Turn X / N และแถบ Pip ของเทิร์นด้านบนจอ (GDD 6.4)
/// - ใน Planning แสดง "Next ritual: N turns"
/// - ใน Resolution แสดง "Turn X / N" พร้อมอัปเดตสี Pip (เทิร์นที่ผ่านแล้วทึบ, เทิร์นปัจจุบันสว่าง/กระพริบ, ที่เหลือโปร่ง)
/// </summary>
[DisallowMultipleComponent]
public class TurnTrackerUI : MonoBehaviour
{
    [Header("Text Labels")]
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI turnOrNextRitualText;

    [Header("Turn Pips (Optional)")]
    [SerializeField] private Transform pipContainer;
    [SerializeField] private Image pipPrefab;
    [SerializeField] private List<Image> pipImages = new List<Image>();

    [Header("Pip Colors")]
    [SerializeField] private Color passedPipColor = new Color(0.85f, 0.78f, 0.55f, 1.0f);
    [SerializeField] private Color currentPipColor = new Color(1.0f, 0.95f, 0.35f, 1.0f);
    [SerializeField] private Color upcomingPipColor = new Color(0.35f, 0.35f, 0.42f, 0.45f);

    private int displayedTurn;
    private int displayedTotalTurns;
    private bool subscribedTurns;

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
            GameStateManager.Instance.OnRoundStarted += HandleRoundStarted;
        }

        TrySubscribeTurnManager();
        RefreshAll();
    }

    private void OnDestroy()
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
    }

    private void Update()
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

    private void TrySubscribeTurnManager()
    {
        if (subscribedTurns || TurnManager.Instance == null) return;

        TurnManager.Instance.OnTurnStarted += HandleTurnStarted;
        subscribedTurns = true;
    }

    private void HandleStateChanged(GameState state)
    {
        TrySubscribeTurnManager();
        RefreshAll();
    }

    private void HandleRoundStarted(int round, int turns)
    {
        displayedTurn = 0;
        displayedTotalTurns = turns;
        RefreshAll();
    }

    private void HandleTurnStarted(int turn, int totalTurns)
    {
        displayedTurn = turn;
        displayedTotalTurns = totalTurns;
        RefreshAll();
    }

    public void RefreshAll()
    {
        UITextData t = UITextProvider.Text;
        int currentRound = Mathf.Max(1, GameStateManager.CurrentRound);
        int totalRounds = Mathf.Max(1, GameStateManager.TotalRounds);
        int turnsThisRound = GameStateManager.TurnsThisRound > 0
            ? GameStateManager.TurnsThisRound
            : (GameStateManager.Instance != null ? GameStateManager.Instance.Rules.GetTurnsForRound(currentRound) : 4);

        if (roundText != null)
        {
            roundText.text = UITextData.Format(t.roundLabel, "current", currentRound, "total", totalRounds);
        }

        if (turnOrNextRitualText != null)
        {
            if (GameStateManager.IsResolution)
            {
                int turn = Mathf.Max(1, displayedTurn);
                turnOrNextRitualText.text = UITextData.Format(t.turnLabel, "current", turn, "total", turnsThisRound);
            }
            else if (GameStateManager.IsPlanning || GameStateManager.IsDawn)
            {
                turnOrNextRitualText.text = UITextData.Format(t.nextRitualInfo, "turns", turnsThisRound);
            }
            else
            {
                turnOrNextRitualText.text = t.GetPhaseLabel(GameStateManager.Current);
            }
        }

        RefreshPips(turnsThisRound, GameStateManager.IsResolution ? displayedTurn : 0);
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
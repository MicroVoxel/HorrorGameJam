using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// แสดง Event Log 3-4 บรรทัดล่าสุดใต้ Turn Tracker และเก็บประวัติของทั้ง Round ไว้ให้ดูได้ (GDD 6.2)
/// ปิดการแสดงผลได้ผ่าน Inspector (showLogOnHUD)
/// </summary>
[DisallowMultipleComponent]
public class EventLogUI : SingletonBehaviour<EventLogUI>
{
    [Header("HUD Display")]
    [SerializeField] private bool showLogOnHUD = true;
    [SerializeField, Range(1, 8)] private int visibleLineCount = 4;
    [SerializeField] private GameObject logContainer;
    [SerializeField] private TextMeshProUGUI logText;

    private readonly List<string> roundHistory = new List<string>(64);
    private bool subscribed;

    public IReadOnlyList<string> RoundHistory => roundHistory;

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted += HandleRoundStarted;
        }

        TrySubscribeTurnManager();
        RefreshDisplay();
    }

    protected override void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted -= HandleRoundStarted;
        }

        if (subscribed && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnLogMessage -= HandleLogMessage;
        }

        base.OnDestroy();
    }

    private void TrySubscribeTurnManager()
    {
        if (subscribed || TurnManager.Instance == null) return;

        TurnManager.Instance.OnLogMessage += HandleLogMessage;
        subscribed = true;
    }

    private void HandleRoundStarted(int round, int turns)
    {
        TrySubscribeTurnManager();
        roundHistory.Clear();
        RefreshDisplay();
    }

    public void PushMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        HandleLogMessage(message);
    }

    private void HandleLogMessage(string message)
    {
        roundHistory.Add(message);
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (logContainer != null && logContainer.activeSelf != showLogOnHUD)
        {
            logContainer.SetActive(showLogOnHUD);
        }

        if (!showLogOnHUD || logText == null) return;

        if (roundHistory.Count == 0)
        {
            logText.text = string.Empty;
            return;
        }

        int start = Mathf.Max(0, roundHistory.Count - visibleLineCount);
        StringBuilder sb = new StringBuilder();
        for (int i = start; i < roundHistory.Count; i++)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(roundHistory[i]);
        }

        logText.text = sb.ToString();
    }
}
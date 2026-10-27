using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// ติดตามศาลเจ้าเป้าหมายบนกระดาน และเป็นจุดเดียวที่ตัดสินชัยชนะ (Victory) หลังขั้น Cleanup ของเทิร์น
/// อ่านจำนวนศาลเจ้าที่ต้องวางจาก GameStateManager.Instance.Rules.RequiredShrines โดยตรง
/// </summary>
[DisallowMultipleComponent]
public class ObjectiveTracker : SingletonBehaviour<ObjectiveTracker>
{
    [Header("Setup Phase Requirements")]
    [Tooltip("ค่าสำรองกรณีไม่มี GameStateManager ในฉาก")]
    [SerializeField, Min(1)] private int fallbackRequiredShrines = 2;
    [Tooltip("จำนวนศาลเจ้าที่ถูกวางสะสมตลอดด่าน (ไม่ลดลงเมื่อศาลพัง)")]
    [SerializeField] private int totalShrinesPlaced = 0;

    [Header("HUD References (Optional)")]
    [SerializeField] private TextMeshProUGUI objectiveCountText;

    private readonly List<TowerObjective> activeTargets = new List<TowerObjective>();
    private bool victoryTriggered = false;
    private bool pendingVictoryCheck = false;
    private bool subscribedToTurns = false;

    public IReadOnlyList<TowerObjective> ActiveTargets => activeTargets;
    public int TargetCount => activeTargets.Count;
    public int RequiredShrinesToPlace => GameStateManager.Instance != null
        ? GameStateManager.Instance.Rules.RequiredShrines
        : Mathf.Max(1, fallbackRequiredShrines);
    public int TotalShrinesPlaced => totalShrinesPlaced;
    public bool IsSetupComplete => totalShrinesPlaced >= RequiredShrinesToPlace;

    /// <summary>
    /// เรียกเมื่อผู้เล่นวางศาลเจ้าลงบนกระดาน (พารามิเตอร์: วางแล้วกี่หลัง, ต้องวางกี่หลัง)
    /// </summary>
    public event Action<int, int> OnSetupProgressChanged;

    /// <summary>
    /// เรียกเมื่อขบวนผีทำลายศาลเจ้าทั้งหมดสำเร็จ (เช็กหลัง Cleanup)
    /// </summary>
    public event Action OnAllTargetsDestroyedVictory;

    private void Start()
    {
        TrySubscribeToTurnCleanup();
        UpdateHUD();
    }

    protected override void OnDestroy()
    {
        if (subscribedToTurns && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnCleanup -= HandleTurnCleanup;
        }

        base.OnDestroy();
    }

    private void TrySubscribeToTurnCleanup()
    {
        if (subscribedToTurns || TurnManager.Instance == null) return;

        TurnManager.Instance.OnTurnCleanup += HandleTurnCleanup;
        subscribedToTurns = true;
    }

    public void RegisterTarget(TowerObjective target)
    {
        if (target == null || activeTargets.Contains(target)) return;

        TrySubscribeToTurnCleanup();

        activeTargets.Add(target);
        totalShrinesPlaced++;

        int required = RequiredShrinesToPlace;
        Debug.Log($"<color=cyan>[ObjectiveTracker]</color> Shrines placed ({totalShrinesPlaced}/{required}) — Active: {activeTargets.Count}");

        OnSetupProgressChanged?.Invoke(totalShrinesPlaced, required);
        UpdateHUD();
    }

    /// <summary>
    /// ถอดศาลเจ้าออกจากรายการเป้าหมาย
    /// หากอยู่ในเฟส Resolution จะรอประกาศชัยชนะในขั้น Cleanup ของเทิร์นนั้น (GDD 2.3 / 4.2)
    /// </summary>
    public void UnregisterTarget(TowerObjective target)
    {
        if (target == null || !activeTargets.Contains(target)) return;

        bool destroyedInPlay = target.Tower != null && target.Tower.IsDead;

        activeTargets.Remove(target);
        UpdateHUD();

        if (!destroyedInPlay || victoryTriggered) return;

        if (IsSetupComplete && activeTargets.Count == 0)
        {
            pendingVictoryCheck = true;
            TrySubscribeToTurnCleanup();

            // หากไม่ได้กำลังรันเทิร์นใน Resolution (หรือไม่มี TurnManager) ให้ตัดสินทันที
            if (!GameStateManager.IsResolution || TurnManager.Instance == null || !TurnManager.Instance.IsRunning)
            {
                EvaluateVictoryAfterCleanup();
            }
        }
    }

    private void HandleTurnCleanup(TurnContext ctx)
    {
        if (pendingVictoryCheck || (IsSetupComplete && activeTargets.Count == 0))
        {
            EvaluateVictoryAfterCleanup();
        }
    }

    public void EvaluateVictoryAfterCleanup()
    {
        pendingVictoryCheck = false;
        if (victoryTriggered || !IsSetupComplete || activeTargets.Count > 0) return;

        victoryTriggered = true;
        Debug.Log("<color=green>[VICTORY!]</color> All objective shrines have been destroyed!");
        OnAllTargetsDestroyedVictory?.Invoke();
    }

    private void UpdateHUD()
    {
        if (objectiveCountText == null) return;

        UITextData t = UITextProvider.Text;
        int required = RequiredShrinesToPlace;
        if (!IsSetupComplete)
        {
            objectiveCountText.text = UITextData.Format(t.objectiveSetup, "placed", totalShrinesPlaced, "required", required);
        }
        else
        {
            objectiveCountText.text = UITextData.Format(t.objectiveRemaining, "current", activeTargets.Count, "total", required);
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// ติดตามศาลเจ้าเป้าหมายบนกระดาน: นับจำนวนที่วางในช่วง Dawn, หาศาลที่ใกล้ที่สุด,
/// และตัดสินชัยชนะหลังขั้น Cleanup ของเทิร์น (TurnManager.OnTurnCleanup) ตาม GDD 2.3 และ 4.2
/// ข้อความ HUD อ่านจาก UITextProvider.Text
/// </summary>
[DisallowMultipleComponent]
public class ObjectiveTracker : SingletonBehaviour<ObjectiveTracker>
{
    [Header("Setup Phase Requirements")]
    [Tooltip("จำนวนการ์ดศาลเจ้าที่บังคับให้ผู้เล่นต้องวางลงบนกระดานก่อนเริ่มคืนแรก")]
    [SerializeField] private int requiredShrinesToPlace = 2;
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
    public int RequiredShrinesToPlace => requiredShrinesToPlace;
    public int TotalShrinesPlaced => totalShrinesPlaced;
    public bool IsSetupComplete => totalShrinesPlaced >= requiredShrinesToPlace;

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

    public void SetRequiredShrinesCount(int count)
    {
        requiredShrinesToPlace = Mathf.Max(1, count);
        UpdateHUD();
    }

    public void RegisterTarget(TowerObjective target)
    {
        if (target == null || activeTargets.Contains(target)) return;

        TrySubscribeToTurnCleanup();

        activeTargets.Add(target);
        totalShrinesPlaced++;

        Debug.Log($"<color=cyan>[ObjectiveTracker]</color> Shrines placed ({totalShrinesPlaced}/{requiredShrinesToPlace}) — Active: {activeTargets.Count}");

        OnSetupProgressChanged?.Invoke(totalShrinesPlaced, requiredShrinesToPlace);
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

    /// <summary>
    /// ค้นหาศาลเจ้าเป้าหมายที่อยู่ใกล้ตำแหน่งของยูนิตผีมากที่สุด
    /// </summary>
    public TowerObjective GetClosestTarget(Vector3 fromPosition)
    {
        TowerObjective closest = null;
        float minSqrDist = float.MaxValue;

        for (int i = activeTargets.Count - 1; i >= 0; i--)
        {
            TowerObjective candidate = activeTargets[i];
            if (candidate == null || !candidate.IsAlive)
            {
                continue;
            }

            float sqrDist = (candidate.transform.position - fromPosition).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closest = candidate;
            }
        }

        return closest;
    }

    private void UpdateHUD()
    {
        if (objectiveCountText == null) return;

        UITextData t = UITextProvider.Text;
        if (!IsSetupComplete)
        {
            objectiveCountText.text = UITextData.Format(t.objectiveSetup, "placed", totalShrinesPlaced, "required", requiredShrinesToPlace);
        }
        else
        {
            objectiveCountText.text = UITextData.Format(t.objectiveRemaining, "current", activeTargets.Count, "total", requiredShrinesToPlace);
        }
    }
}
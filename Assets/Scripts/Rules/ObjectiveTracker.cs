using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// ติดตามศาลเจ้าเป้าหมายบนกระดาน: นับจำนวนที่วางในช่วง Setup, หาศาลที่ใกล้ที่สุด,
/// และตัดสินชัยชนะเมื่อขบวนผีทำลายศาลเจ้าทั้งหมด
///
/// ไม่ยุ่งกับ MoonPhaseManager / IncenseGaugeController อีกแล้ว:
/// การปลดล็อกช่วง Setup เป็นหน้าที่ของ GameStateManager ที่ฟัง OnSetupProgressChanged
/// และการหยุดธูปตอนชนะเป็นหน้าที่ของ GameState.Ended
/// </summary>
[DisallowMultipleComponent]
public class ObjectiveTracker : SingletonBehaviour<ObjectiveTracker>
{
    [Header("Setup Phase Requirements")]
    [Tooltip("จำนวนการ์ดศาลเจ้าที่บังคับให้ผู้เล่นต้องวางลงบนกระดานก่อนเริ่มคืนแรก (Easy=1, Normal=2, Hard=3)")]
    [SerializeField] private int requiredShrinesToPlace = 2;
    [Tooltip("จำนวนศาลเจ้าที่ถูกวางสะสมตลอดด่าน (ไม่ลดลงเมื่อศาลพัง)")]
    [SerializeField] private int totalShrinesPlaced = 0;

    [Header("HUD References (Optional)")]
    [SerializeField] private TextMeshProUGUI objectiveCountText;

    private readonly List<TowerObjective> activeTargets = new List<TowerObjective>();
    private bool victoryTriggered = false;

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
    /// เรียกเมื่อขบวนผีทำลายศาลเจ้าทั้งหมดสำเร็จ (ผู้เล่นชนะด่าน!)
    /// </summary>
    public event Action OnAllTargetsDestroyedVictory;

    private void Start()
    {
        UpdateHUD();
    }

    public void SetRequiredShrinesCount(int count)
    {
        requiredShrinesToPlace = Mathf.Max(1, count);
        UpdateHUD();
    }

    public void RegisterTarget(TowerObjective target)
    {
        if (target == null || activeTargets.Contains(target)) return;

        activeTargets.Add(target);
        totalShrinesPlaced++;

        Debug.Log($"<color=cyan>[ObjectiveTracker]</color> วางศาลเจ้าแล้ว ({totalShrinesPlaced}/{requiredShrinesToPlace}) — ศาลเจ้าบนกระดานปัจจุบัน: {activeTargets.Count}");

        OnSetupProgressChanged?.Invoke(totalShrinesPlaced, requiredShrinesToPlace);
        UpdateHUD();
    }

    /// <summary>
    /// ถอดศาลเจ้าออกจากรายการเป้าหมาย
    /// ชัยชนะจะถูกยิง "เฉพาะ" เมื่อศาลหลังที่ถูกถอดตายจริง (HP = 0) เท่านั้น
    /// การที่ศาลถูก Disable/Destroy จากการรีสตาร์ทหรือโหลดซีนจะไม่ทำให้เกิด Victory ปลอม
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
            victoryTriggered = true;

            Debug.Log("<color=green>[VICTORY!]</color> ศาลเจ้าศักดิ์สิทธิ์ทั้งหมดถูกขบวนร้อยอสูรทำลายสิ้นซาก! ผู้เล่นชนะเกม!");
            OnAllTargetsDestroyedVictory?.Invoke();
        }
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

        if (!IsSetupComplete)
        {
            objectiveCountText.text = $"วางศาลเจ้าเริ่มต้น: {totalShrinesPlaced} / {requiredShrinesToPlace}";
        }
        else
        {
            objectiveCountText.text = $"ศาลเจ้าคงเหลือ: {activeTargets.Count} / {requiredShrinesToPlace}";
        }
    }
}

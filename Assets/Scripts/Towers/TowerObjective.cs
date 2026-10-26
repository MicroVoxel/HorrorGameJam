using UnityEngine;

/// <summary>
/// คอมโพเนนต์ระบุว่าสิ่งก่อสร้างนี้คือ "ศาลเจ้าเป้าหมายหลัก" ที่ขบวนผีของผู้เล่นต้องบุกทำลาย
/// บังคับกฎวางข้างทางเดิน และห้ามวางติดกับศาลเจ้าหลังอื่นในรัศมี 1 ช่อง
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TowerBase))]
public class TowerObjective : MonoBehaviour
{
    [Header("Objective Settings")]
    [SerializeField] private int targetPriority = 1;
    [SerializeField] private bool isCriticalObjective = true;
    [Tooltip("กฎเหล็ก: ห้ามวางศาลเจ้าติดกันในช่องรอบตัว 6 ทิศ")]
    [SerializeField] private bool preventAdjacentDefenseBases = true;

    private TowerBase tower;

    public TowerBase Tower => tower;
    public int TargetPriority => targetPriority;
    public bool IsCriticalObjective => isCriticalObjective;
    public bool PreventAdjacentDefenseBases => preventAdjacentDefenseBases;

    /// <summary>ศาลเจ้ายังตั้งอยู่และยังไม่ถูกทำลาย (ใช้แทนการเช็ก Tower == null || Tower.IsDead ซ้ำๆ)</summary>
    public bool IsAlive => tower != null && !tower.IsDead;

    private void Awake()
    {
        tower = GetComponent<TowerBase>();
    }

    private void OnEnable()
    {
        if (tower != null)
        {
            tower.OnPlacedOnBoard += HandlePlacedOnBoard;
            tower.OnTowerDestroyed += HandleTowerDestroyed;
        }
    }

    private void OnDisable()
    {
        if (tower != null)
        {
            tower.OnPlacedOnBoard -= HandlePlacedOnBoard;
            tower.OnTowerDestroyed -= HandleTowerDestroyed;
        }

        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.UnregisterTarget(this);
        }
    }

    private void HandlePlacedOnBoard(TowerBase placedTower)
    {
        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.RegisterTarget(this);
        }
    }

    private void HandleTowerDestroyed(TowerBase destroyedTower)
    {
        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.UnregisterTarget(this);
        }
    }
}
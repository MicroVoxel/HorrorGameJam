using System;
using UnityEngine;
using UnityEngine.EventSystems;

public enum SwapDenyReason
{
    None,
    NotPlanning,
    NoQuota,
    TooFar
}

/// <summary>
/// ลากสลับตำแหน่งผีในขบวน (Formation Drag and Drop) เฉพาะช่วง Planning
/// - สลับได้เฉพาะผีที่ห่างกันไม่เกิน MaxSwapPathDistance ช่องบนทางเดิน (ค่าตั้งต้น 2)
/// - มีโควตา SwapQuotaPerRound ต่อ Planning (รีเซ็ตเมื่อเริ่ม Round ใหม่) ไม่เสียธูป
/// - UI ฟัง OnSwapQuotaChanged / OnSwapDenied เพื่อแสดงผล (ข้อความอยู่ใน UITextData ใน Part 5)
/// </summary>
[DisallowMultipleComponent]
public class FormationSwapManager : SingletonBehaviour<FormationSwapManager>
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask raycastMask = ~0;

    [Header("Visual")]
    [SerializeField] private float dragHoverHeight = 0.85f;

    private EntityWalkable draggedWalkable;
    private Plane dragPlane = new Plane(Vector3.up, Vector3.zero);
    private int remainingSwaps;

    public bool IsDraggingEntity => draggedWalkable != null;
    public int RemainingSwaps => remainingSwaps;
    public int TotalSwaps => GameStateManager.Instance != null ? GameStateManager.Instance.Rules.SwapQuotaPerRound : 0;

    /// <summary>โควตาเปลี่ยน (พารามิเตอร์: เหลือ, ทั้งหมด)</summary>
    public event Action<int, int> OnSwapQuotaChanged;

    /// <summary>สลับไม่ได้ (UI แสดงเหตุผล)</summary>
    public event Action<SwapDenyReason> OnSwapDenied;

    /// <summary>สลับสำเร็จ</summary>
    public event Action<EntityBase, EntityBase> OnSwapped;

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted += HandleRoundStarted;
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
        }

        ResetQuota();
    }

    protected override void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRoundStarted -= HandleRoundStarted;
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        base.OnDestroy();
    }

    private void HandleRoundStarted(int round, int turns)
    {
        ResetQuota();
    }

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Planning) CancelEntityDrag();
    }

    private void ResetQuota()
    {
        remainingSwaps = TotalSwaps;
        OnSwapQuotaChanged?.Invoke(remainingSwaps, TotalSwaps);
    }

    private void Update()
    {
        if (!GameStateManager.IsPlanning)
        {
            if (draggedWalkable != null) CancelEntityDrag();
            return;
        }

        if (GameInput.LeftPressed)
        {
            TryBeginDragEntity();
        }
        else if (GameInput.LeftHeld && draggedWalkable != null)
        {
            UpdateDraggedEntityPosition();
        }
        else if (GameInput.LeftReleased && draggedWalkable != null)
        {
            CompleteEntityDrag();
        }
    }

    private void TryBeginDragEntity()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // กำลังเลือกการ์ดวางป้อมอยู่ หรือชี้ UI อยู่ ไม่เริ่มลากผี
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (TilePlacementManager.Instance != null && TilePlacementManager.Instance.HasSelection) return;

        Ray ray = mainCamera.ScreenPointToRay(GameInput.MousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 200f, raycastMask);

        for (int i = 0; i < hits.Length; i++)
        {
            EntityWalkable walkable = hits[i].collider.GetComponentInParent<EntityWalkable>();
            if (walkable == null)
            {
                HexTile tile = hits[i].collider.GetComponentInParent<HexTile>();
                if (tile != null && tile.HasEntity)
                {
                    walkable = tile.CurrentEntity.GetComponent<EntityWalkable>();
                }
            }

            if (walkable != null && walkable.IsInitialized && walkable.Entity != null && !walkable.Entity.IsDead)
            {
                draggedWalkable = walkable;
                dragPlane = new Plane(Vector3.up, walkable.transform.position);
                return;
            }
        }
    }

    private void UpdateDraggedEntityPosition()
    {
        if (draggedWalkable == null || mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(GameInput.MousePosition);
        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            draggedWalkable.transform.position = hitPoint + (Vector3.up * dragHoverHeight);
        }
    }

    private void CompleteEntityDrag()
    {
        if (draggedWalkable == null) return;

        EntityWalkable dragged = draggedWalkable;
        draggedWalkable = null;

        EntityWalkable targetWalkable = FindSwapTargetUnderMouse(dragged);

        if (targetWalkable == null)
        {
            dragged.SnapBackToCurrentTile();
            return;
        }

        SwapDenyReason reason = EvaluateSwap(dragged, targetWalkable);
        if (reason != SwapDenyReason.None)
        {
            dragged.SnapBackToCurrentTile();
            OnSwapDenied?.Invoke(reason);
            return;
        }

        dragged.SwapPositionWith(targetWalkable);
        remainingSwaps--;
        AudioManager.Click();

        OnSwapQuotaChanged?.Invoke(remainingSwaps, TotalSwaps);
        OnSwapped?.Invoke(dragged.Entity, targetWalkable.Entity);
    }

    private EntityWalkable FindSwapTargetUnderMouse(EntityWalkable dragged)
    {
        if (mainCamera == null) return null;

        Ray ray = mainCamera.ScreenPointToRay(GameInput.MousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 200f, raycastMask);

        for (int i = 0; i < hits.Length; i++)
        {
            EntityWalkable candidate = hits[i].collider.GetComponentInParent<EntityWalkable>();
            if (candidate != null && candidate != dragged && candidate.Entity != null && !candidate.Entity.IsDead)
            {
                return candidate;
            }

            HexTile tile = hits[i].collider.GetComponentInParent<HexTile>();
            if (tile != null && tile.HasEntity && tile.CurrentEntity != dragged.Entity)
            {
                EntityWalkable onTile = tile.CurrentEntity.GetComponent<EntityWalkable>();
                if (onTile != null) return onTile;
            }
        }

        return null;
    }

    /// <summary>ตรวจว่าสลับ a กับ b ได้ไหม (ใช้ให้ UI ไฮไลต์ตัวที่สลับด้วยได้ในอนาคต)</summary>
    public SwapDenyReason EvaluateSwap(EntityWalkable a, EntityWalkable b)
    {
        if (!GameStateManager.IsPlanning) return SwapDenyReason.NotPlanning;
        if (remainingSwaps <= 0) return SwapDenyReason.NoQuota;

        LoopPathManager path = LoopPathManager.Instance;
        GameStateManager gsm = GameStateManager.Instance;
        if (path == null || gsm == null) return SwapDenyReason.None;

        if (PathDistance(path, a.CurrentPathIndex, b.CurrentPathIndex) > gsm.Rules.MaxSwapPathDistance)
        {
            return SwapDenyReason.TooFar;
        }

        return SwapDenyReason.None;
    }

    private static int PathDistance(LoopPathManager path, int indexA, int indexB)
    {
        int length = path.PathLength;
        if (length <= 0) return 0;

        int d = Mathf.Abs(indexA - indexB);
        return Mathf.Min(d, length - d);
    }

    private void CancelEntityDrag()
    {
        if (draggedWalkable != null)
        {
            draggedWalkable.SnapBackToCurrentTile();
            draggedWalkable = null;
        }
    }
}
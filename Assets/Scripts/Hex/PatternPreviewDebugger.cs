using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ตัวช่วยทดสอบชั่วคราว: ชี้เมาส์ที่ช่องเพื่อดู Range Pattern บนกระดานโดยไม่ต้องมีป้อมจริง
///   Q / E = หมุนทิศหน้า   คลิกซ้าย = เลือกเป้าหมาย (ดูช่อง Hit ของ AttackPatternData)   คลิกขวา = ล้างเป้าหมาย
/// ต้องมี RangeIndicator ในซีนและ HexTile Prefab ที่ลาก Range Indicator ใส่แล้ว
/// ลบทิ้งเมื่อ TilePlacementManager ใช้ Pattern จริงใน Part 4
/// </summary>
[DisallowMultipleComponent]
public class PatternPreviewDebugger : MonoBehaviour
{
    [Header("What to preview (use one)")]
    [SerializeField] private HexRangePattern rangePattern;
    [Tooltip("ถ้าใส่ จะใช้ Range + Hit ของตัวนี้แทน Range Pattern ด้านบน")]
    [SerializeField] private AttackPatternData attackPattern;

    [Header("Options")]
    [SerializeField] private bool shrineSide = true;
    [Tooltip("เปิด = ช่องที่มีผีหรือป้อมยืนอยู่จะบล็อกเส้นของ Pattern ที่เปิด blockedByUnits")]
    [SerializeField] private bool useBoardBlocking = true;
    [SerializeField] private int sizeDelta = 0;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask tileLayerMask = ~0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private readonly List<HexCoordinates> rangeTiles = new List<HexCoordinates>(64);
    private readonly List<HexCoordinates> hitTiles = new List<HexCoordinates>(64);

    private int facing;
    private HexTile hoveredTile;
    private HexCoordinates? target;

    private void Update()
    {
        if (RangeIndicator.Instance == null) return;

        bool changed = false;

        if (GameInput.RotateLeftPressed) { facing = HexCoordinates.NormalizeFacing(facing - 1); changed = true; }
        if (GameInput.RotateRightPressed) { facing = HexCoordinates.NormalizeFacing(facing + 1); changed = true; }
        if (GameInput.RightPressed && target.HasValue) { target = null; changed = true; }

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        HexTile tile = overUI ? null : RaycastTile();

        if (tile != hoveredTile)
        {
            hoveredTile = tile;
            changed = true;
        }

        if (GameInput.LeftPressed && hoveredTile != null && attackPattern != null)
        {
            target = hoveredTile.Coordinates;
            changed = true;
        }

        if (changed) Refresh();
    }

    private void Refresh()
    {
        if (hoveredTile == null)
        {
            RangeIndicator.Hide(this);
            return;
        }

        HexPatternContext ctx = useBoardBlocking
            ? HexPatternContext.ForBoard(LoopPathManager.Instance, sizeDelta)
            : new HexPatternContext { sizeDelta = sizeDelta };

        HexCoordinates origin = hoveredTile.Coordinates;
        hitTiles.Clear();

        if (attackPattern != null)
        {
            attackPattern.CollectRangeTiles(origin, facing, rangeTiles, ctx);

            if (target.HasValue && rangeTiles.Contains(target.Value))
            {
                attackPattern.CollectHitTiles(origin, facing, target.Value, rangeTiles, hitTiles, ctx);
            }
        }
        else if (rangePattern != null)
        {
            rangePattern.CollectTiles(origin, facing, rangeTiles, ctx);
        }
        else
        {
            rangeTiles.Clear();
        }

        RangeIndicator.Instance.Show(this, rangeTiles, hitTiles, shrineSide);
    }

    private HexTile RaycastTile()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return null;

        Ray ray = mainCamera.ScreenPointToRay(GameInput.MousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, tileLayerMask))
        {
            return hit.collider.GetComponentInParent<HexTile>();
        }

        return null;
    }

    private void OnDisable()
    {
        RangeIndicator.Hide(this);
    }
#endif
}

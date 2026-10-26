using System;
using UnityEngine;

/// <summary>
/// ตัวแทนแผ่นกระดานหกเหลี่ยมแต่ละช่อง (Hex Tile)
/// รองรับทั้งการวางสิ่งก่อสร้างบนพื้นดิน (CurrentTower) และการยืนของผีบนถนนตามกฎ 1 ช่อง = 1 ตัว (CurrentEntity)
/// ชั้น Sprite บนช่อง เรียงล่างขึ้นบน: Placeable, Hover, Range (Range อยู่บนสุด)
/// </summary>
[SelectionBase]
[DisallowMultipleComponent]
public class HexTile : MonoBehaviour
{
    [Header("Grid Coordinates & Surface")]
    [SerializeField] private HexCoordinates coordinates;
    [SerializeField] private HexGroundType groundType = HexGroundType.Ground;
    [SerializeField] private int pathIndex = -1;

    [Header("Occupancy (1 Tile = 1 Unit)")]
    [SerializeField] private TowerBase currentTower;
    [SerializeField] private EntityBase currentEntity;

    [Header("Placement Indicator")]
    [Tooltip("Hex sprite ที่วางเป็นลูกของ Tile จะเปิดเฉพาะตอนช่องนี้วางการ์ดที่เลือกอยู่ได้")]
    [SerializeField] private GameObject placeableIndicator;
    [Tooltip("(ไม่บังคับ) Sprite ที่แสดงตอนเมาส์ชี้ช่องนี้")]
    [SerializeField] private GameObject hoverIndicator;

    [Header("Range Indicator (above Hover)")]
    [Tooltip("Hex sprite ชั้นบนสุด ใช้แสดงช่อง Range / Hit ระบบย้อมสีให้เองตามฝั่ง (ใช้สีขาวเป็นฐาน)")]
    [SerializeField] private GameObject rangeIndicator;
    [Tooltip("ยกชั้น Range ให้สูงกว่า Hover เล็กน้อยกัน Sprite ทับซ้อนกัน (หน่วยโลก)")]
    [SerializeField] private float rangeLayerYOffset = 0.02f;

    private SpriteRenderer rangeRenderer;

    public HexCoordinates Coordinates => coordinates;
    public HexGroundType GroundType => groundType;
    public int PathIndex => pathIndex;

    public TowerBase CurrentTower => currentTower;
    public bool HasTower => currentTower != null;

    public EntityBase CurrentEntity => currentEntity;
    public bool HasEntity => currentEntity != null && !currentEntity.IsDead;

    /// <summary>มีหน่วยยืนอยู่ (ผีหรือสิ่งก่อสร้าง) ใช้กับ blockedByUnits ของ Pattern</summary>
    public bool IsOccupied => HasEntity || HasTower;

    public bool IsRangeIndicatorVisible => rangeIndicator != null && rangeIndicator.activeSelf;

    public event Action<HexTile, TowerBase> OnTowerPlaced;
    public event Action<HexTile> OnTowerRemoved;

    private void Awake()
    {
        SetPlaceable(false);
        ConfigureRangeLayer();
        SetRangeIndicator(false, Color.white);
    }

    public void Initialize(HexCoordinates coords, HexGroundType type, int index = -1)
    {
        coordinates = coords;
        groundType = type;
        pathIndex = index;
        gameObject.name = $"Hex_{coords.Q}_{coords.R}_{type}";
    }

    public void SetGroundType(HexGroundType newType, int newPathIndex = -1)
    {
        groundType = newType;
        pathIndex = newPathIndex;
        gameObject.name = $"Hex_{coordinates.Q}_{coordinates.R}_{newType}";
    }

    public bool PlaceTower(TowerBase tower)
    {
        if (tower == null || HasTower) return false;

        LoopPathManager pathMgr = LoopPathManager.Instance;

        if (!tower.CanPlaceOnTile(this, pathMgr)) return false;

        currentTower = tower;
        tower.OnPlaced(this);
        OnTowerPlaced?.Invoke(this, tower);
        return true;
    }

    public void RemoveTower()
    {
        if (currentTower != null)
        {
            TowerBase removed = currentTower;
            currentTower = null;
            removed.OnRemoved();
            OnTowerRemoved?.Invoke(this);
        }
    }

    public void SetEntity(EntityBase entity)
    {
        currentEntity = entity;
    }

    public void ClearEntity(EntityBase entity = null)
    {
        if (entity == null || currentEntity == entity)
        {
            currentEntity = null;
        }
    }

    // ---------- Placement Indicator ----------

    public void SetPlaceable(bool placeable)
    {
        if (placeableIndicator != null) placeableIndicator.SetActive(placeable);
        if (!placeable) SetHover(false);
    }

    public void SetHover(bool hover)
    {
        if (hoverIndicator != null) hoverIndicator.SetActive(hover);
    }

    // ---------- Range Indicator ----------

    /// <summary>เปิด/ปิดชั้น Range พร้อมสี (เรียกโดย RangeIndicator ไม่ควรเรียกตรงจากที่อื่น)</summary>
    public void SetRangeIndicator(bool visible, Color color)
    {
        if (rangeIndicator == null) return;

        if (visible)
        {
            if (rangeRenderer == null) rangeRenderer = rangeIndicator.GetComponentInChildren<SpriteRenderer>(true);
            if (rangeRenderer != null) rangeRenderer.color = color;
        }

        if (rangeIndicator.activeSelf != visible) rangeIndicator.SetActive(visible);
    }

    /// <summary>จัดให้ชั้น Range อยู่เหนือ Hover ทั้งลำดับ Sorting และความสูง</summary>
    private void ConfigureRangeLayer()
    {
        if (rangeIndicator == null) return;

        rangeRenderer = rangeIndicator.GetComponentInChildren<SpriteRenderer>(true);
        if (hoverIndicator == null) return;

        SpriteRenderer hoverRenderer = hoverIndicator.GetComponentInChildren<SpriteRenderer>(true);
        if (hoverRenderer != null && rangeRenderer != null)
        {
            rangeRenderer.sortingLayerID = hoverRenderer.sortingLayerID;
            rangeRenderer.sortingOrder = hoverRenderer.sortingOrder + 1;
        }

        Vector3 pos = rangeIndicator.transform.localPosition;
        pos.y = hoverIndicator.transform.localPosition.y + rangeLayerYOffset;
        rangeIndicator.transform.localPosition = pos;
    }
}
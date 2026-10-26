using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// วางการ์ดแบบคลิก: เลือกการ์ด -> ช่องที่วางได้เปิด Indicator -> คลิกซ้ายที่ช่องเพื่อวาง
/// v2 (4B):
/// - ใช้ได้เฉพาะ Dawn / Planning
/// - Q / E หมุนทิศหน้า 60 องศา (Ghost และ Range Indicator หมุนตามทันที) คลิกซ้ายยืนยันพร้อมทิศ
/// - คลิกขวา / Esc ยกเลิกการเลือก (คลิกการ์ดใบเดิมซ้ำก็ยกเลิก)
/// - ป้อมโจมตีต้องผ่าน Valid Threat Check ไม่ผ่านแสดง Range สีแดงและวางไม่ได้
/// - ส่งเหตุผลที่วางไม่ได้ผ่าน HoverDenyMessage (Tooltip ช่อง Hex) และ OnPlacementDenied (ข้อความแจ้งเตือน)
/// </summary>
[DisallowMultipleComponent]
public class TilePlacementManager : SingletonBehaviour<TilePlacementManager>
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask tileLayerMask = ~0;

    private LoopPathManager PathManager => LoopPathManager.Instance;

    private CardUI selectedCard;
    private CardData selectedData;
    private TowerAttackable selectedAttacker;
    private HexTile hoveredTile;
    private readonly HashSet<HexTile> validTiles = new HashSet<HexTile>();
    private readonly List<HexCoordinates> previewTiles = new List<HexCoordinates>(32);

    private GameObject currentGhostObject;
    private TowerBase ghostTower;

    private int placementFacing;
    private bool previewDirty;

    public CardUI SelectedCard => selectedCard;
    public bool HasSelection => selectedData != null;

    /// <summary>ทิศหน้าที่จะวาง (0-5) จำค่าข้ามการวางหลายใบ</summary>
    public int PlacementFacing => placementFacing;

    /// <summary>เหตุผลที่ช่องที่ชี้อยู่วางไม่ได้ (ว่าง = วางได้หรือไม่ได้เลือกการ์ด) ให้ Tooltip ช่อง Hex อ่าน</summary>
    public string HoverDenyMessage { get; private set; } = string.Empty;

    /// <summary>คลิกวางแต่วางไม่ได้ (พารามิเตอร์: ข้อความเหตุผลจาก UITextData)</summary>
    public event Action<string> OnPlacementDenied;

    /// <summary>ทิศหน้าเปลี่ยนด้วย Q / E</summary>
    public event Action<int> OnFacingChanged;

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    protected override void OnDestroy()
    {
        RangeIndicator.Hide(this);
        base.OnDestroy();
    }

    // ---------- Selection ----------

    /// <summary>คลิกการ์ด: ถ้าเป็นใบที่เลือกอยู่ = ยกเลิก, ถ้าเป็นใบอื่น = สลับไปเลือกใบนั้น</summary>
    public void ToggleCard(CardUI card)
    {
        if (card == null) return;

        if (selectedCard == card)
        {
            CancelSelection();
            return;
        }

        SelectCard(card);
    }

    private void SelectCard(CardUI card)
    {
        if (!GameStateManager.IsPlayerActionPhase) return;
        if (card.CardData == null || card.CardData.TowerPrefab == null) return;

        CancelSelection();

        selectedCard = card;
        selectedData = card.CardData;
        selectedData.TowerPrefab.TryGetComponent(out selectedAttacker);
        selectedCard.SetSelected(true);

        CreateGhost(selectedData);
        HighlightValidTiles();
        previewDirty = true;
    }

    public void CancelSelection()
    {
        if (selectedCard != null) selectedCard.SetSelected(false);

        selectedCard = null;
        selectedData = null;
        selectedAttacker = null;
        HoverDenyMessage = string.Empty;

        RangeIndicator.Hide(this);
        ClearHighlights();
        ClearGhost();
    }

    // ---------- Per-frame ----------

    private void Update()
    {
        if (selectedData == null) return;

        // การ์ดหายไปแล้ว หรือออกจากช่วงที่วางได้ (เริ่ม Resolution / จบเกม) -> เลิกเลือก
        if (selectedCard == null || !GameStateManager.IsPlayerActionPhase)
        {
            CancelSelection();
            return;
        }

        if (GameInput.RightPressed || GameInput.EscapePressed)
        {
            CancelSelection();
            return;
        }

        if (GameInput.RotateLeftPressed) Rotate(-1);
        if (GameInput.RotateRightPressed) Rotate(1);

        bool overUI = IsPointerOverUI();
        HexTile tile = overUI ? null : RaycastHexTileUnderMouse();

        UpdateHover(tile);
        UpdateGhost(tile);

        if (previewDirty)
        {
            RefreshPreview();
            previewDirty = false;
        }

        if (GameInput.LeftPressed && !overUI && !IsDetailPopupBlocking())
        {
            TryPlaceOn(tile);
        }
    }

    private void Rotate(int step)
    {
        placementFacing = HexCoordinates.NormalizeFacing(placementFacing + step);

        if (ghostTower != null) ghostTower.SetFacing(placementFacing);

        previewDirty = true;
        OnFacingChanged?.Invoke(placementFacing);
    }

    private void TryPlaceOn(HexTile tile)
    {
        // คลิกพื้นที่ว่างนอกแมพ: คงการเลือกไว้
        if (tile == null) return;

        TowerBase prefab = selectedData.TowerPrefab;
        PlacementDenyReason reason = ThreatPlacementValidator.Check(prefab, tile, placementFacing, PathManager);

        if (reason != PlacementDenyReason.None)
        {
            string message = ThreatPlacementValidator.GetMessage(reason);
            Debug.LogWarning($"<color=orange>[TilePlacementManager]</color> Cannot place '{selectedData.CardName}' on {tile.Coordinates}: {reason}");
            OnPlacementDenied?.Invoke(message);
            return;
        }

        TowerBase newTower = Instantiate(prefab, tile.transform.position, Quaternion.identity);
        newTower.SetFacing(placementFacing);
        newTower.MarkPlayerPlaced();

        if (!tile.PlaceTower(newTower))
        {
            Destroy(newTower.gameObject);
            return;
        }

        AudioManager.Click();

        CardUI placedCard = selectedCard;
        CancelSelection(); // ปิด Indicator/Ghost และคืนหน้าตาการ์ดก่อนลบ

        if (CardDeckController.Instance != null)
        {
            CardDeckController.Instance.RemoveCardFromHand(placedCard);
        }
    }

    // ---------- Tile Indicators ----------

    private void HighlightValidTiles()
    {
        validTiles.Clear();
        if (PathManager == null) return;

        TowerBase prefab = selectedData.TowerPrefab;
        foreach (HexTile tile in PathManager.AllTiles)
        {
            if (tile != null && prefab.CanPlaceOnTile(tile, PathManager))
            {
                validTiles.Add(tile);
                tile.SetPlaceable(true);
            }
        }
    }

    private void ClearHighlights()
    {
        foreach (HexTile tile in validTiles)
        {
            if (tile != null) tile.SetPlaceable(false);
        }
        validTiles.Clear();

        if (hoveredTile != null) hoveredTile.SetHover(false);
        hoveredTile = null;
    }

    private void UpdateHover(HexTile tile)
    {
        if (tile == hoveredTile) return;

        if (hoveredTile != null) hoveredTile.SetHover(false);

        hoveredTile = tile;

        if (hoveredTile != null && validTiles.Contains(hoveredTile))
        {
            hoveredTile.SetHover(true);
        }

        previewDirty = true;
    }

    /// <summary>
    /// อัปเดต Range Indicator และข้อความเหตุผลของช่องที่ชี้อยู่
    /// ผ่านทุกกฎ = Range สีศาล, ไม่ผ่าน Threat Check = Range สีแดง, เหตุผลอื่น = ไม่แสดง Range
    /// </summary>
    private void RefreshPreview()
    {
        RangeIndicator.Hide(this);
        HoverDenyMessage = string.Empty;

        if (hoveredTile == null || selectedData == null) return;

        PlacementDenyReason reason = ThreatPlacementValidator.Check(selectedData.TowerPrefab, hoveredTile, placementFacing, PathManager);
        HoverDenyMessage = ThreatPlacementValidator.GetMessage(reason);

        if (RangeIndicator.Instance == null || selectedAttacker == null || selectedAttacker.Pattern == null) return;
        if (reason != PlacementDenyReason.None && reason != PlacementDenyReason.NoThreatPath) return;

        HexPatternContext pctx = BoardPatternContext.ForTower();
        selectedAttacker.Pattern.CollectRangeTiles(hoveredTile.Coordinates, placementFacing, previewTiles, pctx);

        if (reason == PlacementDenyReason.NoThreatPath)
        {
            RangeIndicator.Instance.ShowInvalid(this, previewTiles);
        }
        else
        {
            RangeIndicator.Instance.Show(this, previewTiles, null, true);
        }
    }

    // ---------- Ghost ----------

    private void CreateGhost(CardData data)
    {
        ClearGhost();

        currentGhostObject = Instantiate(data.TowerPrefab.gameObject);
        currentGhostObject.name = $"GhostPreview_{data.CardName}";

        // ปิดสคริปต์และ Collider เพื่อไม่ให้รบกวน Raycast หรือทริกเกอร์ระบบเกม
        MonoBehaviour[] scripts = currentGhostObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < scripts.Length; i++) scripts[i].enabled = false;

        Collider[] colliders = currentGhostObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

        // SetFacing เป็นเมธอดธรรมดา เรียกได้แม้คอมโพเนนต์ถูกปิดแล้ว
        ghostTower = currentGhostObject.GetComponent<TowerBase>();
        if (ghostTower != null) ghostTower.SetFacing(placementFacing);

        currentGhostObject.SetActive(false);
    }

    private void UpdateGhost(HexTile tile)
    {
        if (currentGhostObject == null) return;

        // แสดง Ghost เฉพาะช่องที่วางได้ ช่องอื่นซ่อนไว้
        bool show = tile != null && validTiles.Contains(tile);
        if (currentGhostObject.activeSelf != show) currentGhostObject.SetActive(show);
        if (show) currentGhostObject.transform.position = tile.transform.position;
    }

    private void ClearGhost()
    {
        if (currentGhostObject != null)
        {
            Destroy(currentGhostObject);
            currentGhostObject = null;
        }

        ghostTower = null;
    }

    // ---------- Helpers ----------

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // ลบเมธอดนี้พร้อม CardDetailPopup ใน Part 5 (Hover Tooltip แทน Popup)
    private static bool IsDetailPopupBlocking()
    {
        CardDetailPopup popup = CardDetailPopup.Instance;
        return popup != null && (popup.IsOpen || popup.WasClosedThisFrame);
    }

    private HexTile RaycastHexTileUnderMouse()
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
}
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ตัวตรวจจับการชี้เมาส์ (Hover) บนกระดาน 3D (GDD 6.5):
/// - ลำดับความสำคัญ: หากเมาส์ชี้บน UI (หรือลากของอยู่) จะให้ UI แสดงก่อน
/// - หากอยู่บนกระดาน 3D: ตรวจจับ EntityBase, TowerBase, หรือ HexTile
/// - แสดง Tooltip บน TooltipPanel + แสดง Range/Hit Indicator บนกระดาน + แสดง PathPreviewRenderer เมื่อชี้ผีใน Planning
/// </summary>
[DisallowMultipleComponent]
public class HoverRaycaster : SingletonBehaviour<HoverRaycaster>
{
    [Header("Raycast Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask hoverLayerMask = ~0;

    private readonly List<HexCoordinates> rangeBuffer = new List<HexCoordinates>(32);
    private readonly List<HexCoordinates> hitBuffer = new List<HexCoordinates>(32);

    private Object lastHoveredObject;

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        if (TooltipPanel.Instance != null && TooltipPanel.Instance.IsLocked)
        {
            ClearBoardPreview();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearBoardHover();
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(GameInput.MousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 200f, hoverLayerMask);

        EntityBase hoveredEntity = null;
        TowerBase hoveredTower = null;
        HexTile hoveredTile = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;

            if (hoveredEntity == null) hoveredEntity = col.GetComponentInParent<EntityBase>();
            if (hoveredTower == null) hoveredTower = col.GetComponentInParent<TowerBase>();
            if (hoveredTile == null) hoveredTile = col.GetComponentInParent<HexTile>();
        }

        if (hoveredTile != null)
        {
            if (hoveredEntity == null && hoveredTile.HasEntity) hoveredEntity = hoveredTile.CurrentEntity;
            if (hoveredTower == null && hoveredTile.HasTower) hoveredTower = hoveredTile.CurrentTower;
        }

        if (hoveredEntity != null && !hoveredEntity.IsDead)
        {
            HoverEntity(hoveredEntity);
        }
        else if (hoveredTower != null && !hoveredTower.IsDead && hoveredTower.CurrentTile != null)
        {
            HoverTower(hoveredTower);
        }
        else if (hoveredTile != null)
        {
            HoverTile(hoveredTile);
        }
        else
        {
            ClearBoardHover();
        }
    }

    private void HoverEntity(EntityBase entity)
    {
        lastHoveredObject = entity;

        // 1. แสดง Range & Hit Indicator ของผี
        ShowEntityRangeAndHit(entity);

        // 2. แสดงเส้นประเลขเทิร์นใน Planning
        if (PathPreviewRenderer.Instance != null)
        {
            if (GameStateManager.IsPlanning && entity.TryGetComponent(out EntityWalkable walkable))
            {
                PathPreviewRenderer.Instance.ShowFor(walkable);
            }
            else
            {
                PathPreviewRenderer.Instance.Hide();
            }
        }

        // 3. ส่งข้อมูลเข้า TooltipPanel
        if (TooltipPanel.Instance != null)
        {
            TooltipData data = BuildEntityTooltip(entity);
            TooltipPanel.Instance.Show(this, data);
        }
    }

    private void HoverTower(TowerBase tower)
    {
        lastHoveredObject = tower;

        if (PathPreviewRenderer.Instance != null) PathPreviewRenderer.Instance.Hide();
        ShowTowerRange(tower);

        if (TooltipPanel.Instance != null)
        {
            TooltipData data = BuildTowerTooltip(tower);
            TooltipPanel.Instance.Show(this, data);
        }
    }

    private void HoverTile(HexTile tile)
    {
        lastHoveredObject = tile;
        ClearBoardPreview();

        if (TooltipPanel.Instance != null)
        {
            TooltipData data = BuildTileTooltip(tile);
            TooltipPanel.Instance.Show(this, data);
        }
    }

    private void ClearBoardHover()
    {
        if (lastHoveredObject != null)
        {
            lastHoveredObject = null;
            if (TooltipPanel.Instance != null) TooltipPanel.Instance.Clear(this);
        }

        ClearBoardPreview();
    }

    private void ClearBoardPreview()
    {
        RangeIndicator.Hide(this);
        if (PathPreviewRenderer.Instance != null)
        {
            PathPreviewRenderer.Instance.Hide();
        }
    }

    // ---------- Range & Hit Preview ----------

    private void ShowEntityRangeAndHit(EntityBase entity)
    {
        if (RangeIndicator.Instance == null || entity.OccupiedTile == null) return;
        if (!entity.TryGetComponent(out EntityAttackable attacker) || attacker.Pattern == null)
        {
            RangeIndicator.Hide(this);
            return;
        }

        CombatResolver.CollectPreviewRangeAndHit(
            entity.OccupiedTile.Coordinates,
            entity.Facing,
            attacker.Pattern,
            BoardPatternContext.Get(),
            fromSpirit: true,
            canTargetLanterns: attacker.CanTargetLanterns,
            rangeBuffer,
            hitBuffer);

        RangeIndicator.Instance.Show(this, rangeBuffer, hitBuffer, false);
    }

    private void ShowTowerRange(TowerBase tower)
    {
        if (RangeIndicator.Instance == null || tower.CurrentTile == null) return;
        if (!tower.TryGetComponent(out TowerAttackable attacker) || attacker.Pattern == null)
        {
            RangeIndicator.Hide(this);
            return;
        }

        CombatResolver.CollectPreviewRangeAndHit(
            tower.CurrentTile.Coordinates,
            tower.Facing,
            attacker.Pattern,
            BoardPatternContext.ForTower(),
            fromSpirit: false,
            canTargetLanterns: true,
            rangeBuffer,
            hitBuffer);

        RangeIndicator.Instance.Show(this, rangeBuffer, hitBuffer, true);
    }

    // ---------- Tooltip Builders ----------

    public static TooltipData BuildEntityTooltip(EntityBase entity)
    {
        string subtitle = entity.IsFrenzied ? "Spirit (Frenzied)" : "Spirit";

        StringBuilder summary = new StringBuilder();
        summary.Append($"HP: {entity.CurrentHealth:0}/{entity.MaxHealth:0}   DEF: {entity.EffectiveDefense:0}\n");
        summary.Append($"AP: {entity.MaxActionPoints}   Move: {entity.MoveSpeed}");

        if (entity.TryGetComponent(out EntityAttackable atk))
        {
            float effectiveDmg = Mathf.Floor(atk.BaseDamage * entity.AttackMultiplier);
            summary.Append($"   ATK: {effectiveDmg:0}");
        }

        StringBuilder detail = new StringBuilder();
        if (entity.TryGetComponent(out EntityAttackable atkDetail) && atkDetail.Pattern != null && atkDetail.Pattern.RangePattern != null)
        {
            HexRangePattern rp = atkDetail.Pattern.RangePattern;
            detail.Append($"Range: {rp.Type} {rp.Size} ({atkDetail.Pattern.HitMode})");
        }

        if (entity.TryGetComponent(out EntityMutatable mut))
        {
            if (detail.Length > 0) detail.Append('\n');
            detail.Append($"Mutation Stage: {mut.MutationCount} / {mut.MaxMutations}");
        }

        IReadOnlyList<AbilityData> keywords = entity.Abilities != null ? entity.Abilities.All : null;
        return TooltipData.Create(entity.EntityName, summary.ToString(), detail.ToString(), subtitle, keywords);
    }

    public static TooltipData BuildTowerTooltip(TowerBase tower)
    {
        string subtitle = tower.GetComponent<TowerObjective>() != null
            ? "Objective Shrine"
            : (tower.IsShrineSide ? "Shrine Defense" : "Spirit Spawner");

        StringBuilder summary = new StringBuilder();
        summary.Append($"HP: {tower.CurrentHealth:0}/{tower.MaxHealth:0}   DEF: {tower.EffectiveDefense:0}");

        if (tower.TryGetComponent(out TowerAttackable atk))
        {
            summary.Append($"\nATK: {atk.BaseDamage:0}   Facing: {tower.Facing}");
        }
        else if (tower.TryGetComponent(out TowerSpawnable spawner))
        {
            summary.Append($"\nSpawns in: {spawner.TurnsUntilSpawn} turn(s) ({spawner.ActiveSpawnedCount} active)");
        }

        StringBuilder detail = new StringBuilder();
        if (tower.EffectiveDamageReduction > 0f)
        {
            detail.Append($"Altar Damage Reduction: {tower.EffectiveDamageReduction:P0}");
        }
        if (tower.IsArmorStripped)
        {
            if (detail.Length > 0) detail.Append('\n');
            detail.Append("Armor stripped by rotten offering (DEF = 0)");
        }
        if (tower.TryGetComponent(out TowerAttackable atkDetail) && atkDetail.Pattern != null && atkDetail.Pattern.RangePattern != null)
        {
            if (detail.Length > 0) detail.Append('\n');
            HexRangePattern rp = atkDetail.Pattern.RangePattern;
            detail.Append($"Range: {rp.Type} {rp.Size} ({atkDetail.Pattern.HitMode})");
        }

        IReadOnlyList<AbilityData> keywords = tower.Abilities != null ? tower.Abilities.All : null;
        return TooltipData.Create(tower.TowerName, summary.ToString(), detail.ToString(), subtitle, keywords);
    }

    private static TooltipData BuildTileTooltip(HexTile tile)
    {
        string title = $"Hex ({tile.Coordinates.Q}, {tile.Coordinates.R})";
        string subtitle = tile.GroundType.ToString();

        string denyMsg = TilePlacementManager.Instance != null ? TilePlacementManager.Instance.HoverDenyMessage : string.Empty;
        string summary = string.IsNullOrEmpty(denyMsg)
            ? $"Surface: {tile.GroundType}"
            : $"Surface: {tile.GroundType}\nCannot place: {denyMsg}";

        return TooltipData.Create(title, summary, null, subtitle);
    }
}
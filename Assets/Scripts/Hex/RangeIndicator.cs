using System.Collections.Generic;
using UnityEngine;

/// <summary>ชนิดของช่องที่ถูกไฮไลต์ เรียงตามความสำคัญ (ค่ามากชนะเมื่อหลายชั้นซ้อนกันบนช่องเดียว)</summary>
public enum RangeMark
{
    None = 0,
    SpiritRange,
    ShrineRange,
    SpiritHit,
    ShrineHit,
    Invalid
}

/// <summary>
/// ผู้คุมชั้น Range Indicator บนกระดาน (ใช้สีคนละชุดสำหรับฝั่งศาลกับฝั่งผี)
/// หลายผู้เรียกใช้พร้อมกันได้ (เช่น Ghost ตอนวางกับ Hover ป้อม) แต่ละคนส่ง owner ของตัวเอง
/// แล้ว Clear(owner) จะลบเฉพาะของตัวเอง ช่องที่ซ้อนกันใช้ RangeMark ที่สำคัญกว่า
///
/// ใช้งาน: RangeIndicator.Instance.Show(this, rangeTiles, hitTiles, shrineSide: true) ... Clear(this)
/// </summary>
[DisallowMultipleComponent]
public class RangeIndicator : SingletonBehaviour<RangeIndicator>
{
    [Header("Shrine Side (towers and shrines)")]
    [SerializeField] private Color shrineRangeColor = new Color(1.0f, 0.85f, 0.25f, 0.45f);
    [SerializeField] private Color shrineHitColor = new Color(1.0f, 0.45f, 0.1f, 0.65f);

    [Header("Spirit Side (parade)")]
    [SerializeField] private Color spiritRangeColor = new Color(0.65f, 0.3f, 1.0f, 0.45f);
    [SerializeField] private Color spiritHitColor = new Color(1.0f, 0.15f, 0.5f, 0.65f);

    [Header("Invalid Placement")]
    [SerializeField] private Color invalidColor = new Color(1.0f, 0.1f, 0.1f, 0.55f);

    private struct MarkedTile
    {
        public HexTile tile;
        public RangeMark mark;
    }

    private readonly Dictionary<object, List<MarkedTile>> layers = new Dictionary<object, List<MarkedTile>>();
    private readonly Dictionary<HexTile, RangeMark> resolved = new Dictionary<HexTile, RangeMark>();
    private readonly List<HexTile> litTiles = new List<HexTile>(64);

    /// <summary>Hide ที่ปลอดภัยแม้ไม่มี RangeIndicator ในซีน (เรียกจาก OnDisable / OnDestroy ได้)</summary>
    public static void Hide(object owner)
    {
        if (Instance != null) Instance.Clear(owner);
    }

    /// <summary>
    /// แสดงช่อง Range และ Hit ของ owner (แทนที่ของเดิมของ owner นั้น) hitTiles ใส่ null ได้
    /// ช่องที่อยู่ทั้งใน Range และ Hit จะแสดงเป็นสี Hit
    /// </summary>
    public void Show(object owner, IReadOnlyList<HexCoordinates> rangeTiles, IReadOnlyList<HexCoordinates> hitTiles, bool shrineSide)
    {
        if (owner == null) return;

        List<MarkedTile> layer = GetCleanLayer(owner);
        AddTiles(layer, rangeTiles, shrineSide ? RangeMark.ShrineRange : RangeMark.SpiritRange);
        AddTiles(layer, hitTiles, shrineSide ? RangeMark.ShrineHit : RangeMark.SpiritHit);
        Render();
    }

    /// <summary>แสดงช่องเป็นสีแดง (วางไม่ได้ เช่น ไม่ผ่าน Valid Threat Check)</summary>
    public void ShowInvalid(object owner, IReadOnlyList<HexCoordinates> tiles)
    {
        if (owner == null) return;

        List<MarkedTile> layer = GetCleanLayer(owner);
        AddTiles(layer, tiles, RangeMark.Invalid);
        Render();
    }

    public void Clear(object owner)
    {
        if (owner == null) return;
        if (layers.Remove(owner)) Render();
    }

    public void ClearAll()
    {
        layers.Clear();
        Render();
    }

    // ---------- Internals ----------

    private List<MarkedTile> GetCleanLayer(object owner)
    {
        if (!layers.TryGetValue(owner, out List<MarkedTile> layer))
        {
            layer = new List<MarkedTile>(32);
            layers[owner] = layer;
        }

        layer.Clear();
        return layer;
    }

    private static void AddTiles(List<MarkedTile> layer, IReadOnlyList<HexCoordinates> coords, RangeMark mark)
    {
        LoopPathManager board = LoopPathManager.Instance;
        if (board == null || coords == null) return;

        for (int i = 0; i < coords.Count; i++)
        {
            if (board.TryGetTileAt(coords[i], out HexTile tile))
            {
                layer.Add(new MarkedTile { tile = tile, mark = mark });
            }
        }
    }

    private void Render()
    {
        resolved.Clear();

        foreach (KeyValuePair<object, List<MarkedTile>> pair in layers)
        {
            List<MarkedTile> layer = pair.Value;
            for (int i = 0; i < layer.Count; i++)
            {
                MarkedTile m = layer[i];
                if (m.tile == null) continue;

                if (!resolved.TryGetValue(m.tile, out RangeMark current) || m.mark > current)
                {
                    resolved[m.tile] = m.mark;
                }
            }
        }

        for (int i = 0; i < litTiles.Count; i++)
        {
            HexTile tile = litTiles[i];
            if (tile != null && !resolved.ContainsKey(tile))
            {
                tile.SetRangeIndicator(false, Color.white);
            }
        }

        litTiles.Clear();

        foreach (KeyValuePair<HexTile, RangeMark> pair in resolved)
        {
            pair.Key.SetRangeIndicator(true, ColorFor(pair.Value));
            litTiles.Add(pair.Key);
        }
    }

    private Color ColorFor(RangeMark mark)
    {
        switch (mark)
        {
            case RangeMark.ShrineRange: return shrineRangeColor;
            case RangeMark.ShrineHit: return shrineHitColor;
            case RangeMark.SpiritRange: return spiritRangeColor;
            case RangeMark.SpiritHit: return spiritHitColor;
            case RangeMark.Invalid: return invalidColor;
            default: return Color.clear;
        }
    }

    protected override void OnDestroy()
    {
        layers.Clear();
        base.OnDestroy();
    }
}

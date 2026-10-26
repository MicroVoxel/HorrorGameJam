using System;

/// <summary>
/// ตัวสร้าง HexPatternContext ที่ใช้ซ้ำได้ (delegate สร้างครั้งเดียว ไม่สร้างใหม่ทุกการโจมตี)
/// จุดเดียวที่ผลของถวายเข้ามาปรับ Pattern ของป้อม (Fragrance ที่เน่า = sizeDelta -1, Fortune Candle ที่สด = ignoreBlocking)
/// </summary>
public static class BoardPatternContext
{
    private static readonly Func<HexCoordinates, bool> OnBoard =
        c => LoopPathManager.Instance != null && LoopPathManager.Instance.TryGetTileAt(c, out _);

    private static readonly Func<HexCoordinates, bool> Blocking =
        c => LoopPathManager.Instance != null
             && LoopPathManager.Instance.TryGetTileAt(c, out HexTile t)
             && t.IsOccupied;

    public static HexPatternContext Get(int sizeDelta = 0, bool ignoreBlocking = false)
    {
        return new HexPatternContext
        {
            isOnBoard = OnBoard,
            isBlocking = Blocking,
            sizeDelta = sizeDelta,
            ignoreBlocking = ignoreBlocking
        };
    }

    /// <summary>สำหรับป้อมฝั่งศาล: อ่านผลของถวาย Fragrance (เน่า: ลดขนาด N) และ Fortune Candle (สด: ยิงข้ามหน่วย)</summary>
    public static HexPatternContext ForTower()
    {
        AltarManager altar = AltarManager.Instance;
        int sizeDelta = altar != null ? altar.TowerPatternSizeDelta : 0;
        bool ignoreBlocking = altar != null && altar.TowerIgnoreBlocking;
        return Get(sizeDelta, ignoreBlocking);
    }
}
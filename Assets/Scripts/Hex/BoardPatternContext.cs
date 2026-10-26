using System;

/// <summary>
/// ตัวสร้าง HexPatternContext ที่ใช้ซ้ำได้ (delegate สร้างครั้งเดียว ไม่สร้างใหม่ทุกการโจมตี)
/// จุดเดียวที่ผลของถวายจะเข้ามาปรับ Pattern ในภายหลัง (Fragrance ที่เน่า = sizeDelta -1, Fortune Candle ที่สด = ignoreBlocking)
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
}

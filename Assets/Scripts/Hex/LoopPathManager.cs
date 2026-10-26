using System.Collections.Generic;
using UnityEngine;

public enum LoopPresetType
{
    Random,         // สุ่มเลือก 1 ใน 3 รูปแบบด้านล่างตอนเริ่มเกม
    OLoop,          // วงแหวนหกเหลี่ยมมาตรฐาน (O-Loop)
    HourglassLoop,  // วงแหวนคอดกลางรูปนาฬิกาทราย (Hourglass)
    Figure8Loop     // วงแหวนทรงเลขแปด (คอดกลางแนวนอน)
}

/// <summary>
/// สร้างกระดาน Hex เต็มผืน (ค่าเริ่มต้น 13x9 สไตล์ Loop Hero) 
/// พร้อมเจาะสลักทางเดินลูปตาม Preset และมีฟังก์ชันตรวจสอบกฎการวางศาลเจ้า/รังผีข้างทางเดิน
/// </summary>
[DisallowMultipleComponent]
public class LoopPathManager : SingletonBehaviour<LoopPathManager>
{

    [Header("Board Prefab Reference")]
    [SerializeField] private GameObject hexTilePrefab;

    [Header("Board Dimensions (Loop Hero Style: 13x9)")]
    [SerializeField] private int gridWidth = 13;
    [SerializeField] private int gridHeight = 9;
    [SerializeField] private float hexOuterRadius = 1.0f;

    [Header("Tile Rotation Settings")]
    [Tooltip("ปรับองศาการหมุนของโมเดล HexTile (เช่น 0, 30, 0)")]
    [SerializeField] private Vector3 tileRotationEuler = Vector3.zero;

    [Header("Loop Path Preset Configuration")]
    [SerializeField] private LoopPresetType pathPreset = LoopPresetType.Random;
    [SerializeField] private int oLoopRadius = 3;

    [Header("Tiles Registry")]
    [SerializeField] private List<HexTile> pathTiles = new List<HexTile>();
    private readonly Dictionary<HexCoordinates, HexTile> allTiles = new Dictionary<HexCoordinates, HexTile>();

    public List<HexTile> PathTiles => pathTiles;
    public int PathLength => pathTiles.Count;
    public float HexOuterRadius => hexOuterRadius;
    public IEnumerable<HexTile> AllTiles => allTiles.Values;

    protected override void OnSingletonAwake()
    {

        if (allTiles.Count == 0 && transform.childCount == 0)
        {
            GenerateFullGridAndLoop();
        }
        else
        {
            IndexExistingChildTiles();
        }
    }

    [ContextMenu("Generate Full Grid And Loop")]
    public void GenerateFullGridAndLoop()
    {
        if (hexTilePrefab == null)
        {
            Debug.LogError("[LoopPathManager] กรุณาใส่ HexTilePrefab ก่อนสร้างกระดาน!");
            return;
        }

        ClearGrid();

        int halfW = gridWidth / 2;
        int halfH = gridHeight / 2;
        Quaternion tileRotation = Quaternion.Euler(tileRotationEuler);

        // 1. สร้างแผ่นดินเต็มผืน X * Y เป็น Ground ทั้งหมด
        for (int r = -halfH; r <= halfH; r++)
        {
            int rOffset = Mathf.FloorToInt(r / 2.0f);

            // FIX บัก 8: ช่วง q ต้องเลื่อนตาม rOffset ให้แถวเรียงเป็นสี่เหลี่ยมในโลกจริง (ทุกแถวกว้าง gridWidth ช่อง)
            // เดิมใช้ Min/Max ผสมกับ halfW ทำให้บางแถวยาวเกินและกระดานเอียงเป็นสี่เหลี่ยมด้านขนาน
            int qMin = -halfW - rOffset;
            int qMax = halfW - rOffset;

            for (int q = qMin; q <= qMax; q++)
            {
                HexCoordinates coords = new HexCoordinates(q, r);
                Vector3 worldPos = coords.ToWorldPosition(hexOuterRadius, 0f);

                GameObject tileObj = Instantiate(hexTilePrefab, worldPos, tileRotation, transform);
                HexTile tile = tileObj.GetComponent<HexTile>();

                if (tile != null)
                {
                    tile.Initialize(coords, HexGroundType.Ground, -1);
                    allTiles[coords] = tile;
                }
            }
        }

        // 2. สลักทางเดินวนลูป (Path) ตาม Preset
        CarveSelectedPresetPath();

        Debug.Log($"<color=green>[LoopPathManager]</color> สร้างกระดานสำเร็จ: ทั้งหมด {allTiles.Count} ช่อง, ทางเดินลูป {pathTiles.Count} ช่อง");
    }

    [ContextMenu("Apply Rotation To Existing Tiles")]
    public void ApplyRotationToExistingTiles()
    {
        Quaternion rot = Quaternion.Euler(tileRotationEuler);
        HexTile[] existing = GetComponentsInChildren<HexTile>();
        for (int i = 0; i < existing.Length; i++)
        {
            existing[i].transform.localRotation = rot;
        }
    }

    private void CarveSelectedPresetPath()
    {
        pathTiles.Clear();

        LoopPresetType chosen = pathPreset;
        if (chosen == LoopPresetType.Random)
        {
            LoopPresetType[] options = { LoopPresetType.OLoop, LoopPresetType.HourglassLoop, LoopPresetType.Figure8Loop };
            chosen = options[Random.Range(0, options.Length)];
        }

        List<HexCoordinates> coordsList;
        switch (chosen)
        {
            case LoopPresetType.HourglassLoop:
                coordsList = GetHourglassCoordinates();
                break;
            case LoopPresetType.Figure8Loop:
                coordsList = GetFigure8Coordinates();
                break;
            case LoopPresetType.OLoop:
            default:
                coordsList = GetRingCoordinates(Mathf.Max(2, oLoopRadius));
                break;
        }

        int pathIdx = 0;
        for (int i = 0; i < coordsList.Count; i++)
        {
            HexCoordinates coords = coordsList[i];
            if (allTiles.TryGetValue(coords, out HexTile tile))
            {
                if (tile.GroundType != HexGroundType.Path)
                {
                    tile.SetGroundType(HexGroundType.Path, pathIdx);
                    pathTiles.Add(tile);
                    pathIdx++;
                }
            }
            else
            {
                // FIX บัก 2: พิกัดของ Preset อยู่นอกกระดาน (เช่น ตั้ง gridWidth/Height เล็กเกินไป)
                Debug.LogWarning($"[LoopPathManager] Preset {chosen}: พิกัด {coords} อยู่นอกกระดาน จะถูกข้าม ทางเดินจะขาด");
            }
        }

        ValidatePathContinuity(chosen);
    }

    // FIX บัก 2: ตรวจว่าช่องที่อยู่ติดกันใน path (รวมช่องสุดท้ายวนกลับช่องแรก) เป็นเพื่อนบ้านกันจริง
    /// <summary>
    /// เตือนใน Console เมื่อทางเดินมีช่องที่ไม่ติดกัน (DistanceTo != 1) หรือมีช่องน้อยเกินไป
    /// คืน true ถ้าทางเดินต่อกันเป็นวงครบ ใช้ ContextMenu ตรวจกระดานที่สร้างไว้แล้วใน Editor ได้
    /// </summary>
    [ContextMenu("Validate Path Continuity")]
    public bool ValidatePathContinuity()
    {
        return ValidatePathContinuity(pathPreset);
    }

    private bool ValidatePathContinuity(LoopPresetType presetLabel)
    {
        int n = pathTiles.Count;
        if (n < 3)
        {
            Debug.LogWarning($"[LoopPathManager] Preset {presetLabel}: ทางเดินมีเพียง {n} ช่อง ไม่พอสร้างลูป");
            return false;
        }

        bool ok = true;
        for (int i = 0; i < n; i++)
        {
            HexTile a = pathTiles[i];
            HexTile b = pathTiles[(i + 1) % n];
            if (a == null || b == null) continue;

            int dist = a.Coordinates.DistanceTo(b.Coordinates);
            if (dist != 1)
            {
                ok = false;
                Debug.LogWarning(
                    $"[LoopPathManager] Preset {presetLabel}: ช่อง path #{i} {a.Coordinates} → #{(i + 1) % n} {b.Coordinates} " +
                    $"ห่างกัน {dist} ช่อง (ต้องเป็น 1) ผีจะกระโดดข้ามช่อง", a);
            }
        }

        return ok;
    }

    private List<HexCoordinates> GetRingCoordinates(int radius)
    {
        List<HexCoordinates> results = new List<HexCoordinates>();
        if (radius <= 0) return results;

        HexCoordinates current = new HexCoordinates(
            HexCoordinates.Directions[4].Q * radius,
            HexCoordinates.Directions[4].R * radius
        );

        for (int side = 0; side < 6; side++)
        {
            for (int step = 0; step < radius; step++)
            {
                results.Add(current);
                current = new HexCoordinates(
                    current.Q + HexCoordinates.Directions[side].Q,
                    current.R + HexCoordinates.Directions[side].R
                );
            }
        }

        return results;
    }

    /// <summary>
    /// Preset ทางเดินทรงนาฬิกาทราย (Hourglass Loop) บีบคอดเข้าหาใจกลางกระดาน
    /// </summary>
    private List<HexCoordinates> GetHourglassCoordinates()
    {
        return new List<HexCoordinates>
        {
            new HexCoordinates(0, -3),
            new HexCoordinates(1, -3),
            new HexCoordinates(2, -3),
            new HexCoordinates(3, -3),
            new HexCoordinates(3, -2),
            new HexCoordinates(2, -1),
            new HexCoordinates(1, 0),
            new HexCoordinates(1, 1),
            new HexCoordinates(1, 2),
            new HexCoordinates(0, 3),
            new HexCoordinates(-1, 3),
            new HexCoordinates(-2, 3),
            new HexCoordinates(-3, 3),
            new HexCoordinates(-3, 2),
            new HexCoordinates(-2, 1),
            new HexCoordinates(-1, 0),
            new HexCoordinates(-1, -1),
            new HexCoordinates(-1, -2)
        };
    }

    /// <summary>
    /// Preset ทางเดินทรงเลข 8 (Figure-8) เป็นลูปเดียวที่คอดตรงกลางแนวนอน: ปีกซ้ายและปีกขวา
    /// เชื่อมกันด้วยคอบน (0,1) และคอล่าง (0,-1)/(1,-1) โดยมีช่อง (0,0) และ (1,0) เป็นพื้นดินอยู่กลางลูป
    ///
    /// FIX บัก 2: ของเดิมพยายามให้ทางเดินผ่านจุดกลางสองรอบ (ตัดกันแบบเลข 8 จริง) ซึ่งทำไม่ได้
    /// เพราะ 1 ช่องมี PathIndex ได้ค่าเดียว (ช่องซ้ำถูกข้าม) และมีสองรอยต่อที่ห่างกัน 2 ช่อง
    /// แบบใหม่ทุกคู่ที่อยู่ติดกัน (รวมช่องสุดท้ายวนกลับช่องแรก) ห่างกัน 1 ช่องพอดี
    /// </summary>
    private List<HexCoordinates> GetFigure8Coordinates()
    {
        return new List<HexCoordinates>
        {
            // คอบน
            new HexCoordinates(0, 1),
            // ปีกซ้าย
            new HexCoordinates(-1, 1),
            new HexCoordinates(-2, 1),
            new HexCoordinates(-2, 0),
            new HexCoordinates(-1, -1),
            // คอล่าง
            new HexCoordinates(0, -1),
            new HexCoordinates(1, -1),
            // ปีกขวา
            new HexCoordinates(2, -1),
            new HexCoordinates(3, -1),
            new HexCoordinates(3, 0),
            new HexCoordinates(2, 1),
            new HexCoordinates(1, 1)
            // (1,1) -> (0,1) ปิดลูปกลับช่องแรก
        };
    }

    private void ClearGrid()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        allTiles.Clear();
        pathTiles.Clear();
    }

    private void IndexExistingChildTiles()
    {
        allTiles.Clear();
        pathTiles.Clear();

        HexTile[] existing = GetComponentsInChildren<HexTile>();
        for (int i = 0; i < existing.Length; i++)
        {
            HexTile tile = existing[i];
            allTiles[tile.Coordinates] = tile;
            if (tile.GroundType == HexGroundType.Path)
            {
                pathTiles.Add(tile);
            }
        }

        pathTiles.Sort((a, b) => a.PathIndex.CompareTo(b.PathIndex));
    }

    /// <summary>
    /// วนค่า index ให้อยู่ในช่วง 0..PathLength-1 (รองรับค่าติดลบ) ใช้ร่วมกับ EntityWalkable
    /// </summary>
    public int WrapIndex(int index) => ((index % pathTiles.Count) + pathTiles.Count) % pathTiles.Count;

    public Vector3 GetWaypoint(int index)
    {
        if (pathTiles.Count == 0) return transform.position;
        int safeIndex = WrapIndex(index);
        return pathTiles[safeIndex].transform.position;
    }

    public HexTile GetTile(int index)
    {
        if (pathTiles.Count == 0) return null;
        int safeIndex = WrapIndex(index);
        return pathTiles[safeIndex];
    }

    public bool TryGetTileAt(HexCoordinates coords, out HexTile tile)
    {
        return allTiles.TryGetValue(coords, out tile);
    }

    public HexTile GetTileAt(HexCoordinates coords)
    {
        allTiles.TryGetValue(coords, out HexTile tile);
        return tile;
    }

    /// <summary>
    /// ดึงรายการช่องเพื่อนบ้านทั้ง 6 ทิศที่มีอยู่จริงบนกระดาน
    /// </summary>
    public List<HexTile> GetNeighbors(HexCoordinates coords)
    {
        List<HexTile> neighbors = new List<HexTile>(6);
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates nCoords = coords.GetNeighbor(dir);
            if (allTiles.TryGetValue(nCoords, out HexTile neighborTile))
            {
                neighbors.Add(neighborTile);
            }
        }
        return neighbors;
    }

    /// <summary>
    /// ตรวจสอบว่าช่องนี้ติดกับทางเดิน Path อย่างน้อย 1 ด้านหรือไม่ (Roadside Check)
    /// </summary>
    public bool IsAdjacentToPath(HexCoordinates coords)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates nCoords = coords.GetNeighbor(dir);
            if (allTiles.TryGetValue(nCoords, out HexTile neighborTile) && neighborTile.GroundType == HexGroundType.Path)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ตรวจสอบว่าช่องรอบตัว 6 ทิศมีศาลเจ้าหลัก (TowerDefenseBase) ตั้งอยู่แล้วหรือไม่ (ห้ามวางศาลติดกัน)
    /// </summary>
    public bool HasAdjacentDefenseBase(HexCoordinates coords)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            HexCoordinates nCoords = coords.GetNeighbor(dir);
            if (allTiles.TryGetValue(nCoords, out HexTile neighborTile) && neighborTile.HasTower)
            {
                if (neighborTile.CurrentTower.GetComponent<TowerObjective>() != null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// ค้นหาช่องทางเดิน Path ที่อยู่ใกล้พิกัดที่กำหนดมากที่สุด (สำหรับให้รังผีปล่อยยูนิตลงถนน)
    /// </summary>
    public int GetClosestPathIndex(Vector3 worldPos)
    {
        if (pathTiles.Count == 0) return 0;

        int closestIdx = 0;
        float minSqrDist = float.MaxValue;

        for (int i = 0; i < pathTiles.Count; i++)
        {
            float sqrDist = (pathTiles[i].transform.position - worldPos).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closestIdx = i;
            }
        }
        return closestIdx;
    }

    private void OnDrawGizmos()
    {
        if (pathTiles == null || pathTiles.Count < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < pathTiles.Count; i++)
        {
            if (pathTiles[i] == null || pathTiles[(i + 1) % pathTiles.Count] == null) continue;

            Vector3 from = pathTiles[i].transform.position;
            Vector3 to = pathTiles[(i + 1) % pathTiles.Count].transform.position;
            Gizmos.DrawLine(from + Vector3.up * 0.15f, to + Vector3.up * 0.15f);
            Gizmos.DrawSphere(from + Vector3.up * 0.15f, 0.12f);
        }
    }
}
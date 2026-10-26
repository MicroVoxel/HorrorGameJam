using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// วาดเส้นประและป้ายเลขเทิร์น 1..N บนทางเดินเมื่อผู้เล่น Hover ผีในเฟส Planning (GDD 3 & 4.8)
/// คำนวณจากการเดินเต็ม AP บนทางโล่งผ่าน EntityWalkable.GetProjectedPathIndices
/// </summary>
[DisallowMultipleComponent]
public class PathPreviewRenderer : SingletonBehaviour<PathPreviewRenderer>
{
    [Header("Line Settings")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Color lineColor = new Color(0.75f, 0.45f, 1.0f, 0.85f);
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float heightOffset = 0.18f;

    [Header("Turn Number Markers")]
    [SerializeField] private GameObject turnMarkerPrefab;
    [SerializeField] private Color markerTextColor = new Color(0.95f, 0.88f, 1.0f, 1.0f);

    private readonly List<int> projectedIndices = new List<int>(16);
    private readonly List<GameObject> markerPool = new List<GameObject>(16);
    private EntityWalkable currentTarget;

    protected override void OnSingletonAwake()
    {
        EnsureLineRenderer();
        Hide();
    }

    private void EnsureLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.positionCount = 0;

        if (lineRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
            }
        }
    }

    public void ShowFor(EntityWalkable walkable)
    {
        if (walkable == null || !walkable.IsActorActive || LoopPathManager.Instance == null)
        {
            Hide();
            return;
        }

        LoopPathManager board = LoopPathManager.Instance;
        int turns = GameStateManager.TurnsThisRound > 0
            ? GameStateManager.TurnsThisRound
            : (GameStateManager.Instance != null ? GameStateManager.Instance.Rules.GetTurnsForRound(Mathf.Max(1, GameStateManager.CurrentRound)) : 4);

        walkable.GetProjectedPathIndices(turns, projectedIndices);
        if (projectedIndices.Count == 0)
        {
            Hide();
            return;
        }

        currentTarget = walkable;
        EnsureLineRenderer();

        // สร้างจุดเส้นทางเดินละเอียดทีละช่องตั้งแต่จุดปัจจุบันไปจนถึงช่องจบ Round
        int stepsPerTurn = walkable.Entity.MaxActionPoints * walkable.Entity.MoveSpeed;
        int totalSteps = Mathf.Max(1, stepsPerTurn * turns);

        lineRenderer.enabled = true;
        lineRenderer.positionCount = totalSteps + 1;

        Vector3 up = Vector3.up * heightOffset;
        lineRenderer.SetPosition(0, board.GetWaypoint(walkable.CurrentPathIndex) + up);

        for (int s = 1; s <= totalSteps; s++)
        {
            Vector3 wp = board.GetWaypoint(walkable.CurrentPathIndex + s) + up;
            lineRenderer.SetPosition(s, wp);
        }

        // วางป้ายเลขเทิร์น 1..N
        EnsureMarkerPool(projectedIndices.Count);
        for (int i = 0; i < markerPool.Count; i++)
        {
            if (i < projectedIndices.Count)
            {
                Vector3 pos = board.GetWaypoint(projectedIndices[i]) + Vector3.up * (heightOffset + 0.22f);
                GameObject marker = markerPool[i];
                marker.transform.position = pos;
                marker.SetActive(true);

                TextMeshPro label = marker.GetComponentInChildren<TextMeshPro>(true);
                if (label != null)
                {
                    label.text = (i + 1).ToString();
                }
            }
            else
            {
                markerPool[i].SetActive(false);
            }
        }
    }

    public void Hide()
    {
        currentTarget = null;
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }

        for (int i = 0; i < markerPool.Count; i++)
        {
            if (markerPool[i] != null) markerPool[i].SetActive(false);
        }
    }

    private void EnsureMarkerPool(int needed)
    {
        while (markerPool.Count < needed)
        {
            GameObject marker;
            if (turnMarkerPrefab != null)
            {
                marker = Instantiate(turnMarkerPrefab, transform);
            }
            else
            {
                marker = new GameObject($"TurnMarker_{markerPool.Count + 1}");
                marker.transform.SetParent(transform, false);
                TextMeshPro tmp = marker.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 3.2f;
                tmp.color = markerTextColor;
                marker.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
            }

            marker.SetActive(false);
            markerPool.Add(marker);
        }
    }
}
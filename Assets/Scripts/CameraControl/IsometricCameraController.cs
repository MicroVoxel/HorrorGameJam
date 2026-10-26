using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// กล้อง Isometric มุมคงที่ (ไม่หมุน) สไตล์เกมวางแผนอย่าง Civ
/// - เลื่อนกล้อง (Pan): W/A/S/D หรือ ลูกศร, ลากด้วยเมาส์ปุ่มกลาง (จับแผนที่แล้วลาก), Edge Scroll (ปิดเป็นค่าเริ่มต้น)
/// - ซูม: ล้อเมาส์ (ซูมเข้าหาตำแหน่งเมาส์ได้)
/// - ใช้ unscaledDeltaTime ทั้งหมด จึงทำงานปกติในโหมด Moonlit (Tactical Pause)
///
/// วิธีใช้: ถอด OrbitCameraController ออกจากกล้อง แล้วใส่สคริปต์นี้แทน (ห้ามมีทั้งสองตัวพร้อมกัน)
/// ต้องใช้ GameInput ฉบับอัปเดต (มี MiddlePressed / MiddleHeld)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class IsometricCameraController : MonoBehaviour
{
    [Header("Fixed Isometric Angle (ล็อก ไม่หมุนตามผู้เล่น)")]
    [Tooltip("มุมหมุนรอบแกนตั้ง: 45 = Isometric จริง, 0 = มองตรงแนวแถว Hex")]
    [SerializeField] private float yaw = 45f;
    [Tooltip("มุมก้ม: 35.264 = Isometric จริง, 30 = แบบเกมทั่วไป, 45-55 = สไตล์ Civ")]
    [Range(20f, 80f)]
    [SerializeField] private float pitch = 35.264f;
    [Tooltip("true = Orthographic (Isometric จริง ไม่มี Perspective) / false = Perspective มุมเอียงแบบ Civ")]
    [SerializeField] private bool useOrthographic = true;
    [Tooltip("ระยะวางกล้องห่างจากจุดโฟกัส (ใช้เฉพาะโหมด Orthographic ไม่มีผลต่อขนาดภาพ)")]
    [SerializeField] private float orthoCameraDistance = 50f;

    [Header("Focus & Pan Bounds")]
    [Tooltip("จุดโฟกัสเริ่มต้น และเป็นจุดกึ่งกลางของขอบเขตเลื่อนกล้อง")]
    [SerializeField] private Vector3 focusPoint = Vector3.zero;
    [Tooltip("ความสูงของพื้นกระดาน (ใช้คำนวณจุดที่เมาส์ชี้บนพื้น)")]
    [SerializeField] private float groundY = 0f;
    [Tooltip("ระยะที่เลื่อนออกจากจุดกึ่งกลางได้สูงสุด (X, Z) กระดาน 13x9 กว้างราว ±11 x ±6")]
    [SerializeField] private Vector2 panLimit = new Vector2(12f, 8f);

    [Header("Pan Settings")]
    [Tooltip("ความเร็วเลื่อนกล้อง (หน่วย/วินาที) ที่ระดับซูมเริ่มต้น จะเร็วขึ้นเองเมื่อซูมออก")]
    [SerializeField] private float panSpeed = 14f;
    [SerializeField] private float panSmoothTime = 0.08f;
    [Tooltip("กดล้อเมาส์ค้างแล้วลากเพื่อ 'จับ' แผนที่เลื่อน")]
    [SerializeField] private bool allowMiddleMouseDrag = true;
    [Tooltip("เลื่อนกล้องเมื่อเมาส์ชิดขอบจอ (ปิดไว้ก่อน เพราะแถบการ์ด/กระเป๋าอยู่ขอบล่าง)")]
    [SerializeField] private bool edgeScroll = false;
    [SerializeField] private float edgeMarginPixels = 12f;

    [Header("Zoom Settings (Mouse Scroll Wheel)")]
    [Tooltip("สัดส่วนที่ซูมต่อ 1 คลิกล้อง (0.12 = 12%) ไม่ขึ้นกับเฟรมเรต")]
    [Range(0.03f, 0.4f)]
    [SerializeField] private float zoomStepPercent = 0.12f;
    [SerializeField] private float zoomSmoothTime = 0.08f;
    [Tooltip("ซูมเข้า/ออกโดยให้จุดใต้เมาส์อยู่ที่เดิม (แบบ Civ)")]
    [SerializeField] private bool zoomToCursor = true;

    [Header("Zoom Range — Orthographic (Orthographic Size)")]
    [SerializeField] private float orthoMinSize = 4f;
    [SerializeField] private float orthoMaxSize = 12f;
    [SerializeField] private float orthoStartSize = 8f;

    [Header("Zoom Range — Perspective (Distance)")]
    [SerializeField] private float perspMinDistance = 6f;
    [SerializeField] private float perspMaxDistance = 24f;
    [SerializeField] private float perspStartDistance = 14f;

    private Camera cam;
    private Quaternion fixedRotation;
    private Plane groundPlane;

    private Vector3 focus;
    private Vector3 targetFocus;
    private Vector3 focusVelocity;

    private float zoom;
    private float targetZoom;
    private float zoomVelocity;

    private bool isGrabbing;
    private Vector3 grabAnchor;

    private float MinZoom => useOrthographic ? orthoMinSize : perspMinDistance;
    private float MaxZoom => useOrthographic ? orthoMaxSize : perspMaxDistance;
    private float StartZoom => useOrthographic ? orthoStartSize : perspStartDistance;

    public float CurrentZoom => zoom;
    public Vector3 FocusPosition => focus;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        fixedRotation = Quaternion.Euler(pitch, yaw, 0f);
        groundPlane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));

        cam.orthographic = useOrthographic;
        if (useOrthographic)
        {
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, orthoCameraDistance * 3f);
        }

        focus = targetFocus = ClampFocus(focusPoint);
        zoom = targetZoom = Mathf.Clamp(StartZoom, MinZoom, MaxZoom);

        ApplyCamera();
    }

    private void LateUpdate()
    {
        bool overUI = IsPointerOverUI();

        HandleGrabDrag(overUI);
        HandleKeyboardAndEdgePan(overUI);
        HandleScrollZoom(overUI);
        UpdateSmoothedCamera();
    }

    // ---------- Input ----------

    private void HandleGrabDrag(bool overUI)
    {
        if (!allowMiddleMouseDrag) return;

        if (!isGrabbing)
        {
            if (GameInput.MiddlePressed && !overUI && TryGetGroundPoint(out Vector3 start))
            {
                isGrabbing = true;
                grabAnchor = start;
                focusVelocity = Vector3.zero;
            }
            return;
        }

        if (!GameInput.MiddleHeld)
        {
            isGrabbing = false;
            return;
        }

        if (!TryGetGroundPoint(out Vector3 current)) return;

        // ขยับกล้องให้จุดที่จับไว้กลับมาอยู่ใต้เมาส์ (ไม่มีหน่วง เพื่อให้รู้สึกเหมือนลากแผนที่จริง)
        Vector3 delta = grabAnchor - current;
        delta.y = 0f;

        focus = ClampFocus(focus + delta);
        targetFocus = focus;
        focusVelocity = Vector3.zero;
        ApplyCamera();

        // ถ้าชนขอบเขต ให้ยึดจุดใหม่ใต้เมาส์ เพื่อไม่ให้ delta สะสมจนกล้องกระชาก
        if (TryGetGroundPoint(out Vector3 reanchored))
        {
            grabAnchor = reanchored;
        }
    }

    private void HandleKeyboardAndEdgePan(bool overUI)
    {
        if (isGrabbing) return;

        Vector2 move = GameInput.MoveAxis;

        if (edgeScroll && !overUI && Application.isFocused)
        {
            move += GetEdgeScrollAxis();
        }

        if (move.sqrMagnitude < 0.0001f) return;
        move = Vector2.ClampMagnitude(move, 1f);

        // W = ขึ้นบนจอ, D = ขวาจอ (อิงแนวกล้องบนพื้นราบ ไม่ขึ้นกับมุมก้ม)
        Quaternion flatYaw = Quaternion.Euler(0f, yaw, 0f);
        Vector3 direction = (flatYaw * Vector3.right) * move.x + (flatYaw * Vector3.forward) * move.y;

        float zoomFactor = StartZoom > 0.01f ? zoom / StartZoom : 1f;
        float speed = panSpeed * zoomFactor;

        targetFocus = ClampFocus(targetFocus + direction * (speed * Time.unscaledDeltaTime));
    }

    private Vector2 GetEdgeScrollAxis()
    {
        Vector2 m = GameInput.MousePosition;
        if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return Vector2.zero;

        float x = 0f;
        float y = 0f;
        if (m.x <= edgeMarginPixels) x = -1f;
        else if (m.x >= Screen.width - edgeMarginPixels) x = 1f;

        if (m.y <= edgeMarginPixels) y = -1f;
        else if (m.y >= Screen.height - edgeMarginPixels) y = 1f;

        return new Vector2(x, y);
    }

    private void HandleScrollZoom(bool overUI)
    {
        if (overUI) return;

        float scroll = GameInput.ScrollY;
        if (Mathf.Abs(scroll) < 0.01f) return;

        // เลื่อนล้อขึ้น = ซูมเข้า (ค่าลด) / ล้อลง = ซูมออก (ค่าเพิ่ม) — ขั้นคงที่ต่อคลิก
        float factor = 1f - Mathf.Sign(scroll) * zoomStepPercent;
        targetZoom = Mathf.Clamp(targetZoom * factor, MinZoom, MaxZoom);
    }

    // ---------- Camera Update ----------

    private void UpdateSmoothedCamera()
    {
        if (!isGrabbing)
        {
            focus = Vector3.SmoothDamp(focus, targetFocus, ref focusVelocity, panSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        // วางกล้องตามโฟกัสล่าสุด (ซูมค่าเดิม) เพื่อจำจุดใต้เมาส์ก่อนเปลี่ยนซูม
        ApplyCamera();

        float previousZoom = zoom;
        zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        if (Mathf.Approximately(previousZoom, zoom)) return;

        Vector3 before = default;
        bool compensate = zoomToCursor && !isGrabbing && TryGetGroundPoint(out before);

        ApplyCamera();

        if (compensate && TryGetGroundPoint(out Vector3 after))
        {
            // ชดเชยให้จุดบนพื้นใต้เมาส์ยังอยู่ที่เดิมหลังซูม
            Vector3 delta = before - after;
            delta.y = 0f;

            focus = ClampFocus(focus + delta);
            targetFocus = ClampFocus(targetFocus + delta);
            ApplyCamera();
        }
    }

    private void ApplyCamera()
    {
        transform.rotation = fixedRotation;
        Vector3 back = fixedRotation * Vector3.forward;

        if (useOrthographic)
        {
            cam.orthographicSize = zoom;
            transform.position = focus - back * orthoCameraDistance;
        }
        else
        {
            transform.position = focus - back * zoom;
        }
    }

    // ---------- Helpers ----------

    private Vector3 ClampFocus(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, focusPoint.x - panLimit.x, focusPoint.x + panLimit.x);
        position.z = Mathf.Clamp(position.z, focusPoint.z - panLimit.y, focusPoint.z + panLimit.y);
        position.y = groundY;
        return position;
    }

    private bool TryGetGroundPoint(out Vector3 point)
    {
        Ray ray = cam.ScreenPointToRay(GameInput.MousePosition);
        if (groundPlane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // ---------- Public API ----------

    /// <summary>เลื่อนกล้องไปโฟกัสจุดที่กำหนด (ค่อยๆ เลื่อนไป)</summary>
    public void SetFocusPoint(Vector3 newFocusPoint)
    {
        targetFocus = ClampFocus(newFocusPoint);
    }

    /// <summary>กระโดดไปโฟกัสจุดที่กำหนดทันที</summary>
    public void SnapToFocusPoint(Vector3 newFocusPoint)
    {
        focus = targetFocus = ClampFocus(newFocusPoint);
        focusVelocity = Vector3.zero;
        ApplyCamera();
    }

    public void SetZoom(float value)
    {
        targetZoom = Mathf.Clamp(value, MinZoom, MaxZoom);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        orthoMinSize = Mathf.Max(0.5f, orthoMinSize);
        orthoMaxSize = Mathf.Max(orthoMinSize, orthoMaxSize);
        orthoStartSize = Mathf.Clamp(orthoStartSize, orthoMinSize, orthoMaxSize);

        perspMinDistance = Mathf.Max(0.5f, perspMinDistance);
        perspMaxDistance = Mathf.Max(perspMinDistance, perspMaxDistance);
        perspStartDistance = Mathf.Clamp(perspStartDistance, perspMinDistance, perspMaxDistance);

        panLimit.x = Mathf.Max(0f, panLimit.x);
        panLimit.y = Mathf.Max(0f, panLimit.y);
    }
#endif
}
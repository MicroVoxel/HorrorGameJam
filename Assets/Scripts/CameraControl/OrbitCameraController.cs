using UnityEngine;


[DisallowMultipleComponent]
public class OrbitCameraController : MonoBehaviour
{
    [Header("Target & Focus")]
    [Tooltip("จุดกึ่งกลางที่กล้องจะมองและหมุนวนรอบ (ค่าเริ่มต้น Vector3.zero คือใจกลางแมพ)")]
    [SerializeField] private Vector3 focusPoint = Vector3.zero;

    [Header("Rotation Settings (WASD / Arrow Keys)")]
    [SerializeField] private float rotateSpeed = 75f;
    [SerializeField] private float pitchSpeed = 45f;
    [SerializeField] private float minPitchAngle = 25f;
    [SerializeField] private float maxPitchAngle = 80f;

    [Header("Zoom Settings (Mouse Scroll Wheel)")]
    [SerializeField] private float zoomSpeed = 8f;
    [SerializeField] private float minDistance = 6f;
    [SerializeField] private float maxDistance = 24f;
    [SerializeField] private float currentDistance = 14f;

    [Header("Smoothing")]
    [SerializeField] private float smoothTime = 0.08f;

    private float currentYaw = 0f;
    private float currentPitch = 50f;
    private Vector3 currentVelocity;

    private void Start()
    {
        Vector3 offset = transform.position - focusPoint;
        if (offset.sqrMagnitude > 0.01f)
        {
            currentDistance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
        }

        Vector3 angles = transform.eulerAngles;
        currentYaw = angles.y;
        currentPitch = Mathf.Clamp(angles.x, minPitchAngle, maxPitchAngle);

        UpdateCameraTransformImmediate();
    }

    private void LateUpdate()
    {
        HandleKeyboardRotation();
        HandleMouseScrollZoom();
        UpdateCameraTransformSmooth();
    }

    private void HandleKeyboardRotation()
    {
        Vector2 move = GameInput.MoveAxis;
        float inputHorizontal = move.x;
        float inputVertical = move.y;

        // A/D หมุนซ้าย-ขวา (Yaw) และ W/S ก้ม-เงย (Pitch)
        currentYaw += inputHorizontal * rotateSpeed * Time.unscaledDeltaTime;
        currentPitch -= inputVertical * pitchSpeed * Time.unscaledDeltaTime;
        currentPitch = Mathf.Clamp(currentPitch, minPitchAngle, maxPitchAngle);
    }

    private void HandleMouseScrollZoom()
    {
        float scrollDelta = GameInput.ScrollY;

        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            float zoomStep = Mathf.Sign(scrollDelta) * zoomSpeed * Time.unscaledDeltaTime * 10f;
            currentDistance = Mathf.Clamp(currentDistance - zoomStep, minDistance, maxDistance);
        }
    }

    private void UpdateCameraTransformSmooth()
    {
        Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 targetPosition = focusPoint - (targetRotation * Vector3.forward * currentDistance);

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        transform.LookAt(focusPoint);
    }

    private void UpdateCameraTransformImmediate()
    {
        Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        transform.position = focusPoint - (targetRotation * Vector3.forward * currentDistance);
        transform.LookAt(focusPoint);
    }

    public void SetFocusPoint(Vector3 newFocusPoint)
    {
        focusPoint = newFocusPoint;
    }
}
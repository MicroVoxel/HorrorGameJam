using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// จุดอ่านอินพุตกลางของเกม (ตาม GDD ใช้ Unity New Input System เท่านั้น)
///
/// ต้องตั้ง Project Settings > Player > Active Input Handling เป็น
/// "Input System Package (New)" หรือ "Both"
/// </summary>
public static class GameInput
{
    // ---------- Mouse ----------
    public static Vector2 MousePosition =>
        Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    public static float ScrollY =>
        Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

    public static bool LeftPressed =>
        Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

    public static bool LeftHeld =>
        Mouse.current != null && Mouse.current.leftButton.isPressed;

    public static bool LeftReleased =>
        Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

    public static bool RightPressed =>
        Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

    // ปุ่มกลาง (กดล้อเมาส์) ใช้ลากเลื่อนกล้อง
    public static bool MiddlePressed =>
        Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;

    public static bool MiddleHeld =>
        Mouse.current != null && Mouse.current.middleButton.isPressed;

    public static bool MiddleReleased =>
        Mouse.current != null && Mouse.current.middleButton.wasReleasedThisFrame;

    public static bool AnyMouseButtonPressed => LeftPressed || RightPressed;

    // ---------- Keyboard ----------
    public static bool SpacePressed =>
        Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

    public static bool EscapePressed =>
        Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

    /// <summary>Shift ค้าง (ซ้ายหรือขวา) ใช้แสดง Tooltip แบบละเอียด</summary>
    public static bool ShiftHeld =>
        Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

    /// <summary>Q: หมุนทิศหน้าของป้อมที่กำลังวางไป -60 องศา (Facing - 1)</summary>
    public static bool RotateLeftPressed =>
        Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;

    /// <summary>E: หมุนทิศหน้าของป้อมที่กำลังวางไป +60 องศา (Facing + 1)</summary>
    public static bool RotateRightPressed =>
        Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;

    /// <summary>ปุ่ม C สำหรับจั่วการ์ดทดสอบ (ใช้เฉพาะใน Editor)</summary>
    public static bool DebugDrawCardPressed =>
        Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;

    /// <summary>
    /// แกนเลื่อนกล้อง: x = A/← (-1) ถึง D/→ (+1), y = S/↓ (-1) ถึง W/↑ (+1)
    /// </summary>
    public static Vector2 MoveAxis
    {
        get
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return Vector2.zero;

            float x = 0f;
            float y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            return new Vector2(x, y);
        }
    }
}
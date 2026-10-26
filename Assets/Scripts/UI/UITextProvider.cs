using UnityEngine;

/// <summary>
/// จุดเข้าถึง UITextData ตัวเดียวของทั้งเกม ให้คลาส UI ทุกตัวเรียก UITextProvider.Text แทนการฝังสตริง
/// วางบน GameObject ในซีนเมนูและซีนเกมได้ (ตัวซ้ำจะถูกทำลายเอง) แล้วลาก UITextData ใส่ช่อง Text Data
/// ถ้าไม่ได้ลาก จะใช้ค่าตั้งต้นภาษาอังกฤษจาก UITextData เอง (ไม่ใช่ค่าที่ฝังในคลาส UI)
/// </summary>
[DisallowMultipleComponent]
public class UITextProvider : SingletonBehaviour<UITextProvider>
{
    protected override bool Persistent => true;

    [SerializeField] private UITextData textData;

    private static UITextData fallback;

    public static UITextData Text
    {
        get
        {
            if (Instance != null && Instance.textData != null) return Instance.textData;

            if (fallback == null)
            {
                fallback = ScriptableObject.CreateInstance<UITextData>();
                fallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return fallback;
        }
    }
}
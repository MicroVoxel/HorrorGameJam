using UnityEngine;

/// <summary>
/// คลาสฐานของ Singleton ทุกตัวในโปรเจกต์ (แทนโค้ด Awake เช็ก Instance ที่เคยซ้ำกัน ~15 คลาส)
///
/// วิธีใช้:
///   public class FooManager : SingletonBehaviour&lt;FooManager&gt;
///   {
///       protected override void OnSingletonAwake() { /* โค้ดที่เคยอยู่ใน Awake */ }
///       protected override void OnDestroy() { /* ถอด event ของตัวเอง */ base.OnDestroy(); }
///   }
///
/// กฎสำคัญ: คลาสลูกห้ามประกาศ Awake() เอง ให้ใช้ OnSingletonAwake() แทน
/// เพราะ OnSingletonAwake จะถูกเรียกเฉพาะตัวที่เป็น Instance จริงเท่านั้น
/// (ตัวซ้ำที่ถูกสั่ง Destroy จะไม่รันโค้ดเริ่มต้นของตัวเอง)
/// </summary>
public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
{
    public static T Instance { get; private set; }

    /// <summary>true = ไม่ถูกทำลายตอนเปลี่ยนซีน (เช่น AudioManager, SettingsManager)</summary>
    protected virtual bool Persistent => false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = (T)this;
        if (Persistent) DontDestroyOnLoad(gameObject);

        OnSingletonAwake();
    }

    /// <summary>แทน Awake ของคลาสลูก — เรียกเฉพาะตอนที่ตัวนี้เป็น Instance จริง</summary>
    protected virtual void OnSingletonAwake() { }

    protected virtual void OnDestroy()
    {
        // เคลียร์ Instance เพื่อไม่ให้ค้างข้ามการรีโหลดซีน / ปิด Domain Reload
        if (Instance == this) Instance = null;
    }
}

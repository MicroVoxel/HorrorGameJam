using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// ควบคุมสภาวะจบเกม (Victory / Defeat Flow) ตาม GDD:
/// - ชัยชนะ (Victory): ขบวนผีทำลายศาลเจ้าศักดิ์สิทธิ์ (TowerDefenseBase) ทั้งหมดสำเร็จ
/// - พ่ายแพ้ (Defeat #1 - Time's Up): ก้านธูปดอกที่ 7 มอดดับลง
/// - พ่ายแพ้ (Defeat #2 - Extinction): ขบวนผีสูญพันธุ์ ไร้หนทางต่อสู้
///
/// การ "หยุดเกม" ทำผ่าน GameState.Ended เท่านั้น (ธูปหยุดเผา / Spacebar ใช้ไม่ได้)
/// ไม่ต้องไปสั่งปิดระบบอื่นทีละตัวอีกแล้ว
/// </summary>
[DisallowMultipleComponent]
public class GameOverController : SingletonBehaviour<GameOverController>
{
    [Header("Game Over UI Panels")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;

    [Header("Defeat Reason Texts (Optional)")]
    [SerializeField] private TextMeshProUGUI defeatTitleText;
    [SerializeField] private TextMeshProUGUI defeatReasonText;

    [Header("Victory Summary Texts (Optional)")]
    [SerializeField] private TextMeshProUGUI victoryTitleText;
    [SerializeField] private TextMeshProUGUI victoryDetailText;

    [Header("Action Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public bool IsGameOver => GameStateManager.IsEnded;

    protected override void OnSingletonAwake()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    private void Start()
    {
        // ชนะ: ObjectiveTracker แจ้งว่าศาลเจ้าถูกทำลายหมด
        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.OnAllTargetsDestroyedVictory += TriggerVictory;
        }

        // แพ้ (หมดเวลา): ธูปมอดครบ 7 ดอก
        if (IncenseGaugeController.Instance != null)
        {
            IncenseGaugeController.Instance.OnAllIncenseExhausted += TriggerDefeatByTimeOut;
        }

        if (retryButton != null) retryButton.onClick.AddListener(RestartCurrentScene);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(LoadMainMenuScene);
    }

    protected override void OnDestroy()
    {
        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.OnAllTargetsDestroyedVictory -= TriggerVictory;
        }

        if (IncenseGaugeController.Instance != null)
        {
            IncenseGaugeController.Instance.OnAllIncenseExhausted -= TriggerDefeatByTimeOut;
        }

        base.OnDestroy();
    }

    /// <summary>
    /// ชัยชนะ: ขบวนร้อยอสูรทำลายศาลเจ้าศักดิ์สิทธิ์ทั้งหมดบนกระดานสำเร็จ!
    /// </summary>
    public void TriggerVictory()
    {
        if (!GameStateManager.TryEndGame()) return;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        int completedLoops = IncenseGaugeController.Instance != null ? IncenseGaugeController.Instance.CurrentLoop : 1;
        if (victoryTitleText != null)
        {
            victoryTitleText.text = "ขบวนร้อยอสูรคว้าชัย!";
        }

        if (victoryDetailText != null)
        {
            victoryDetailText.text = $"ศาลเจ้าศักดิ์สิทธิ์ถูกทำลายสิ้นซากในคืนที่ {completedLoops}\nความมืดมิดครอบงำผืนป่าชั่วนิรันดร์";
        }

        AudioManager.Bell();
        Debug.Log("<color=green>[GameOverController]</color> VICTORY! ผู้เล่นพาขบวนผีทำลายศาลเจ้าสำเร็จ");
    }

    /// <summary>
    /// พ่ายแพ้: ก้านธูปมอดดับหมด 7 ดอก (แสงตะวันสาดส่องชะล้างผี)
    /// </summary>
    public void TriggerDefeatByTimeOut()
    {
        TriggerDefeat("แสงตะวันชะล้าง!", "ก้านธูปทั้ง 7 ดอกมอดดับลง แสงอาทิตย์ยามเช้าขับไล่ขบวนผีสลายไปจนหมดสิ้น");
    }

    /// <summary>
    /// พ่ายแพ้: ผีบนกระดานตายหมด และไม่มีทรัพยากรสร้างกองทัพต่อ (Extinction)
    /// </summary>
    public void TriggerDefeatByExtinction()
    {
        TriggerDefeat("ขบวนผีสูญพันธุ์!", "ภูตผีทั้งหมดในขบวนถูกป้อมอาคมทำลายจนหมดสิ้น ไร้ซึ่งพลังความมืดที่จะต่อกรกับศาลเจ้า");
    }

    public void TriggerDefeat(string title, string reason)
    {
        if (!GameStateManager.TryEndGame()) return;

        if (defeatPanel != null)
        {
            defeatPanel.SetActive(true);
        }

        if (defeatTitleText != null)
        {
            defeatTitleText.text = title;
        }

        if (defeatReasonText != null)
        {
            defeatReasonText.text = reason;
        }

        Debug.Log($"<color=red>[GameOverController]</color> DEFEAT: {title} — {reason}");
    }

    public void RestartCurrentScene()
    {
        Time.timeScale = 1.0f;
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void LoadMainMenuScene()
    {
        Time.timeScale = 1.0f;
        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}

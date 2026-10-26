using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// ควบคุมสภาวะจบเกม (Victory / Defeat Flow) ตาม GDD v2:
/// - ชัยชนะ (Victory): ศาลเจ้าเป้าหมายทั้งหมด HP = 0 (เช็กหลังทุก Cleanup)
/// - พ่ายแพ้ (Defeat #1 - Time's Up): จบ Round สุดท้ายแล้วยังมีศาลเจ้าเหลือ
/// - พ่ายแพ้ (Defeat #2 - Extinction): ไม่มีผีบนทาง ไม่มีรังผีบนกระดาน และไม่มีการ์ดรังผีในมือ (เช็กหลังทุก Cleanup)
/// ข้อความทั้งหมดดึงจาก UITextProvider.Text (UITextData)
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

    private bool subscribedToTurns;

    public bool IsGameOver => GameStateManager.IsEnded;

    protected override void OnSingletonAwake()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    private void Start()
    {
        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.OnAllTargetsDestroyedVictory += TriggerVictory;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnAllRoundsCompleted += TriggerDefeatByTimeOut;
        }

        if (IncenseGaugeController.Instance != null)
        {
            IncenseGaugeController.Instance.OnAllIncenseExhausted += TriggerDefeatByTimeOut;
        }

        TrySubscribeToTurnCleanup();
        ApplyButtonLabels();

        if (retryButton != null) retryButton.onClick.AddListener(RestartCurrentScene);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(LoadMainMenuScene);
    }

    protected override void OnDestroy()
    {
        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.OnAllTargetsDestroyedVictory -= TriggerVictory;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnAllRoundsCompleted -= TriggerDefeatByTimeOut;
        }

        if (IncenseGaugeController.Instance != null)
        {
            IncenseGaugeController.Instance.OnAllIncenseExhausted -= TriggerDefeatByTimeOut;
        }

        if (subscribedToTurns && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnCleanup -= HandleTurnCleanup;
        }

        base.OnDestroy();
    }

    private void TrySubscribeToTurnCleanup()
    {
        if (subscribedToTurns || TurnManager.Instance == null) return;

        TurnManager.Instance.OnTurnCleanup += HandleTurnCleanup;
        subscribedToTurns = true;
    }

    private void HandleTurnCleanup(TurnContext ctx)
    {
        if (IsGameOver) return;

        if (ObjectiveTracker.Instance != null
            && ObjectiveTracker.Instance.IsSetupComplete
            && ObjectiveTracker.Instance.TargetCount == 0)
        {
            TriggerVictory();
        }
    }

    private void ApplyButtonLabels()
    {
        UITextData t = UITextProvider.Text;

        if (retryButton != null)
        {
            TextMeshProUGUI label = retryButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = t.btnRetry;
        }

        if (mainMenuButton != null)
        {
            TextMeshProUGUI label = mainMenuButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = t.btnMainMenu;
        }
    }

    /// <summary>
    /// ชัยชนะ: ขบวนร้อยอสูรทำลายศาลเจ้าศักดิ์สิทธิ์ทั้งหมดบนกระดานสำเร็จ
    /// </summary>
    public void TriggerVictory()
    {
        if (!GameStateManager.TryEndGame()) return;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        UITextData t = UITextProvider.Text;
        int round = GameStateManager.CurrentRound > 0
            ? GameStateManager.CurrentRound
            : (IncenseGaugeController.Instance != null ? IncenseGaugeController.Instance.CurrentLoop : 1);

        if (victoryTitleText != null)
        {
            victoryTitleText.text = t.victoryTitle;
        }

        if (victoryDetailText != null)
        {
            victoryDetailText.text = UITextData.Format(t.victoryDetail, "round", round);
        }

        AudioManager.Bell();
        Debug.Log("<color=green>[GameOverController]</color> VICTORY! All shrines destroyed.");
    }

    /// <summary>
    /// พ่ายแพ้: จบ Round สุดท้ายแล้วยังมีศาลเจ้าเหลือ
    /// </summary>
    public void TriggerDefeatByTimeOut()
    {
        UITextData t = UITextProvider.Text;
        TriggerDefeat(t.defeatTimeTitle, t.defeatTimeReason);
    }

    /// <summary>
    /// พ่ายแพ้: ผีบนกระดานตายหมด ไร้รังผี และไม่มีการ์ดรังผีในมือ (Extinction)
    /// </summary>
    public void TriggerDefeatByExtinction()
    {
        UITextData t = UITextProvider.Text;
        TriggerDefeat(t.defeatExtinctionTitle, t.defeatExtinctionReason);
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
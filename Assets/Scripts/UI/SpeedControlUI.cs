using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ปุ่มควบคุมความเร็วระหว่างเฟส Resolution (1x / 2x / 4x / Skip) และการจำค่าความเร็วข้าม Round ใน SettingsManager (GDD 6.4)
/// </summary>
[DisallowMultipleComponent]
public class SpeedControlUI : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Button cycleSpeedButton;
    [SerializeField] private TextMeshProUGUI speedLabelText;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI skipLabelText;

    private bool subscribed;

    private void Start()
    {
        if (cycleSpeedButton != null) cycleSpeedButton.onClick.AddListener(HandleCycleSpeedClicked);
        if (skipButton != null) skipButton.onClick.AddListener(HandleSkipClicked);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
        }

        TrySubscribeTurnManager();
        ApplySavedSpeed();
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (cycleSpeedButton != null) cycleSpeedButton.onClick.RemoveListener(HandleCycleSpeedClicked);
        if (skipButton != null) skipButton.onClick.RemoveListener(HandleSkipClicked);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        if (subscribed && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnSpeedChanged -= HandleSpeedChanged;
            TurnManager.Instance.OnFastForwardStarted -= HandleFastForwardStarted;
        }
    }

    private void TrySubscribeTurnManager()
    {
        if (subscribed || TurnManager.Instance == null) return;

        TurnManager.Instance.OnSpeedChanged += HandleSpeedChanged;
        TurnManager.Instance.OnFastForwardStarted += HandleFastForwardStarted;
        subscribed = true;
    }

    private void ApplySavedSpeed()
    {
        if (TurnManager.Instance != null && SettingsManager.Instance != null)
        {
            TurnManager.Instance.SetSpeedIndex(SettingsManager.Instance.TurnSpeedIndex);
        }
    }

    private void HandleStateChanged(GameState state)
    {
        TrySubscribeTurnManager();
        RefreshUI();
    }

    private void HandleSpeedChanged(int newSpeedIdx)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetTurnSpeedIndex(newSpeedIdx);
        }

        RefreshUI();
    }

    private void HandleFastForwardStarted()
    {
        RefreshUI();
    }

    private void HandleCycleSpeedClicked()
    {
        AudioManager.Click();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.CycleSpeed();
        }
    }

    private void HandleSkipClicked()
    {
        AudioManager.Click();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RequestFastForward();
        }
    }

    public void RefreshUI()
    {
        bool visible = GameStateManager.IsResolution;
        if (rootPanel != null && rootPanel.activeSelf != visible)
        {
            rootPanel.SetActive(visible);
        }

        UITextData t = UITextProvider.Text;
        float mult = TurnManager.Instance != null ? TurnManager.Instance.SpeedMultiplier : 1f;

        if (speedLabelText != null)
        {
            speedLabelText.text = UITextData.Format(t.speedLabel, "speed", mult.ToString("0.#"));
        }

        if (skipLabelText != null)
        {
            skipLabelText.text = t.btnSkip;
        }

        if (skipButton != null)
        {
            skipButton.interactable = TurnManager.Instance != null && !TurnManager.Instance.IsFastForward;
        }
    }
}
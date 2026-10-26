using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ควบคุมพฤติกรรมแผงขวา, แถบการ์ดด้านล่าง และปุ่มหลักตามเฟส (GDD 6.2 & 6.3):
/// - Dawn: แสดงแผงขวาครบ 3 ช่อง + แถบการ์ด + ปุ่ม Start Night (ปิดจนกว่าจะวางศาลครบ)
/// - Planning: แสดงแผงขวาครบ 3 ช่อง + แถบการ์ด + ปุ่ม Begin Ritual
/// - Resolution: เลื่อนช่อง Altar (1) และ Inventory (2) ออกขวา เหลือเฉพาะ Tooltip, เลื่อนแถบการ์ดลง, โชว์ Speed Control
/// - RoundEnd: เลื่อนแผงกลับมา และแสดงปุ่ม Continue
/// </summary>
[DisallowMultipleComponent]
public class SidePanelController : SingletonBehaviour<SidePanelController>
{
    [Header("Sliding Panels (RectTransforms)")]
    [SerializeField] private RectTransform altarAndInventoryPanel;
    [SerializeField] private RectTransform cardHandBar;
    [SerializeField] private float slideSpeed = 8f;
    [SerializeField] private Vector2 rightPanelHiddenOffset = new Vector2(420f, 0f);
    [SerializeField] private Vector2 cardBarHiddenOffset = new Vector2(0f, -240f);

    [Header("Primary Phase Action Button")]
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private TextMeshProUGUI primaryButtonLabel;
    [SerializeField] private TextMeshProUGUI statusHintText;

    private Vector2 rightPanelShownPos;
    private Vector2 cardBarShownPos;
    private Vector2 rightPanelTargetPos;
    private Vector2 cardBarTargetPos;

    protected override void OnSingletonAwake()
    {
        if (altarAndInventoryPanel != null)
        {
            rightPanelShownPos = rightPanelTargetPos = altarAndInventoryPanel.anchoredPosition;
        }

        if (cardHandBar != null)
        {
            cardBarShownPos = cardBarTargetPos = cardHandBar.anchoredPosition;
        }
    }

    private void Start()
    {
        if (primaryActionButton != null)
        {
            primaryActionButton.onClick.AddListener(HandlePrimaryButtonClicked);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
            GameStateManager.Instance.OnStartNightAvailabilityChanged += HandleStartNightAvailabilityChanged;
        }

        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.OnSetupProgressChanged += HandleSetupProgressChanged;
        }

        if (TilePlacementManager.Instance != null)
        {
            TilePlacementManager.Instance.OnPlacementDenied += ShowTemporaryHint;
        }

        if (FormationSwapManager.Instance != null)
        {
            FormationSwapManager.Instance.OnSwapDenied += HandleSwapDenied;
        }

        RefreshForState(GameStateManager.Current, immediate: true);
    }

    protected override void OnDestroy()
    {
        if (primaryActionButton != null)
        {
            primaryActionButton.onClick.RemoveListener(HandlePrimaryButtonClicked);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
            GameStateManager.Instance.OnStartNightAvailabilityChanged -= HandleStartNightAvailabilityChanged;
        }

        if (ObjectiveTracker.Instance != null)
        {
            ObjectiveTracker.Instance.OnSetupProgressChanged -= HandleSetupProgressChanged;
        }

        if (TilePlacementManager.Instance != null)
        {
            TilePlacementManager.Instance.OnPlacementDenied -= ShowTemporaryHint;
        }

        if (FormationSwapManager.Instance != null)
        {
            FormationSwapManager.Instance.OnSwapDenied -= HandleSwapDenied;
        }

        base.OnDestroy();
    }

    private void Update()
    {
        float t = Time.unscaledDeltaTime * slideSpeed;

        if (altarAndInventoryPanel != null)
        {
            altarAndInventoryPanel.anchoredPosition = Vector2.Lerp(altarAndInventoryPanel.anchoredPosition, rightPanelTargetPos, t);
        }

        if (cardHandBar != null)
        {
            cardHandBar.anchoredPosition = Vector2.Lerp(cardHandBar.anchoredPosition, cardBarTargetPos, t);
        }
    }

    private void HandleStateChanged(GameState state)
    {
        RefreshForState(state, immediate: false);
    }

    private void HandleStartNightAvailabilityChanged(bool _)
    {
        RefreshButtonAndHint(GameStateManager.Current);
    }

    private void HandleSetupProgressChanged(int placed, int required)
    {
        RefreshButtonAndHint(GameStateManager.Current);
    }

    private void HandlePrimaryButtonClicked()
    {
        if (GameStateManager.Instance == null) return;

        AudioManager.Click();

        switch (GameStateManager.Current)
        {
            case GameState.Dawn:
                GameStateManager.Instance.TryStartNight();
                break;

            case GameState.Planning:
                GameStateManager.Instance.TryBeginRitual();
                break;

            case GameState.RoundEnd:
                GameStateManager.Instance.FinishRoundEnd();
                break;
        }
    }

    private void RefreshForState(GameState state, bool immediate)
    {
        bool hidePlanningPanels = state == GameState.Resolution;

        rightPanelTargetPos = hidePlanningPanels ? rightPanelShownPos + rightPanelHiddenOffset : rightPanelShownPos;
        cardBarTargetPos = hidePlanningPanels ? cardBarShownPos + cardBarHiddenOffset : cardBarShownPos;

        if (immediate)
        {
            if (altarAndInventoryPanel != null) altarAndInventoryPanel.anchoredPosition = rightPanelTargetPos;
            if (cardHandBar != null) cardHandBar.anchoredPosition = cardBarTargetPos;
        }

        RefreshButtonAndHint(state);
    }

    private void RefreshButtonAndHint(GameState state)
    {
        UITextData t = UITextProvider.Text;

        if (primaryActionButton != null)
        {
            bool showButton = state == GameState.Dawn || state == GameState.Planning || state == GameState.RoundEnd;
            primaryActionButton.gameObject.SetActive(showButton);

            if (state == GameState.Dawn)
            {
                primaryActionButton.interactable = GameStateManager.CanStartNight;
                if (primaryButtonLabel != null) primaryButtonLabel.text = t.btnStartNight;
            }
            else if (state == GameState.Planning)
            {
                primaryActionButton.interactable = true;
                if (primaryButtonLabel != null) primaryButtonLabel.text = t.btnBeginRitual;
            }
            else if (state == GameState.RoundEnd)
            {
                primaryActionButton.interactable = true;
                if (primaryButtonLabel != null) primaryButtonLabel.text = t.btnContinue;
            }
        }

        if (statusHintText != null)
        {
            if (state == GameState.Dawn && !GameStateManager.CanStartNight && ObjectiveTracker.Instance != null)
            {
                statusHintText.text = UITextData.Format(
                    t.startNightDisabledHint,
                    "placed", ObjectiveTracker.Instance.TotalShrinesPlaced,
                    "required", ObjectiveTracker.Instance.RequiredShrinesToPlace);
            }
            else if (state == GameState.Planning)
            {
                int turns = GameStateManager.TurnsThisRound;
                statusHintText.text = UITextData.Format(t.nextRitualInfo, "turns", turns);
            }
            else
            {
                statusHintText.text = string.Empty;
            }
        }
    }

    private void HandleSwapDenied(SwapDenyReason reason)
    {
        UITextData t = UITextProvider.Text;
        int maxDist = GameStateManager.Instance != null ? GameStateManager.Instance.Rules.MaxSwapPathDistance : 2;

        switch (reason)
        {
            case SwapDenyReason.NotPlanning:
                ShowTemporaryHint(t.swapDeniedNotPlanning);
                break;
            case SwapDenyReason.NoQuota:
                ShowTemporaryHint(t.swapDeniedNoQuota);
                break;
            case SwapDenyReason.TooFar:
                ShowTemporaryHint(UITextData.Format(t.swapDeniedTooFar, "max", maxDist));
                break;
        }
    }

    public void ShowTemporaryHint(string message)
    {
        if (statusHintText == null || string.IsNullOrEmpty(message)) return;
        statusHintText.text = message;
    }
}
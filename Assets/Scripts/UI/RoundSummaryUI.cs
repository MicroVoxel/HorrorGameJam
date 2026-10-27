using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// หน้าต่างสรุปผลเมื่อเข้าสู่เฟส RoundEnd (GDD 6.4):
/// หากผูก continueButton ไว้ในหน้าต่างนี้ SidePanelController จะซ่อนปุ่มหลักในเฟส RoundEnd อัตโนมัติ (มีปุ่ม Continue ปุ่มเดียวเสมอ)
/// </summary>
[DisallowMultipleComponent]
public class RoundSummaryUI : SingletonBehaviour<RoundSummaryUI>
{
    [Header("Panel & Controls")]
    [SerializeField] private GameObject summaryPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI shrinesLineText;
    [SerializeField] private TextMeshProUGUI spiritsLineText;
    [SerializeField] private TextMeshProUGUI lootLineText;
    [SerializeField] private TextMeshProUGUI altarLineText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TextMeshProUGUI continueButtonLabel;

    private float shrineHpAtRoundStart;
    private int spiritsDiedThisRound;
    private int spiritsMutatedThisRound;
    private int offeringsRottedThisRound;

    private readonly HashSet<EntityBase> trackedEntities = new HashSet<EntityBase>();
    private readonly HashSet<EntityMutatable> trackedMutatables = new HashSet<EntityMutatable>();

    /// <summary>หน้าต่างสรุปนี้มีปุ่ม Continue ของตัวเองหรือไม่ (ให้ SidePanelController เช็กเพื่อไม่แสดงปุ่มซ้ำ)</summary>
    public bool HasContinueButton => summaryPanel != null && continueButton != null;

    protected override void OnSingletonAwake()
    {
        if (summaryPanel != null) summaryPanel.SetActive(false);
    }

    private void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(HandleContinueClicked);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
            GameStateManager.Instance.OnRoundEndProcessed += HandleRoundEndProcessed;
        }

        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.OnOfferingRotten += HandleOfferingRotten;
        }

        EntityRegistry.OnEntityRegistered += HandleEntityRegistered;
    }

    protected override void OnDestroy()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(HandleContinueClicked);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
            GameStateManager.Instance.OnRoundEndProcessed -= HandleRoundEndProcessed;
        }

        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.OnOfferingRotten -= HandleOfferingRotten;
        }

        EntityRegistry.OnEntityRegistered -= HandleEntityRegistered;

        UnsubscribeAllEntities();
        base.OnDestroy();
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Resolution)
        {
            BeginRoundTracking();
            if (summaryPanel != null) summaryPanel.SetActive(false);
        }
        else if (state != GameState.RoundEnd && summaryPanel != null)
        {
            summaryPanel.SetActive(false);
        }
    }

    private void HandleRoundEndProcessed()
    {
        PopulateAndShowSummary();
    }

    private void BeginRoundTracking()
    {
        shrineHpAtRoundStart = SumActiveShrineHealth();
        spiritsDiedThisRound = 0;
        spiritsMutatedThisRound = 0;
        offeringsRottedThisRound = 0;

        IReadOnlyList<EntityBase> entities = EntityRegistry.ActiveEntities;
        for (int i = 0; i < entities.Count; i++)
        {
            TrackEntity(entities[i]);
        }
    }

    private void HandleEntityRegistered(EntityBase entity)
    {
        if (GameStateManager.IsResolution) TrackEntity(entity);
    }

    private void TrackEntity(EntityBase e)
    {
        if (e == null || !trackedEntities.Add(e)) return;

        e.OnEntityDied += HandleEntityDied;

        if (e.TryGetComponent(out EntityMutatable mut) && trackedMutatables.Add(mut))
        {
            mut.OnEntityMutated += HandleEntityMutated;
        }
    }

    private void UnsubscribeAllEntities()
    {
        foreach (EntityBase e in trackedEntities)
        {
            if (e != null) e.OnEntityDied -= HandleEntityDied;
        }
        trackedEntities.Clear();

        foreach (EntityMutatable m in trackedMutatables)
        {
            if (m != null) m.OnEntityMutated -= HandleEntityMutated;
        }
        trackedMutatables.Clear();
    }

    private void HandleEntityDied(EntityBase dead)
    {
        spiritsDiedThisRound++;
    }

    private void HandleEntityMutated(EntityMutatable mut, int stage)
    {
        spiritsMutatedThisRound++;
    }

    private void HandleOfferingRotten(OfferingItemData item)
    {
        offeringsRottedThisRound++;
    }

    private void PopulateAndShowSummary()
    {
        if (!GameStateManager.IsRoundEnd) return;

        UnsubscribeAllEntities();

        UITextData t = UITextProvider.Text;
        int round = Mathf.Max(1, GameStateManager.CurrentRound);
        int totalRounds = Mathf.Max(1, GameStateManager.TotalRounds);

        float currentShrineHp = SumActiveShrineHealth();
        float shrineDamageDealt = Mathf.Max(0f, shrineHpAtRoundStart - currentShrineHp);

        int remainingShrines = ObjectiveTracker.Instance != null ? ObjectiveTracker.Instance.TargetCount : 0;
        int requiredShrines = ObjectiveTracker.Instance != null ? ObjectiveTracker.Instance.RequiredShrinesToPlace : 0;

        int lootItems = LootStash.Instance != null ? LootStash.Instance.LastRoundOfferings.Count : 0;
        int lootCards = LootStash.Instance != null ? LootStash.Instance.LastRoundCards.Count : 0;
        int sticksRemaining = Mathf.Max(0, totalRounds - round);

        if (titleText != null)
        {
            titleText.text = UITextData.Format(t.summaryTitle, "round", round);
        }

        if (shrinesLineText != null)
        {
            shrinesLineText.text = UITextData.Format(
                t.summaryShrinesLine,
                "damage", Mathf.RoundToInt(shrineDamageDealt),
                "remaining", remainingShrines,
                "total", requiredShrines);
        }

        if (spiritsLineText != null)
        {
            spiritsLineText.text = UITextData.Format(
                t.summarySpiritsLine,
                "died", spiritsDiedThisRound,
                "mutated", spiritsMutatedThisRound);
        }

        if (lootLineText != null)
        {
            lootLineText.text = UITextData.Format(
                t.summaryLootLine,
                "items", lootItems,
                "cards", lootCards);
        }

        if (altarLineText != null)
        {
            altarLineText.text = UITextData.Format(
                t.summaryAltarLine,
                "rotten", offeringsRottedThisRound,
                "sticks", sticksRemaining);
        }

        if (continueButtonLabel != null)
        {
            continueButtonLabel.text = t.btnContinue;
        }

        if (summaryPanel != null)
        {
            summaryPanel.SetActive(true);
        }
    }

    public void HideSummary()
    {
        if (summaryPanel != null) summaryPanel.SetActive(false);
    }

    private void HandleContinueClicked()
    {
        AudioManager.Click();
        HideSummary();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.FinishRoundEnd();
        }
    }

    private static float SumActiveShrineHealth()
    {
        if (ObjectiveTracker.Instance == null) return 0f;

        float sum = 0f;
        IReadOnlyList<TowerObjective> targets = ObjectiveTracker.Instance.ActiveTargets;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null && targets[i].IsAlive)
            {
                sum += targets[i].Tower.CurrentHealth;
            }
        }
        return sum;
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวขับเทิร์นของเฟส Resolution (Coroutine ตัวเดียว หน่วยต่างๆ ไม่ต้องมี Update เอง)
///
/// ลำดับใน 1 เทิร์น:
///   1. Start of Turn  2. Spawner Step  3. Quick Step  4. Entity Step (หัวขบวนก่อน)
///   5. Tower Step     6. รอแอนิเมชันที่ปล่อยพร้อมกันให้จบ  7. Last Breath แล้วจึง Cleanup  8. End of Turn
/// </summary>
[DisallowMultipleComponent]
public class TurnManager : SingletonBehaviour<TurnManager>
{
    private const float AnimationWaitTimeout = 5f;

    private readonly List<ITurnActor> buffer = new List<ITurnActor>(64);
    private readonly List<ITurnActor> quickActors = new List<ITurnActor>(16);

    private Coroutine resolutionRoutine;
    private bool isRunning;
    private TurnContext context;
    private int speedIndex = -1;

    // ---------- State ----------
    public bool IsRunning => isRunning;
    public TurnContext Context => context;
    public int CurrentTurn => context != null ? context.Turn : 0;
    public int TotalTurns => context != null ? context.TotalTurns : 0;
    public bool IsFastForward => isRunning && context != null && context.IsFastForward;

    public int SpeedIndex => ResolveSpeedIndex();
    public float SpeedMultiplier => GameStateManager.Instance != null
        ? GameStateManager.Instance.Rules.GetSpeedMultiplier(ResolveSpeedIndex())
        : 1f;

    // ---------- Events ----------
    public event Action<int, int> OnResolutionStarted;
    public event Action<int, int> OnTurnStarted;
    public event Action<int, int> OnTurnEnded;
    public event Action<TurnContext> OnTurnCleanup;
    public event Action<bool> OnResolutionFinished;
    public event Action<int> OnSpeedChanged;
    public event Action OnFastForwardStarted;
    public event Action<string> OnLogMessage;

    private void Start()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += HandleGameStateChanged;
        }
    }

    protected override void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= HandleGameStateChanged;
        }

        base.OnDestroy();
    }

    // ---------- Public controls ----------

    public void SetSpeedIndex(int index)
    {
        if (GameStateManager.Instance == null) return;

        GameRulesData rules = GameStateManager.Instance.Rules;
        int clamped = Mathf.Clamp(index, 0, Mathf.Max(0, rules.SpeedStepCount - 1));
        if (clamped == speedIndex) return;

        speedIndex = clamped;
        if (context != null) context.SpeedMultiplier = rules.GetSpeedMultiplier(speedIndex);
        OnSpeedChanged?.Invoke(speedIndex);
    }

    public void CycleSpeed()
    {
        if (GameStateManager.Instance == null) return;

        int count = GameStateManager.Instance.Rules.SpeedStepCount;
        if (count <= 0) return;

        SetSpeedIndex((ResolveSpeedIndex() + 1) % count);
    }

    [ContextMenu("Fast Forward (Test)")]
    public void RequestFastForward()
    {
        if (!IsRunning || context == null || context.IsFastForward) return;

        context.IsFastForward = true;
        OnFastForwardStarted?.Invoke();
    }

    // ---------- Flow ----------

    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.Resolution)
        {
            if (!isRunning)
            {
                isRunning = true;
                resolutionRoutine = StartCoroutine(RunResolution());
            }
        }
        else if (isRunning)
        {
            if (resolutionRoutine != null) StopCoroutine(resolutionRoutine);
            resolutionRoutine = null;
            isRunning = false;
        }
    }

    private IEnumerator RunResolution()
    {
        GameRulesData rules = GameStateManager.Instance.Rules;
        int round = GameStateManager.CurrentRound;
        int totalTurns = Mathf.Max(1, GameStateManager.TurnsThisRound);

        context = new TurnContext(round, totalTurns, PostLog) { SpeedMultiplier = SpeedMultiplier };

        yield return null;
        if (!GameStateManager.IsResolution)
        {
            isRunning = false;
            resolutionRoutine = null;
            yield break;
        }

        OnResolutionStarted?.Invoke(round, totalTurns);

        bool endedEarly = false;

        for (int turn = 1; turn <= totalTurns; turn++)
        {
            if (!GameStateManager.IsResolution) break;

            context.BeginTurn(turn, SpeedMultiplier);
            yield return RunSingleTurn(rules);

            if (!GameStateManager.IsResolution) break;

            if (rules.EndResolutionEarlyOnWipe && turn < totalTurns && !TurnActorRegistry.HasActiveParadeOrSpawner())
            {
                endedEarly = true;
                break;
            }
        }

        resolutionRoutine = null;
        isRunning = false;

        if (GameStateManager.IsResolution)
        {
            OnResolutionFinished?.Invoke(endedEarly);
            GameStateManager.Instance.CompleteResolution();
        }
    }

    private IEnumerator RunSingleTurn(GameRulesData rules)
    {
        OnTurnStarted?.Invoke(context.Turn, context.TotalTurns);

        // 1. Start of Turn
        TurnActorRegistry.CollectAll(buffer);
        for (int i = 0; i < buffer.Count; i++)
        {
            if (TurnActorRegistry.IsUsable(buffer[i])) buffer[i].OnTurnStart(context);
        }

        // 2-5. Spawner, Quick, Entity, Tower
        yield return RunGroup(TurnActorGroup.Spawner);
        if (!GameStateManager.IsResolution) yield break;

        yield return RunQuickStep();
        if (!GameStateManager.IsResolution) yield break;

        yield return RunGroup(TurnActorGroup.Entity);
        if (!GameStateManager.IsResolution) yield break;

        yield return RunGroup(TurnActorGroup.Tower);
        if (!GameStateManager.IsResolution) yield break;

        // 6. รอแอนิเมชันที่ปล่อยพร้อมกันให้จบ
        yield return WaitForAnimations();

        // 7. Last Breath ก่อน แล้วจึง Cleanup
        AbilityResolver.ResolvePending(context);
        if (!GameStateManager.IsResolution) yield break;

        OnTurnCleanup?.Invoke(context);
        if (!GameStateManager.IsResolution) yield break;

        // 8. End of Turn
        TurnActorRegistry.CollectAll(buffer);
        for (int i = 0; i < buffer.Count; i++)
        {
            if (TurnActorRegistry.IsUsable(buffer[i])) buffer[i].OnTurnEnd(context);
        }

        OnTurnEnded?.Invoke(context.Turn, context.TotalTurns);

        if (!rules.AutoSkipEmptyTurns || context.ActionPerformedThisTurn)
        {
            yield return context.Pause(rules.TurnStepDelay);
        }
    }

    private IEnumerator RunGroup(TurnActorGroup group)
    {
        TurnActorRegistry.CollectSorted(group, buffer);

        for (int i = 0; i < buffer.Count; i++)
        {
            ITurnActor actor = buffer[i];
            if (!TurnActorRegistry.IsUsable(actor)) continue;

            yield return actor.ExecuteTurn(context);
            if (!GameStateManager.IsResolution) yield break;

            yield return PauseAfterAction();
        }
    }

    private IEnumerator RunQuickStep()
    {
        quickActors.Clear();

        TurnActorRegistry.CollectSorted(TurnActorGroup.Entity, buffer);
        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer[i] is IQuickActor) quickActors.Add(buffer[i]);
        }

        TurnActorRegistry.CollectSorted(TurnActorGroup.Tower, buffer);
        for (int i = 0; i < buffer.Count; i++)
        {
            if (buffer[i] is IQuickActor) quickActors.Add(buffer[i]);
        }

        for (int i = 0; i < quickActors.Count; i++)
        {
            if (!TurnActorRegistry.IsUsable(quickActors[i])) continue;

            yield return ((IQuickActor)quickActors[i]).ExecuteQuickAction(context);
            if (!GameStateManager.IsResolution) yield break;

            yield return PauseAfterAction();
        }
    }

    private IEnumerator PauseAfterAction()
    {
        if (!context.ConsumeActionFlag()) yield break;

        yield return context.Pause(GameStateManager.Instance.Rules.TurnStepDelay);
    }

    private IEnumerator WaitForAnimations()
    {
        float elapsed = 0f;

        while (context.PendingAnimations > 0 && !context.IsFastForward)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > AnimationWaitTimeout)
            {
                Debug.LogWarning("[TurnManager] แอนิเมชันบางตัวไม่เรียก EndAnimation ภายในเวลาที่กำหนด จึงบังคับไปต่อ");
                break;
            }

            yield return null;
        }

        context.ResetAnimations();
    }

    private int ResolveSpeedIndex()
    {
        if (speedIndex < 0 && GameStateManager.Instance != null)
        {
            speedIndex = GameStateManager.Instance.Rules.DefaultSpeedIndex;
        }

        return Mathf.Max(0, speedIndex);
    }

    private void PostLog(string message)
    {
        OnLogMessage?.Invoke(message);
    }
}
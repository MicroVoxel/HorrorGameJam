using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวขับเทิร์นของเฟส Resolution (Coroutine ตัวเดียว หน่วยต่างๆ ไม่ต้องมี Update เอง)
///
/// ลำดับใน 1 เทิร์น:
///   1. Start of Turn  2. Spawner Step  3. Quick Step  4. Entity Step (หัวขบวนก่อน)
///   5. Tower Step     6. รอแอนิเมชันที่ปล่อยพร้อมกันให้จบ  7. Cleanup  8. End of Turn
///
/// เริ่มเองเมื่อ GameState เป็น Resolution และเรียก GameStateManager.CompleteResolution() เมื่อครบเทิร์น
/// ถ้าเกมจบ (Ended) กลางคันจะหยุดทันที
/// </summary>
[DisallowMultipleComponent]
public class TurnManager : SingletonBehaviour<TurnManager>
{
    private const float AnimationWaitTimeout = 5f;

    private readonly List<ITurnActor> buffer = new List<ITurnActor>(64);
    private readonly List<ITurnActor> quickActors = new List<ITurnActor>(16);

    private Coroutine resolutionRoutine;
    private TurnContext context;
    private int speedIndex = -1;

    // ---------- State ----------
    public bool IsRunning => resolutionRoutine != null;
    public TurnContext Context => context;
    public int CurrentTurn => context != null ? context.Turn : 0;
    public int TotalTurns => context != null ? context.TotalTurns : 0;
    public bool IsFastForward => context != null && context.IsFastForward;
    public int SpeedIndex => ResolveSpeedIndex();
    public float SpeedMultiplier => GameStateManager.Instance != null
        ? GameStateManager.Instance.Rules.GetSpeedMultiplier(ResolveSpeedIndex())
        : 1f;

    // ---------- Events ----------
    /// <summary>เริ่ม Resolution (พารามิเตอร์: Round, จำนวนเทิร์น)</summary>
    public event Action<int, int> OnResolutionStarted;

    /// <summary>เริ่มเทิร์น (พารามิเตอร์: เทิร์นที่, จำนวนเทิร์นทั้งหมด)</summary>
    public event Action<int, int> OnTurnStarted;

    /// <summary>เทิร์นจบ หลัง Cleanup และ OnTurnEnd ของทุกตัวแล้ว</summary>
    public event Action<int, int> OnTurnEnded;

    /// <summary>ขั้น Cleanup ของเทิร์น: ระบบอื่นเก็บศพ ดรอป เช็กกลายพันธุ์ เช็ก Extinction ผ่าน Event นี้</summary>
    public event Action<TurnContext> OnTurnCleanup;

    /// <summary>จบ Resolution (true = จบก่อนครบเทิร์นเพราะไม่เหลืออะไรเกิดขึ้นอีก)</summary>
    public event Action<bool> OnResolutionFinished;

    public event Action<int> OnSpeedChanged;
    public event Action OnFastForwardStarted;

    /// <summary>ข้อความ Event Log จาก ctx.Log()</summary>
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

    // ---------- Public controls (SpeedControlUI ใช้ใน Part 5) ----------

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

    /// <summary>Skip: รันเทิร์นที่เหลือโดยไม่แอนิเมต (Logic เท่าเดิมเพราะใช้ GameRandom แบบ Seeded)</summary>
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
            if (resolutionRoutine == null)
            {
                resolutionRoutine = StartCoroutine(RunResolution());
            }
        }
        else if (resolutionRoutine != null)
        {
            // ออกจาก Resolution กลางคัน (เช่น ชนะหรือแพ้ระหว่างเทิร์น)
            StopCoroutine(resolutionRoutine);
            resolutionRoutine = null;
        }
    }

    private IEnumerator RunResolution()
    {
        GameRulesData rules = GameStateManager.Instance.Rules;
        int round = GameStateManager.CurrentRound;
        int totalTurns = Mathf.Max(1, GameStateManager.TurnsThisRound);

        context = new TurnContext(round, totalTurns, PostLog) { SpeedMultiplier = SpeedMultiplier };
        OnResolutionStarted?.Invoke(round, totalTurns);

        bool endedEarly = false;

        for (int turn = 1; turn <= totalTurns; turn++)
        {
            if (!GameStateManager.IsResolution) break;

            context.BeginTurn(turn, SpeedMultiplier);
            yield return RunSingleTurn(rules);

            if (!GameStateManager.IsResolution) break;

            if (rules.EndResolutionEarlyOnWipe && turn < totalTurns && NothingLeftToHappen())
            {
                endedEarly = true;
                break;
            }
        }

        resolutionRoutine = null;

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

        // 7. Cleanup
        OnTurnCleanup?.Invoke(context);
        if (!GameStateManager.IsResolution) yield break;

        // 8. End of Turn
        TurnActorRegistry.CollectAll(buffer);
        for (int i = 0; i < buffer.Count; i++)
        {
            if (TurnActorRegistry.IsUsable(buffer[i])) buffer[i].OnTurnEnd(context);
        }

        OnTurnEnded?.Invoke(context.Turn, context.TotalTurns);

        // หน่วงท้ายเทิร์น (ข้ามถ้าเทิร์นว่างและเปิด Auto Skip Empty Turns)
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

        // ผีหัวขบวนก่อน แล้วจึงป้อม
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
            // ตัวที่ตายใน Quick Step (จากตัวก่อนหน้า) จะไม่ได้ทำ action ต่อ
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

    /// <summary>ไม่มีผีเหลือและไม่มีรังผีที่ยังเสกได้ = ไม่มีอะไรเกิดขึ้นอีกแล้ว</summary>
    private static bool NothingLeftToHappen()
    {
        return EntityRegistry.ActiveCount == 0 && !TurnActorRegistry.AnyActive(TurnActorGroup.Spawner);
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

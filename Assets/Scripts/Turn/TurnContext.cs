using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ข้อมูลของ Resolution รอบปัจจุบันที่ส่งให้ทุก ITurnActor
/// ใช้บอกเลขเทิร์น, โหมด Skip, ความเร็ว, ส่ง Event Log และนับแอนิเมชันที่ยังเล่นไม่จบ
/// </summary>
public class TurnContext
{
    private readonly Action<string> logSink;
    private bool actionSinceLastPause;

    public int Round { get; }
    public int TotalTurns { get; }
    public int Turn { get; private set; }

    /// <summary>true = กด Skip แล้ว ให้ข้ามแอนิเมชันและหน่วงเวลาทั้งหมด (ผลทาง Logic ต้องเท่าเดิมเป๊ะ)</summary>
    public bool IsFastForward { get; internal set; }

    public float SpeedMultiplier { get; internal set; } = 1f;
    public int PendingAnimations { get; private set; }
    public bool ActionPerformedThisTurn { get; private set; }

    public TurnContext(int round, int totalTurns, Action<string> logSink)
    {
        Round = round;
        TotalTurns = totalTurns;
        this.logSink = logSink;
    }

    internal void BeginTurn(int turn, float speedMultiplier)
    {
        Turn = turn;
        SpeedMultiplier = speedMultiplier;
        ActionPerformedThisTurn = false;
        actionSinceLastPause = false;
    }

    internal bool ConsumeActionFlag()
    {
        bool had = actionSinceLastPause;
        actionSinceLastPause = false;
        return had;
    }

    internal void ResetAnimations()
    {
        PendingAnimations = 0;
    }

    /// <summary>บอกว่ามี action เกิดขึ้นจริง (ใช้คุมจังหวะหน่วงและการข้ามเทิร์นว่าง)</summary>
    public void MarkAction()
    {
        ActionPerformedThisTurn = true;
        actionSinceLastPause = true;
    }

    /// <summary>ส่งข้อความเข้า Event Log เป็นภาษาอังกฤษสั้นๆ เช่น "Skeleton hits Core Shrine for 4"</summary>
    public void Log(string message)
    {
        if (!string.IsNullOrEmpty(message)) logSink?.Invoke(message);
    }

    /// <summary>เรียกก่อนปล่อยแอนิเมชันที่เล่นพร้อมกัน และเรียก EndAnimation เมื่อเล่นจบ</summary>
    public void BeginAnimation()
    {
        PendingAnimations++;
    }

    public void EndAnimation()
    {
        PendingAnimations = Mathf.Max(0, PendingAnimations - 1);
    }

    /// <summary>แปลงระยะเวลาแอนิเมชันตามความเร็วที่เลือก (Skip = 0)</summary>
    public float ScaleDuration(float seconds)
    {
        if (IsFastForward) return 0f;
        return seconds / Mathf.Max(0.1f, SpeedMultiplier);
    }

    /// <summary>หน่วงเวลาตามความเร็ว ถ้า Skip จะจบทันที</summary>
    public IEnumerator Pause(float seconds)
    {
        float scaled = ScaleDuration(seconds);
        if (scaled <= 0f) yield break;

        yield return new WaitForSeconds(scaled);
    }
}

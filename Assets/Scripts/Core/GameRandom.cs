using System;
using System.Collections.Generic;

/// <summary>
/// ตัวสุ่มกลางของเกมเพลย์ (Seeded ต่อ Round) ทุกการสุ่มที่กระทบผลในเกม (ดรอป, เลือกเป้าหมายเสมอกัน, เสกป้อม)
/// ต้องใช้ตัวนี้แทน UnityEngine.Random เพื่อให้ Skip / Replay ได้ผลลัพธ์เหมือนเล่นปกติเป๊ะ
/// (ห้ามใช้กับเรื่องภาพล้วนๆ เช่น เอฟเฟกต์ เพราะจะไปกินลำดับสุ่มของตรรกะ)
/// </summary>
public static class GameRandom
{
    private static int baseSeed;
    private static Random rng = new Random();

    public static int BaseSeed => baseSeed;
    public static int CurrentSeed { get; private set; }

    /// <summary>เรียกครั้งเดียวตอนเริ่มด่าน configuredSeed = 0 หมายถึงสุ่ม seed ใหม่ทุกครั้ง</summary>
    public static void Initialize(int configuredSeed)
    {
        baseSeed = configuredSeed != 0 ? configuredSeed : Environment.TickCount;
        BeginRound(0);
    }

    /// <summary>เรียกตอนเริ่ม Resolution ของแต่ละ Round เพื่อรีเซ็ตลำดับสุ่มเป็น seed ของ Round นั้น</summary>
    public static void BeginRound(int roundNumber)
    {
        unchecked
        {
            CurrentSeed = (baseSeed * 397) ^ (roundNumber * 7919);
        }

        rng = new Random(CurrentSeed);
    }

    /// <summary>จำนวนเต็มในช่วง [minInclusive, maxExclusive)</summary>
    public static int Range(int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }

    /// <summary>จำนวนจริงในช่วง [min, max)</summary>
    public static float Range(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    /// <summary>ค่า 0.0 ถึงน้อยกว่า 1.0</summary>
    public static float Value => (float)rng.NextDouble();

    /// <summary>true ด้วยความน่าจะเป็น probability (0-1)</summary>
    public static bool Chance(float probability)
    {
        return Value < probability;
    }

    public static T Pick<T>(IList<T> list)
    {
        if (list == null || list.Count == 0) return default;
        return list[rng.Next(list.Count)];
    }
}
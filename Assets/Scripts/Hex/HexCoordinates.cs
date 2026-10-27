using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// โครงสร้างพิกัด Axial (q, r, s) สำหรับกระดานหกเหลี่ยม (Pointy-topped Hex)
/// Facing / ทิศ 0-5 ตรงกับ Directions: 0 = +q (ตะวันออก) และเพิ่มทีละ 60 องศาทวนเข็มเมื่อมองจากด้านบน
/// </summary>
[Serializable]
public struct HexCoordinates : IEquatable<HexCoordinates>
{
    [SerializeField] private int q;
    [SerializeField] private int r;

    public int Q => q;
    public int R => r;
    public int S => -q - r;

    public static readonly HexCoordinates Zero = new HexCoordinates(0, 0);

    /// <summary>6 ทิศตามขอบ Hex (ห่าง 1 ช่อง)</summary>
    public static readonly HexCoordinates[] Directions = new HexCoordinates[]
    {
        new HexCoordinates(1, 0),
        new HexCoordinates(0, 1),
        new HexCoordinates(-1, 1),
        new HexCoordinates(-1, 0),
        new HexCoordinates(0, -1),
        new HexCoordinates(1, -1)
    };

    /// <summary>6 ทิศมุม (ผลรวมของขอบที่ติดกัน ห่าง 2 ช่อง) Diagonal[i] อยู่ระหว่าง Directions[i] กับ Directions[i+1]</summary>
    public static readonly HexCoordinates[] DiagonalDirections = new HexCoordinates[]
    {
        new HexCoordinates(1, 1),
        new HexCoordinates(-1, 2),
        new HexCoordinates(-2, 1),
        new HexCoordinates(-1, -1),
        new HexCoordinates(1, -2),
        new HexCoordinates(2, -1)
    };

    public HexCoordinates(int q, int r)
    {
        this.q = q;
        this.r = r;
    }

    // ---------- Operators ----------
    public static HexCoordinates operator +(HexCoordinates a, HexCoordinates b) => new HexCoordinates(a.q + b.q, a.r + b.r);
    public static HexCoordinates operator -(HexCoordinates a, HexCoordinates b) => new HexCoordinates(a.q - b.q, a.r - b.r);
    public static HexCoordinates operator *(HexCoordinates a, int k) => new HexCoordinates(a.q * k, a.r * k);
    public static bool operator ==(HexCoordinates a, HexCoordinates b) => a.q == b.q && a.r == b.r;
    public static bool operator !=(HexCoordinates a, HexCoordinates b) => !(a == b);

    // ---------- World conversion ----------
    public Vector3 ToWorldPosition(float outerRadius = 1.0f, float heightY = 0.0f)
    {
        float x = outerRadius * Mathf.Sqrt(3.0f) * (q + r * 0.5f);
        float z = outerRadius * 1.5f * r;
        return new Vector3(x, heightY, z);
    }

    public static HexCoordinates FromWorldPosition(Vector3 position, float outerRadius = 1.0f)
    {
        float q = (Mathf.Sqrt(3.0f) / 3.0f * position.x - 1.0f / 3.0f * position.z) / outerRadius;
        float r = (2.0f / 3.0f * position.z) / outerRadius;
        return RoundToHex(q, r);
    }

    private static HexCoordinates RoundToHex(float q, float r)
    {
        float s = -q - r;
        int roundQ = Mathf.RoundToInt(q);
        int roundR = Mathf.RoundToInt(r);
        int roundS = Mathf.RoundToInt(s);

        float qDiff = Mathf.Abs(roundQ - q);
        float rDiff = Mathf.Abs(roundR - r);
        float sDiff = Mathf.Abs(roundS - s);

        if (qDiff > rDiff && qDiff > sDiff)
        {
            roundQ = -roundR - roundS;
        }
        else if (rDiff > sDiff)
        {
            roundR = -roundQ - roundS;
        }

        return new HexCoordinates(roundQ, roundR);
    }

    // ---------- Neighbors / distance / sorting ----------
    public HexCoordinates GetNeighbor(int directionIndex)
    {
        return this + Directions[NormalizeFacing(directionIndex)];
    }

    public int DistanceTo(HexCoordinates other)
    {
        HexCoordinates d = this - other;
        return (Mathf.Abs(d.q) + Mathf.Abs(d.r) + Mathf.Abs(d.S)) / 2;
    }

    /// <summary>เรียงรายการพิกัดจากใกล้ origin ไปไกล เสมอกันเรียงตาม Q แล้ว R (ให้ลำดับ Deterministic)</summary>
    public static void SortByDistanceFrom(List<HexCoordinates> list, HexCoordinates origin)
    {
        if (list == null || list.Count <= 1) return;

        list.Sort((a, b) =>
        {
            int da = origin.DistanceTo(a);
            int db = origin.DistanceTo(b);
            if (da != db) return da.CompareTo(db);
            if (a.q != b.q) return a.q.CompareTo(b.q);
            return a.r.CompareTo(b.r);
        });
    }

    // ---------- Rotation / facing ----------

    public static int NormalizeFacing(int facing) => ((facing % 6) + 6) % 6;

    public HexCoordinates Rotate(int steps)
    {
        int n = NormalizeFacing(steps);
        int rq = q;
        int rr = r;

        for (int i = 0; i < n; i++)
        {
            int nq = -rr;
            int nr = rq + rr;
            rq = nq;
            rr = nr;
        }

        return new HexCoordinates(rq, rr);
    }

    public static int NearestDirection(HexCoordinates delta)
    {
        Vector2 v = ToPlane(delta);
        int best = 0;
        float bestDot = float.NegativeInfinity;

        for (int i = 0; i < 6; i++)
        {
            float dot = Vector2.Dot(v, ToPlane(Directions[i]));
            if (dot > bestDot + 0.0001f)
            {
                bestDot = dot;
                best = i;
            }
        }

        return best;
    }

    public int FacingTo(HexCoordinates target) => NearestDirection(target - this);

    public static float FacingToYaw(int facing) => 90f - 60f * NormalizeFacing(facing);

    private static Vector2 ToPlane(HexCoordinates h)
    {
        return new Vector2(Mathf.Sqrt(3.0f) * (h.q + h.r * 0.5f), 1.5f * h.r);
    }

    // ---------- Equality ----------
    public bool Equals(HexCoordinates other) => q == other.q && r == other.r;
    public override bool Equals(object obj) => obj is HexCoordinates other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(q, r);
    public override string ToString() => $"Hex({Q}, {R})";
}
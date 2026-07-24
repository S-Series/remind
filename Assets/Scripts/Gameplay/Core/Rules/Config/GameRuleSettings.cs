using System;
using UnityEngine;

[Serializable]
public struct JudgeWindows
{
    [SerializeField, Min(0)] private int perfectWindowMs;
    [SerializeField, Min(0)] private int greatWindowMs;
    [SerializeField, Min(0)] private int goodWindowMs;
    [SerializeField, Min(0)] private int missWindowMs;

    public int PerfectWindowMs => perfectWindowMs;
    public int GreatWindowMs => greatWindowMs;
    public int GoodWindowMs => goodWindowMs;
    public int MissWindowMs => missWindowMs;

    public JudgeWindows(int perfectWindowMs, int greatWindowMs, int goodWindowMs, int missWindowMs)
    {
        this.perfectWindowMs = perfectWindowMs;
        this.greatWindowMs = greatWindowMs;
        this.goodWindowMs = goodWindowMs;
        this.missWindowMs = missWindowMs;
        Normalize();
    }

    public JudgeResult Evaluate(double offsetMs)
    {
        if (double.IsNaN(offsetMs) || double.IsInfinity(offsetMs))
        {
            return JudgeResult.None;
        }

        double absoluteOffsetMs = Math.Abs(offsetMs);

        if (absoluteOffsetMs <= perfectWindowMs)
        {
            return JudgeResult.Perfect;
        }

        if (absoluteOffsetMs <= greatWindowMs)
        {
            return JudgeResult.Great;
        }

        if (absoluteOffsetMs <= goodWindowMs)
        {
            return JudgeResult.Good;
        }

        return absoluteOffsetMs <= missWindowMs ? JudgeResult.Miss : JudgeResult.None;
    }

    internal void Normalize()
    {
        perfectWindowMs = Mathf.Max(0, perfectWindowMs);
        greatWindowMs = Mathf.Max(perfectWindowMs, greatWindowMs);
        goodWindowMs = Mathf.Max(greatWindowMs, goodWindowMs);
        missWindowMs = Mathf.Max(goodWindowMs, missWindowMs);
    }
}

[Serializable]
public sealed class JudgeHealthDeltaSettings
{
    [SerializeField] private int perfect = 1;
    [SerializeField] private int great = 1;
    [SerializeField] private int good = -2;
    [SerializeField] private int miss = -10;

    public int GetValue(JudgeResult result)
    {
        switch (result)
        {
            case JudgeResult.Perfect:
                return perfect;
            case JudgeResult.Great:
                return great;
            case JudgeResult.Good:
                return good;
            case JudgeResult.Miss:
                return miss;
            default:
                return 0;
        }
    }
}

[Serializable]
public sealed class JudgeScoreWeightSettings
{
    [SerializeField] private double perfect = 1d;
    [SerializeField] private double great = 0.7d;
    [SerializeField] private double good = 0.3d;
    [SerializeField] private double miss;

    public double GetValue(JudgeResult result)
    {
        switch (result)
        {
            case JudgeResult.Perfect:
                return perfect;
            case JudgeResult.Great:
                return great;
            case JudgeResult.Good:
                return good;
            case JudgeResult.Miss:
                return miss;
            default:
                return 0d;
        }
    }

    internal void Normalize()
    {
        perfect = Math.Max(0d, perfect);
        great = Math.Max(0d, great);
        good = Math.Max(0d, good);
        miss = Math.Max(0d, miss);
    }
}

[Serializable]
public sealed class JudgeComboBehaviorSettings
{
    [SerializeField] private ComboBehavior perfect = ComboBehavior.Increase;
    [SerializeField] private ComboBehavior great = ComboBehavior.Increase;
    [SerializeField] private ComboBehavior good = ComboBehavior.Increase;
    [SerializeField] private ComboBehavior miss = ComboBehavior.Reset;

    public ComboBehavior GetValue(JudgeResult result)
    {
        switch (result)
        {
            case JudgeResult.Perfect:
                return perfect;
            case JudgeResult.Great:
                return great;
            case JudgeResult.Good:
                return good;
            case JudgeResult.Miss:
                return miss;
            default:
                return ComboBehavior.Keep;
        }
    }
}

[Serializable]
public sealed class RankThresholdSettings
{
    [SerializeField, Range(0f, 1f)] private float rankS = 0.95f;
    [SerializeField, Range(0f, 1f)] private float rankA = 0.9f;
    [SerializeField, Range(0f, 1f)] private float rankB = 0.8f;
    [SerializeField, Range(0f, 1f)] private float rankC = 0.7f;

    public RankGrade Evaluate(double scoreRatio)
    {
        if (scoreRatio >= rankS)
        {
            return RankGrade.S;
        }

        if (scoreRatio >= rankA)
        {
            return RankGrade.A;
        }

        if (scoreRatio >= rankB)
        {
            return RankGrade.B;
        }

        return scoreRatio >= rankC ? RankGrade.C : RankGrade.D;
    }

    internal void Normalize()
    {
        rankS = Mathf.Clamp01(rankS);
        rankA = Mathf.Clamp(rankA, 0f, rankS);
        rankB = Mathf.Clamp(rankB, 0f, rankA);
        rankC = Mathf.Clamp(rankC, 0f, rankB);
    }
}

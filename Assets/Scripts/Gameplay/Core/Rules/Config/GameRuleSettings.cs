using System;
using UnityEngine;

[Serializable]
public struct JudgeWindows
{
    [SerializeField, Min(0)] private int perfectPlusWindowMs;
    [SerializeField, Min(0)] private int perfectWindowMs;
    [SerializeField, Min(0)] private int greatWindowMs;
    [SerializeField, Min(0)] private int missWindowMs;

    public int PerfectPlusWindowMs => perfectPlusWindowMs;
    public int PerfectWindowMs => perfectWindowMs;
    public int GreatWindowMs => greatWindowMs;
    public int MissWindowMs => missWindowMs;

    public JudgeWindows(int perfectPlusWindowMs, int perfectWindowMs,
        int greatWindowMs, int missWindowMs)
    {
        this.perfectPlusWindowMs = perfectPlusWindowMs;
        this.perfectWindowMs = perfectWindowMs;
        this.greatWindowMs = greatWindowMs;
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

        if (absoluteOffsetMs <= perfectPlusWindowMs)
        {
            return JudgeResult.PerfectPlus;
        }

        if (absoluteOffsetMs <= perfectWindowMs)
        {
            return JudgeResult.Perfect;
        }

        if (absoluteOffsetMs <= greatWindowMs)
        {
            return JudgeResult.Great;
        }

        return absoluteOffsetMs <= missWindowMs ? JudgeResult.Miss : JudgeResult.None;
    }

    internal void Normalize()
    {
        perfectPlusWindowMs = Mathf.Max(0, perfectPlusWindowMs);
        perfectWindowMs = Mathf.Max(perfectPlusWindowMs, perfectWindowMs);
        greatWindowMs = Mathf.Max(perfectWindowMs, greatWindowMs);
        missWindowMs = Mathf.Max(greatWindowMs, missWindowMs);
    }
}

[Serializable]
public sealed class JudgeHealthDeltaSettings
{
    [SerializeField] private int perfectPlus = 1;
    [SerializeField] private int perfect = 1;
    [SerializeField] private int great = 1;
    [SerializeField] private int miss = -10;

    public int GetValue(JudgeResult result)
    {
        switch (result)
        {
            case JudgeResult.PerfectPlus:
                return perfectPlus;
            case JudgeResult.Perfect:
                return perfect;
            case JudgeResult.Great:
                return great;
            case JudgeResult.Miss:
                return miss;
            default:
                return 0;
        }
    }
}

[Serializable]
public sealed class SpecialHardGaugeSettings
{
    [SerializeField, Min(1)] private int rfReferenceCount = 1000;
    [SerializeField] private float perfect = 0.25f;
    [SerializeField] private float great = -0.5f;
    [SerializeField] private float miss = -7.5f;
    [SerializeField, Range(0, 100)] private int damageReductionThresholdPercent = 30;
    [SerializeField, Range(0f, 1f)] private float damageMultiplierBelowThreshold = 0.5f;

    public double GetDelta(JudgeResult result, double currentHealth,
        int maxHealth, int judgementCount)
    {
        if (judgementCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(judgementCount));
        float rf = Mathf.Clamp(Mathf.Sqrt((float)rfReferenceCount /
            judgementCount),
            0.75f, 1.35f);
        float baseDelta;
        switch (result)
        {
            case JudgeResult.PerfectPlus:
            case JudgeResult.Perfect: baseDelta = perfect; break;
            case JudgeResult.Great: baseDelta = great; break;
            case JudgeResult.Miss: baseDelta = miss; break;
            default: return 0d;
        }

        double delta = baseDelta * rf;
        if ((result == JudgeResult.Great || result == JudgeResult.Miss) &&
            currentHealth * 100d <=
                maxHealth * damageReductionThresholdPercent)
            delta *= damageMultiplierBelowThreshold;
        return delta;
    }

    public void Validate()
    {
        if (rfReferenceCount < 1 ||
            float.IsNaN(perfect) || float.IsInfinity(perfect) ||
            float.IsNaN(great) || float.IsInfinity(great) ||
            float.IsNaN(miss) || float.IsInfinity(miss) ||
            float.IsNaN(damageMultiplierBelowThreshold) ||
            damageReductionThresholdPercent < 0 ||
            damageReductionThresholdPercent > 100 ||
            damageMultiplierBelowThreshold < 0f ||
            damageMultiplierBelowThreshold > 1f)
            throw new InvalidOperationException(
                "Special Hard gauge has invalid health values.");
    }
}

[Serializable]
public sealed class JudgeScoreWeightSettings
{
    [SerializeField] private double perfectPlus = 1d;
    [SerializeField] private double perfect = 1d;
    [SerializeField] private double great = 0.7d;
    [SerializeField] private double miss;

    public double GetValue(JudgeResult result)
    {
        switch (result)
        {
            case JudgeResult.PerfectPlus:
                return perfectPlus;
            case JudgeResult.Perfect:
                return perfect;
            case JudgeResult.Great:
                return great;
            case JudgeResult.Miss:
                return miss;
            default:
                return 0d;
        }
    }

    internal void Normalize()
    {
        perfectPlus = Math.Max(0d, perfectPlus);
        perfect = Math.Max(0d, perfect);
        great = Math.Max(0d, great);
        miss = Math.Max(0d, miss);
    }
}

[Serializable]
public sealed class JudgeComboBehaviorSettings
{
    [SerializeField] private ComboBehavior perfectPlus = ComboBehavior.Increase;
    [SerializeField] private ComboBehavior perfect = ComboBehavior.Increase;
    [SerializeField] private ComboBehavior great = ComboBehavior.Increase;
    [SerializeField] private ComboBehavior miss = ComboBehavior.Reset;

    public ComboBehavior GetValue(JudgeResult result)
    {
        switch (result)
        {
            case JudgeResult.PerfectPlus:
                return perfectPlus;
            case JudgeResult.Perfect:
                return perfect;
            case JudgeResult.Great:
                return great;
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

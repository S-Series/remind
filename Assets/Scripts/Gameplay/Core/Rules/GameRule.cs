using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class GameRule : MonoBehaviour
{
    [SerializeField] private GameRuleConfig config;

    private readonly List<IRuleModifier> modifiers = new List<IRuleModifier>();

    public GameRuleConfig Config => config;
    public IReadOnlyList<IRuleModifier> Modifiers => modifiers;

    public JudgeWindows BaseJudgeWindows => RequiredConfig.BaseJudgeWindows;
    public int BaseMaxHealth => RequiredConfig.MaxHealth;
    public int BaseInitialHealth => RequiredConfig.InitialHealth;
    public int BaseClearHealth => RequiredConfig.ClearHealth;
    public HealthGaugeType GaugeType => RequiredConfig.GaugeType;
    public int MaxScore => RequiredConfig.MaxScore;
    public bool FailImmediately => RequiredConfig.FailImmediately;
    public bool ContinueAfterFail => RequiredConfig.ContinueAfterFail;
    public LongNoteJudgeMode LongNoteJudgeMode => RequiredConfig.LongNoteJudgeMode;

    private GameRuleConfig RequiredConfig
    {
        get
        {
            if (config == null)
            {
                throw new InvalidOperationException($"GameRuleConfig is not assigned to {name}.");
            }

            return config;
        }
    }

    protected virtual void Awake()
    {
        if (config == null)
        {
            Debug.LogError("GameRuleConfig is not assigned.", this);
            enabled = false;
        }
    }

    public void RegisterModifier(IRuleModifier modifier)
    {
        if (modifier != null && !modifiers.Contains(modifier))
        {
            modifiers.Add(modifier);
        }
    }

    public bool UnregisterModifier(IRuleModifier modifier)
    {
        return modifier != null && modifiers.Remove(modifier);
    }

    public void ClearModifiers()
    {
        modifiers.Clear();
    }

    public TimingSide GetTimingSide(double offsetMs)
    {
        if (offsetMs < 0d)
        {
            return TimingSide.Early;
        }

        return offsetMs > 0d ? TimingSide.Late : TimingSide.Exact;
    }

    public JudgeWindows GetJudgeWindows(RuleContext context)
    {
        JudgeWindows windows = BaseJudgeWindows;

        for (int i = 0; i < modifiers.Count; i++)
        {
            windows = modifiers[i].ModifyJudgeWindows(windows, context);
        }

        windows.Normalize();
        return windows;
    }

    public JudgeResult Judge(double offsetMs, RuleContext context)
    {
        JudgeResult result = GetJudgeWindows(context).Evaluate(offsetMs);

        for (int i = 0; i < modifiers.Count; i++)
        {
            result = modifiers[i].ModifyJudgeResult(result, context);
        }

        return IsValidJudgeResult(result) ? result : JudgeResult.None;
    }

    public int GetMaxHealth(RuleContext context)
    {
        int maxHealth = BaseMaxHealth;

        for (int i = 0; i < modifiers.Count; i++)
        {
            maxHealth = modifiers[i].ModifyMaxHealth(maxHealth, context);
        }

        return Mathf.Max(0, maxHealth);
    }

    public int GetInitialHealth(RuleContext context)
    {
        return Mathf.Clamp(BaseInitialHealth, 0, GetMaxHealth(context));
    }

    public int GetHealthDelta(JudgeResult result, RuleContext context)
    {
        int delta = CalculateBaseHealthDelta(result, context);

        for (int i = 0; i < modifiers.Count; i++)
        {
            delta = modifiers[i].ModifyHealthDelta(delta, result, context);
        }

        return delta;
    }

    public int ApplyHealthDelta(int currentHealth, JudgeResult result, RuleContext context)
    {
        long changedHealth = (long)currentHealth + GetHealthDelta(result, context);
        return ClampHealth(changedHealth, GetMaxHealth(context));
    }

    public double GetScoreWeight(JudgeResult result, RuleContext context)
    {
        double weight = CalculateBaseScoreWeight(result, context);

        for (int i = 0; i < modifiers.Count; i++)
        {
            weight = modifiers[i].ModifyScoreWeight(weight, result, context);
        }

        return double.IsNaN(weight) ? 0d : Math.Max(0d, weight);
    }

    public double CalculateScoreDelta(JudgeResult result, RuleContext context, int totalNoteCount)
    {
        if (totalNoteCount <= 0)
        {
            return 0d;
        }

        return MaxScore / (double)totalNoteCount * GetScoreWeight(result, context);
    }

    public double ClampScore(double score)
    {
        if (double.IsNaN(score) || score <= 0d)
        {
            return 0d;
        }

        return Math.Min(score, MaxScore);
    }

    public ComboBehavior GetComboBehavior(JudgeResult result, RuleContext context)
    {
        ComboBehavior behavior = CalculateBaseComboBehavior(result, context);

        for (int i = 0; i < modifiers.Count; i++)
        {
            behavior = modifiers[i].ModifyComboBehavior(behavior, result, context);
        }

        return behavior;
    }

    public int ApplyComboBehavior(int currentCombo, JudgeResult result, RuleContext context)
    {
        currentCombo = Mathf.Max(0, currentCombo);

        switch (GetComboBehavior(result, context))
        {
            case ComboBehavior.Increase:
                return currentCombo == int.MaxValue ? int.MaxValue : currentCombo + 1;
            case ComboBehavior.Reset:
                return 0;
            default:
                return currentCombo;
        }
    }

    public bool ShouldFail(RuleContext context)
    {
        bool shouldFail = EvaluateBaseShouldFail(context);

        for (int i = 0; i < modifiers.Count; i++)
        {
            shouldFail = modifiers[i].ModifyShouldFail(shouldFail, context);
        }

        return shouldFail;
    }

    public bool IsCleared(RuleContext context)
    {
        bool isCleared = EvaluateBaseIsCleared(context);

        for (int i = 0; i < modifiers.Count; i++)
        {
            isCleared = modifiers[i].ModifyIsCleared(isCleared, context);
        }

        return isCleared;
    }

    public bool ShouldStopAfterFail(RuleContext context)
    {
        return ShouldFail(context) && !ContinueAfterFail;
    }

    public RankGrade GetRank(double score)
    {
        double scoreRatio = MaxScore <= 0 ? 0d : ClampScore(score) / MaxScore;
        return RequiredConfig.RankThresholds.Evaluate(scoreRatio);
    }

    protected abstract int CalculateBaseHealthDelta(JudgeResult result, RuleContext context);
    protected abstract double CalculateBaseScoreWeight(JudgeResult result, RuleContext context);
    protected abstract ComboBehavior CalculateBaseComboBehavior(JudgeResult result, RuleContext context);
    protected abstract bool EvaluateBaseShouldFail(RuleContext context);
    protected abstract bool EvaluateBaseIsCleared(RuleContext context);

    private static int ClampHealth(long health, int maxHealth)
    {
        if (health <= 0L)
        {
            return 0;
        }

        return health >= maxHealth ? maxHealth : (int)health;
    }

    private static bool IsValidJudgeResult(JudgeResult result)
    {
        return result >= JudgeResult.None && result <= JudgeResult.Miss;
    }
}

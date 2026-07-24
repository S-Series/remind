using UnityEngine;

[AddComponentMenu("REmind/Gameplay/Default Game Rule")]
[DisallowMultipleComponent]
public sealed class DefaultGameRule : GameRule
{
    protected override int CalculateBaseHealthDelta(JudgeResult result, RuleContext context)
    {
        return Config.HealthDelta.GetValue(result);
    }

    protected override double CalculateBaseScoreWeight(JudgeResult result, RuleContext context)
    {
        return Config.ScoreWeight.GetValue(result);
    }

    protected override ComboBehavior CalculateBaseComboBehavior(JudgeResult result, RuleContext context)
    {
        return Config.ComboBehavior.GetValue(result);
    }

    protected override bool EvaluateBaseShouldFail(RuleContext context)
    {
        if (FailImmediately && context.CurrentHealth <= 0)
        {
            return true;
        }

        return context.IsChartCompleted && context.CurrentHealth < BaseClearHealth;
    }

    protected override bool EvaluateBaseIsCleared(RuleContext context)
    {
        return context.IsChartCompleted && context.CurrentHealth >= BaseClearHealth;
    }
}

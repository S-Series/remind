using UnityEngine;

public abstract class RuleModifier : ScriptableObject, IRuleModifier
{
    public virtual JudgeWindows ModifyJudgeWindows(JudgeWindows current, RuleContext context) => current;
    public virtual JudgeResult ModifyJudgeResult(JudgeResult current, RuleContext context) => current;
    public virtual int ModifyMaxHealth(int current, RuleContext context) => current;
    public virtual int ModifyHealthDelta(int current, JudgeResult result, RuleContext context) => current;
    public virtual double ModifyScoreWeight(double current, JudgeResult result, RuleContext context) => current;
    public virtual ComboBehavior ModifyComboBehavior(
        ComboBehavior current,
        JudgeResult result,
        RuleContext context) => current;
    public virtual bool ModifyShouldFail(bool current, RuleContext context) => current;
    public virtual bool ModifyIsCleared(bool current, RuleContext context) => current;
}

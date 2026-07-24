public interface IRuleModifier
{
    JudgeWindows ModifyJudgeWindows(JudgeWindows current, RuleContext context);
    JudgeResult ModifyJudgeResult(JudgeResult current, RuleContext context);
    int ModifyMaxHealth(int current, RuleContext context);
    int ModifyHealthDelta(int current, JudgeResult result, RuleContext context);
    double ModifyScoreWeight(double current, JudgeResult result, RuleContext context);
    ComboBehavior ModifyComboBehavior(ComboBehavior current, JudgeResult result, RuleContext context);
    bool ModifyShouldFail(bool current, RuleContext context);
    bool ModifyIsCleared(bool current, RuleContext context);
}

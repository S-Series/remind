using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GameRuleConfig", menuName = "REmind/Gameplay/Game Rule Config")]
public sealed class GameRuleConfig : ScriptableObject
{
    [Header("Judgement")]
    [SerializeField] private JudgeWindows baseJudgeWindows = new JudgeWindows(50, 50, 100, 100);

    [Header("Gauge")]
    [SerializeField] private HealthGaugeType gaugeType = HealthGaugeType.Normal;
    [SerializeField, Min(0)] private int maxHealth = 100;
    [SerializeField, Min(0)] private int initialHealth = 100;
    [SerializeField, Min(0)] private int clearHealth = 70;
    [SerializeField] private JudgeHealthDeltaSettings healthDelta = new JudgeHealthDeltaSettings();

    [Header("Score")]
    [SerializeField, Min(0)] private int maxScore = 1_000_000;
    [SerializeField] private JudgeScoreWeightSettings scoreWeight = new JudgeScoreWeightSettings();
    [SerializeField] private RankThresholdSettings rankThresholds = new RankThresholdSettings();

    [Header("Combo")]
    [SerializeField] private JudgeComboBehaviorSettings comboBehavior = new JudgeComboBehaviorSettings();

    [Header("Clear")]
    [SerializeField] private bool failImmediately;
    [SerializeField] private bool continueAfterFail = true;

    [Header("Long Note")]
    [SerializeField] private LongNoteJudgeMode longNoteJudgeMode = LongNoteJudgeMode.StartAndEnd;

    public JudgeWindows BaseJudgeWindows => baseJudgeWindows;
    public HealthGaugeType GaugeType => gaugeType;
    public int MaxHealth => maxHealth;
    public int InitialHealth => initialHealth;
    public int ClearHealth => clearHealth;
    public JudgeHealthDeltaSettings HealthDelta => healthDelta;
    public int MaxScore => maxScore;
    public JudgeScoreWeightSettings ScoreWeight => scoreWeight;
    public RankThresholdSettings RankThresholds => rankThresholds;
    public JudgeComboBehaviorSettings ComboBehavior => comboBehavior;
    public bool FailImmediately => failImmediately;
    public bool ContinueAfterFail => continueAfterFail;
    public LongNoteJudgeMode LongNoteJudgeMode => longNoteJudgeMode;

    /// <summary>Reject malformed authored data before it becomes a live session rule.</summary>
    public void ValidateForSession()
    {
        if (!Enum.IsDefined(typeof(HealthGaugeType), gaugeType) ||
            !Enum.IsDefined(typeof(LongNoteJudgeMode), longNoteJudgeMode) ||
            maxHealth < 0 || initialHealth < 0 ||
            initialHealth > maxHealth || clearHealth < 0 ||
            clearHealth > maxHealth || maxScore < 0 ||
            healthDelta == null || scoreWeight == null ||
            rankThresholds == null || comboBehavior == null ||
            baseJudgeWindows.PerfectWindowMs < 0 ||
            baseJudgeWindows.GreatWindowMs <
                baseJudgeWindows.PerfectWindowMs ||
            baseJudgeWindows.GoodWindowMs <
                baseJudgeWindows.GreatWindowMs ||
            baseJudgeWindows.MissWindowMs <
                baseJudgeWindows.GoodWindowMs)
            throw new InvalidOperationException(
                "GameRuleConfig has invalid session values.");

        for (int grade = (int)JudgeResult.Perfect;
             grade <= (int)JudgeResult.Miss; grade++)
        {
            double weight = scoreWeight.GetValue((JudgeResult)grade);
            if (double.IsNaN(weight) || double.IsInfinity(weight) ||
                weight < 0d)
                throw new InvalidOperationException(
                    "GameRuleConfig has an invalid score weight.");
        }
    }

    private void OnValidate()
    {
        baseJudgeWindows.Normalize();
        maxHealth = Mathf.Max(0, maxHealth);
        initialHealth = Mathf.Clamp(initialHealth, 0, maxHealth);
        clearHealth = Mathf.Clamp(clearHealth, 0, maxHealth);
        maxScore = Mathf.Max(0, maxScore);

        if (healthDelta == null)
        {
            healthDelta = new JudgeHealthDeltaSettings();
        }

        if (scoreWeight == null)
        {
            scoreWeight = new JudgeScoreWeightSettings();
        }

        if (rankThresholds == null)
        {
            rankThresholds = new RankThresholdSettings();
        }

        if (comboBehavior == null)
        {
            comboBehavior = new JudgeComboBehaviorSettings();
        }

        scoreWeight.Normalize();
        rankThresholds.Normalize();
    }
}

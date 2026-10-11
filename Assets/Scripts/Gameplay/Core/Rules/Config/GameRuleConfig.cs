using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GameRuleConfig", menuName = "REmind/Gameplay/Game Rule Config")]
public sealed class GameRuleConfig : ScriptableObject
{
    [Header("Judgement")]
    [SerializeField] private JudgeWindows baseJudgeWindows = new JudgeWindows(40, 80, 130, 175);

    [Header("Gauge")]
    [SerializeField] private HealthGaugeType gaugeType = HealthGaugeType.Normal;
    [SerializeField, Min(0)] private int maxHealth = 100;
    [SerializeField, Min(0)] private int initialHealth = 100;
    [SerializeField, Min(0)] private int clearHealth = 70;
    [SerializeField] private JudgeHealthDeltaSettings healthDelta = new JudgeHealthDeltaSettings();
    [SerializeField] private bool useSpecialHardGauge;
    [SerializeField] private SpecialHardGaugeSettings specialHardGauge;

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
    public SpecialHardGaugeSettings SpecialHardGauge =>
        useSpecialHardGauge ? specialHardGauge : null;
    public int MaxScore => maxScore;
    public JudgeScoreWeightSettings ScoreWeight => scoreWeight;
    public RankThresholdSettings RankThresholds => rankThresholds;
    public JudgeComboBehaviorSettings ComboBehavior => comboBehavior;
    public bool FailImmediately => gaugeType == HealthGaugeType.Hard;
    public bool ContinueAfterFail =>
        gaugeType != HealthGaugeType.Hard && continueAfterFail;
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
            baseJudgeWindows.PerfectPlusWindowMs < 0 ||
            baseJudgeWindows.PerfectWindowMs <
                baseJudgeWindows.PerfectPlusWindowMs ||
            baseJudgeWindows.GreatWindowMs <
                baseJudgeWindows.PerfectWindowMs ||
            baseJudgeWindows.MissWindowMs <
                baseJudgeWindows.GreatWindowMs)
            throw new InvalidOperationException(
                "GameRuleConfig has invalid session values.");

        for (int grade = (int)JudgeResult.PerfectPlus;
             grade <= (int)JudgeResult.Miss; grade++)
        {
            double weight = scoreWeight.GetValue((JudgeResult)grade);
            if (double.IsNaN(weight) || double.IsInfinity(weight) ||
                weight < 0d)
                throw new InvalidOperationException(
                    "GameRuleConfig has an invalid score weight.");
        }
        if (useSpecialHardGauge)
        {
            if (specialHardGauge == null ||
                gaugeType != HealthGaugeType.Hard || maxHealth == 0)
                throw new InvalidOperationException(
                    "Special Hard health values require a positive Hard gauge.");
            specialHardGauge.Validate();
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

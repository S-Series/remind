using REmind.Data;

public readonly struct RuleContext
{
    public int CurrentHealth { get; }
    public int CurrentCombo { get; }
    public NoteType NoteType { get; }
    public bool IsFever { get; }
    public bool MissGuardAvailable { get; }
    public bool IsChartCompleted { get; }
    // Chart time of the input/automatic event, not the render frame time.
    public double EvaluationTimeMs { get; }

    public RuleContext(
        int currentHealth,
        int currentCombo,
        NoteType noteType,
        bool isFever,
        bool missGuardAvailable,
        bool isChartCompleted,
        double evaluationTimeMs = double.NaN)
    {
        CurrentHealth = currentHealth;
        CurrentCombo = currentCombo;
        NoteType = noteType;
        IsFever = isFever;
        MissGuardAvailable = missGuardAvailable;
        IsChartCompleted = isChartCompleted;
        EvaluationTimeMs = evaluationTimeMs;
    }

    public RuleContext AtTime(double chartTimeMs) => new RuleContext(
        CurrentHealth, CurrentCombo, NoteType, IsFever, MissGuardAvailable,
        IsChartCompleted, chartTimeMs);
}

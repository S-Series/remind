using REmind.Data;

public readonly struct RuleContext
{
    public int CurrentHealth { get; }
    public int CurrentCombo { get; }
    public NoteType NoteType { get; }
    public bool IsFever { get; }
    public bool MissGuardAvailable { get; }
    public bool IsChartCompleted { get; }

    public RuleContext(
        int currentHealth,
        int currentCombo,
        NoteType noteType,
        bool isFever,
        bool missGuardAvailable,
        bool isChartCompleted)
    {
        CurrentHealth = currentHealth;
        CurrentCombo = currentCombo;
        NoteType = noteType;
        IsFever = isFever;
        MissGuardAvailable = missGuardAvailable;
        IsChartCompleted = isChartCompleted;
    }
}

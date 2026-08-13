using REmind.Data;

namespace REmind.Gameplay.Input.Judgement
{
    public readonly struct NoteJudgementEvent
    {
        public NoteData Note { get; }
        public JudgeResult Result { get; }
        public TimingSide TimingSide { get; }
        public double OffsetMs { get; }
        public double EffectiveHitTimeMs { get; }
        public bool IsAutomaticMiss { get; }

        public NoteJudgementEvent(
            NoteData note,
            JudgeResult result,
            TimingSide timingSide,
            double offsetMs,
            double effectiveHitTimeMs,
            bool isAutomaticMiss)
        {
            Note = note;
            Result = result;
            TimingSide = timingSide;
            OffsetMs = offsetMs;
            EffectiveHitTimeMs = effectiveHitTimeMs;
            IsAutomaticMiss = isAutomaticMiss;
        }
    }
}

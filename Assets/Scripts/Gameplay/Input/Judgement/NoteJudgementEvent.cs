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
        /// <summary>
        /// Chart time at which the input or automatic judgement was evaluated.
        /// Rule modifiers use this instead of the later render-frame time.
        /// </summary>
        public double EvaluationTimeMs { get; }
        public bool IsAutomaticMiss { get; }

        public NoteJudgementEvent(
            NoteData note,
            JudgeResult result,
            TimingSide timingSide,
            double offsetMs,
            double effectiveHitTimeMs,
            double evaluationTimeMs,
            bool isAutomaticMiss)
        {
            Note = note;
            Result = result;
            TimingSide = timingSide;
            OffsetMs = offsetMs;
            EffectiveHitTimeMs = effectiveHitTimeMs;
            EvaluationTimeMs = evaluationTimeMs;
            IsAutomaticMiss = isAutomaticMiss;
        }
    }
}

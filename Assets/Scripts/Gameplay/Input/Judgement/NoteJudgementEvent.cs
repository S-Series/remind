using REmind.Charting;

namespace REmind.Gameplay.Input.Judgement
{
    public readonly struct NoteJudgementEvent
    {
        public PlayableNoteSnapshot ChartNote { get; }
        public int SegmentIndex { get; }
        public string NoteId => ChartNote?.Id;
        public int Lane => ChartNote?.Lane ?? -1;
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

        public NoteJudgementEvent(PlayableNoteSnapshot note, int segmentIndex,
            JudgeResult result, TimingSide timingSide, double offsetMs,
            double effectiveHitTimeMs, double evaluationTimeMs,
            bool isAutomaticMiss)
        {
            ChartNote = note;
            SegmentIndex = segmentIndex;
            Result = result;
            TimingSide = timingSide;
            OffsetMs = offsetMs;
            EffectiveHitTimeMs = effectiveHitTimeMs;
            EvaluationTimeMs = evaluationTimeMs;
            IsAutomaticMiss = isAutomaticMiss;
        }
    }
}

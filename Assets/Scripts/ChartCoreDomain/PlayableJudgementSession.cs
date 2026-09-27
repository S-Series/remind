using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    public enum ChartJudgementGrade { None, Perfect, Great, Good, Miss }

    public readonly struct ChartJudgementResolution
    {
        public ChartJudgementResolution(PlayableNoteSnapshot note, int segmentIndex,
            ChartJudgementGrade grade, double offsetMs, double targetTimeMs,
            double evaluationTimeMs, bool automaticMiss)
        {
            Note = note;
            SegmentIndex = segmentIndex;
            Grade = grade;
            OffsetMs = offsetMs;
            TargetTimeMs = targetTimeMs;
            EvaluationTimeMs = evaluationTimeMs;
            IsAutomaticMiss = automaticMiss;
        }

        public PlayableNoteSnapshot Note { get; }
        public int SegmentIndex { get; }
        public ChartJudgementGrade Grade { get; }
        public double OffsetMs { get; }
        public double TargetTimeMs { get; }
        public double EvaluationTimeMs { get; }
        public bool IsAutomaticMiss { get; }
    }

    /// <summary>
    /// Shared point/segment lifecycle. A Long interval is scored at its next
    /// point. A missed interval can be rejoined by a new press at every Mid.
    /// Window values and grade modifiers are supplied by the product rule set.
    /// </summary>
    public sealed class PlayableJudgementSession
    {
        private sealed class Entry
        {
            public PlayableNoteSnapshot Note;
            public int SegmentIndex;
            public bool Complete;
            public bool Joined;
            public bool PendingMidJoin;
            public bool Held;
            public bool JoinAttempted;
            public ChartJudgementGrade JoinGrade;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Func<PlayableNoteSnapshot, double, ChartJudgementGrade> judge;
        private readonly Func<PlayableNoteSnapshot, double> missWindow;
        private readonly Action<ChartJudgementResolution> resolved;
        private readonly double userOffsetMs;

        public PlayableJudgementSession(PlayableChartSnapshot chart,
            Func<PlayableNoteSnapshot, double, ChartJudgementGrade> judge,
            Func<PlayableNoteSnapshot, double> missWindow,
            Action<ChartJudgementResolution> resolved, double userOffsetMs = 0d)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            this.judge = judge ?? throw new ArgumentNullException(nameof(judge));
            this.missWindow = missWindow ?? throw new ArgumentNullException(nameof(missWindow));
            this.resolved = resolved ?? throw new ArgumentNullException(nameof(resolved));
            if (double.IsNaN(userOffsetMs) || double.IsInfinity(userOffsetMs))
                throw new ArgumentOutOfRangeException(nameof(userOffsetMs));
            this.userOffsetMs = userOffsetMs;
            foreach (PlayableNoteSnapshot note in chart.Notes)
                entries.Add(new Entry { Note = note });
        }

        public int PendingNoteCount
        {
            get
            {
                int count = 0;
                foreach (Entry entry in entries) if (!entry.Complete) count++;
                return count;
            }
        }

        public void Reset()
        {
            foreach (Entry entry in entries)
            {
                entry.SegmentIndex = 0;
                entry.Complete = false;
                entry.Joined = false;
                entry.PendingMidJoin = false;
                entry.Held = false;
                entry.JoinAttempted = false;
                entry.JoinGrade = ChartJudgementGrade.None;
            }
        }

        public void BreakReleasedHolds(Func<int, bool> isPressed)
        {
            if (isPressed == null) throw new ArgumentNullException(nameof(isPressed));
            foreach (Entry entry in entries)
                if (!entry.Complete && entry.Held &&
                    !isPressed(entry.Note.Lane))
                {
                    entry.Held = false;
                    entry.Joined = false;
                    entry.PendingMidJoin = false;
                }
        }

        public void Input(int lane, double chartTimeMs, bool pressed)
        {
            if (double.IsNaN(chartTimeMs) || double.IsInfinity(chartTimeMs)) return;
            foreach (Entry entry in entries)
            {
                if (entry.Complete || entry.Note.Lane != lane) continue;
                PlayableNoteSnapshot note = entry.Note;
                if (note.Points.Count == 1)
                {
                    if (!pressed) continue;
                    double offset = chartTimeMs - userOffsetMs - note.StartTimeMs;
                    ChartJudgementGrade grade = judge(note, offset);
                    if (grade == ChartJudgementGrade.None) continue;
                    Emit(entry, -1, grade, offset, note.StartTimeMs, chartTimeMs, false);
                    entry.Complete = true;
                    return;
                }

                int index = entry.SegmentIndex;
                PlayableNotePoint start = note.Points[index];
                PlayableNotePoint end = note.Points[index + 1];
                if (pressed)
                {
                    double startOffset = chartTimeMs - userOffsetMs - start.TimeMs;
                    ChartJudgementGrade grade = judge(note, startOffset);
                    if (grade != ChartJudgementGrade.None &&
                        !entry.JoinAttempted)
                    {
                        entry.Joined = true;
                        entry.Held = true;
                        entry.JoinAttempted = true;
                        entry.JoinGrade = grade;
                        return;
                    }
                    if (index + 1 < note.Points.Count - 1 && !entry.Held)
                    {
                        double midOffset = chartTimeMs - userOffsetMs - end.TimeMs;
                        grade = judge(note, midOffset);
                        if (grade != ChartJudgementGrade.None)
                        {
                            entry.PendingMidJoin = true;
                            entry.Held = true;
                            entry.JoinGrade = grade;
                            return;
                        }
                    }
                }
                else
                {
                    entry.Held = false;
                    double endOffset = chartTimeMs - userOffsetMs - end.TimeMs;
                    if (index + 1 == note.Points.Count - 1 && entry.Joined)
                    {
                        ChartJudgementGrade grade = judge(note, endOffset);
                        if (grade != ChartJudgementGrade.None)
                        {
                            ChartJudgementGrade intervalGrade =
                                (ChartJudgementGrade)Math.Max((int)grade,
                                    (int)entry.JoinGrade);
                            Emit(entry, index, intervalGrade, endOffset,
                                end.TimeMs,
                                chartTimeMs, false);
                            entry.Complete = true;
                            return;
                        }
                    }
                    // Releasing outside the End window breaks the interval.
                    entry.Joined = false;
                    entry.PendingMidJoin = false;
                }
            }
        }

        public double NextAutomaticTime(bool autoPlay)
        {
            double next = double.PositiveInfinity;
            foreach (Entry entry in entries)
            {
                if (entry.Complete) continue;
                PlayableNoteSnapshot note = entry.Note;
                if (note.Points.Count == 1)
                {
                    double time = note.StartTimeMs + userOffsetMs +
                        (autoPlay ? 0d : missWindow(note) + 0.000001d);
                    next = Math.Min(next, time);
                }
                else
                {
                    int endIndex = entry.SegmentIndex + 1;
                    bool isEnd = endIndex == note.Points.Count - 1;
                    double time = note.Points[endIndex].TimeMs + userOffsetMs +
                        (autoPlay || !isEnd ? 0d :
                            missWindow(note) + 0.000001d);
                    next = Math.Min(next, time);
                }
            }
            return next;
        }

        public void ProcessAutomatic(double timeMs, bool autoPlay)
        {
            foreach (Entry entry in entries)
            {
                if (entry.Complete) continue;
                PlayableNoteSnapshot note = entry.Note;
                bool single = note.Points.Count == 1;
                int index = entry.SegmentIndex;
                int endIndex = single ? 0 : index + 1;
                bool isEnd = endIndex == note.Points.Count - 1;
                double target = note.Points[endIndex].TimeMs;
                double deadline = target + userOffsetMs +
                    (autoPlay || !isEnd ? 0d :
                        missWindow(note) + 0.000001d);
                if (timeMs < deadline) continue;

                if (single)
                {
                    Emit(entry, -1, autoPlay ? ChartJudgementGrade.Perfect :
                        ChartJudgementGrade.Miss, timeMs - userOffsetMs - target,
                        target, timeMs, !autoPlay);
                    entry.Complete = true;
                    continue;
                }

                ChartJudgementGrade grade = autoPlay
                    ? ChartJudgementGrade.Perfect
                    : !isEnd && entry.Joined && entry.Held
                        ? entry.JoinGrade
                        : ChartJudgementGrade.Miss;
                Emit(entry, index, grade, timeMs - userOffsetMs - target,
                    target, timeMs, grade == ChartJudgementGrade.Miss);
                if (isEnd)
                {
                    entry.Complete = true;
                    continue;
                }
                entry.SegmentIndex++;
                bool rejoinedAtMid = entry.PendingMidJoin;
                entry.Joined = autoPlay || (entry.Joined && entry.Held) ||
                    rejoinedAtMid;
                entry.JoinAttempted = entry.Joined;
                if (!rejoinedAtMid && entry.Joined)
                    entry.JoinGrade = ChartJudgementGrade.Perfect;
                entry.PendingMidJoin = false;
            }
        }

        private void Emit(Entry entry, int segmentIndex,
            ChartJudgementGrade grade, double offset, double target,
            double evaluated, bool automaticMiss)
        {
            resolved(new ChartJudgementResolution(entry.Note, segmentIndex,
                grade, offset, target, evaluated, automaticMiss));
        }
    }
}

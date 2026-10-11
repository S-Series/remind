using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    /// <summary>Song-time position tracking; scores remain on their original target times.</summary>
    internal sealed class KnobJudgementSession
    {
        private sealed class State
        {
            public PlayableNoteSnapshot Note;
            public bool[] Resolved;
            public bool[] Reserved;
            public bool[] Steps;
            public int ResolvedCount;
            public bool Initialized;
            public double SnapUntil = double.NegativeInfinity;
            public double? EndX;
        }

        private readonly List<State> states = new List<State>();
        private readonly bool[] held = new bool[3];
        private readonly TimingMap timing;
        private readonly Action<ChartJudgementResolution> resolved;
        private readonly double userOffsetMs;
        private double playerX;
        private double lastTime = double.NegativeInfinity;
        private double displayTime = double.NegativeInfinity;
        private bool displayAutoPlay;
        private State cursorOwner;
        private int generation;
        private bool halted;

        public KnobJudgementSession(PlayableChartSnapshot chart,
            Action<ChartJudgementResolution> resolved, double userOffsetMs)
        {
            timing = chart.TimingMap;
            this.resolved = resolved;
            this.userOffsetMs = userOffsetMs;
            foreach (PlayableNoteSnapshot note in chart.Notes)
                if (note.Kind == ChartNoteKind.Knob)
                    states.Add(new State { Note = note, Resolved = new bool[note.KnobTargets.Count],
                        Reserved = new bool[note.KnobTargets.Count], Steps = new bool[note.Points.Count] });
        }

        public double PlayerX => double.IsNegativeInfinity(displayTime) ? playerX :
            CalculatePosition(Math.Max(displayTime, lastTime), displayAutoPlay, false);
        public string PlayerNoteId => cursorOwner?.Note.Id;
        public bool IsCentralInputHeld => held[0] || held[1] || held[2];

        public int PendingNoteCount
        {
            get
            {
                int count = 0;
                foreach (State state in states)
                    if (state.ResolvedCount < state.Resolved.Length) count++;
                return count;
            }
        }

        public void Reset()
        {
            unchecked { generation++; }
            halted = false;
            Array.Clear(held, 0, held.Length);
            playerX = 0d;
            lastTime = displayTime = double.NegativeInfinity;
            displayAutoPlay = false;
            cursorOwner = null;
            foreach (State state in states)
            {
                Array.Clear(state.Resolved, 0, state.Resolved.Length);
                Array.Clear(state.Reserved, 0, state.Reserved.Length);
                Array.Clear(state.Steps, 0, state.Steps.Length);
                state.ResolvedCount = 0;
                state.Initialized = false;
                state.SnapUntil = double.NegativeInfinity;
                state.EndX = null;
            }
        }

        public void Halt()
        {
            playerX = PlayerX;
            displayTime = double.NegativeInfinity;
            halted = true;
            unchecked { generation++; }
        }

        public void UpdateDisplayTime(double time, bool autoPlay)
        {
            if (halted || double.IsNaN(time) || double.IsInfinity(time)) return;
            // Projection preserves next-frame device timestamps preceding the render watermark.
            displayTime = time;
            displayAutoPlay = autoPlay;
        }

        public void SynchronizeHeld(Func<int, bool> isPressed, double? chartTimeMs)
        {
            if (halted) return;
            if (chartTimeMs.HasValue) AdvanceTo(chartTimeMs.Value, false);
            for (int side = 0; side < held.Length; side++)
                held[side] = isPressed((int)ChartLane.GroundLeft + side);
        }

        public bool UpdateInput(int lane, double time, bool pressed)
        {
            int side = lane - (int)ChartLane.GroundLeft;
            if (halted || side < 0 || side > 2) return false;
            AdvanceTo(time, false);
            bool fresh = pressed && !held[side];
            held[side] = pressed;
            return fresh;
        }

        public bool TryConsumePress(int lane, double time)
        {
            int side = lane - (int)ChartLane.GroundLeft;
            if (halted || side < 0 || side > 1) return false;
            double chartTime = time - userOffsetMs;
            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                State state = states[stateIndex];
                bool nextStarted = stateIndex + 1 < states.Count &&
                    states[stateIndex + 1].Note.StartTimeMs <= chartTime;
                if (chartTime > state.Note.EndTimeMs && nextStarted) continue;
                for (int point = 1; point < state.Note.Points.Count; point++)
                {
                    PlayableNotePoint end = state.Note.Points[point];
                    double delta = end.KnobX - state.Note.Points[point - 1].KnobX;
                    if (end.KnobInterpolation != ChartKnobInterpolation.Step || state.Steps[point] ||
                        delta == 0d || (delta < 0d ? 0 : 1) != side ||
                        Math.Abs(chartTime - end.TimeMs) > KnobJudgementRules.StepInputWindowMs) continue;
                    state.Steps[point] = true;
                    double destination = KnobJudgementRules.ToPlayerX(end.KnobX);
                    // A touching next Start has already initialized its own player value.
                    if (!nextStarted)
                    {
                        playerX = destination;
                        cursorOwner = state;
                        // Only an accepted pre-start Step can replace this note's Start initialization.
                        if (chartTime < state.Note.StartTimeMs)
                        {
                            state.Initialized = true;
                            for (int index = 0; index < state.Resolved.Length; index++)
                                if (state.Note.KnobTargets[index].Kind == JudgementTargetKind.KnobStart &&
                                    !state.Resolved[index]) state.Reserved[index] = true;
                        }
                    }
                    if (chartTime >= state.Note.EndTimeMs) state.EndX = destination;
                    if (chartTime < end.TimeMs)
                    {
                        state.SnapUntil = end.TimeMs;
                        for (int index = 0; index < state.Resolved.Length; index++)
                        {
                            JudgementTarget target = state.Note.KnobTargets[index];
                            if (!state.Resolved[index] && target.TargetTimeMs >= chartTime &&
                                target.TargetTimeMs < end.TimeMs &&
                                KnobPathEvaluator.SegmentIndex(state.Note, target.TargetTimeMs) == point - 1)
                                state.Reserved[index] = true;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public double NextAutomaticTime(bool autoPlay)
        {
            if (halted) return double.PositiveInfinity;
            double next = double.PositiveInfinity;
            foreach (State state in states)
                for (int index = 0; index < state.Resolved.Length; index++)
                    if (!state.Resolved[index])
                        next = Math.Min(next, state.Note.KnobTargets[index].TargetTimeMs + userOffsetMs);
            return next;
        }

        public void ProcessAutomatic(double time, bool autoPlay)
        {
            int currentGeneration = generation;
            while (!halted && currentGeneration == generation)
            {
                State selected = null;
                int selectedIndex = -1;
                double next = double.PositiveInfinity;
                foreach (State state in states)
                    for (int index = 0; index < state.Resolved.Length; index++)
                    {
                        if (state.Resolved[index]) continue;
                        double deadline = state.Note.KnobTargets[index].TargetTimeMs + userOffsetMs;
                        if (deadline >= next) continue;
                        next = deadline;
                        selected = state;
                        selectedIndex = index;
                    }
                if (selected == null || next > time) break;
                AdvanceTo(next, autoPlay);
                JudgementTarget target = selected.Note.KnobTargets[selectedIndex];
                double actual = target.TargetTimeMs == selected.Note.EndTimeMs && selected.EndX.HasValue
                    ? selected.EndX.Value : playerX;
                double wanted = KnobPathEvaluator.TargetX(selected.Note, timing, target.TargetTimeMs);
                bool success = autoPlay || selected.Reserved[selectedIndex] ||
                    Math.Abs(actual - wanted) <= KnobJudgementRules.PositionTolerance + 0.000000001d;
                selected.Resolved[selectedIndex] = true;
                selected.ResolvedCount++;
                resolved(new ChartJudgementResolution(selected.Note, selectedIndex,
                    success ? KnobJudgementRules.PositionSuccessGrade : ChartJudgementGrade.Miss,
                    0d, target.TargetTimeMs, next, !success,
                    selected.ResolvedCount == selected.Resolved.Length, target.Kind));
            }
        }

        private void AdvanceTo(double time, bool autoPlay)
        {
            if (time < lastTime) return;
            playerX = CalculatePosition(time, autoPlay, true);
            lastTime = time;
        }

        private double CalculatePosition(double time, bool autoPlay, bool commit)
        {
            double value = playerX;
            double chartTime = time - userOffsetMs;
            double previous = lastTime - userOffsetMs;
            foreach (State state in states)
            {
                if (state.Note.StartTimeMs > chartTime) break;
                if (state.Note.EndTimeMs < previous) continue;
                bool previewLocked = cursorOwner != null && cursorOwner != state &&
                    state.Note.StartTimeMs < cursorOwner.SnapUntil;
                if (!state.Initialized)
                {
                    if (!previewLocked) value = KnobJudgementRules.ToPlayerX(state.Note.Points[0].KnobX);
                    if (commit)
                    {
                        state.Initialized = true;
                    }
                }
                if (commit && previous < state.Note.StartTimeMs && !previewLocked)
                    cursorOwner = state;
                double from = Math.Max(previous, state.Note.StartTimeMs);
                double to = Math.Min(chartTime, state.Note.EndTimeMs.Value);
                if (!autoPlay && cursorOwner != null)
                    from = Math.Max(from, Math.Min(to, cursorOwner.SnapUntil));
                if (autoPlay) value = KnobPathEvaluator.TargetX(state.Note, timing, to);
                else if (to > from) Move(ref value, state, from, to);
                if (commit && previous < state.Note.EndTimeMs && chartTime >= state.Note.EndTimeMs)
                    state.EndX = value;
            }
            return value;
        }

        private void Move(ref double value, State state, double from, double to)
        {
            if (from < state.SnapUntil) from = Math.Min(to, state.SnapUntil);
            while (from < to)
            {
                int segment = KnobPathEvaluator.SegmentIndex(state.Note, from);
                double end = Math.Min(to, state.Note.Points[segment + 1].TimeMs);
                foreach (TimingPoint point in timing.Points)
                    if (point.StartTimeMs > from && point.StartTimeMs < end) end = point.StartTimeMs;
                PlayableNotePoint source = state.Note.Points[segment];
                PlayableNotePoint destination = state.Note.Points[segment + 1];
                if (KnobPathEvaluator.IsCurve(destination.KnobInterpolation))
                {
                    double fraction = (timing.PositionAtTime(from) - source.Position) /
                        (destination.Position - source.Position);
                    int nextSample = Math.Min(KnobPathEvaluator.CurveSegments,
                        (int)Math.Floor(fraction * KnobPathEvaluator.CurveSegments + 0.000000001d) + 1);
                    double sampleTime = timing.TimeAtPosition(source.Position +
                        (destination.Position - source.Position) * (double)nextSample / KnobPathEvaluator.CurveSegments);
                    if (sampleTime > from) end = Math.Min(end, sampleTime);
                }
                double target = KnobPathEvaluator.SegmentX(state.Note, timing, segment, from);
                double targetEnd = KnobPathEvaluator.SegmentX(state.Note, timing, segment, end);
                MoveLinear(ref value, target, (targetEnd - target) / (end - from), end - from);
                from = end;
            }
        }

        private void MoveLinear(ref double value, double target, double velocity, double duration)
        {
            double tolerance = KnobJudgementRules.PositionTolerance;
            bool single = held[0] != held[1];
            bool correct = single && (velocity < 0d ? held[0] : held[1]);
            double error = value - target;
            if (Math.Abs(error) <= tolerance + 0.000000001d)
            {
                if (Math.Abs(velocity) < 0.000000001d) return;
                if (correct) { value = target + velocity * duration; return; }
            }
            double manual = single ? (held[0] ? -1d : 1d) *
                KnobJudgementRules.ManualSpeedPerSecond / 1000d : 0d;
            double maximum = KnobJudgementRules.MaximumPlayerX;
            if ((value >= maximum && manual > 0d) || (value <= -maximum && manual < 0d)) manual = 0d;
            double boundTime = manual == 0d ? double.PositiveInfinity :
                ((manual > 0d ? maximum : -maximum) - value) / manual;
            if (boundTime > 0d && boundTime < duration)
            {
                MoveLinear(ref value, target, velocity, boundTime);
                MoveLinear(ref value, target + velocity * boundTime, velocity, duration - boundTime);
                return;
            }
            double relative = manual - velocity;
            if ((correct || Math.Abs(velocity) < 0.000000001d) && relative != 0d)
            {
                double contact = ((error > tolerance ? tolerance : -tolerance) - error) / relative;
                if (Math.Abs(error) > tolerance && contact >= 0d && contact <= duration)
                {
                    value = Math.Abs(velocity) < 0.000000001d
                        ? value + manual * contact : target + velocity * duration;
                    return;
                }
            }
            value = Math.Max(-maximum, Math.Min(maximum, value + manual * duration));
        }
    }
}

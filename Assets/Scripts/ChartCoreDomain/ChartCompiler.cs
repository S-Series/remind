using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    public enum CompileIssueSeverity
    {
        Warning = 0,
        Error = 1
    }

    public sealed class CompileIssue
    {
        public CompileIssue(
            string code,
            string message,
            CompileIssueSeverity severity = CompileIssueSeverity.Error)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Severity = severity;
        }

        public string Code { get; }
        public string Message { get; }
        public CompileIssueSeverity Severity { get; }
    }

    public sealed class ChartCompileResult
    {
        internal ChartCompileResult(
            PlayableChartSnapshot snapshot,
            CompileIssue[] issues)
        {
            Snapshot = snapshot;
            Issues = Array.AsReadOnly(
                issues != null
                    ? (CompileIssue[])issues.Clone()
                    : Array.Empty<CompileIssue>());
        }

        public PlayableChartSnapshot Snapshot { get; }
        public IReadOnlyList<CompileIssue> Issues { get; }
        public bool Succeeded => Snapshot != null;
    }

    public static class ChartCompiler
    {
        public const double CameraSpinDurationAt120BpmMs = 175d;
        private const double CameraSpinReferenceBpm = 120d;

        public static ChartCompileResult Compile(
            ChartDocument document,
            double floorUnitsPerPosition = 1d)
        {
            List<CompileIssue> issues = new List<CompileIssue>();

            if (document == null)
            {
                issues.Add(new CompileIssue(
                    "CHART_NULL",
                    "ChartDocument is missing."));
                return Failed(issues);
            }

            if (document.PositionUnitsPerMeasure <= 0)
            {
                issues.Add(new CompileIssue(
                    "TIMING_POSITION_RESOLUTION",
                    "PositionUnitsPerMeasure must be greater than zero."));
            }

            if (document.BeatsPerMeasure <= 0)
            {
                issues.Add(new CompileIssue(
                    "TIMING_BEATS_PER_MEASURE",
                    "BeatsPerMeasure must be greater than zero."));
            }

            if (!IsFinitePositive(document.BaseBpm))
            {
                issues.Add(new CompileIssue(
                    "TIMING_BASE_BPM",
                    "Base BPM must be positive and finite."));
            }

            if (!IsFinitePositive(floorUnitsPerPosition))
            {
                issues.Add(new CompileIssue(
                    "SCROLL_SCALE",
                    "Floor units per position must be positive and finite."));
            }

            if (HasErrors(issues))
            {
                return Failed(issues);
            }

            double positionUnitsPerBeat =
                document.PositionUnitsPerMeasure /
                (double)document.BeatsPerMeasure;
            TimingMap timingMap = CompileTimingMap(
                document,
                positionUnitsPerBeat,
                issues);

            if (timingMap == null || HasErrors(issues))
            {
                return Failed(issues);
            }

            ScrollMap scrollMap = CompileScrollMap(
                document,
                timingMap,
                floorUnitsPerPosition,
                issues);

            if (scrollMap == null || HasErrors(issues))
            {
                return Failed(issues);
            }

            CameraMotionMap cameraMotionMap = CompileCameraMotionMap(
                document,
                timingMap,
                scrollMap,
                issues);

            if (cameraMotionMap == null || HasErrors(issues))
            {
                return Failed(issues);
            }

            CompileNotes(
                document,
                timingMap,
                scrollMap,
                issues,
                out List<PlayableNoteSnapshot> playableNotes,
                out List<JudgementTarget> judgementTargets);

            if (HasErrors(issues))
            {
                return Failed(issues);
            }

            playableNotes.Sort(CompareNotes);
            judgementTargets.Sort(CompareTargets);

            PlayableChartSnapshot snapshot = new PlayableChartSnapshot(
                timingMap,
                scrollMap,
                cameraMotionMap,
                playableNotes.ToArray(),
                judgementTargets.ToArray());
            return new ChartCompileResult(snapshot, issues.ToArray());
        }

        private static TimingMap CompileTimingMap(
            ChartDocument document,
            double positionUnitsPerBeat,
            List<CompileIssue> issues)
        {
            List<ChartTimingEvent> timingEvents =
                new List<ChartTimingEvent>(document.TimingEvents.Count);

            for (int i = 0; i < document.TimingEvents.Count; i++)
            {
                ChartTimingEvent timingEvent = document.TimingEvents[i];

                if (timingEvent == null)
                {
                    issues.Add(new CompileIssue(
                        "TIMING_EVENT_NULL",
                        $"Timing event {i} is missing."));
                    continue;
                }

                if (timingEvent.Position < 0)
                {
                    issues.Add(new CompileIssue(
                        "TIMING_EVENT_POSITION",
                        $"Timing event {i} has a negative position."));
                }

                if (!IsFinitePositive(timingEvent.Bpm))
                {
                    issues.Add(new CompileIssue(
                        "TIMING_EVENT_BPM",
                        $"Timing event {i} BPM must be positive and finite."));
                }

                timingEvents.Add(timingEvent);
            }

            if (HasErrors(issues))
            {
                return null;
            }

            timingEvents.Sort(
                (left, right) => left.Position.CompareTo(right.Position));

            for (int i = 1; i < timingEvents.Count; i++)
            {
                if (timingEvents[i - 1].Position == timingEvents[i].Position)
                {
                    issues.Add(new CompileIssue(
                        "TIMING_EVENT_DUPLICATE",
                        $"Multiple BPM events exist at position " +
                        $"{timingEvents[i].Position}."));
                }
            }

            if (HasErrors(issues))
            {
                return null;
            }

            int eventIndex = 0;
            double initialBpm = document.BaseBpm;

            if (timingEvents.Count > 0 && timingEvents[0].Position == 0)
            {
                initialBpm = timingEvents[0].Bpm;
                eventIndex = 1;
            }

            List<TimingPoint> points = new List<TimingPoint>
            {
                new TimingPoint(0d, 0d, initialBpm)
            };

            for (; eventIndex < timingEvents.Count; eventIndex++)
            {
                ChartTimingEvent timingEvent = timingEvents[eventIndex];
                TimingPoint previous = points[points.Count - 1];
                double startTimeMs = previous.StartTimeMs +
                    TimingMap.PositionDeltaToMilliseconds(
                        timingEvent.Position - previous.StartPosition,
                        previous.Bpm,
                        positionUnitsPerBeat);
                points.Add(new TimingPoint(
                    startTimeMs,
                    timingEvent.Position,
                    timingEvent.Bpm));
            }

            return new TimingMap(points, positionUnitsPerBeat);
        }

        private static ScrollMap CompileScrollMap(
            ChartDocument document,
            TimingMap timingMap,
            double floorUnitsPerPosition,
            List<CompileIssue> issues)
        {
            List<ChartLineSpeedEvent> lineSpeedEvents =
                new List<ChartLineSpeedEvent>(
                    document.LineSpeedEvents.Count);

            for (int i = 0; i < document.LineSpeedEvents.Count; i++)
            {
                ChartLineSpeedEvent lineSpeedEvent =
                    document.LineSpeedEvents[i];

                if (lineSpeedEvent == null)
                {
                    issues.Add(new CompileIssue(
                        "LINE_SPEED_EVENT_NULL",
                        $"Line Speed event {i} is missing."));
                    continue;
                }

                if (lineSpeedEvent.Position < 0)
                {
                    issues.Add(new CompileIssue(
                        "LINE_SPEED_EVENT_POSITION",
                        $"Line Speed event {i} has a negative position."));
                }

                if (!IsFinitePositive(lineSpeedEvent.Multiplier))
                {
                    issues.Add(new CompileIssue(
                        "LINE_SPEED_EVENT_VALUE",
                        $"Line Speed event {i} must be positive and finite."));
                }

                lineSpeedEvents.Add(lineSpeedEvent);
            }

            if (HasErrors(issues))
            {
                return null;
            }

            lineSpeedEvents.Sort(
                (left, right) => left.Position.CompareTo(right.Position));

            for (int i = 1; i < lineSpeedEvents.Count; i++)
            {
                if (lineSpeedEvents[i - 1].Position ==
                    lineSpeedEvents[i].Position)
                {
                    issues.Add(new CompileIssue(
                        "LINE_SPEED_EVENT_DUPLICATE",
                        $"Multiple Line Speed events exist at position " +
                        $"{lineSpeedEvents[i].Position}."));
                }
            }

            if (HasErrors(issues))
            {
                return null;
            }

            SortedSet<int> boundaryPositions = new SortedSet<int> { 0 };

            for (int i = 0; i < timingMap.Points.Count; i++)
            {
                boundaryPositions.Add(checked((int)Math.Round(
                    timingMap.Points[i].StartPosition,
                    MidpointRounding.AwayFromZero)));
            }

            Dictionary<int, double> lineSpeedAtPosition =
                new Dictionary<int, double>();

            for (int i = 0; i < lineSpeedEvents.Count; i++)
            {
                ChartLineSpeedEvent lineSpeedEvent = lineSpeedEvents[i];
                boundaryPositions.Add(lineSpeedEvent.Position);
                lineSpeedAtPosition.Add(
                    lineSpeedEvent.Position,
                    lineSpeedEvent.Multiplier);
            }

            double currentLineSpeed = 1d;
            double previousTimeMs = 0d;
            double previousFloorPosition = 0d;
            double previousFloorUnitsPerMillisecond = 0d;
            bool isFirst = true;
            List<ScrollPoint> points =
                new List<ScrollPoint>(boundaryPositions.Count);

            foreach (int position in boundaryPositions)
            {
                double startTimeMs = timingMap.TimeAtPosition(position);
                double startFloorPosition = isFirst
                    ? 0d
                    : previousFloorPosition +
                      (startTimeMs - previousTimeMs) *
                      previousFloorUnitsPerMillisecond;

                if (lineSpeedAtPosition.TryGetValue(
                        position,
                        out double changedLineSpeed))
                {
                    currentLineSpeed = changedLineSpeed;
                }

                TimingPoint timingPoint =
                    timingMap.GetPointAtPosition(position);
                double floorUnitsPerMillisecond =
                    TimingMap.MillisecondsToPositionDelta(
                        1d,
                        timingPoint.Bpm,
                        timingMap.PositionUnitsPerBeat) *
                    floorUnitsPerPosition *
                    currentLineSpeed;
                points.Add(new ScrollPoint(
                    startTimeMs,
                    startFloorPosition,
                    timingPoint.Bpm,
                    currentLineSpeed,
                    floorUnitsPerMillisecond));
                previousTimeMs = startTimeMs;
                previousFloorPosition = startFloorPosition;
                previousFloorUnitsPerMillisecond =
                    floorUnitsPerMillisecond;
                isFirst = false;
            }

            return new ScrollMap(timingMap, points);
        }

        private static CameraMotionMap CompileCameraMotionMap(
            ChartDocument document,
            TimingMap timingMap,
            ScrollMap scrollMap,
            List<CompileIssue> issues)
        {
            List<ChartCameraEvent> cameraEvents =
                new List<ChartCameraEvent>(document.CameraEvents.Count);

            for (int i = 0; i < document.CameraEvents.Count; i++)
            {
                ChartCameraEvent cameraEvent = document.CameraEvents[i];

                if (cameraEvent == null)
                {
                    issues.Add(new CompileIssue(
                        "CAMERA_EVENT_NULL",
                        $"Camera event {i} is missing."));
                    continue;
                }

                if (cameraEvent.Position < 0)
                {
                    issues.Add(new CompileIssue(
                        "CAMERA_EVENT_POSITION",
                        $"Camera event {i} has a negative position."));
                }

                if (!IsFinite(cameraEvent.OffsetX))
                {
                    issues.Add(new CompileIssue(
                        "CAMERA_EVENT_OFFSET",
                        $"Camera event {i} offset X must be finite."));
                }

                if (!Enum.IsDefined(
                        typeof(ChartCameraSpinDirection),
                        cameraEvent.SpinDirection))
                {
                    issues.Add(new CompileIssue(
                        "CAMERA_EVENT_SPIN",
                        $"Camera event {i} has an unsupported spin " +
                        $"direction: {cameraEvent.SpinDirection}."));
                }

                cameraEvents.Add(cameraEvent);
            }

            if (HasErrors(issues))
            {
                return null;
            }

            cameraEvents.Sort(
                (left, right) => left.Position.CompareTo(right.Position));

            for (int i = 1; i < cameraEvents.Count; i++)
            {
                if (cameraEvents[i - 1].Position ==
                    cameraEvents[i].Position)
                {
                    issues.Add(new CompileIssue(
                        "CAMERA_EVENT_DUPLICATE",
                        $"Multiple Camera events exist at position " +
                        $"{cameraEvents[i].Position}."));
                }
            }

            if (HasErrors(issues))
            {
                return null;
            }

            List<CameraMotionPoint> points =
                new List<CameraMotionPoint>(cameraEvents.Count);

            for (int i = 0; i < cameraEvents.Count; i++)
            {
                ChartCameraEvent cameraEvent = cameraEvents[i];
                double timeMs = timingMap.TimeAtPosition(
                    cameraEvent.Position);
                double bpm = timingMap
                    .GetPointAtPosition(cameraEvent.Position)
                    .Bpm;
                double spinDurationMs =
                    CameraSpinDurationAt120BpmMs *
                    CameraSpinReferenceBpm /
                    bpm;
                points.Add(new CameraMotionPoint(
                    cameraEvent.Position,
                    timeMs,
                    scrollMap.FloorPositionAtTime(timeMs),
                    cameraEvent.OffsetX,
                    cameraEvent.SpinDirection,
                    spinDurationMs));
            }

            return new CameraMotionMap(points);
        }

        private static void CompileNotes(
            ChartDocument document,
            TimingMap timingMap,
            ScrollMap scrollMap,
            List<CompileIssue> issues,
            out List<PlayableNoteSnapshot> playableNotes,
            out List<JudgementTarget> judgementTargets)
        {
            playableNotes = new List<PlayableNoteSnapshot>(
                document.Notes.Count);
            judgementTargets = new List<JudgementTarget>(
                document.Notes.Count * 2);
            HashSet<string> noteIds =
                new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < document.Notes.Count; i++)
            {
                ChartDocumentNote note = document.Notes[i];

                if (!ValidateNote(note, i, noteIds, issues))
                {
                    continue;
                }

                double startTimeMs =
                    timingMap.TimeAtPosition(note.StartPosition);
                bool isLong = IsLong(note.Kind);
                double? endTimeMs = isLong
                    ? timingMap.TimeAtPosition(note.EndPosition.Value)
                    : (double?)null;
                double startFloorPosition =
                    scrollMap.FloorPositionAtTime(startTimeMs);
                double? endFloorPosition = isLong
                    ? scrollMap.FloorPositionAtTime(endTimeMs.Value)
                    : (double?)null;
                playableNotes.Add(new PlayableNoteSnapshot(
                    note.Id,
                    note.Kind,
                    note.Lane,
                    note.StartPosition,
                    note.EndPosition,
                    startTimeMs,
                    endTimeMs,
                    startFloorPosition,
                    endFloorPosition));

                if (!isLong)
                {
                    judgementTargets.Add(new JudgementTarget(
                        note.Id + "@tap",
                        note.Id,
                        note.Kind,
                        JudgementTargetKind.Tap,
                        note.Lane,
                        note.StartPosition,
                        startTimeMs,
                        startFloorPosition));
                    continue;
                }

                judgementTargets.Add(new JudgementTarget(
                    note.Id + "@start",
                    note.Id,
                    note.Kind,
                    JudgementTargetKind.HoldStart,
                    note.Lane,
                    note.StartPosition,
                    startTimeMs,
                    startFloorPosition));
                judgementTargets.Add(new JudgementTarget(
                    note.Id + "@end",
                    note.Id,
                    note.Kind,
                    JudgementTargetKind.HoldEnd,
                    note.Lane,
                    note.EndPosition.Value,
                    endTimeMs.Value,
                    endFloorPosition.Value));
            }
        }

        private static bool ValidateNote(
            ChartDocumentNote note,
            int index,
            HashSet<string> noteIds,
            List<CompileIssue> issues)
        {
            bool valid = true;

            if (note == null)
            {
                issues.Add(new CompileIssue(
                    "NOTE_NULL",
                    $"Note {index} is missing."));
                return false;
            }

            if (string.IsNullOrWhiteSpace(note.Id) ||
                !noteIds.Add(note.Id))
            {
                issues.Add(new CompileIssue(
                    "NOTE_ID",
                    $"Note {index} has a missing or duplicate ID."));
                valid = false;
            }

            if (!Enum.IsDefined(typeof(ChartNoteKind), note.Kind))
            {
                issues.Add(new CompileIssue(
                    "NOTE_KIND",
                    $"Note '{note.Id}' has an unsupported kind."));
                valid = false;
            }

            if (!ChartLaneLayout.IsValid(note.Lane))
            {
                issues.Add(new CompileIssue(
                    "NOTE_LANE",
                    $"Note '{note.Id}' lane must be between 0 and " +
                    $"{ChartLaneLayout.LaneCount - 1}."));
                valid = false;
            }

            if (note.StartPosition < 0)
            {
                issues.Add(new CompileIssue(
                    "NOTE_START_POSITION",
                    $"Note '{note.Id}' has a negative start position."));
                valid = false;
            }

            bool isLong = IsLong(note.Kind);

            if (isLong &&
                (!note.EndPosition.HasValue ||
                 note.EndPosition.Value <= note.StartPosition))
            {
                issues.Add(new CompileIssue(
                    "NOTE_END_POSITION",
                    $"Long note '{note.Id}' requires an end position after " +
                    "its start."));
                valid = false;
            }
            else if (!isLong && note.EndPosition.HasValue)
            {
                issues.Add(new CompileIssue(
                    "NOTE_UNEXPECTED_END",
                    $"Single note '{note.Id}' cannot have an end position."));
                valid = false;
            }

            return valid;
        }

        private static bool IsLong(ChartNoteKind kind)
        {
            return kind == ChartNoteKind.Hold ||
                kind == ChartNoteKind.LongScratch;
        }

        private static int CompareNotes(
            PlayableNoteSnapshot left,
            PlayableNoteSnapshot right)
        {
            int timeComparison =
                left.StartTimeMs.CompareTo(right.StartTimeMs);
            return timeComparison != 0
                ? timeComparison
                : string.CompareOrdinal(left.Id, right.Id);
        }

        private static int CompareTargets(
            JudgementTarget left,
            JudgementTarget right)
        {
            int timeComparison =
                left.TargetTimeMs.CompareTo(right.TargetTimeMs);

            if (timeComparison != 0)
            {
                return timeComparison;
            }

            int laneComparison = left.Lane.CompareTo(right.Lane);
            return laneComparison != 0
                ? laneComparison
                : string.CompareOrdinal(left.Id, right.Id);
        }

        private static ChartCompileResult Failed(
            List<CompileIssue> issues)
        {
            return new ChartCompileResult(null, issues.ToArray());
        }

        private static bool HasErrors(IReadOnlyList<CompileIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == CompileIssueSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0d;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

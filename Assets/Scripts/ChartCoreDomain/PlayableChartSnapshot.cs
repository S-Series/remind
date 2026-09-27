using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    public enum JudgementTargetKind
    {
        Tap = 0,
        HoldStart = 1,
        HoldEnd = 2,
        HoldMid = 3
    }

    public sealed class PlayableNotePoint
    {
        internal PlayableNotePoint(ChartNotePoint source, double timeMs,
            double floorPosition)
        {
            Position = source.Position;
            Kind = source.Kind;
            Motion = source.Motion;
            MoveAmount = source.MoveAmount;
            TimeMs = timeMs;
            FloorPosition = floorPosition;
        }

        public int Position { get; }
        public ChartNotePointKind Kind { get; }
        public ChartScratchMotionKind Motion { get; }
        public int MoveAmount { get; }
        public double TimeMs { get; }
        public double FloorPosition { get; }
    }

    public sealed class PlayableNoteSnapshot
    {
        internal PlayableNoteSnapshot(
            string id,
            ChartNoteKind kind,
            int lane,
            int startPosition,
            int? endPosition,
            double startTimeMs,
            double? endTimeMs,
            double startFloorPosition,
            double? endFloorPosition,
            PlayableNotePoint[] points)
        {
            Id = id;
            Kind = kind;
            Lane = lane;
            StartPosition = startPosition;
            EndPosition = endPosition;
            StartTimeMs = startTimeMs;
            EndTimeMs = endTimeMs;
            StartFloorPosition = startFloorPosition;
            EndFloorPosition = endFloorPosition;
            Points = Array.AsReadOnly((PlayableNotePoint[])points.Clone());
        }

        public string Id { get; }
        public ChartNoteKind Kind { get; }
        public int Lane { get; }
        public int StartPosition { get; }
        public int? EndPosition { get; }
        public double StartTimeMs { get; }
        public double? EndTimeMs { get; }
        public double StartFloorPosition { get; }
        public double? EndFloorPosition { get; }
        public IReadOnlyList<PlayableNotePoint> Points { get; }
    }

    public sealed class JudgementTarget
    {
        internal JudgementTarget(
            string id,
            string noteId,
            ChartNoteKind noteKind,
            JudgementTargetKind kind,
            int lane,
            int position,
            double targetTimeMs,
            double floorPosition)
        {
            Id = id;
            NoteId = noteId;
            NoteKind = noteKind;
            Kind = kind;
            Lane = lane;
            Position = position;
            TargetTimeMs = targetTimeMs;
            FloorPosition = floorPosition;
        }

        public string Id { get; }
        public string NoteId { get; }
        public ChartNoteKind NoteKind { get; }
        public JudgementTargetKind Kind { get; }
        public int Lane { get; }
        public int Position { get; }
        public double TargetTimeMs { get; }
        public double FloorPosition { get; }
    }

    /// <summary>One independently scored interval of a Long note.</summary>
    public sealed class JudgementSegment
    {
        internal JudgementSegment(PlayableNoteSnapshot note, int index)
        {
            NoteId = note.Id;
            NoteKind = note.Kind;
            Lane = note.Lane;
            Index = index;
            StartPosition = note.Points[index].Position;
            EndPosition = note.Points[index + 1].Position;
            StartTimeMs = note.Points[index].TimeMs;
            EndTimeMs = note.Points[index + 1].TimeMs;
        }

        public string NoteId { get; }
        public ChartNoteKind NoteKind { get; }
        public int Lane { get; }
        public int Index { get; }
        public int StartPosition { get; }
        public int EndPosition { get; }
        public double StartTimeMs { get; }
        public double EndTimeMs { get; }
    }

    public sealed class PlayableChartSnapshot
    {
        internal PlayableChartSnapshot(
            TimingMap timingMap,
            ScrollMap scrollMap,
            CameraMotionMap cameraMotionMap,
            ScratchCameraTiltMap scratchCameraTiltMap,
            PlayableNoteSnapshot[] notes,
            JudgementTarget[] judgementTargets,
            JudgementSegment[] judgementSegments,
            PlayableEffectEvent[] effectEvents = null)
        {
            TimingMap = timingMap ??
                throw new ArgumentNullException(nameof(timingMap));
            ScrollMap = scrollMap ??
                throw new ArgumentNullException(nameof(scrollMap));
            CameraMotionMap = cameraMotionMap ??
                throw new ArgumentNullException(nameof(cameraMotionMap));
            ScratchCameraTiltMap = scratchCameraTiltMap ??
                throw new ArgumentNullException(
                    nameof(scratchCameraTiltMap));
            Notes = Array.AsReadOnly(
                notes != null
                    ? (PlayableNoteSnapshot[])notes.Clone()
                    : Array.Empty<PlayableNoteSnapshot>());
            JudgementTargets = Array.AsReadOnly(
                judgementTargets != null
                    ? (JudgementTarget[])judgementTargets.Clone()
                    : Array.Empty<JudgementTarget>());
            JudgementSegments = Array.AsReadOnly(
                judgementSegments != null
                    ? (JudgementSegment[])judgementSegments.Clone()
                    : Array.Empty<JudgementSegment>());
            EffectEvents = Array.AsReadOnly(effectEvents != null
                ? (PlayableEffectEvent[])effectEvents.Clone()
                : Array.Empty<PlayableEffectEvent>());
        }

        public TimingMap TimingMap { get; }
        public ScrollMap ScrollMap { get; }
        public CameraMotionMap CameraMotionMap { get; }
        public ScratchCameraTiltMap ScratchCameraTiltMap { get; }
        public IReadOnlyList<PlayableNoteSnapshot> Notes { get; }
        public IReadOnlyList<JudgementTarget> JudgementTargets { get; }
        public IReadOnlyList<JudgementSegment> JudgementSegments { get; }
        public IReadOnlyList<PlayableEffectEvent> EffectEvents { get; }
    }

    public sealed class PlayableEffectEvent
    {
        internal PlayableEffectEvent(ChartEffectEvent source, double timeMs)
        {
            Position = source.Position;
            EffectId = source.EffectId;
            EffectTypeId = source.EffectTypeId;
            CommandId = source.CommandId ?? string.Empty;
            Order = source.Order;
            TimeMs = timeMs;
        }

        public int Position { get; }
        public string EffectId { get; }
        public string EffectTypeId { get; }
        public string CommandId { get; }
        public int Order { get; }
        public double TimeMs { get; }
    }
}

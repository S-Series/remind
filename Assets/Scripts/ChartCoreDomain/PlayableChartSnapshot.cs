using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    public enum JudgementTargetKind
    {
        Tap = 0,
        HoldStart = 1,
        HoldEnd = 2
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
            double? endFloorPosition)
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

    public sealed class PlayableChartSnapshot
    {
        internal PlayableChartSnapshot(
            TimingMap timingMap,
            ScrollMap scrollMap,
            CameraMotionMap cameraMotionMap,
            PlayableNoteSnapshot[] notes,
            JudgementTarget[] judgementTargets)
        {
            TimingMap = timingMap ??
                throw new ArgumentNullException(nameof(timingMap));
            ScrollMap = scrollMap ??
                throw new ArgumentNullException(nameof(scrollMap));
            CameraMotionMap = cameraMotionMap ??
                throw new ArgumentNullException(nameof(cameraMotionMap));
            Notes = Array.AsReadOnly(
                notes != null
                    ? (PlayableNoteSnapshot[])notes.Clone()
                    : Array.Empty<PlayableNoteSnapshot>());
            JudgementTargets = Array.AsReadOnly(
                judgementTargets != null
                    ? (JudgementTarget[])judgementTargets.Clone()
                    : Array.Empty<JudgementTarget>());
        }

        public TimingMap TimingMap { get; }
        public ScrollMap ScrollMap { get; }
        public CameraMotionMap CameraMotionMap { get; }
        public IReadOnlyList<PlayableNoteSnapshot> Notes { get; }
        public IReadOnlyList<JudgementTarget> JudgementTargets { get; }
    }
}

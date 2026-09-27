using System;

namespace REmind.Data
{
    public sealed class NoteData
    {
        public string Id { get; }
        public NoteType Type { get; }
        public int Lane { get; }
        public long TimeMs { get; }
        public long DurationMs { get; }
        /// <summary>
        /// Canonical chart time used by the chronological gameplay pump. The
        /// integer properties remain available for legacy chart compatibility.
        /// </summary>
        public double TimelineTimeMs { get; }
        public double TimelineDurationMs { get; }
        public ScratchMotionData ScratchMotion { get; }

        public long EndTimeMs => TimeMs + DurationMs;
        public double TimelineEndTimeMs =>
            TimelineTimeMs + TimelineDurationMs;

        internal NoteData(
            string id,
            NoteType type,
            int lane,
            long timeMs,
            long durationMs,
            ScratchMotionData scratchMotion = null)
            : this(
                id,
                type,
                lane,
                timeMs,
                durationMs,
                timeMs,
                durationMs,
                scratchMotion)
        {
        }

        internal NoteData(
            string id,
            NoteType type,
            int lane,
            long timeMs,
            long durationMs,
            double timelineTimeMs,
            double timelineDurationMs,
            ScratchMotionData scratchMotion = null)
        {
            Id = id;
            Type = type;
            Lane = lane;
            TimeMs = timeMs;
            DurationMs = durationMs;
            TimelineTimeMs = timelineTimeMs;
            TimelineDurationMs = timelineDurationMs;
            ScratchMotion = type.IsScratch()
                ? (scratchMotion ?? ScratchMotionData.CreateDefault(type)).Clone()
                : null;
        }
    }
}

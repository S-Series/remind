using System;

namespace REmind.Data
{
    public enum ScratchMotionType
    {
        Instant = 0,
        Gradual = 1
    }

    [Serializable]
    public sealed class ScratchMotionData : IEquatable<ScratchMotionData>
    {
        public int StartOffsetUnits { get; }
        public int EndOffsetUnits { get; }
        public ScratchMotionType MotionType { get; }

        public long TravelUnits => Math.Abs(
            (long)EndOffsetUnits - StartOffsetUnits);
        public int Direction => EndOffsetUnits.CompareTo(StartOffsetUnits);

        public ScratchMotionData(
            int startOffsetUnits,
            int endOffsetUnits,
            ScratchMotionType motionType)
        {
            if (!Enum.IsDefined(typeof(ScratchMotionType), motionType))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(motionType),
                    motionType,
                    "Unsupported Scratch motion type.");
            }

            StartOffsetUnits = startOffsetUnits;
            EndOffsetUnits = endOffsetUnits;
            MotionType = motionType;
        }

        public static ScratchMotionData CreateDefault(NoteType noteType)
        {
            return new ScratchMotionData(
                0,
                0,
                noteType == NoteType.LongScratch
                    ? ScratchMotionType.Gradual
                    : ScratchMotionType.Instant);
        }

        public ScratchMotionData Clone()
        {
            return new ScratchMotionData(
                StartOffsetUnits,
                EndOffsetUnits,
                MotionType);
        }

        public bool Equals(ScratchMotionData other)
        {
            return other != null &&
                StartOffsetUnits == other.StartOffsetUnits &&
                EndOffsetUnits == other.EndOffsetUnits &&
                MotionType == other.MotionType;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScratchMotionData);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StartOffsetUnits,
                EndOffsetUnits,
                MotionType);
        }
    }

    public sealed class NoteData
    {
        public string Id { get; }
        public NoteType Type { get; }
        public int Lane { get; }
        public long TimeMs { get; }
        public long DurationMs { get; }
        public ScratchMotionData ScratchMotion { get; }

        public long EndTimeMs => TimeMs + DurationMs;

        internal NoteData(
            string id,
            NoteType type,
            int lane,
            long timeMs,
            long durationMs,
            ScratchMotionData scratchMotion = null)
        {
            Id = id;
            Type = type;
            Lane = lane;
            TimeMs = timeMs;
            DurationMs = durationMs;
            ScratchMotion = type.IsScratch()
                ? (scratchMotion ?? ScratchMotionData.CreateDefault(type)).Clone()
                : null;
        }
    }
}

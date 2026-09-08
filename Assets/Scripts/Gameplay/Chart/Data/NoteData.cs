using System;
using System.Collections.Generic;

namespace REmind.Data
{
    public enum ScratchMotionType
    {
        Instant = 0,
        Gradual = 1,
        None = 2,
        Release = 3
    }

    public enum ScratchPointType
    {
        Tap = 0,
        Start = 1,
        Mid = 2,
        End = 3
    }

    [Serializable]
    public sealed class ScratchMotionData : IEquatable<ScratchMotionData>
    {
        public const int DefaultMoveAmount = 10;
        public const int MaximumMoveAmount = 99;
        private const int LegacyMaximumTravelUnits = 800;

        public int StartOffsetUnits { get; }
        public int EndOffsetUnits { get; }
        public int MoveAmount { get; }
        public ScratchMotionType MotionType { get; }

        public long TravelUnits => Math.Abs(
            (long)EndOffsetUnits - StartOffsetUnits);
        public int Direction => EndOffsetUnits.CompareTo(StartOffsetUnits);

        public ScratchMotionData(
            int startOffsetUnits,
            int endOffsetUnits,
            ScratchMotionType motionType)
            : this(
                startOffsetUnits,
                endOffsetUnits,
                ConvertLegacyMoveAmount(startOffsetUnits, endOffsetUnits),
                motionType)
        {
        }

        public ScratchMotionData(
            int moveAmount,
            ScratchMotionType motionType)
            : this(0, moveAmount, moveAmount, motionType)
        {
        }

        private ScratchMotionData(
            int startOffsetUnits,
            int endOffsetUnits,
            int moveAmount,
            ScratchMotionType motionType)
        {
            if (!Enum.IsDefined(typeof(ScratchMotionType), motionType))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(motionType),
                    motionType,
                    "Unsupported Scratch motion type.");
            }

            if (moveAmount < 0 || moveAmount > MaximumMoveAmount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moveAmount),
                    moveAmount,
                    $"Scratch move amount must be between 0 and " +
                    $"{MaximumMoveAmount}.");
            }

            StartOffsetUnits = startOffsetUnits;
            EndOffsetUnits = endOffsetUnits;
            MoveAmount = moveAmount;
            MotionType = motionType;
        }

        public static ScratchMotionData CreateDefault(NoteType noteType)
        {
            return new ScratchMotionData(
                DefaultMoveAmount,
                ScratchMotionType.None);
        }

        public ScratchMotionData Clone()
        {
            return new ScratchMotionData(
                StartOffsetUnits,
                EndOffsetUnits,
                MoveAmount,
                MotionType);
        }

        public ScratchMotionData WithMotionType(
            ScratchMotionType motionType)
        {
            return new ScratchMotionData(
                StartOffsetUnits,
                EndOffsetUnits,
                MoveAmount,
                motionType);
        }

        public bool Equals(ScratchMotionData other)
        {
            return other != null &&
                StartOffsetUnits == other.StartOffsetUnits &&
                EndOffsetUnits == other.EndOffsetUnits &&
                MoveAmount == other.MoveAmount &&
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
                MoveAmount,
                MotionType);
        }

        private static int ConvertLegacyMoveAmount(
            int startOffsetUnits,
            int endOffsetUnits)
        {
            long travel = Math.Abs(
                (long)endOffsetUnits - startOffsetUnits);
            long scaled = checked(
                travel * MaximumMoveAmount +
                LegacyMaximumTravelUnits / 2L);
            return (int)Math.Min(
                MaximumMoveAmount,
                scaled / LegacyMaximumTravelUnits);
        }
    }

    /// <summary>
    /// ChartMaker와 실제 게임이 공유하는 Scratch 이동 규칙입니다.
    /// </summary>
    public static class ScratchMotionRules
    {
        /// <summary>
        /// Instant Scratch의 화면 이동은 노트 위치부터 이 분할 길이만큼 보간합니다.
        /// 판정 시각과 저장 타입은 Instant 그대로 유지됩니다.
        /// </summary>
        public const int InstantTransitionDivisionsPerMeasure = 32;

        public const float HorizontalUnitsPerTenAmount = 7.5f;
        public const float HorizontalUnitsPerAmount =
            HorizontalUnitsPerTenAmount / 10f;

        public static float MoveAmountToHorizontalUnits(int moveAmount)
        {
            if (moveAmount < 0 ||
                moveAmount > ScratchMotionData.MaximumMoveAmount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moveAmount),
                    moveAmount,
                    $"Scratch move amount must be between 0 and " +
                    $"{ScratchMotionData.MaximumMoveAmount}.");
            }

            return moveAmount * HorizontalUnitsPerAmount;
        }

        public static ScratchMotionType GetEffectiveMotionType(
            NoteType noteType,
            ScratchPointType pointType,
            ScratchMotionType motionType)
        {
            if (!Enum.IsDefined(typeof(ScratchMotionType), motionType))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(motionType),
                    motionType,
                    "Unsupported Scratch motion type.");
            }

            if (motionType == ScratchMotionType.Release)
            {
                return IsReleaseAllowed(noteType, pointType)
                        ? ScratchMotionType.Release
                        : ScratchMotionType.None;
            }

            return noteType == NoteType.LongScratch &&
                pointType == ScratchPointType.End
                    ? ScratchMotionType.None
                    : motionType;
        }

        public static bool IsReleaseAllowed(
            NoteType noteType,
            ScratchPointType pointType)
        {
            return noteType == NoteType.LongScratch &&
                (pointType == ScratchPointType.Mid ||
                 pointType == ScratchPointType.End);
        }

        public static bool UsesInstantTransition(
            ScratchMotionType motionType)
        {
            return motionType == ScratchMotionType.Instant ||
                motionType == ScratchMotionType.Release;
        }

        public static float GetHorizontalDirectionMultiplier(
            ScratchMotionType motionType)
        {
            return motionType == ScratchMotionType.Release ? -1f : 1f;
        }

        public static ScratchMotionData NormalizeMotion(
            NoteType noteType,
            ScratchPointType pointType,
            ScratchMotionData motion)
        {
            ScratchMotionData source = motion ??
                ScratchMotionData.CreateDefault(noteType);
            ScratchMotionType effectiveMotionType =
                GetEffectiveMotionType(
                    noteType,
                    pointType,
                    source.MotionType);
            return effectiveMotionType == source.MotionType
                ? source
                : source.WithMotionType(effectiveMotionType);
        }
    }

    /// <summary>
    /// 특정 타임라인 구간에 적용되는 하나의 Scratch 수평 이동입니다.
    /// 타임라인 단위는 ChartMaker 좌표나 실제 게임의 밀리초처럼 호출자가 정합니다.
    /// </summary>
    public readonly struct ScratchMotionEffect
    {
        public double StartPosition { get; }
        public double EndPosition { get; }
        public float DeltaX { get; }
        public ScratchMotionType MotionType { get; }

        public ScratchMotionEffect(
            double startPosition,
            double endPosition,
            float deltaX,
            ScratchMotionType motionType)
        {
            if (!Enum.IsDefined(typeof(ScratchMotionType), motionType))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(motionType),
                    motionType,
                    "Unsupported Scratch motion type.");
            }

            StartPosition = startPosition;
            EndPosition = endPosition;
            DeltaX = deltaX;
            MotionType = motionType;
        }
    }

    /// <summary>
    /// Scratch 이동들을 누적해 임의 시점의 노트 라인 X 오프셋을 계산합니다.
    /// Unity 오브젝트를 참조하지 않아 ChartMaker와 실제 게임 양쪽에서 재사용할 수 있습니다.
    /// </summary>
    public sealed class ScratchMotionPath
    {
        private readonly ScratchMotionEffect[] effects;

        public static ScratchMotionPath Empty { get; } =
            new ScratchMotionPath(Array.Empty<ScratchMotionEffect>());

        public IReadOnlyList<ScratchMotionEffect> Effects => effects;

        public ScratchMotionPath(
            IReadOnlyList<ScratchMotionEffect> sourceEffects)
        {
            if (sourceEffects == null)
            {
                throw new ArgumentNullException(nameof(sourceEffects));
            }

            effects = new ScratchMotionEffect[sourceEffects.Count];

            for (int i = 0; i < sourceEffects.Count; i++)
            {
                effects[i] = sourceEffects[i];
            }
        }

        public float EvaluateOffset(
            double position,
            bool includeInstantAtPosition = true)
        {
            float offsetX = 0f;

            for (int i = 0; i < effects.Length; i++)
            {
                ScratchMotionEffect effect = effects[i];
                bool isZeroLength =
                    effect.EndPosition <= effect.StartPosition;

                if (effect.MotionType == ScratchMotionType.None)
                {
                    continue;
                }

                if (isZeroLength)
                {
                    if (position > effect.StartPosition ||
                        (includeInstantAtPosition &&
                         position == effect.StartPosition))
                    {
                        offsetX += effect.DeltaX;
                    }

                    continue;
                }

                if (position <= effect.StartPosition)
                {
                    continue;
                }

                if (position >= effect.EndPosition)
                {
                    offsetX += effect.DeltaX;
                    continue;
                }

                double progress =
                    (position - effect.StartPosition) /
                    (effect.EndPosition - effect.StartPosition);
                offsetX += effect.DeltaX * (float)progress;
            }

            return offsetX;
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

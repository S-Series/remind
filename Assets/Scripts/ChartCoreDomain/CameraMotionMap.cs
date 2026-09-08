using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    public readonly struct CameraMotionPoint
    {
        public CameraMotionPoint(
            int position,
            double timeMs,
            double floorPosition,
            double offsetX,
            ChartCameraSpinDirection spinDirection,
            double spinDurationMs)
        {
            Position = position;
            TimeMs = timeMs;
            FloorPosition = floorPosition;
            OffsetX = offsetX;
            SpinDirection = spinDirection;
            SpinDurationMs = spinDurationMs;
        }

        public int Position { get; }
        public double TimeMs { get; }
        public double FloorPosition { get; }
        public double OffsetX { get; }
        public ChartCameraSpinDirection SpinDirection { get; }
        public double SpinDurationMs { get; }
    }

    public readonly struct CameraMotionState
    {
        internal CameraMotionState(
            bool hasReference,
            double referenceFloorPosition,
            double referenceOffsetX,
            double spinDegrees)
        {
            HasReference = hasReference;
            ReferenceFloorPosition = referenceFloorPosition;
            ReferenceOffsetX = referenceOffsetX;
            SpinDegrees = spinDegrees;
        }

        public bool HasReference { get; }
        public double ReferenceFloorPosition { get; }
        public double ReferenceOffsetX { get; }
        public double SpinDegrees { get; }
    }

    /// <summary>
    /// Camera Note의 기준 X 갱신과 회전 진행도를 ChartTimeMs의 절대식으로
    /// 평가합니다. 완료된 360도 회전은 원래 자세와 같으므로 활성 구간의 회전값만
    /// 반환합니다.
    /// </summary>
    public sealed class CameraMotionMap
    {
        private readonly CameraMotionPoint[] points;
        private readonly IReadOnlyList<CameraMotionPoint> readOnlyPoints;

        internal CameraMotionMap(
            IReadOnlyList<CameraMotionPoint> sourcePoints)
        {
            if (sourcePoints == null)
            {
                throw new ArgumentNullException(nameof(sourcePoints));
            }

            points = new CameraMotionPoint[sourcePoints.Count];

            for (int i = 0; i < sourcePoints.Count; i++)
            {
                CameraMotionPoint point = sourcePoints[i];

                if (point.Position < 0 ||
                    !IsFinite(point.TimeMs) ||
                    !IsFinite(point.FloorPosition) ||
                    !IsFinite(point.OffsetX) ||
                    !Enum.IsDefined(
                        typeof(ChartCameraSpinDirection),
                        point.SpinDirection) ||
                    !IsFinite(point.SpinDurationMs) ||
                    point.SpinDurationMs <= 0d ||
                    (i > 0 && point.TimeMs <= points[i - 1].TimeMs))
                {
                    throw new ArgumentException(
                        $"Invalid camera motion point at index {i}.",
                        nameof(sourcePoints));
                }

                points[i] = point;
            }

            readOnlyPoints = Array.AsReadOnly(points);
        }

        public IReadOnlyList<CameraMotionPoint> Points => readOnlyPoints;

        public CameraMotionState EvaluateAtTime(double chartTimeMs)
        {
            if (!IsFinite(chartTimeMs))
            {
                throw new ArgumentOutOfRangeException(nameof(chartTimeMs));
            }

            int referenceIndex = FindPointIndexByTime(chartTimeMs);
            bool hasReference = referenceIndex >= 0;
            double referenceFloorPosition = hasReference
                ? points[referenceIndex].FloorPosition
                : 0d;
            double referenceOffsetX = hasReference
                ? points[referenceIndex].OffsetX
                : 0d;
            double spinDegrees = 0d;

            for (int i = 0; i < points.Length; i++)
            {
                CameraMotionPoint point = points[i];
                double elapsedMs = chartTimeMs - point.TimeMs;

                if (elapsedMs < 0d)
                {
                    break;
                }

                if (elapsedMs >= point.SpinDurationMs ||
                    point.SpinDirection == ChartCameraSpinDirection.None)
                {
                    continue;
                }

                double progress = elapsedMs / point.SpinDurationMs;
                double easedProgress =
                    progress * progress * (3d - 2d * progress);
                double direction = point.SpinDirection ==
                    ChartCameraSpinDirection.Left
                        ? 1d
                        : -1d;
                spinDegrees += direction * 360d * easedProgress;
            }

            return new CameraMotionState(
                hasReference,
                referenceFloorPosition,
                referenceOffsetX,
                spinDegrees);
        }

        private int FindPointIndexByTime(double chartTimeMs)
        {
            int low = 0;
            int high = points.Length;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

                if (points[middle].TimeMs <= chartTimeMs)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low - 1;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public readonly struct ScratchCameraTiltPoint
    {
        public ScratchCameraTiltPoint(
            int position,
            double timeMs,
            double targetDegrees,
            double durationMs)
        {
            Position = position;
            TimeMs = timeMs;
            TargetDegrees = targetDegrees;
            DurationMs = durationMs;
        }

        public int Position { get; }
        public double TimeMs { get; }
        public double TargetDegrees { get; }
        public double DurationMs { get; }
    }

    public readonly struct ScratchCameraTiltHoldPoint
    {
        public ScratchCameraTiltHoldPoint(
            int startPosition,
            int endPosition,
            double startTimeMs,
            double endTimeMs,
            double targetDegrees,
            double attackDurationMs,
            double releaseDurationMs)
        {
            StartPosition = startPosition;
            EndPosition = endPosition;
            StartTimeMs = startTimeMs;
            EndTimeMs = endTimeMs;
            TargetDegrees = targetDegrees;
            AttackDurationMs = attackDurationMs;
            ReleaseDurationMs = releaseDurationMs;
        }

        public int StartPosition { get; }
        public int EndPosition { get; }
        public double StartTimeMs { get; }
        public double EndTimeMs { get; }
        public double TargetDegrees { get; }
        public double AttackDurationMs { get; }
        public double ReleaseDurationMs { get; }
    }

    /// <summary>
    /// Instant Scratch의 충격과 Gradual Scratch의 유지/해제 카메라 롤을
    /// ChartTimeMs의 절대식으로 평가합니다.
    /// </summary>
    public sealed class ScratchCameraTiltMap
    {
        public const double PeakTiltDegrees = 10d;
        public const double AttackDurationRatio = 0.1d;
        public const double GradualPeakTiltDegrees = 5d;
        public const double GradualAttackDurationMs = 100d;
        public const double GradualReleaseDurationMs = 20d;

        private readonly ScratchCameraTiltPoint[] points;
        private readonly IReadOnlyList<ScratchCameraTiltPoint> readOnlyPoints;
        private readonly ScratchCameraTiltHoldPoint[] holdPoints;
        private readonly IReadOnlyList<ScratchCameraTiltHoldPoint>
            readOnlyHoldPoints;

        internal ScratchCameraTiltMap(
            IReadOnlyList<ScratchCameraTiltPoint> sourcePoints,
            IReadOnlyList<ScratchCameraTiltHoldPoint> sourceHoldPoints)
        {
            if (sourcePoints == null)
            {
                throw new ArgumentNullException(nameof(sourcePoints));
            }

            if (sourceHoldPoints == null)
            {
                throw new ArgumentNullException(nameof(sourceHoldPoints));
            }

            points = new ScratchCameraTiltPoint[sourcePoints.Count];

            for (int i = 0; i < sourcePoints.Count; i++)
            {
                ScratchCameraTiltPoint point = sourcePoints[i];

                if (point.Position < 0 ||
                    !IsFinite(point.TimeMs) ||
                    !IsFinite(point.TargetDegrees) ||
                    Math.Abs(point.TargetDegrees) > PeakTiltDegrees ||
                    !IsFinite(point.DurationMs) ||
                    point.DurationMs <= 0d ||
                    (i > 0 && point.TimeMs < points[i - 1].TimeMs))
                {
                    throw new ArgumentException(
                        $"Invalid Scratch camera tilt point at index {i}.",
                        nameof(sourcePoints));
                }

                points[i] = point;
            }

            readOnlyPoints = Array.AsReadOnly(points);

            holdPoints = new ScratchCameraTiltHoldPoint[
                sourceHoldPoints.Count];

            for (int i = 0; i < sourceHoldPoints.Count; i++)
            {
                ScratchCameraTiltHoldPoint point = sourceHoldPoints[i];

                if (point.StartPosition < 0 ||
                    point.EndPosition <= point.StartPosition ||
                    !IsFinite(point.StartTimeMs) ||
                    !IsFinite(point.EndTimeMs) ||
                    point.EndTimeMs <= point.StartTimeMs ||
                    !IsFinite(point.TargetDegrees) ||
                    Math.Abs(point.TargetDegrees) > PeakTiltDegrees ||
                    !IsFinite(point.AttackDurationMs) ||
                    point.AttackDurationMs <= 0d ||
                    !IsFinite(point.ReleaseDurationMs) ||
                    point.ReleaseDurationMs <= 0d ||
                    (i > 0 && point.StartTimeMs <
                        holdPoints[i - 1].StartTimeMs))
                {
                    throw new ArgumentException(
                        $"Invalid Gradual Scratch camera tilt point at " +
                        $"index {i}.",
                        nameof(sourceHoldPoints));
                }

                holdPoints[i] = point;
            }

            readOnlyHoldPoints = Array.AsReadOnly(holdPoints);
        }

        public IReadOnlyList<ScratchCameraTiltPoint> Points =>
            readOnlyPoints;
        public IReadOnlyList<ScratchCameraTiltHoldPoint> HoldPoints =>
            readOnlyHoldPoints;

        public double EvaluateAtTime(double chartTimeMs)
        {
            if (!IsFinite(chartTimeMs))
            {
                throw new ArgumentOutOfRangeException(nameof(chartTimeMs));
            }

            double tiltDegrees = 0d;

            for (int i = 0; i < holdPoints.Length; i++)
            {
                ScratchCameraTiltHoldPoint point = holdPoints[i];

                if (chartTimeMs < point.StartTimeMs)
                {
                    break;
                }

                tiltDegrees += point.TargetDegrees *
                    EvaluateGradualEnvelope(point, chartTimeMs);
            }

            for (int i = 0; i < points.Length; i++)
            {
                ScratchCameraTiltPoint point = points[i];
                double elapsedMs = chartTimeMs - point.TimeMs;

                if (elapsedMs < 0d)
                {
                    break;
                }

                if (elapsedMs >= point.DurationMs)
                {
                    continue;
                }

                double progress = elapsedMs / point.DurationMs;
                double envelope;

                if (progress < AttackDurationRatio)
                {
                    double attackProgress =
                        progress / AttackDurationRatio;
                    double remaining = 1d - attackProgress;
                    envelope = 1d - remaining * remaining * remaining;
                }
                else
                {
                    double returnProgress =
                        (progress - AttackDurationRatio) /
                        (1d - AttackDurationRatio);
                    double remaining = 1d - returnProgress;
                    envelope = remaining * remaining;
                }

                tiltDegrees += point.TargetDegrees * envelope;
            }

            return Math.Max(
                -PeakTiltDegrees,
                Math.Min(PeakTiltDegrees, tiltDegrees));
        }

        private static double EvaluateGradualEnvelope(
            ScratchCameraTiltHoldPoint point,
            double chartTimeMs)
        {
            double activeDurationMs =
                point.EndTimeMs - point.StartTimeMs;
            double endEnvelope = activeDurationMs >= point.AttackDurationMs
                ? 1d
                : EvaluateLinearAttack(
                    activeDurationMs / point.AttackDurationMs);

            if (chartTimeMs < point.EndTimeMs)
            {
                double elapsedMs = chartTimeMs - point.StartTimeMs;
                return elapsedMs >= point.AttackDurationMs
                    ? 1d
                    : EvaluateLinearAttack(
                        elapsedMs / point.AttackDurationMs);
            }

            double releaseProgress =
                (chartTimeMs - point.EndTimeMs) /
                point.ReleaseDurationMs;

            if (releaseProgress >= 1d)
            {
                return 0d;
            }

            double easedRelease = releaseProgress * releaseProgress *
                (3d - 2d * releaseProgress);
            return endEnvelope * (1d - easedRelease);
        }

        private static double EvaluateLinearAttack(double progress)
        {
            return Math.Max(0d, Math.Min(1d, progress));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

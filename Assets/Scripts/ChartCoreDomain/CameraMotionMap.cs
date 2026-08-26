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
}

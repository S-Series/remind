using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    /// <summary>
    /// BPM 변경 시각과 위치를 함께 고정한 하나의 연속 구간 기준점입니다.
    /// </summary>
    public readonly struct TimingPoint
    {
        public TimingPoint(double startTimeMs, double startPosition, double bpm)
        {
            StartTimeMs = startTimeMs;
            StartPosition = startPosition;
            Bpm = bpm;
        }

        public double StartTimeMs { get; }
        public double StartPosition { get; }
        public double Bpm { get; }
    }

    /// <summary>
    /// Position과 ChartTimeMs 사이의 구간별 양방향 변환입니다.
    /// 같은 입력에는 항상 같은 절대 결과를 반환하며 프레임 누적값을 사용하지 않습니다.
    /// </summary>
    public sealed class TimingMap
    {
        private const double MillisecondsPerMinute = 60000d;
        private readonly TimingPoint[] points;
        private readonly IReadOnlyList<TimingPoint> readOnlyPoints;

        public TimingMap(
            IReadOnlyList<TimingPoint> sourcePoints,
            double positionUnitsPerBeat)
        {
            if (sourcePoints == null)
            {
                throw new ArgumentNullException(nameof(sourcePoints));
            }

            if (sourcePoints.Count == 0)
            {
                throw new ArgumentException(
                    "A TimingMap requires at least one point.",
                    nameof(sourcePoints));
            }

            if (!IsFinite(positionUnitsPerBeat) || positionUnitsPerBeat <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(positionUnitsPerBeat));
            }

            PositionUnitsPerBeat = positionUnitsPerBeat;
            points = new TimingPoint[sourcePoints.Count];

            for (int i = 0; i < sourcePoints.Count; i++)
            {
                TimingPoint point = sourcePoints[i];

                if (!IsFinite(point.StartTimeMs) ||
                    !IsFinite(point.StartPosition) ||
                    !IsFinite(point.Bpm) ||
                    point.Bpm <= 0d ||
                    (i > 0 &&
                     (point.StartTimeMs <= points[i - 1].StartTimeMs ||
                      point.StartPosition <= points[i - 1].StartPosition)))
                {
                    throw new ArgumentException(
                        $"Invalid timing point at index {i}.",
                        nameof(sourcePoints));
                }

                points[i] = point;
            }

            readOnlyPoints = Array.AsReadOnly(points);
        }

        public double PositionUnitsPerBeat { get; }
        public IReadOnlyList<TimingPoint> Points => readOnlyPoints;

        public double TimeAtPosition(double position)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            TimingPoint point = points[FindPointIndexByPosition(position)];
            return point.StartTimeMs +
                PositionDeltaToMilliseconds(
                    position - point.StartPosition,
                    point.Bpm,
                    PositionUnitsPerBeat);
        }

        public double PositionAtTime(double chartTimeMs)
        {
            if (!IsFinite(chartTimeMs))
            {
                throw new ArgumentOutOfRangeException(nameof(chartTimeMs));
            }

            TimingPoint point = points[FindPointIndexByTime(chartTimeMs)];
            return point.StartPosition +
                MillisecondsToPositionDelta(
                    chartTimeMs - point.StartTimeMs,
                    point.Bpm,
                    PositionUnitsPerBeat);
        }

        public TimingPoint GetPointAtTime(double chartTimeMs)
        {
            if (!IsFinite(chartTimeMs))
            {
                throw new ArgumentOutOfRangeException(nameof(chartTimeMs));
            }

            return points[FindPointIndexByTime(chartTimeMs)];
        }

        public TimingPoint GetPointAtPosition(double position)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            return points[FindPointIndexByPosition(position)];
        }

        public static double PositionDeltaToMilliseconds(
            double positionDelta,
            double bpm,
            double positionUnitsPerBeat)
        {
            ValidateConversionValues(positionDelta, bpm, positionUnitsPerBeat);
            return positionDelta * MillisecondsPerMinute /
                (bpm * positionUnitsPerBeat);
        }

        public static double MillisecondsToPositionDelta(
            double milliseconds,
            double bpm,
            double positionUnitsPerBeat)
        {
            ValidateConversionValues(milliseconds, bpm, positionUnitsPerBeat);
            return milliseconds * bpm * positionUnitsPerBeat /
                MillisecondsPerMinute;
        }

        private int FindPointIndexByPosition(double position)
        {
            int low = 0;
            int high = points.Length;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

                if (points[middle].StartPosition <= position)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return Math.Max(0, low - 1);
        }

        private int FindPointIndexByTime(double chartTimeMs)
        {
            int low = 0;
            int high = points.Length;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

                if (points[middle].StartTimeMs <= chartTimeMs)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return Math.Max(0, low - 1);
        }

        private static void ValidateConversionValues(
            double value,
            double bpm,
            double positionUnitsPerBeat)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (!IsFinite(bpm) || bpm <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(bpm));
            }

            if (!IsFinite(positionUnitsPerBeat) || positionUnitsPerBeat <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(positionUnitsPerBeat));
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public readonly struct ScrollPoint
    {
        public ScrollPoint(
            double startTimeMs,
            double startFloorPosition,
            double bpm,
            double lineSpeed,
            double floorUnitsPerMillisecond)
        {
            StartTimeMs = startTimeMs;
            StartFloorPosition = startFloorPosition;
            Bpm = bpm;
            LineSpeed = lineSpeed;
            FloorUnitsPerMillisecond = floorUnitsPerMillisecond;
        }

        public double StartTimeMs { get; }
        public double StartFloorPosition { get; }
        public double Bpm { get; }
        public double LineSpeed { get; }
        public double FloorUnitsPerMillisecond { get; }
    }

    /// <summary>
    /// ChartTimeMs를 표시 전용 FloorPosition으로 변환합니다. BPM과 LineSpeed의
    /// 모든 경계를 절대 기준점으로 가지므로 프레임 이동량을 누적하지 않습니다.
    /// </summary>
    public sealed class ScrollMap
    {
        private readonly ScrollPoint[] points;
        private readonly IReadOnlyList<ScrollPoint> readOnlyPoints;

        internal ScrollMap(
            TimingMap timingMap,
            IReadOnlyList<ScrollPoint> sourcePoints)
        {
            TimingMap = timingMap ??
                throw new ArgumentNullException(nameof(timingMap));

            if (sourcePoints == null || sourcePoints.Count == 0)
            {
                throw new ArgumentException(
                    "A ScrollMap requires at least one point.",
                    nameof(sourcePoints));
            }

            points = new ScrollPoint[sourcePoints.Count];

            for (int i = 0; i < sourcePoints.Count; i++)
            {
                ScrollPoint point = sourcePoints[i];

                if (!IsFinite(point.StartTimeMs) ||
                    !IsFinite(point.StartFloorPosition) ||
                    !IsFinite(point.Bpm) ||
                    point.Bpm <= 0d ||
                    !IsFinite(point.LineSpeed) ||
                    !IsFinite(point.FloorUnitsPerMillisecond) ||
                    (i > 0 &&
                     point.StartTimeMs <= points[i - 1].StartTimeMs))
                {
                    throw new ArgumentException(
                        $"Invalid scroll point at index {i}.",
                        nameof(sourcePoints));
                }

                points[i] = point;
            }

            readOnlyPoints = Array.AsReadOnly(points);
        }

        public TimingMap TimingMap { get; }
        public IReadOnlyList<ScrollPoint> Points => readOnlyPoints;

        public double FloorPositionAtTime(double chartTimeMs)
        {
            if (!IsFinite(chartTimeMs))
            {
                throw new ArgumentOutOfRangeException(nameof(chartTimeMs));
            }

            ScrollPoint point = points[FindPointIndexByTime(chartTimeMs)];
            return point.StartFloorPosition +
                (chartTimeMs - point.StartTimeMs) *
                point.FloorUnitsPerMillisecond;
        }

        public double FloorPositionAtChartPosition(double chartPosition)
        {
            return FloorPositionAtTime(
                TimingMap.TimeAtPosition(chartPosition));
        }

        public ScrollPoint GetPointAtTime(double chartTimeMs)
        {
            if (!IsFinite(chartTimeMs))
            {
                throw new ArgumentOutOfRangeException(nameof(chartTimeMs));
            }

            return points[FindPointIndexByTime(chartTimeMs)];
        }

        private int FindPointIndexByTime(double chartTimeMs)
        {
            int low = 0;
            int high = points.Length;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

                if (points[middle].StartTimeMs <= chartTimeMs)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return Math.Max(0, low - 1);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

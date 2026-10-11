using System;

namespace REmind.Charting
{
    /// <summary>Authored-position interpolation shared by judgement and both product views.</summary>
    public static class KnobPathEvaluator
    {
        public const int CurveSegments = 64;

        public static bool IsCurve(ChartKnobInterpolation interpolation) =>
            interpolation == ChartKnobInterpolation.Sin || interpolation == ChartKnobInterpolation.Cos;

        public static double Interpolate(double startX, double endX, double fraction,
            ChartKnobInterpolation interpolation)
        {
            fraction = Math.Max(0d, Math.Min(1d, fraction));
            if (interpolation == ChartKnobInterpolation.Step) fraction = 0d;
            else if (IsCurve(interpolation))
            {
                double scaled = fraction * CurveSegments;
                int index = Math.Min(CurveSegments - 1, (int)scaled);
                double a = QuarterWave((double)index / CurveSegments, interpolation);
                double b = QuarterWave((double)(index + 1) / CurveSegments, interpolation);
                fraction = a + (b - a) * (scaled - index);
            }
            return startX + (endX - startX) * fraction;
        }

        private static double QuarterWave(double fraction, ChartKnobInterpolation interpolation) =>
            interpolation == ChartKnobInterpolation.Sin ? Math.Sin(fraction * Math.PI * 0.5d)
                : 1d - Math.Cos(fraction * Math.PI * 0.5d);

        public static int SegmentIndex(PlayableNoteSnapshot note, double chartTimeMs)
        {
            int index = 0;
            while (index + 2 < note.Points.Count &&
                note.Points[index + 1].TimeMs <= chartTimeMs) index++;
            return index;
        }

        public static double TargetX(PlayableNoteSnapshot note, TimingMap timing,
            double chartTimeMs)
        {
            if (chartTimeMs < note.StartTimeMs)
                return KnobJudgementRules.ToPlayerX(note.Points[0].KnobX);
            if (chartTimeMs >= note.EndTimeMs.Value)
                return KnobJudgementRules.ToPlayerX(note.Points[note.Points.Count - 1].KnobX);
            int index = SegmentIndex(note, chartTimeMs);
            return SegmentX(note, timing, index, chartTimeMs);
        }

        internal static double SegmentX(PlayableNoteSnapshot note, TimingMap timing,
            int index, double chartTimeMs)
        {
            PlayableNotePoint start = note.Points[index];
            PlayableNotePoint end = note.Points[index + 1];
            if (end.Position == start.Position)
                return KnobJudgementRules.ToPlayerX(chartTimeMs < end.TimeMs ? start.KnobX : end.KnobX);
            double fraction = (timing.PositionAtTime(chartTimeMs) - start.Position) /
                (end.Position - start.Position);
            return KnobJudgementRules.ToPlayerX(Interpolate(start.KnobX, end.KnobX,
                fraction, end.KnobInterpolation));
        }
    }
}

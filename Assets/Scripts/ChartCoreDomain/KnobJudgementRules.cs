namespace REmind.Charting
{
    /// <summary>Shared development values, pending play-test balancing.</summary>
    public static class KnobJudgementRules
    {
        public const double MaximumPlayerX = 2.5d;
        public const double ManualSpeedPerSecond = 5d;
        public const double PositionTolerance = 0.25d;
        public const double StepInputWindowMs = 80d;
        public const double HoldTickIntervalMs = 100d;
        public const ChartJudgementGrade PositionSuccessGrade = ChartJudgementGrade.PerfectPlus;

        public static double ToPlayerX(double normalizedX) => normalizedX * MaximumPlayerX;
    }
}

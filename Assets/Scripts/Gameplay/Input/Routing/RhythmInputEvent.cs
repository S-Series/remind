namespace REmind.Gameplay.Input.Routing
{
    public readonly struct RhythmInputEvent
    {
        public int Lane { get; }
        public double EventTime { get; }

        public RhythmInputEvent(int lane, double eventTime)
        {
            Lane = lane;
            EventTime = eventTime;
        }
    }
}

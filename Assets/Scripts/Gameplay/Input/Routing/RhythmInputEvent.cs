namespace REmind.Gameplay.Input.Routing
{
    public readonly struct RhythmInputEvent
    {
        public int Lane { get; }
        public double EventTime { get; }
        public bool Pressed { get; }

        public RhythmInputEvent(int lane, double eventTime, bool pressed = true)
        {
            Lane = lane;
            EventTime = eventTime;
            Pressed = pressed;
        }
    }
}

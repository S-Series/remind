namespace REmind.Data
{
    public sealed class NoteData
    {
        public string Id { get; }
        public NoteType Type { get; }
        public int Lane { get; }
        public long TimeMs { get; }
        public long DurationMs { get; }

        public long EndTimeMs => TimeMs + DurationMs;

        internal NoteData(string id, NoteType type, int lane, long timeMs, long durationMs)
        {
            Id = id;
            Type = type;
            Lane = lane;
            TimeMs = timeMs;
            DurationMs = durationMs;
        }
    }
}

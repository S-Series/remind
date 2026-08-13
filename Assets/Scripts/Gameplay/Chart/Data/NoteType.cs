namespace REmind.Data
{
    public enum NoteType
    {
        Unknown = 0,
        Tap = 1,
        LongTap = 2,
        Scratch = 3,
        LongScratch = 4,
        Air = 5,
        Speed = 6,
        Effect = 7,
        Camera = 8,
    }

    public static class NoteTypeExtensions
    {
        public static bool IsLong(this NoteType noteType)
        {
            return noteType == NoteType.LongTap ||
                noteType == NoteType.LongScratch;
        }

        public static bool IsScratch(this NoteType noteType)
        {
            return noteType == NoteType.Scratch ||
                noteType == NoteType.LongScratch;
        }

        public static bool IsGameplayNote(this NoteType noteType)
        {
            return noteType == NoteType.Tap ||
                noteType == NoteType.LongTap ||
                noteType == NoteType.Scratch ||
                noteType == NoteType.LongScratch ||
                noteType == NoteType.Air;
        }

        public static bool IsChartEvent(this NoteType noteType)
        {
            return noteType == NoteType.Speed ||
                noteType == NoteType.Effect ||
                noteType == NoteType.Camera;
        }
    }

    public enum NoteHandleType
    {
        Unknown = 0,
        Left = 1,
        Right = 2,
    }
}

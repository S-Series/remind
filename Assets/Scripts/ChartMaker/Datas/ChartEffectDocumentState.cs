using UnityEngine;

/// <summary>Metadata of the open editor document, never runtime gimmick state.</summary>
public static class ChartEffectDocumentState
{
    public static string MusicId { get; set; } = "untitled";
    public static string DifficultyId { get; set; } = "default";
    public static int Level { get; set; }
    public static string Author { get; set; } = "Unknown";
    public static string ExtraName { get; set; } = "";
    public static string GimmickId { get; set; } = "";
    /// <summary>
    /// Revision of the last chart/parameter pair loaded from or written to disk.
    /// This is persistence bookkeeping, not an undoable editor field.
    /// </summary>
    public static string Revision { get; set; } = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        MusicId = "untitled";
        DifficultyId = "default";
        Level = 0;
        Author = "Unknown";
        ExtraName = "";
        GimmickId = Revision = "";
    }

    public static Metadata Capture() => new Metadata(MusicId, DifficultyId, GimmickId, Revision,
        Level, Author, ExtraName);

    public static EditableMetadata CaptureEditable() =>
        new EditableMetadata(MusicId, DifficultyId, GimmickId, Level, Author, ExtraName);

    public static void Restore(Metadata value)
    {
        MusicId = value.MusicId;
        DifficultyId = value.DifficultyId;
        Level = value.Level;
        Author = value.Author;
        ExtraName = value.ExtraName;
        GimmickId = value.GimmickId;
        Revision = value.Revision;
    }

    public static void RestoreEditable(EditableMetadata value)
    {
        MusicId = value.MusicId;
        DifficultyId = value.DifficultyId;
        Level = value.Level;
        Author = value.Author;
        ExtraName = value.ExtraName;
        GimmickId = value.GimmickId;
    }

    public static void Apply(ChartFile file)
    {
        MusicId = string.IsNullOrWhiteSpace(file.MusicId) ? "untitled" : file.MusicId;
        DifficultyId = string.IsNullOrWhiteSpace(file.DifficultyId) ? "default" : file.DifficultyId;
        Level = file.Level;
        Author = file.Author ?? "Unknown";
        ExtraName = file.ExtraName ?? "";
        GimmickId = file.GimmickId ?? "";
        Revision = file.EffectRevision ?? "";
    }

    public readonly struct Metadata
    {
        public readonly string MusicId, DifficultyId, GimmickId, Revision, Author, ExtraName;
        public readonly int Level;
        public Metadata(string musicId, string difficultyId, string gimmickId, string revision,
            int level = 0, string author = "Unknown", string extraName = "")
        {
            MusicId = musicId; DifficultyId = difficultyId; GimmickId = gimmickId;
            Revision = revision; Level = level; Author = author; ExtraName = extraName;
        }
    }

    /// <summary>Chart-wide fields that participate in editor Undo/Redo.</summary>
    public readonly struct EditableMetadata
    {
        public readonly string MusicId, DifficultyId, GimmickId, Author, ExtraName;
        public readonly int Level;

        public EditableMetadata(string musicId, string difficultyId, string gimmickId,
            int level, string author, string extraName)
        {
            MusicId = musicId;
            DifficultyId = difficultyId;
            GimmickId = gimmickId;
            Level = level;
            Author = author;
            ExtraName = extraName;
        }
    }
}

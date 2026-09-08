using UnityEngine;

/// <summary>Metadata of the open editor document, never runtime gimmick state.</summary>
public static class ChartEffectDocumentState
{
    public static string MusicId { get; set; } = "untitled";
    public static string DifficultyId { get; set; } = "default";
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
        GimmickId = Revision = "";
    }

    public static Metadata Capture() => new Metadata(MusicId, DifficultyId, GimmickId, Revision);

    public static EditableMetadata CaptureEditable() =>
        new EditableMetadata(MusicId, DifficultyId, GimmickId);

    public static void Restore(Metadata value)
    {
        MusicId = value.MusicId;
        DifficultyId = value.DifficultyId;
        GimmickId = value.GimmickId;
        Revision = value.Revision;
    }

    public static void RestoreEditable(EditableMetadata value)
    {
        MusicId = value.MusicId;
        DifficultyId = value.DifficultyId;
        GimmickId = value.GimmickId;
    }

    public static void Apply(ChartFile file)
    {
        MusicId = string.IsNullOrWhiteSpace(file.MusicId) ? "untitled" : file.MusicId;
        DifficultyId = string.IsNullOrWhiteSpace(file.DifficultyId) ? "default" : file.DifficultyId;
        GimmickId = file.GimmickId ?? "";
        Revision = file.EffectRevision ?? "";
    }

    public readonly struct Metadata
    {
        public readonly string MusicId, DifficultyId, GimmickId, Revision;
        public Metadata(string musicId, string difficultyId, string gimmickId, string revision)
        { MusicId = musicId; DifficultyId = difficultyId; GimmickId = gimmickId; Revision = revision; }
    }

    /// <summary>Chart-wide fields that participate in editor Undo/Redo.</summary>
    public readonly struct EditableMetadata
    {
        public readonly string MusicId, DifficultyId, GimmickId;

        public EditableMetadata(string musicId, string difficultyId, string gimmickId)
        {
            MusicId = musicId;
            DifficultyId = difficultyId;
            GimmickId = gimmickId;
        }
    }
}

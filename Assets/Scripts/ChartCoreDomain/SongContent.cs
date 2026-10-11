using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace REmind.Charting
{
    public enum SongHiddenType
    {
        NotHidden = 0,
        Masked = 1,
        Puzzle = 2,
        Invisible = 3
    }

    public sealed class SongContent
    {
        internal SongContent(SongContentFile source, SongHiddenType hiddenType)
        {
            MusicId = source.MusicId;
            MetadataStatus = source.MetadataStatus;
            SongSeason = source.SongSeason;
            Title = source.Title;
            Artist = source.Artist;
            MusicVolumeMultiplier = source.MusicVolumeMultiplier ?? 1d;
            AudioFile = source.AudioFile ?? SongContentCodec.DefaultAudioFile;
            JacketFile = source.JacketFile ?? SongContentCodec.DefaultJacketFile;
            JacketIllustrator = source.JacketIllustrator ?? string.Empty;
            PreviewStartMs = source.PreviewStartMs;
            PreviewDurationMs = source.PreviewDurationMs;
            HiddenType = hiddenType;
            UnlockHint = source.UnlockHint ?? string.Empty;
            RequiresPurchase = source.RequiresPurchase;
        }

        public string MusicId { get; }
        public string MetadataStatus { get; }
        public int SongSeason { get; }
        public string Title { get; }
        public string Artist { get; }
        public double MusicVolumeMultiplier { get; }
        public float PlaybackVolume => (float)(MusicVolumeMultiplier * 0.5d);
        public string AudioFile { get; }
        public string JacketFile { get; }
        public string JacketIllustrator { get; }
        public double PreviewStartMs { get; }
        public double PreviewDurationMs { get; }
        public SongHiddenType HiddenType { get; }
        public string UnlockHint { get; }
        public bool RequiresPurchase { get; }
    }

    /// <summary>Canonical song chart filenames; the filename owns the difficulty ID.</summary>
    public static class SongChartFiles
    {
        private static readonly string[] Ids = { "easy", "normal", "hard", "chaos", "extra" };
        private static readonly string[] Names = { "Easy.rd", "Normal.rd", "Hard.rd", "Chaos.rd", "Extra.rd" };
        public const int Count = 5;

        public static string GetDifficultyId(int index) =>
            index >= 0 && index < Count ? Ids[index] : null;

        public static string GetFileName(string difficultyId)
        {
            for (int index = 0; index < Count; index++)
                if (string.Equals(Ids[index], difficultyId, StringComparison.Ordinal))
                    return Names[index];
            return null;
        }

        public static string GetDifficultyIdFromFileName(string fileName)
        {
            for (int index = 0; index < Count; index++)
                if (string.Equals(Names[index], fileName, StringComparison.Ordinal))
                    return Ids[index];
            return null;
        }

        public static void Validate(string fileName, int level,
            string author, string extraName)
        {
            string difficultyId = GetDifficultyIdFromFileName(fileName);
            if (difficultyId == null)
                throw new FormatException("Chart filename must be a fixed difficulty: " + fileName);
            if (level < 0)
                throw new FormatException("Chart level cannot be negative.");
            if (string.IsNullOrWhiteSpace(author))
                throw new FormatException("Chart author is required.");
            if (difficultyId == "extra")
            {
                if (string.IsNullOrWhiteSpace(extraName))
                    throw new FormatException("Extra.rd requires extraName.");
            }
            else if (!string.IsNullOrEmpty(extraName))
                throw new FormatException("extraName is only valid in Extra.rd.");
        }
    }

    /// <summary>Unity-independent common song metadata contract.</summary>
    public static class SongContentCodec
    {
        public const int CurrentFormatVersion = 1;
        public const string DefaultAudioFile = "audio.mp3";
        public const string DefaultJacketFile = "art.jpg";
        private const string FormatName = "REmindSong";

        public static SongContent Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("Song data is empty.");
            SongContentFile source;
            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                source = (SongContentFile)new DataContractJsonSerializer(
                    typeof(SongContentFile)).ReadObject(stream);
            }
            catch (SerializationException exception)
            {
                throw new FormatException("Invalid song JSON.", exception);
            }
            if (source == null || source.Format != FormatName ||
                source.FormatVersion != CurrentFormatVersion)
                throw new FormatException("Expected REmindSong format version 1.");
            ValidateId(source.MusicId, "musicId");
            RequireText(source.Title, "title");
            RequireText(source.Artist, "artist");
            double musicVolumeMultiplier = source.MusicVolumeMultiplier ?? 1d;
            if (!IsFinite(musicVolumeMultiplier) ||
                musicVolumeMultiplier < 0d || musicVolumeMultiplier > 2d)
                throw new FormatException("musicVolumeMultiplier must be between 0 and 2.");
            if (source.SongSeason < 0)
                throw new FormatException("songSeason cannot be negative.");
            ValidateFileName(source.AudioFile ?? DefaultAudioFile, "audioFile");
            ValidateFileName(source.JacketFile ?? DefaultJacketFile, "jacketFile");
            if (!IsFinite(source.PreviewStartMs) || source.PreviewStartMs < 0 ||
                !IsFinite(source.PreviewDurationMs) || source.PreviewDurationMs <= 0)
                throw new FormatException("Preview start and duration must be valid milliseconds.");
            if (source.Charts != null)
                throw new FormatException("charts belongs in the fixed .rd files, not data.json.");
            return new SongContent(source, ParseHiddenType(source.HiddenType));
        }

        private static SongHiddenType ParseHiddenType(string value)
        {
            switch (value)
            {
                case null:
                case "NotHidden": return SongHiddenType.NotHidden;
                case "Masked": return SongHiddenType.Masked;
                case "Puzzle": return SongHiddenType.Puzzle;
                case "Invisible": return SongHiddenType.Invisible;
                default:
                    throw new FormatException(
                        "hiddenType must be NotHidden, Masked, Puzzle or Invisible.");
            }
        }

        public static string ResolveReference(string songDataPath, string relativePath)
        {
            ValidateFileName(relativePath, nameof(relativePath));
            string folder = Path.GetDirectoryName(Path.GetFullPath(songDataPath));
            string resolved = Path.GetFullPath(Path.Combine(folder,
                relativePath));
            string prefix = folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, Path.DirectorySeparatorChar == '\\'
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new FormatException("Song reference escapes its song folder.");
            return resolved;
        }

        public static void ValidateId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 80)
                throw new FormatException(name + " must contain 1-80 letters, numbers, '_' or '-'.");
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    throw new FormatException(name + " must use letters, numbers, '_' or '-'.");
        }

        public static void ValidateFileName(string path, string name)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
                path == "." || path == ".." ||
                path.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|',
                    '\0', '\r', '\n' }) >= 0)
                throw new FormatException(name + " must be a filename in the song folder.");
            foreach (char character in path)
                if (char.IsControl(character))
                    throw new FormatException(name + " contains a control character.");
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new FormatException(name + " is required.");
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    [DataContract]
    internal sealed class SongContentFile
    {
        [DataMember(Name = "format")] public string Format { get; set; }
        [DataMember(Name = "formatVersion")] public int FormatVersion { get; set; }
        [DataMember(Name = "metadataStatus")] public string MetadataStatus { get; set; }
        [DataMember(Name = "musicId")] public string MusicId { get; set; }
        [DataMember(Name = "songSeason")] public int SongSeason { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "artist")] public string Artist { get; set; }
        [DataMember(Name = "musicVolumeMultiplier")]
        public double? MusicVolumeMultiplier { get; set; }
        [DataMember(Name = "audioFile")] public string AudioFile { get; set; }
        [DataMember(Name = "jacketFile")] public string JacketFile { get; set; }
        [DataMember(Name = "jacketIllustrator")] public string JacketIllustrator { get; set; }
        [DataMember(Name = "previewStartMs")] public double PreviewStartMs { get; set; }
        [DataMember(Name = "previewDurationMs")] public double PreviewDurationMs { get; set; }
        [DataMember(Name = "hiddenType")] public string HiddenType { get; set; }
        [DataMember(Name = "unlockHint")] public string UnlockHint { get; set; }
        [DataMember(Name = "requiresPurchase")] public bool RequiresPurchase { get; set; }
        [DataMember(Name = "charts", EmitDefaultValue = false)] public object[] Charts { get; set; }
    }
}

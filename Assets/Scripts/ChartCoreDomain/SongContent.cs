using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace REmind.Charting
{
    public sealed class SongContent
    {
        internal SongContent(SongContentFile source, IReadOnlyList<SongChartEntry> charts)
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
            HiddenType = source.HiddenType ?? string.Empty;
            RequiresPurchase = source.RequiresPurchase;
            Charts = charts;
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
        public string HiddenType { get; }
        public bool RequiresPurchase { get; }
        public IReadOnlyList<SongChartEntry> Charts { get; }

        public SongChartEntry FindChart(string difficultyId)
        {
            foreach (SongChartEntry chart in Charts)
                if (string.Equals(chart.DifficultyId, difficultyId, StringComparison.Ordinal))
                    return chart;
            return null;
        }
    }

    public sealed class SongChartEntry
    {
        internal SongChartEntry(string difficultyId, int level, string chartAuthor, string chartFile)
        {
            DifficultyId = difficultyId;
            Level = level;
            ChartAuthor = chartAuthor;
            ChartFile = chartFile;
        }

        public string DifficultyId { get; }
        /// <summary>Zero means a test chart has no assigned display level.</summary>
        public int Level { get; }
        public string ChartAuthor { get; }
        public string ChartFile { get; }
    }

    /// <summary>Unity-independent song metadata and chart-reference contract.</summary>
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
            if (source.Charts == null || source.Charts.Length == 0)
                throw new FormatException("A song needs at least one chart entry.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var charts = new List<SongChartEntry>(source.Charts.Length);
            foreach (SongChartFile chart in source.Charts)
            {
                if (chart == null) throw new FormatException("Chart entry cannot be null.");
                ValidateId(chart.DifficultyId, "difficultyId");
                if (!ids.Add(chart.DifficultyId))
                    throw new FormatException("Duplicate difficultyId: " + chart.DifficultyId);
                if (chart.Level < 0)
                    throw new FormatException("level cannot be negative.");
                RequireText(chart.ChartAuthor, "chartAuthor");
                string chartFile = chart.ChartFile ?? chart.DifficultyId + ".rd";
                ValidateFileName(chartFile, "chartFile");
                if (!chartFile.EndsWith(".rd", StringComparison.OrdinalIgnoreCase))
                    throw new FormatException("chartFile must point to a .rd file.");
                if (!paths.Add(chartFile))
                    throw new FormatException("Duplicate chartFile: " + chartFile);
                charts.Add(new SongChartEntry(chart.DifficultyId, chart.Level,
                    chart.ChartAuthor, chartFile));
            }
            return new SongContent(source, new ReadOnlyCollection<SongChartEntry>(charts));
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
        [DataMember(Name = "requiresPurchase")] public bool RequiresPurchase { get; set; }
        [DataMember(Name = "charts")] public SongChartFile[] Charts { get; set; }
    }

    [DataContract]
    internal sealed class SongChartFile
    {
        [DataMember(Name = "difficultyId")] public string DifficultyId { get; set; }
        [DataMember(Name = "level")] public int Level { get; set; }
        [DataMember(Name = "chartAuthor")] public string ChartAuthor { get; set; }
        [DataMember(Name = "chartFile")] public string ChartFile { get; set; }
    }
}

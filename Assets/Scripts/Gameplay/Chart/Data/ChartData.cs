using System;
using System.Collections.Generic;

namespace REmind.Gameplay.Chart.Data
{
    public sealed class ChartData
    {
        public string FormatVersion { get; }
        public string ChartId { get; }
        public string SongId { get; }
        public string Title { get; }
        public string Artist { get; }
        public string Charter { get; }
        public ChartDifficultyData Difficulty { get; }
        public int LaneCount { get; }
        public string AudioFile { get; }
        public long ChartOffsetMs { get; }
        public ChartPreviewData Preview { get; }
        public ChartTimingData Timing { get; }
        public IReadOnlyList<NoteData> Notes { get; }

        internal ChartData(
            string formatVersion,
            string chartId,
            string songId,
            string title,
            string artist,
            string charter,
            ChartDifficultyData difficulty,
            int laneCount,
            string audioFile,
            long chartOffsetMs,
            ChartPreviewData preview,
            ChartTimingData timing,
            NoteData[] notes)
        {
            FormatVersion = formatVersion;
            ChartId = chartId;
            SongId = songId;
            Title = title;
            Artist = artist;
            Charter = charter;
            Difficulty = difficulty;
            LaneCount = laneCount;
            AudioFile = audioFile;
            ChartOffsetMs = chartOffsetMs;
            Preview = preview;
            Timing = timing;
            Notes = Array.AsReadOnly(notes);
        }
    }

    public sealed class ChartDifficultyData
    {
        public string Id { get; }
        public string Name { get; }
        public double Level { get; }

        internal ChartDifficultyData(string id, string name, double level)
        {
            Id = id;
            Name = name;
            Level = level;
        }
    }

    public sealed class ChartPreviewData
    {
        public long StartMs { get; }
        public long DurationMs { get; }

        internal ChartPreviewData(long startMs, long durationMs)
        {
            StartMs = startMs;
            DurationMs = durationMs;
        }
    }

    public sealed class ChartTimingData
    {
        public double BaseBpm { get; }
        public IReadOnlyList<BpmChangeData> BpmChanges { get; }
        public IReadOnlyList<TimeSignatureData> TimeSignatures { get; }

        internal ChartTimingData(
            double baseBpm,
            BpmChangeData[] bpmChanges,
            TimeSignatureData[] timeSignatures)
        {
            BaseBpm = baseBpm;
            BpmChanges = Array.AsReadOnly(bpmChanges);
            TimeSignatures = Array.AsReadOnly(timeSignatures);
        }
    }

    public sealed class BpmChangeData
    {
        public long TimeMs { get; }
        public double Bpm { get; }

        internal BpmChangeData(long timeMs, double bpm)
        {
            TimeMs = timeMs;
            Bpm = bpm;
        }
    }

    public sealed class TimeSignatureData
    {
        public long TimeMs { get; }
        public int Numerator { get; }
        public int Denominator { get; }

        internal TimeSignatureData(long timeMs, int numerator, int denominator)
        {
            TimeMs = timeMs;
            Numerator = numerator;
            Denominator = denominator;
        }
    }
}

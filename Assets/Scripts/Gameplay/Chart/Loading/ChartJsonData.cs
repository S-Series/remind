using System;

#pragma warning disable CS0649 // Populated by JsonUtility through serialized fields.

namespace REmind.Gameplay.Chart.Loading
{
    [Serializable]
    internal sealed class ChartJsonData
    {
        public string formatVersion;
        public string chartId;
        public string songId;
        public string title;
        public string artist;
        public string charter;
        public ChartDifficultyJsonData difficulty;
        public int laneCount;
        public string audioFile;
        public long chartOffsetMs;
        public ChartPreviewJsonData preview;
        public ChartTimingJsonData timing;
        public NoteJsonData[] notes;
    }

    [Serializable]
    internal sealed class ChartDifficultyJsonData
    {
        public string id;
        public string name;
        public double level;
    }

    [Serializable]
    internal sealed class ChartPreviewJsonData
    {
        public long startMs;
        public long durationMs;
    }

    [Serializable]
    internal sealed class ChartTimingJsonData
    {
        public double baseBpm;
        public BpmChangeJsonData[] bpmChanges;
        public TimeSignatureJsonData[] timeSignatures;
    }

    [Serializable]
    internal sealed class BpmChangeJsonData
    {
        public long timeMs;
        public double bpm;
    }

    [Serializable]
    internal sealed class TimeSignatureJsonData
    {
        public long timeMs;
        public int numerator;
        public int denominator;
    }

    [Serializable]
    internal sealed class NoteJsonData
    {
        public string id;
        public string type;
        public int lane;
        public long timeMs;
        public long durationMs;
    }
}

#pragma warning restore CS0649

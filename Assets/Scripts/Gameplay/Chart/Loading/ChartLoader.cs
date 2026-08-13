using System;
using System.Collections.Generic;
using REmind.Data;
using UnityEngine;

namespace REmind.Gameplay.Chart.Loading
{
    public static class ChartLoader
    {
        private const int SupportedLaneCount = 10;
        private const int SupportedFormatMajor = 0;

        public static ChartData Load(TextAsset chartAsset)
        {
            if (chartAsset == null)
            {
                throw new ChartLoadException("Chart TextAsset is not assigned.");
            }

            return Parse(chartAsset.text, chartAsset.name);
        }

        public static ChartData Parse(string json, string sourceName = "Chart JSON")
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ChartLoadException($"{sourceName}: JSON is empty.");
            }

            ChartJsonData jsonData;

            try
            {
                jsonData = JsonUtility.FromJson<ChartJsonData>(json);
            }
            catch (ArgumentException exception)
            {
                throw new ChartLoadException($"{sourceName}: Invalid JSON format.", exception);
            }

            if (jsonData == null)
            {
                throw new ChartLoadException($"{sourceName}: JSON could not be converted to chart data.");
            }

            ValidateVersion(jsonData.formatVersion, sourceName);
            RequireText(jsonData.chartId, "chartId", sourceName);
            RequireText(jsonData.songId, "songId", sourceName);
            RequireText(jsonData.title, "title", sourceName);
            RequireText(jsonData.artist, "artist", sourceName);
            RequireText(jsonData.charter, "charter", sourceName);
            RequireText(jsonData.audioFile, "audioFile", sourceName);

            if (jsonData.laneCount != SupportedLaneCount)
            {
                throw new ChartLoadException(
                    $"{sourceName}: laneCount must be {SupportedLaneCount}. Actual: {jsonData.laneCount}");
            }

            ChartDifficultyData difficulty = ConvertDifficulty(jsonData.difficulty, sourceName);
            ChartPreviewData preview = ConvertPreview(jsonData.preview, sourceName);
            ChartTimingData timing = ConvertTiming(jsonData.timing, sourceName);
            NoteData[] notes = ConvertNotes(jsonData.notes, jsonData.laneCount, sourceName);

            return new ChartData(
                jsonData.formatVersion,
                jsonData.chartId,
                jsonData.songId,
                jsonData.title,
                jsonData.artist,
                jsonData.charter,
                difficulty,
                jsonData.laneCount,
                jsonData.audioFile,
                jsonData.chartOffsetMs,
                preview,
                timing,
                notes);
        }

        private static ChartDifficultyData ConvertDifficulty(
            ChartDifficultyJsonData difficulty,
            string sourceName)
        {
            if (difficulty == null)
            {
                throw new ChartLoadException($"{sourceName}: difficulty is missing.");
            }

            RequireText(difficulty.id, "difficulty.id", sourceName);
            RequireText(difficulty.name, "difficulty.name", sourceName);

            if (!IsFinite(difficulty.level))
            {
                throw new ChartLoadException($"{sourceName}: difficulty.level must be finite.");
            }

            return new ChartDifficultyData(difficulty.id, difficulty.name, difficulty.level);
        }

        private static ChartPreviewData ConvertPreview(ChartPreviewJsonData preview, string sourceName)
        {
            if (preview == null)
            {
                return null;
            }

            if (preview.startMs < 0)
            {
                throw new ChartLoadException($"{sourceName}: preview.startMs must be non-negative.");
            }

            if (preview.durationMs <= 0)
            {
                throw new ChartLoadException($"{sourceName}: preview.durationMs must be positive.");
            }

            return new ChartPreviewData(preview.startMs, preview.durationMs);
        }

        private static ChartTimingData ConvertTiming(ChartTimingJsonData timing, string sourceName)
        {
            if (timing == null)
            {
                throw new ChartLoadException($"{sourceName}: timing is missing.");
            }

            if (!IsFinitePositive(timing.baseBpm))
            {
                throw new ChartLoadException($"{sourceName}: timing.baseBpm must be positive and finite.");
            }

            BpmChangeData[] bpmChanges = ConvertBpmChanges(timing.bpmChanges, sourceName);
            TimeSignatureData[] timeSignatures = ConvertTimeSignatures(timing.timeSignatures, sourceName);

            return new ChartTimingData(timing.baseBpm, bpmChanges, timeSignatures);
        }

        private static BpmChangeData[] ConvertBpmChanges(
            BpmChangeJsonData[] source,
            string sourceName)
        {
            if (source == null || source.Length == 0)
            {
                throw new ChartLoadException($"{sourceName}: timing.bpmChanges is empty.");
            }

            var result = new BpmChangeData[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                BpmChangeJsonData item = source[i];

                if (item == null)
                {
                    throw new ChartLoadException($"{sourceName}: timing.bpmChanges[{i}] is null.");
                }

                if (item.timeMs < 0 || !IsFinitePositive(item.bpm))
                {
                    throw new ChartLoadException(
                        $"{sourceName}: timing.bpmChanges[{i}] has an invalid time or BPM.");
                }

                result[i] = new BpmChangeData(item.timeMs, item.bpm);
            }

            Array.Sort(result, (left, right) => left.TimeMs.CompareTo(right.TimeMs));

            if (result[0].TimeMs != 0)
            {
                throw new ChartLoadException($"{sourceName}: The first BPM change must be at 0ms.");
            }

            for (int i = 1; i < result.Length; i++)
            {
                if (result[i - 1].TimeMs == result[i].TimeMs)
                {
                    throw new ChartLoadException(
                        $"{sourceName}: Duplicate BPM changes at {result[i].TimeMs}ms.");
                }
            }

            return result;
        }

        private static TimeSignatureData[] ConvertTimeSignatures(
            TimeSignatureJsonData[] source,
            string sourceName)
        {
            if (source == null || source.Length == 0)
            {
                throw new ChartLoadException($"{sourceName}: timing.timeSignatures is empty.");
            }

            var result = new TimeSignatureData[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                TimeSignatureJsonData item = source[i];

                if (item == null)
                {
                    throw new ChartLoadException($"{sourceName}: timing.timeSignatures[{i}] is null.");
                }

                if (item.timeMs < 0 || item.numerator <= 0 || !IsSupportedDenominator(item.denominator))
                {
                    throw new ChartLoadException(
                        $"{sourceName}: timing.timeSignatures[{i}] has invalid values.");
                }

                result[i] = new TimeSignatureData(item.timeMs, item.numerator, item.denominator);
            }

            Array.Sort(result, (left, right) => left.TimeMs.CompareTo(right.TimeMs));

            if (result[0].TimeMs != 0)
            {
                throw new ChartLoadException(
                    $"{sourceName}: The first time signature must be at 0ms.");
            }

            for (int i = 1; i < result.Length; i++)
            {
                if (result[i - 1].TimeMs == result[i].TimeMs)
                {
                    throw new ChartLoadException(
                        $"{sourceName}: Duplicate time signatures at {result[i].TimeMs}ms.");
                }
            }

            return result;
        }

        private static NoteData[] ConvertNotes(NoteJsonData[] source, int laneCount, string sourceName)
        {
            if (source == null)
            {
                throw new ChartLoadException($"{sourceName}: notes array is missing.");
            }

            var result = new NoteData[source.Length];
            var noteIds = new HashSet<string>(StringComparer.Ordinal);
            var occupiedSlots = new HashSet<(long TimeMs, int Lane)>();

            for (int i = 0; i < source.Length; i++)
            {
                NoteJsonData note = source[i];

                if (note == null)
                {
                    throw new ChartLoadException($"{sourceName}: notes[{i}] is null.");
                }

                RequireText(note.id, $"notes[{i}].id", sourceName);

                if (!noteIds.Add(note.id))
                {
                    throw new ChartLoadException($"{sourceName}: Duplicate note ID '{note.id}'.");
                }

                if (!Enum.TryParse(note.type, true, out NoteType noteType) ||
                    !noteType.IsGameplayNote())
                {
                    throw new ChartLoadException(
                        $"{sourceName}: notes[{i}].type '{note.type}' is not a gameplay note.");
                }

                if (note.lane < 0 || note.lane >= laneCount)
                {
                    throw new ChartLoadException(
                        $"{sourceName}: notes[{i}].lane must be between 0 and {laneCount - 1}.");
                }

                if (note.timeMs < 0)
                {
                    throw new ChartLoadException($"{sourceName}: notes[{i}].timeMs must be non-negative.");
                }

                if (noteType.IsLong() && note.durationMs <= 0)
                {
                    throw new ChartLoadException(
                        $"{sourceName}: Long note '{note.id}' must have a positive durationMs.");
                }

                if (!noteType.IsLong() && note.durationMs != 0)
                {
                    throw new ChartLoadException(
                        $"{sourceName}: Non-long note '{note.id}' cannot have durationMs.");
                }

                try
                {
                    checked
                    {
                        _ = note.timeMs + note.durationMs;
                    }
                }
                catch (OverflowException exception)
                {
                    throw new ChartLoadException(
                        $"{sourceName}: End time for note '{note.id}' is out of range.", exception);
                }

                if (!occupiedSlots.Add((note.timeMs, note.lane)))
                {
                    throw new ChartLoadException(
                        $"{sourceName}: Multiple notes at {note.timeMs}ms " +
                        $"on lane {note.lane} would require the same input.");
                }

                result[i] = new NoteData(note.id, noteType, note.lane, note.timeMs, note.durationMs);
            }

            Array.Sort(result, CompareNotes);
            return result;
        }

        private static int CompareNotes(NoteData left, NoteData right)
        {
            int timeComparison = left.TimeMs.CompareTo(right.TimeMs);
            if (timeComparison != 0)
            {
                return timeComparison;
            }

            int laneComparison = left.Lane.CompareTo(right.Lane);
            return laneComparison != 0 ? laneComparison : string.CompareOrdinal(left.Id, right.Id);
        }

        private static void ValidateVersion(string version, string sourceName)
        {
            RequireText(version, "formatVersion", sourceName);

            string[] parts = version.Split('.');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out int major) ||
                !int.TryParse(parts[1], out int minor) ||
                !int.TryParse(parts[2], out int patch) ||
                major < 0 || minor < 0 || patch < 0)
            {
                throw new ChartLoadException(
                    $"{sourceName}: formatVersion must use MAJOR.MINOR.PATCH. Actual: '{version}'");
            }

            if (major != SupportedFormatMajor)
            {
                throw new ChartLoadException(
                    $"{sourceName}: formatVersion '{version}' is not supported by this loader.");
            }
        }

        private static void RequireText(string value, string fieldName, string sourceName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ChartLoadException($"{sourceName}: Required field '{fieldName}' is empty.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0 && IsFinite(value);
        }

        private static bool IsSupportedDenominator(int denominator)
        {
            return denominator == 1 ||
                   denominator == 2 ||
                   denominator == 4 ||
                   denominator == 8 ||
                   denominator == 16;
        }
    }
}

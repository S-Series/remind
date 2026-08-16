using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using REmind.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FileToChart : MonoBehaviour
{
    [SerializeField] private ChartPlacementController placementController;
    [SerializeField] private ChartToFile chartToFile;
    [SerializeField] private ChartCore chartCore;

    private void Awake()
    {
        if (!placementController)
        {
            placementController = FindFirstObjectByType<ChartPlacementController>();
        }

        if (!chartToFile)
        {
            chartToFile = GetComponent<ChartToFile>();
        }

        ResolveChartCore();
    }

    /// <summary>텍스트를 검증하고 현재 ChartManager와 노트 뷰에 적용합니다.</summary>
    public ChartFile LoadText(string text)
    {
        ChartFile chartFile = ParseSupportedChart(text);
        ApplyTimingMetadata(chartFile);
        ChartManager.ReplaceChartData(chartFile.chartDatas);

        if (placementController)
        {
            placementController.RebuildChartViews();
        }
        else
        {
            Debug.LogWarning(
                "Chart data loaded, but no ChartPlacementController was found.",
                this);
        }

        return chartFile;
    }

    private static ChartFile ParseSupportedChart(string text)
    {
        return TryParseLegacyJson(text, out ChartFile legacyChart)
            ? legacyChart
            : ChartFileCodec.Parse(text);
    }

    /// <summary>구버전 JSON 병렬 배열을 현재 편집기 채보 데이터로 변환합니다.</summary>
    private static bool TryParseLegacyJson(
        string text,
        out ChartFile chartFile)
    {
        chartFile = null;

        if (!StartsWithJsonObject(text))
        {
            return false;
        }

        LegacyChartJson legacy;

        try
        {
            legacy = JsonUtility.FromJson<LegacyChartJson>(text);
        }
        catch (Exception exception)
        {
            throw new FormatException(
                $"Invalid legacy chart JSON: {exception.Message}",
                exception);
        }

        if (legacy == null || legacy.NotePos == null || legacy.NoteLine == null)
        {
            throw new FormatException(
                "Legacy chart JSON requires NotePos and NoteLine arrays.");
        }

        if (!IsFinite(legacy.bpm) || legacy.bpm <= 0d)
        {
            throw new FormatException(
                $"Legacy chart BPM must be greater than zero: {legacy.bpm}");
        }

        int noteCount = legacy.NotePos.Length;

        if (legacy.NoteLine.Length != noteCount)
        {
            throw new FormatException(
                "Legacy chart NotePos and NoteLine arrays must have the same length.");
        }

        if (legacy.NotePowered != null &&
            legacy.NotePowered.Length != noteCount)
        {
            throw new FormatException(
                "Legacy chart NotePowered must match the note array length.");
        }

        SortedDictionary<int, ChartHolder> holdersByPosition =
            new SortedDictionary<int, ChartHolder>();

        for (int noteIndex = 0; noteIndex < noteCount; noteIndex++)
        {
            int absolutePosition = ParseLegacyPosition(
                legacy.NotePos[noteIndex],
                noteIndex);
            int storageIndex = ParseLegacyLine(
                legacy.NoteLine[noteIndex],
                noteIndex);

            if (!holdersByPosition.TryGetValue(
                    absolutePosition,
                    out ChartHolder holder))
            {
                holder = new ChartHolder(
                    absolutePosition / ChartHolder.PositionUnitsPerMeasure,
                    absolutePosition % ChartHolder.PositionUnitsPerMeasure);
                holdersByPosition.Add(absolutePosition, holder);
            }

            if (holder.noteTypes[storageIndex] != NoteType.Unknown)
            {
                throw new FormatException(
                    $"Legacy chart contains duplicate notes at index {noteIndex}.");
            }

            bool isPowered = legacy.NotePowered != null &&
                legacy.NotePowered[noteIndex];
            holder.isPoweredNotes[storageIndex] = isPowered;

            if (storageIndex < ChartHolder.MainLineCount)
            {
                holder.noteTypes[storageIndex] = NoteType.Tap;
                holder.noteHandles[storageIndex] =
                    storageIndex < ChartHolder.MainLineCount / 2
                        ? NoteHandleType.Left
                        : NoteHandleType.Right;
            }
            else
            {
                holder.noteTypes[storageIndex] = NoteType.Scratch;
            }
        }

        ChartHolder[] holders = new ChartHolder[holdersByPosition.Count];
        holdersByPosition.Values.CopyTo(holders, 0);
        chartFile = new ChartFile
        {
            FormatVersion = 0,
            HasBaseBpm = true,
            BaseBpm = legacy.bpm,
            HasMusicStartCorrectionMs = true,
            MusicStartCorrectionMs = -legacy.startDelayMs,
            chartDatas = holders
        };
        return true;
    }

    private static bool StartsWithJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return text[i] == '{';
            }
        }

        return false;
    }

    private static int ParseLegacyPosition(double value, int noteIndex)
    {
        if (!IsFinite(value) || value < 0d || value > int.MaxValue)
        {
            throw new FormatException(
                $"Legacy note {noteIndex} has an invalid position: {value}");
        }

        int absolutePosition = checked((int)Math.Round(
            value,
            MidpointRounding.AwayFromZero));
        int chartNumber = absolutePosition /
            ChartHolder.PositionUnitsPerMeasure;

        if (chartNumber > 999)
        {
            throw new FormatException(
                $"Legacy note {noteIndex} is beyond measure 999: {value}");
        }

        return absolutePosition;
    }

    private static int ParseLegacyLine(int line, int noteIndex)
    {
        if (line >= 1 && line <= ChartHolder.MainLineCount)
        {
            return line - 1;
        }

        if (line == 5 || line == 6)
        {
            return ChartHolder.MainLineCount + line - 5;
        }

        throw new FormatException(
            $"Legacy note {noteIndex} has an unsupported line: {line}");
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>파일에 존재하는 편집용 BPM과 음악 시작 보정값을 ChartCore에 복원합니다.</summary>
    private void ApplyTimingMetadata(ChartFile chartFile)
    {
        ResolveChartCore();

        if (!chartCore)
        {
            if (chartFile.HasBaseBpm || chartFile.HasMusicStartCorrectionMs)
            {
                Debug.LogWarning(
                    "Chart timing metadata was loaded, but ChartCore was not found.",
                    this);
            }

            return;
        }

        if (chartFile.HasBaseBpm)
        {
            chartCore.SetBpm(chartFile.BaseBpm);
        }

        if (chartFile.HasMusicStartCorrectionMs)
        {
            chartCore.SetStartCorrectionMs(chartFile.MusicStartCorrectionMs);
        }
    }

    private void ResolveChartCore()
    {
        if (!chartCore)
        {
            chartCore = ChartCore.Instance != null
                ? ChartCore.Instance
                : FindFirstObjectByType<ChartCore>();
        }
    }

    /// <summary>지정한 UTF-8 채보 파일을 읽어 현재 채보에 적용합니다.</summary>
    public ChartFile LoadFromPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "A chart file path is required.",
                nameof(filePath));
        }

        string fullPath = Path.GetFullPath(filePath);
        string text = File.ReadAllText(
            fullPath,
            Encoding.UTF8);
        bool isLegacyJson = StartsWithJsonObject(text);
        ChartFile chartFile = LoadText(text);

        if (chartToFile)
        {
            // Legacy JSON is imported into the richer native text format. Clear
            // its save path so a later Save cannot overwrite the source JSON.
            chartToFile.SetSavePath(isLegacyJson ? null : fullPath);
            chartToFile.MarkCurrentStateAsSaved();
        }

        return chartFile;
    }

    public bool TryLoadText(
        string text,
        out ChartFile chartFile,
        out string error)
    {
        try
        {
            chartFile = LoadText(text);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            chartFile = null;
            error = exception.Message;
            return false;
        }
    }

    public bool TryLoadFromPath(
        string filePath,
        out ChartFile chartFile,
        out string error)
    {
        try
        {
            chartFile = LoadFromPath(filePath);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            chartFile = null;
            error = exception.Message;
            return false;
        }
    }

    [Serializable]
    private sealed class LegacyChartJson
    {
        public double bpm = 0d;
        public int startDelayMs = 0;
        public double[] NotePos = null;
        public int[] NoteLine = null;
        public bool[] NotePowered = null;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using REmind.Charting;
using REmind.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FileToChart : MonoBehaviour
{
    [SerializeField] private ChartPlacementController placementController;
    [SerializeField] private ChartToFile chartToFile;
    [SerializeField] private ChartCore chartCore;

    [Header("Startup")]
    [SerializeField] private bool restoreRecentFilesOnStart = true;

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

    private IEnumerator Start()
    {
        if (!restoreRecentFilesOnStart)
        {
            yield break;
        }

        // Let every chart view finish Awake/OnEnable before native dialogs open.
        yield return null;
        RestoreRecentChartOrPrompt();
        yield return RestoreRecentMusicOrPrompt();
    }

    private void RestoreRecentChartOrPrompt()
    {
        string recentPath = ChartMakerRecentFiles.LastChartPath;

        if (!string.IsNullOrWhiteSpace(recentPath))
        {
            if (TryLoadFromPath(recentPath, out _, out string recentError))
            {
                ChartEditHistory.Clear();
                Debug.Log($"Recent chart restored: {recentPath}", this);
                return;
            }

            Debug.LogWarning(
                $"Could not restore recent chart '{recentPath}': " +
                $"{recentError}",
                this);
        }

        ChartMakerRecentFiles.ForgetChartPath();
        string selectedPath = ChartFileDialog.OpenChartFile(recentPath);

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        if (!TryLoadFromPath(selectedPath, out _, out string selectedError))
        {
            Debug.LogError(
                $"Failed to open selected chart: {selectedError}",
                this);
            return;
        }

        ChartEditHistory.Clear();
        Debug.Log($"Startup chart selected: {selectedPath}", this);
    }

    private IEnumerator RestoreRecentMusicOrPrompt()
    {
        ResolveChartCore();

        if (!chartCore)
        {
            Debug.LogError("ChartCore was not found.", this);
            yield break;
        }

        if (chartToFile && chartToFile.HasSavePath)
        {
            string dataPath = SongContentFileStore.FindDataPath(
                chartToFile.CurrentFilePath);
            if (dataPath != null)
            {
                string audioPath;
                float songVolume = 0.5f;
                try
                {
                    SongContent song = SongContentFileStore.Load(dataPath);
                    audioPath = SongContentCodec.ResolveReference(dataPath,
                        song.AudioFile);
                    songVolume = song.PlaybackVolume;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Song data could not restore audio: " +
                        exception.Message, this);
                    audioPath = null;
                }
                if (audioPath != null)
                {
                    while (chartCore.IsAudioLoading) yield return null;
                    if (ChartMakerRecentFiles.AreSamePath(
                            chartCore.CurrentAudioFilePath, audioPath))
                    {
                        if (chartCore.AudioSource)
                            chartCore.AudioSource.volume = songVolume;
                        yield break;
                    }
                    if (chartCore.LoadAudioFile(audioPath, songVolume))
                    {
                        while (chartCore.IsAudioLoading) yield return null;
                        if (ChartMakerRecentFiles.AreSamePath(
                                chartCore.CurrentAudioFilePath, audioPath))
                            yield break;
                    }
                    Debug.LogWarning("Song audio could not be loaded: " +
                        audioPath, this);
                }
            }
        }

        string recentPath = ChartMakerRecentFiles.LastAudioPath;

        if (!string.IsNullOrWhiteSpace(recentPath) &&
            chartCore.LoadAudioFile(recentPath))
        {
            while (chartCore.IsAudioLoading)
            {
                yield return null;
            }

            if (ChartMakerRecentFiles.AreSamePath(
                    chartCore.CurrentAudioFilePath,
                    recentPath))
            {
                Debug.Log($"Recent music restored: {recentPath}", this);
                yield break;
            }
        }

        if (!string.IsNullOrWhiteSpace(recentPath))
        {
            Debug.LogWarning(
                $"Could not restore recent music '{recentPath}'.",
                this);
        }

        ChartMakerRecentFiles.ForgetAudioPath();
        string selectedPath = ChartFileDialog.OpenAudioFile(recentPath);

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            yield break;
        }

        if (!chartCore.LoadAudioFile(selectedPath))
        {
            Debug.LogError(
                $"The selected music could not start loading: {selectedPath}",
                this);
            yield break;
        }

        while (chartCore.IsAudioLoading)
        {
            yield return null;
        }

        if (!ChartMakerRecentFiles.AreSamePath(
                chartCore.CurrentAudioFilePath,
                selectedPath))
        {
            Debug.LogError(
                $"The selected music could not be loaded: {selectedPath}",
                this);
        }
    }

    /// <summary>텍스트를 검증하고 현재 ChartManager와 노트 뷰에 적용합니다.</summary>
    public ChartFile LoadText(string text)
    {
        ChartFile chartFile = ParseSupportedChart(text);
        if (chartFile.HasEffectParameterFile)
            throw new FormatException("This older chart needs its paired Effect JSON. Open the .rd file by path.");
        return ApplyLoadedChart(chartFile);
    }

    private ChartFile ApplyLoadedChart(ChartFile chartFile)
    {
        ApplyTimingMetadata(chartFile);
        ChartManager.ReplaceChartData(chartFile.chartDatas);
        ChartEffectDocumentState.Apply(chartFile);
        chartToFile?.SetDocumentJacketFile(chartFile.JacketFile);

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
        if (ChartFileCodec.IsCurrentJsonDocument(text))
        {
            return ChartFileCodec.Parse(text);
        }

        return TryParseLegacyJson(text, out ChartFile legacyChart)
            ? legacyChart
            : ChartFileCodec.ParseLegacyText(text);
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

            if (storageIndex < ChartHolder.MainLineCount)
            {
                holder.noteTypes[storageIndex] = NoteType.Tap;
                holder.isPoweredNotes[storageIndex] = false;
                holder.noteHandles[storageIndex] =
                    storageIndex < ChartHolder.MainLineCount / 2
                        ? NoteHandleType.Left
                        : NoteHandleType.Right;
            }
            else
            {
                holder.noteTypes[storageIndex] = NoteType.Scratch;
                holder.isPoweredNotes[storageIndex] = isPowered;
                int scratchIndex =
                    storageIndex - ChartHolder.MainLineCount;
                holder.scratchPointTypes[scratchIndex] =
                    ScratchPointType.Tap;
                holder.scratchMotions[scratchIndex] =
                    new ScratchMotionData(
                        0,
                        isPowered
                            ? ScratchMotionType.Instant
                            : ScratchMotionType.None);
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

        int legacyAbsolutePosition = checked((int)Math.Round(
            value,
            MidpointRounding.AwayFromZero));
        int absolutePosition = ChartHolder.ConvertAbsolutePosition(
            legacyAbsolutePosition,
            ChartFileCodec.LegacyPositionUnitsPerMeasure);
        int chartNumber = absolutePosition /
            ChartHolder.PositionUnitsPerMeasure;

        if (chartNumber > ChartHolder.MaximumMeasureNumber)
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
        ResolveChartCore();

        if (chartCore && chartCore.IsTestPlaying)
        {
            throw new InvalidOperationException(
                "Stop test playback before opening another chart.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "A chart file path is required.",
                nameof(filePath));
        }

        string fullPath = Path.GetFullPath(filePath);

        if (!ChartToFile.HasChartFileExtension(fullPath))
        {
            throw new NotSupportedException(
                $"Only {ChartToFile.ChartFileExtension} chart files " +
                "can be opened.");
        }

        bool currentExists = File.Exists(fullPath);
        string text = currentExists
            ? File.ReadAllText(fullPath, Encoding.UTF8)
            : null;
        bool isCurrentJson = currentExists &&
            ChartFileCodec.IsCurrentJsonDocument(text);
        bool backupIsCurrentJson = IsCurrentJsonFile(fullPath + ".bak");
        bool loadedFromPairedStore = false;
        bool recovered = false;
        ChartFile chartFile;
        if (isCurrentJson || (!currentExists && backupIsCurrentJson))
        {
            chartFile = ChartEffectFileStore.Load(fullPath, out recovered, out _);
            loadedFromPairedStore = true;
        }
        else if (currentExists)
        {
            try
            {
                chartFile = ParseSupportedChart(text);
                if (chartFile.HasEffectParameterFile)
                    throw new FormatException(
                        "This older chart needs its paired Effect JSON. Open the .rd file by path.");
            }
            catch (Exception currentException)
            {
                if (!backupIsCurrentJson) throw;
                try
                {
                    chartFile = ChartEffectFileStore.Load(fullPath,
                        out recovered, out _);
                    loadedFromPairedStore = true;
                }
                catch (Exception recoveryException)
                {
                    throw new IOException(
                        "The current chart is invalid and its paired backup could " +
                        "not be recovered.",
                        new AggregateException(currentException,
                            recoveryException));
                }
            }
        }
        else
        {
            throw new FileNotFoundException("The chart and a recoverable backup do not exist.", fullPath);
        }

        SongContent songContent = SongContentFileStore.ValidateChart(fullPath,
            chartFile);
        ApplyLoadedChart(chartFile);

        if (songContent != null && chartCore)
        {
            string dataPath = SongContentFileStore.FindDataPath(fullPath);
            string audioPath = SongContentCodec.ResolveReference(dataPath,
                songContent.AudioFile);
            if (ChartMakerRecentFiles.AreSamePath(
                    chartCore.CurrentAudioFilePath, audioPath))
            {
                if (chartCore.AudioSource)
                    chartCore.AudioSource.volume = songContent.PlaybackVolume;
            }
            else if (!chartCore.LoadAudioFile(audioPath,
                         songContent.PlaybackVolume))
                Debug.LogWarning("Song audio could not start loading: " +
                    audioPath, this);
        }

        if (recovered)
            Debug.LogWarning("Loaded a backup chart. Save to make this recovered state current.", this);

        if (chartToFile)
        {
            // 현재 JSON 문서만 같은 .rd 경로에 다시 저장할 수 있습니다.
            // 이전 포맷을 담은 .rd는 새 저장 경로를 요구합니다.
            chartToFile.SetSavePath(loadedFromPairedStore ? fullPath : null);
            chartToFile.MarkCurrentStateAsSaved();
            if (loadedFromPairedStore && recovered)
                chartToFile.MarkRecoveredStateRequiresSave();
        }

        ChartMakerRecentFiles.RememberChartPath(fullPath);

        return chartFile;
    }

    private static bool IsCurrentJsonFile(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            return ChartFileCodec.IsCurrentJsonDocument(
                File.ReadAllText(path, Encoding.UTF8));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TryReloadEffectParameters(out string error)
    {
        try
        {
            if (chartCore && chartCore.IsTestPlaying)
                throw new InvalidOperationException("Stop test playback before reloading Effect parameters.");
            if (!chartToFile || !chartToFile.HasSavePath)
                throw new InvalidOperationException("Save/open a .rd file before reloading Effect parameters.");
            ChartFile saved = ChartEffectFileStore.Load(chartToFile.CurrentFilePath,
                out _, out _);
            ChartEffectJsonCodec.BuildParameterMap(saved.chartDatas, saved.GimmickId);
            var positions = new List<int>();
            var savedParameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ChartHolder holder in saved.chartDatas)
                if (holder.isEffect)
                    savedParameters.Add(holder.effectId, holder.effectParametersJson);
            foreach (ChartHolder holder in ChartManager.ChartHolders)
                if (holder.isEffect)
                {
                    if (!savedParameters.ContainsKey(holder.effectId))
                        throw new FormatException(
                            "The saved .rd has no matching Effect ID: " + holder.effectId);
                    positions.Add(holder.AbsoluteChartPosition);
                }
            if (positions.Count != savedParameters.Count)
                throw new FormatException(
                    "The saved .rd and editor have different Effect sets. Reopen the chart to reload all data.");
            var transaction = ChartEditHistory.BeginChange(positions.ToArray());
            foreach (ChartHolder holder in ChartManager.ChartHolders)
                if (holder.isEffect &&
                    savedParameters.TryGetValue(holder.effectId, out string parameters))
                    holder.effectParametersJson = parameters;
            ChartEditHistory.CommitChange(transaction);
            ChartManager.NotifyChartChanged();
            error = null;
            return true;
        }
        catch (Exception exception) { error = exception.Message; return false; }
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

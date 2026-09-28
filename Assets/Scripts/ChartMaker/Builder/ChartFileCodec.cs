using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using REmind.Charting;
using REmind.Data;
using UnityEngine;

public static class ChartFileCodec
{
    public const int CurrentFormatVersion = 1;
    private const int PreviousEffectJsonFormatVersion = 8;
    private const int CompactJsonFormatVersion = 7;
    internal const int LegacyPositionUnitsPerMeasure = 1600;

    private const string JsonFormatName = "REmindChart";
    private const int ObjectJsonFormatVersion = 6;
    private const int LegacyNativeFormatVersion = 6;
    private const string FormatHeader = "#REmindChart";
    private const string BpmHeader = "#BPM";
    private const string MusicStartCorrectionHeader =
        "#MUSIC_START_CORRECTION_MS";
    private const int LegacyFieldCount = 8;
    private const int LegacyMotionFieldCount = 9;
    private const int Version4FieldCount = 8;
    private const int CurrentFieldCount = 9;
    private const int MainNoteTextLength = 8;
    private const int LegacyScratchNoteTextLength = 4;
    private const int ScratchNoteTextLength = 8;
    private const int AirNoteTextLength = 8;

    private static readonly CultureInfo Invariant =
        CultureInfo.InvariantCulture;

    /// <summary>현재 채보와 편집용 타이밍 설정을 JSON 문서로 변환합니다.</summary>
    public static string Serialize(
        IReadOnlyList<ChartHolder> holders,
        double baseBpm,
        double musicStartCorrectionMs,
        ChartEffectDocumentState.Metadata? effectMetadata = null,
        string jacketFile = null)
    {
        // 기존 Native codec의 검증/정규화 규칙을 단일 기준으로 재사용한 뒤
        // 검증된 편집 데이터를 JSON DTO로 변환합니다.
        string validatedText = SerializeLegacyText(
            holders,
            baseBpm,
            musicStartCorrectionMs);
        ChartFile normalized = ParseLegacyText(validatedText);
        ChartEffectDocumentState.Metadata metadata = effectMetadata ??
            new ChartEffectDocumentState.Metadata("untitled", "default", "", "");
        normalized.MusicId = metadata.MusicId;
        normalized.DifficultyId = metadata.DifficultyId;
        if (!string.IsNullOrEmpty(jacketFile))
            SongContentCodec.ValidateFileName(jacketFile, "jacketFile");
        normalized.JacketFile = jacketFile;
        normalized.GimmickId = metadata.GimmickId;
        normalized.EffectRevision = metadata.Revision;
        Dictionary<int, ChartHolder> sources = new Dictionary<int, ChartHolder>();
        foreach (ChartHolder source in holders)
        {
            if (source == null || !source.isEffect) continue;
            source.EnsureEffectIdentity();
            sources.Add(source.AbsoluteChartPosition, source);
        }
        normalized.HasEffectParameterFile = false;
        foreach (ChartHolder target in normalized.chartDatas)
        {
            if (!target.isEffect) continue;
            ChartHolder source = sources[target.AbsoluteChartPosition];
            target.effectId = source.effectId;
            target.effectTypeId = source.effectTypeId;
            target.effectCommandId = source.effectCommandId;
            target.effectOrder = source.effectOrder;
            target.effectParametersJson = source.effectParametersJson;
        }
        return SerializeJson(normalized);
    }

    /// <summary>기존 .txt 채보를 읽기 위한 Native 텍스트 직렬화 규칙입니다.</summary>
    internal static string SerializeLegacyText(
        IReadOnlyList<ChartHolder> holders,
        double baseBpm,
        double musicStartCorrectionMs)
    {
        if (holders == null)
        {
            throw new ArgumentNullException(nameof(holders));
        }

        ValidateBaseBpm(baseBpm);
        ValidateFinite(
            musicStartCorrectionMs,
            nameof(musicStartCorrectionMs));

        List<ChartHolder> ordered = new List<ChartHolder>(holders.Count);

        for (int i = 0; i < holders.Count; i++)
        {
            if (holders[i] != null)
            {
                holders[i].EnsureStorage();
                ordered.Add(holders[i].CloneData());
            }
        }

        ordered.Sort(
            (left, right) => left.AbsoluteChartPosition.CompareTo(
                right.AbsoluteChartPosition));
        ChartLongNoteSaveNormalizer.CreatePlan(ordered).Apply();

        StringBuilder output = new StringBuilder();
        AppendMetadata(
            output,
            baseBpm,
            musicStartCorrectionMs);
        bool[] openLongs = new bool[ChartHolder.TotalLineCount];
        int previousPosition = -1;

        for (int i = 0; i < ordered.Count; i++)
        {
            ChartHolder holder = ordered[i];
            ValidateHolderPosition(holder);

            if (holder.AbsoluteChartPosition <= previousPosition)
            {
                throw new InvalidOperationException(
                    $"Duplicate or overlapping chart position: " +
                    $"{holder.ChartNumber:D3}|{holder.ChartPos:D4}");
            }

            if (i > 0)
            {
                output.Append('\n');
            }

            output.Append(holder.ChartNumber.ToString("D3", Invariant));
            output.Append('|');
            output.Append(holder.ChartPos.ToString("D4", Invariant));
            output.Append('|');
            AppendMainNotes(output, holder, openLongs);
            output.Append('|');
            AppendScratchNotes(output, holder, openLongs);
            output.Append('|');
            AppendAirNotes(output, holder);
            output.Append('|');
            output.Append(FormatBpm(holder.targetBpm));
            output.Append('|');
            output.Append(FormatLineSpeed(holder));
            output.Append('|');
            output.Append(holder.isEffect ? 'T' : 'F');
            output.Append('|');
            output.Append(FormatCamera(holder));

            if (holder.isMarker)
            {
                output.Append('*');
            }

            previousPosition = holder.AbsoluteChartPosition;
        }

        EnsureAllLongsClosed(openLongs, "Cannot save chart");
        return output.ToString();
    }

    /// <summary>JSON 전체를 검증한 뒤 독립된 채보 데이터로 변환합니다.</summary>
    public static ChartFile Parse(string text)
    {
        return ParseJson(text);
    }

    /// <summary>기존 Native 텍스트 전체를 검증해 현재 채보 데이터로 변환합니다.</summary>
    internal static ChartFile ParseLegacyText(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        List<ChartHolder> holders = new List<ChartHolder>();
        bool[] openLongs = new bool[ChartHolder.TotalLineCount];
        int formatVersion = 0;
        bool hasFormatVersion = false;
        bool hasBaseBpm = false;
        double baseBpm = 0d;
        bool hasMusicStartCorrectionMs = false;
        double musicStartCorrectionMs = 0d;
        int previousPosition = -1;
        int lineNumber = 0;

        using StringReader reader = new StringReader(text);
        string line;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            line = line.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            bool isMarker = false;

            if (!line.StartsWith("#", StringComparison.Ordinal) &&
                line.EndsWith("*", StringComparison.Ordinal))
            {
                isMarker = true;
                line = line.Substring(0, line.Length - 1);
            }

            if (line.IndexOf('*') >= 0)
            {
                throw CreateFormatException(
                    lineNumber,
                    "Marker '*' is only allowed once at the end of a chart row.");
            }

            if (TryParseMetadata(
                    line,
                    lineNumber,
                    ref formatVersion,
                    ref hasFormatVersion,
                    ref baseBpm,
                    ref hasBaseBpm,
                    ref musicStartCorrectionMs,
                    ref hasMusicStartCorrectionMs))
            {
                continue;
            }

            string[] fields = line.Split('|');

            int expectedFieldCount = formatVersion >= 5
                ? CurrentFieldCount
                : formatVersion >= 4
                    ? Version4FieldCount
                : formatVersion >= 2
                    ? LegacyMotionFieldCount
                    : LegacyFieldCount;

            if (fields.Length != expectedFieldCount)
            {
                throw CreateFormatException(
                    lineNumber,
                    $"Expected {expectedFieldCount} fields but found " +
                    $"{fields.Length}.");
            }

            int chartNumber = ParseFixedDigits(
                fields[0],
                3,
                0,
                ChartHolder.MaximumMeasureNumber,
                lineNumber,
                "measure number");
            int sourceUnitsPerMeasure = formatVersion >= 3
                ? ChartHolder.PositionUnitsPerMeasure
                : LegacyPositionUnitsPerMeasure;
            int maximumSourcePosition = formatVersion >= 3
                ? sourceUnitsPerMeasure - 1
                : sourceUnitsPerMeasure;
            int sourceChartPosition = ParseFixedDigits(
                fields[1],
                4,
                0,
                maximumSourcePosition,
                lineNumber,
                "measure position");
            int sourceAbsolutePosition = checked(
                chartNumber * sourceUnitsPerMeasure +
                sourceChartPosition);
            int absolutePosition = ChartHolder.ConvertAbsolutePosition(
                sourceAbsolutePosition,
                sourceUnitsPerMeasure);

            if (absolutePosition > ChartHolder.MaximumAbsolutePosition)
            {
                throw CreateFormatException(
                    lineNumber,
                    "Chart position exceeds measure 999 after conversion.");
            }

            if (absolutePosition <= previousPosition)
            {
                throw CreateFormatException(
                    lineNumber,
                    "Rows must be ordered by position without duplicates.");
            }

            ChartHolder holder = new ChartHolder(
                absolutePosition / ChartHolder.PositionUnitsPerMeasure,
                absolutePosition % ChartHolder.PositionUnitsPerMeasure);
            holder.isMarker = isMarker;
            ParseMainNotes(fields[2], holder, openLongs, lineNumber);

            if (formatVersion >= 4)
            {
                ParseScratchNotes(fields[3], holder, openLongs, lineNumber);
            }
            else
            {
                ParseLegacyScratchNotes(
                    fields[3],
                    holder,
                    openLongs,
                    lineNumber);
            }

            ParseAirNotes(fields[4], holder, lineNumber);
            holder.targetBpm = ParseBpm(fields[5], lineNumber);
            int effectFieldIndex;

            if (formatVersion >= 5)
            {
                ParseLineSpeed(
                    fields[6],
                    lineNumber,
                    out holder.hasLineSpeedChange,
                    out holder.targetLineSpeed);
                effectFieldIndex = 7;
            }
            else
            {
                holder.hasLineSpeedChange = false;
                holder.targetLineSpeed = 1f;
                effectFieldIndex = 6;
            }

            holder.isEffect = ParseBoolean(
                fields[effectFieldIndex],
                lineNumber,
                "effect");
            if (formatVersion >= 6)
            {
                ParseCamera(
                    fields[effectFieldIndex + 1],
                    lineNumber,
                    holder);
            }
            else
            {
                holder.isCameraMove = ParseBoolean(
                    fields[effectFieldIndex + 1],
                    lineNumber,
                    "camera movement");
                holder.cameraOffsetX = 0f;
                holder.cameraSpinDirection =
                    ChartCameraSpinDirection.None;
            }

            if (formatVersion >= 2 && formatVersion < 4)
            {
                ParseLegacyScratchMotions(fields[8], holder, lineNumber);
            }

            if (formatVersion < 4)
            {
                NormalizeLegacyScratchData(holder, formatVersion);
            }

            holders.Add(holder);
            previousPosition = absolutePosition;
        }

        EnsureAllLongsClosed(openLongs, "End of file");

        if (hasFormatVersion &&
            (!hasBaseBpm || !hasMusicStartCorrectionMs))
        {
            throw new FormatException(
                $"Chart format {formatVersion} requires both {BpmHeader} and " +
                $"{MusicStartCorrectionHeader} headers.");
        }

        return new ChartFile
        {
            FormatVersion = formatVersion,
            HasBaseBpm = hasBaseBpm,
            BaseBpm = baseBpm,
            HasMusicStartCorrectionMs = hasMusicStartCorrectionMs,
            MusicStartCorrectionMs = musicStartCorrectionMs,
            chartDatas = holders.ToArray()
        };
    }

    /// <summary>문서가 현재 ChartMaker JSON 형식인지 헤더만 확인합니다.</summary>
    public static bool IsCurrentJsonDocument(string text)
    {
        if (!StartsWithJsonObject(text))
        {
            return false;
        }

        try
        {
            JsonChartHeader header =
                JsonUtility.FromJson<JsonChartHeader>(text);
            return header != null &&
                string.Equals(
                    header.format,
                    JsonFormatName,
                    StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string SerializeJson(ChartFile chartFile)
    {
        ChartHolder[] holders = chartFile.chartDatas ??
            Array.Empty<ChartHolder>();
        string nativeText = SerializeLegacyText(
            holders,
            chartFile.BaseBpm,
            chartFile.MusicStartCorrectionMs);
        List<string> eventRows = new List<string>(holders.Length);

        using StringReader reader = new StringReader(nativeText);
        string row;

        while ((row = reader.ReadLine()) != null)
        {
            row = row.Trim();

            if (row.Length > 0 &&
                !row.StartsWith("#", StringComparison.Ordinal))
            {
                eventRows.Add(row);
            }
        }

        JsonCurrentChartDocument document = new JsonCurrentChartDocument
        {
            format = JsonFormatName,
            formatVersion = CurrentFormatVersion,
            musicId = chartFile.MusicId,
            difficultyId = chartFile.DifficultyId,
            jacketFile = chartFile.JacketFile,
            gimmickId = chartFile.GimmickId,
            baseBpm = chartFile.BaseBpm,
            musicStartCorrectionMs = chartFile.MusicStartCorrectionMs,
            revision = chartFile.EffectRevision,
            notes = eventRows.ToArray(),
            eventDictionary = BuildEffectDefinitions(holders)
        };
        XElement root = ChartEffectJsonCodec.ReadObject(JsonUtility.ToJson(document));
        XElement entries = ChartEffectJsonCodec.Member(root, "eventDictionary");
        ChartEffectJsonCodec.RequireType(entries, "array");
        Dictionary<string, ChartHolder> effects = new Dictionary<string, ChartHolder>(StringComparer.Ordinal);
        foreach (ChartHolder holder in holders)
            if (holder.isEffect) effects.Add(holder.effectId, holder);
        foreach (XElement entry in entries.Elements())
        {
            string id = ChartEffectJsonCodec.String(entry, "effectId");
            string parametersJson = effects[id].effectParametersJson;
            if (string.IsNullOrWhiteSpace(parametersJson)) continue;
            XElement parameters = ChartEffectJsonCodec.ReadObject(parametersJson);
            parameters.Name = "parameters";
            entry.Add(parameters);
        }
        return ChartEffectJsonCodec.WriteObject(root);
    }

    private static ChartFile ParseJson(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (!StartsWithJsonObject(text))
        {
            throw new FormatException(
                "Chart JSON must start with an object.");
        }

        JsonChartHeader header;

        try
        {
            header = JsonUtility.FromJson<JsonChartHeader>(text);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException(
                $"Invalid chart JSON: {exception.Message}",
                exception);
        }

        if (header == null ||
            !string.Equals(
                header.format,
                JsonFormatName,
                StringComparison.Ordinal))
        {
            throw new FormatException(
                $"Chart JSON requires format '{JsonFormatName}'.");
        }

        if (header.formatVersion == CurrentFormatVersion ||
            header.formatVersion == PreviousEffectJsonFormatVersion ||
            header.formatVersion == CompactJsonFormatVersion)
        {
            return ParseCompactJson(text);
        }

        if (header.formatVersion != ObjectJsonFormatVersion)
        {
            throw new FormatException(
                $"Unsupported JSON chart format version " +
                $"'{header.formatVersion}'. Current version is " +
                $"{CurrentFormatVersion}.");
        }

        JsonChartDocument document;

        try
        {
            document = JsonUtility.FromJson<JsonChartDocument>(text);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException(
                $"Invalid chart JSON: {exception.Message}",
                exception);
        }

        if (document == null ||
            !string.Equals(
                document.format,
                JsonFormatName,
                StringComparison.Ordinal))
        {
            throw new FormatException(
                $"Chart JSON requires format '{JsonFormatName}'.");
        }

        if (document.formatVersion != ObjectJsonFormatVersion)
        {
            throw new FormatException(
                $"Unsupported JSON chart format version " +
                $"'{document.formatVersion}'. Expected " +
                $"{ObjectJsonFormatVersion}.");
        }

        ValidateBaseBpm(document.baseBpm);
        ValidateFinite(
            document.musicStartCorrectionMs,
            nameof(document.musicStartCorrectionMs));
        JsonChartEvent[] sourceEvents = document.events ??
            Array.Empty<JsonChartEvent>();
        ChartHolder[] holders = new ChartHolder[sourceEvents.Length];
        bool[] openLongTaps = new bool[ChartHolder.MainLineCount];
        bool[] openLongScratches =
            new bool[ChartHolder.ScratchLineCount];
        int previousPosition = -1;

        for (int eventIndex = 0;
             eventIndex < sourceEvents.Length;
             eventIndex++)
        {
            JsonChartEvent source = sourceEvents[eventIndex];

            if (source == null)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    "Event cannot be null.");
            }

            if (source.measure < 0 ||
                source.measure > ChartHolder.MaximumMeasureNumber ||
                source.position < 0 ||
                source.position >= ChartHolder.PositionUnitsPerMeasure)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Position must be measure 0-{ChartHolder.MaximumMeasureNumber} " +
                    $"and position 0-{ChartHolder.PositionUnitsPerMeasure - 1}.");
            }

            int absolutePosition = checked(
                source.measure * ChartHolder.PositionUnitsPerMeasure +
                source.position);

            if (absolutePosition <= previousPosition)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    "Events must be ordered by position without duplicates.");
            }

            ChartHolder holder = new ChartHolder(
                source.measure,
                source.position);
            ParseJsonMainNotes(
                source.mainNotes,
                holder,
                openLongTaps,
                eventIndex);
            ParseJsonScratchNotes(
                source.scratchNotes,
                holder,
                openLongScratches,
                eventIndex);
            ParseJsonAirNotes(source.airNotes, holder, eventIndex);
            ParseJsonEvents(source, holder, eventIndex);
            holders[eventIndex] = holder;
            previousPosition = absolutePosition;
        }

        EnsureNoOpenLongTaps(openLongTaps, "End of JSON chart");
        EnsureNoOpenLongScratches(
            openLongScratches,
            "End of JSON chart");
        return new ChartFile
        {
            FormatVersion = document.formatVersion,
            HasBaseBpm = true,
            BaseBpm = document.baseBpm,
            HasMusicStartCorrectionMs = true,
            MusicStartCorrectionMs = document.musicStartCorrectionMs,
            chartDatas = holders
        };
    }

    private static ChartFile ParseCompactJson(string text)
    {
        JsonCompactChartDocument document;

        try
        {
            document = JsonUtility.FromJson<JsonCompactChartDocument>(text);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException(
                $"Invalid compact chart JSON: {exception.Message}",
                exception);
        }

        if (document == null ||
            !string.Equals(
                document.format,
                JsonFormatName,
                StringComparison.Ordinal) ||
            (document.formatVersion != CurrentFormatVersion &&
             document.formatVersion != PreviousEffectJsonFormatVersion &&
             document.formatVersion != CompactJsonFormatVersion))
        {
            throw new FormatException(
                $"Unsupported compact chart JSON format or version. " +
                $"Current format is '{JsonFormatName}' version {CurrentFormatVersion}.");
        }

        ValidateBaseBpm(document.baseBpm);
        ValidateFinite(
            document.musicStartCorrectionMs,
            nameof(document.musicStartCorrectionMs));
        bool inline = document.formatVersion == CurrentFormatVersion;
        if (inline && (document.notes == null || document.eventDictionary == null))
            throw new FormatException("Chart version 1 requires both notes and eventDictionary.");
        string[] eventRows = inline ? document.notes : document.events ?? Array.Empty<string>();
        StringBuilder nativeText = new StringBuilder();
        AppendMetadata(
            nativeText,
            document.baseBpm,
            document.musicStartCorrectionMs);

        for (int eventIndex = 0;
             eventIndex < eventRows.Length;
             eventIndex++)
        {
            string row = eventRows[eventIndex];

            if (string.IsNullOrWhiteSpace(row))
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    "Compact event row cannot be empty.");
            }

            row = row.Trim();

            if (row.StartsWith("#", StringComparison.Ordinal) ||
                row.IndexOf('\r') >= 0 ||
                row.IndexOf('\n') >= 0)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    "Compact event row cannot contain metadata or a newline.");
            }

            nativeText.Append(row);
            nativeText.Append('\n');
        }

        ChartFile parsed = ParseLegacyText(nativeText.ToString());
        parsed.FormatVersion = document.formatVersion;
        parsed.MusicId = document.musicId;
        parsed.DifficultyId = document.difficultyId;
        if (!string.IsNullOrEmpty(document.jacketFile))
            SongContentCodec.ValidateFileName(document.jacketFile, "jacketFile");
        parsed.JacketFile = document.jacketFile;
        parsed.GimmickId = document.gimmickId;
        parsed.EffectRevision = document.revision;
        parsed.HasEffectParameterFile = !inline && document.hasEffectParameters;
        if (document.formatVersion == CurrentFormatVersion ||
            document.formatVersion == PreviousEffectJsonFormatVersion)
            ApplyEffectDefinitions(parsed.chartDatas,
                inline ? document.eventDictionary : document.effectDefinitions,
                inline ? ReadInlineParameters(text, document.eventDictionary) : null);
        else
            foreach (ChartHolder holder in parsed.chartDatas) holder.EnsureEffectIdentity();
        if (inline)
            ChartEffectJsonCodec.ValidateKnownParameters(parsed.chartDatas,
                parsed.GimmickId);
        return parsed;
    }

    private static Dictionary<string, string> ReadInlineParameters(
        string text, JsonEffectDefinition[] definitions)
    {
        XElement root = ChartEffectJsonCodec.ReadObject(text);
        ChartEffectJsonCodec.Only(root, "format", "formatVersion", "musicId",
            "difficultyId", "jacketFile", "gimmickId", "baseBpm",
            "musicStartCorrectionMs", "revision", "notes", "eventDictionary");
        XElement entries = ChartEffectJsonCodec.Member(root, "eventDictionary");
        ChartEffectJsonCodec.RequireType(entries, "array");
        XElement[] items = new List<XElement>(entries.Elements()).ToArray();
        if (items.Length != definitions.Length)
            throw new FormatException("eventDictionary entry count changed during parsing.");
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Length; i++)
        {
            XElement entry = items[i];
            ChartEffectJsonCodec.RequireType(entry, "object");
            ChartEffectJsonCodec.Only(entry, "position", "effectId", "effectTypeId",
                "commandId", "order", "parameters");
            if (ChartEffectJsonCodec.String(entry, "effectId") != definitions[i].effectId)
                throw new FormatException("eventDictionary Effect IDs do not match.");
            XElement parameters = entry.Element("parameters");
            if (parameters == null) continue;
            ChartEffectJsonCodec.RequireType(parameters, "object");
            result.Add(definitions[i].effectId,
                ChartEffectJsonCodec.WriteObject(parameters));
        }
        return result;
    }

    private static JsonEffectDefinition[] BuildEffectDefinitions(IReadOnlyList<ChartHolder> holders)
    {
        List<JsonEffectDefinition> result = new List<JsonEffectDefinition>();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChartHolder holder in holders)
        {
            if (!holder.isEffect) continue;
            if (string.IsNullOrWhiteSpace(holder.effectId) || !ids.Add(holder.effectId))
                throw new FormatException("Effect IDs must be nonempty and unique: " + holder.effectId);
            if (holder.effectOrder < 0) throw new FormatException("Effect order cannot be negative.");
            result.Add(new JsonEffectDefinition {
                position = holder.AbsoluteChartPosition,
                effectId = holder.effectId,
                effectTypeId = holder.effectTypeId ?? "",
                commandId = holder.effectCommandId ?? "",
                order = holder.effectOrder
            });
        }
        return result.ToArray();
    }

    private static void ApplyEffectDefinitions(ChartHolder[] holders,
        JsonEffectDefinition[] definitions,
        IReadOnlyDictionary<string, string> parametersById = null)
    {
        Dictionary<int, ChartHolder> effects = new Dictionary<int, ChartHolder>();
        foreach (ChartHolder holder in holders)
            if (holder.isEffect) effects.Add(holder.AbsoluteChartPosition, holder);
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonEffectDefinition definition in definitions ?? Array.Empty<JsonEffectDefinition>())
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.effectId) ||
                !ids.Add(definition.effectId) || definition.order < 0 ||
                !effects.TryGetValue(definition.position, out ChartHolder holder))
                throw new FormatException("Effect definition has a duplicate ID, invalid order, or no matching Effect row.");
            holder.effectId = definition.effectId;
            holder.effectTypeId = definition.effectTypeId;
            holder.effectCommandId = definition.commandId;
            holder.effectOrder = definition.order;
            if (parametersById != null &&
                parametersById.TryGetValue(definition.effectId, out string parametersJson))
                holder.effectParametersJson = parametersJson;
            effects.Remove(definition.position);
        }
        if (effects.Count > 0)
            throw new FormatException("An Effect row has no matching definition. Restore the matching chart revision.");
    }

    private static void ParseJsonMainNotes(
        JsonMainNote[] notes,
        ChartHolder holder,
        bool[] openLongTaps,
        int eventIndex)
    {
        notes ??= Array.Empty<JsonMainNote>();

        for (int noteIndex = 0; noteIndex < notes.Length; noteIndex++)
        {
            JsonMainNote note = notes[noteIndex];

            if (note == null ||
                note.line < 1 ||
                note.line > ChartHolder.MainLineCount)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Main note {noteIndex} requires line 1-4.");
            }

            int storageIndex = note.line - 1;

            if (holder.noteTypes[storageIndex] != NoteType.Unknown)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Main line {note.line} contains duplicate notes.");
            }

            if (!TryParseDefinedEnum(note.type, out NoteType noteType) ||
                (noteType != NoteType.Tap &&
                 noteType != NoteType.LongTap))
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Main note {noteIndex} has invalid type '{note.type}'.");
            }

            if (!TryParseDefinedEnum(
                    note.hand,
                    out NoteHandleType handleType) ||
                handleType == NoteHandleType.Unknown)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Main note {noteIndex} has invalid hand '{note.hand}'.");
            }

            if (noteType == NoteType.Tap)
            {
                if (!string.Equals(
                        note.point,
                        ScratchPointType.Tap.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw CreateJsonFormatException(
                        eventIndex,
                        $"Tap note {noteIndex} requires point 'Tap'.");
                }
            }
            else if (string.Equals(
                         note.point,
                         ScratchPointType.Start.ToString(),
                         StringComparison.OrdinalIgnoreCase))
            {
                if (openLongTaps[storageIndex])
                {
                    throw CreateJsonFormatException(
                        eventIndex,
                        $"Long Tap line {note.line} is already open.");
                }

                openLongTaps[storageIndex] = true;
            }
            else if (string.Equals(
                         note.point,
                         ScratchPointType.End.ToString(),
                         StringComparison.OrdinalIgnoreCase))
            {
                if (!openLongTaps[storageIndex])
                {
                    throw CreateJsonFormatException(
                        eventIndex,
                        $"Long Tap line {note.line} has no Start.");
                }

                openLongTaps[storageIndex] = false;
            }
            else
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Long Tap note {noteIndex} requires Start or End.");
            }

            holder.noteTypes[storageIndex] = noteType;
            holder.noteHandles[storageIndex] = handleType;
            holder.isPoweredNotes[storageIndex] = false;
        }
    }

    private static void ParseJsonScratchNotes(
        JsonScratchNote[] notes,
        ChartHolder holder,
        bool[] openLongScratches,
        int eventIndex)
    {
        notes ??= Array.Empty<JsonScratchNote>();

        for (int noteIndex = 0; noteIndex < notes.Length; noteIndex++)
        {
            JsonScratchNote note = notes[noteIndex];

            if (note == null)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Scratch note {noteIndex} cannot be null.");
            }

            int scratchIndex = string.Equals(
                note.side,
                "Left",
                StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : string.Equals(
                        note.side,
                        "Right",
                        StringComparison.OrdinalIgnoreCase)
                            ? 1
                            : -1;

            if (scratchIndex < 0)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Scratch note {noteIndex} has invalid side '{note.side}'.");
            }

            int storageIndex =
                ChartHolder.MainLineCount + scratchIndex;

            if (holder.noteTypes[storageIndex] != NoteType.Unknown)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Scratch side {note.side} contains duplicate notes.");
            }

            if (!TryParseDefinedEnum(note.type, out NoteType noteType) ||
                !noteType.IsScratch() ||
                !TryParseDefinedEnum(
                    note.point,
                    out ScratchPointType pointType) ||
                !TryParseDefinedEnum(
                    note.motion,
                    out ScratchMotionType motionType))
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Scratch note {noteIndex} has an invalid type, point, " +
                    "or motion.");
            }

            if (noteType == NoteType.Scratch)
            {
                if (pointType != ScratchPointType.Tap)
                {
                    throw CreateJsonFormatException(
                        eventIndex,
                        "Single Scratch requires point 'Tap'.");
                }
            }
            else
            {
                ValidateJsonLongScratchPoint(
                    pointType,
                    openLongScratches,
                    scratchIndex,
                    eventIndex,
                    note.side);
            }

            if (motionType == ScratchMotionType.Release &&
                !ScratchMotionRules.IsReleaseAllowed(noteType, pointType))
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Scratch note {noteIndex}: Release is only valid on " +
                    "Long Scratch Mid or End points.");
            }

            ScratchMotionData motion;

            try
            {
                motion = ScratchMotionRules.NormalizeMotion(
                    noteType,
                    pointType,
                    new ScratchMotionData(note.amount, motionType));
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Scratch note {noteIndex}: {exception.Message}");
            }

            holder.noteTypes[storageIndex] = noteType;
            holder.scratchPointTypes[scratchIndex] = pointType;
            holder.scratchMotions[scratchIndex] = motion;
            holder.isPoweredNotes[storageIndex] =
                motion.MotionType != ScratchMotionType.None;
        }
    }

    private static void ValidateJsonLongScratchPoint(
        ScratchPointType pointType,
        bool[] openLongScratches,
        int scratchIndex,
        int eventIndex,
        string side)
    {
        switch (pointType)
        {
            case ScratchPointType.Start:
                if (openLongScratches[scratchIndex])
                {
                    throw CreateJsonFormatException(
                        eventIndex,
                        $"Long Scratch {side} is already open.");
                }

                openLongScratches[scratchIndex] = true;
                break;
            case ScratchPointType.Mid:
                if (!openLongScratches[scratchIndex])
                {
                    throw CreateJsonFormatException(
                        eventIndex,
                        $"Long Scratch {side} Mid has no Start.");
                }

                break;
            case ScratchPointType.End:
                if (!openLongScratches[scratchIndex])
                {
                    throw CreateJsonFormatException(
                        eventIndex,
                        $"Long Scratch {side} End has no Start.");
                }

                openLongScratches[scratchIndex] = false;
                break;
            default:
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Long Scratch {side} requires Start, Mid, or End.");
        }
    }

    private static void ParseJsonAirNotes(
        JsonAirNote[] notes,
        ChartHolder holder,
        int eventIndex)
    {
        notes ??= Array.Empty<JsonAirNote>();

        for (int noteIndex = 0; noteIndex < notes.Length; noteIndex++)
        {
            JsonAirNote note = notes[noteIndex];

            if (note == null ||
                note.line < 1 ||
                note.line > ChartHolder.AirNoteCount ||
                note.value < 1 ||
                note.value > 99)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Air note {noteIndex} requires line 1-4 and value 1-99.");
            }

            int airIndex = note.line - 1;

            if (holder.airNoteValues[airIndex] != 0)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Air line {note.line} contains duplicate notes.");
            }

            holder.airNoteValues[airIndex] = note.value;
        }
    }

    private static void ParseJsonEvents(
        JsonChartEvent source,
        ChartHolder holder,
        int eventIndex)
    {
        if (source.bpmChange != null && source.bpmChange.enabled)
        {
            double bpm = source.bpmChange.bpm;

            if (!IsFinite(bpm) ||
                bpm <= 0d ||
                bpm > float.MaxValue)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"BPM must be positive and finite: {bpm}.");
            }

            holder.targetBpm = (float)bpm;
        }

        if (source.lineSpeedChange != null &&
            source.lineSpeedChange.enabled)
        {
            double multiplier = source.lineSpeedChange.multiplier;

            if (!IsFinite(multiplier) ||
                multiplier <= 0d ||
                multiplier > float.MaxValue)
            {
                throw CreateJsonFormatException(
                    eventIndex,
                    $"Line Speed must be positive and finite: " +
                    $"{multiplier}.");
            }

            holder.hasLineSpeedChange = true;
            holder.targetLineSpeed = (float)multiplier;
        }

        holder.isEffect = source.effect;

        if (source.camera == null || !source.camera.enabled)
        {
            return;
        }

        if (!IsFinite(source.camera.offsetX) ||
            source.camera.offsetX < -float.MaxValue ||
            source.camera.offsetX > float.MaxValue ||
            !TryParseDefinedEnum(
                source.camera.spin,
                out ChartCameraSpinDirection spinDirection))
        {
            throw CreateJsonFormatException(
                eventIndex,
                "Camera requires a finite offsetX and spin None, Left, or Right.");
        }

        holder.isCameraMove = true;
        holder.cameraOffsetX = (float)source.camera.offsetX;
        holder.cameraSpinDirection = spinDirection;
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

    private static bool TryParseDefinedEnum<T>(
        string text,
        out T value)
        where T : struct, Enum
    {
        value = default;

        if (string.IsNullOrWhiteSpace(text) ||
            !Enum.TryParse(text, true, out value) ||
            !Enum.IsDefined(typeof(T), value))
        {
            return false;
        }

        string definedName = Enum.GetName(typeof(T), value);
        return string.Equals(
            text,
            definedName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNoOpenLongTaps(
        bool[] openLongTaps,
        string context)
    {
        for (int index = 0; index < openLongTaps.Length; index++)
        {
            if (openLongTaps[index])
            {
                throw new FormatException(
                    $"{context}: Long Tap in line {index + 1} has no End.");
            }
        }
    }

    private static void EnsureNoOpenLongScratches(
        bool[] openLongScratches,
        string context)
    {
        for (int index = 0; index < openLongScratches.Length; index++)
        {
            if (openLongScratches[index])
            {
                string side = index == 0 ? "Left" : "Right";
                throw new FormatException(
                    $"{context}: Long Scratch {side} has no End.");
            }
        }
    }

    private static FormatException CreateJsonFormatException(
        int eventIndex,
        string message)
    {
        return new FormatException(
            $"Chart JSON event {eventIndex}: {message}");
    }

    private static void AppendMetadata(
        StringBuilder output,
        double baseBpm,
        double musicStartCorrectionMs)
    {
        output.Append(FormatHeader);
        output.Append('|');
        output.Append(LegacyNativeFormatVersion.ToString(Invariant));
        output.Append('\n');
        output.Append(BpmHeader);
        output.Append('|');
        output.Append(baseBpm.ToString("R", Invariant));
        output.Append('\n');
        output.Append(MusicStartCorrectionHeader);
        output.Append('|');
        output.Append(musicStartCorrectionMs.ToString("R", Invariant));
        output.Append('\n');
    }

    /// <summary>알려진 메타데이터와 일반 주석을 처리하고 채보 행 여부를 반환합니다.</summary>
    private static bool TryParseMetadata(
        string line,
        int lineNumber,
        ref int formatVersion,
        ref bool hasFormatVersion,
        ref double baseBpm,
        ref bool hasBaseBpm,
        ref double musicStartCorrectionMs,
        ref bool hasMusicStartCorrectionMs)
    {
        if (!line.StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        string[] fields = line.Split('|');

        switch (fields[0])
        {
            case FormatHeader:
                EnsureUniqueHeader(
                    hasFormatVersion,
                    lineNumber,
                    FormatHeader);
                EnsureHeaderFieldCount(fields, lineNumber, FormatHeader);

                if (!int.TryParse(
                        fields[1],
                        NumberStyles.None,
                        Invariant,
                        out formatVersion) ||
                    formatVersion < 1 ||
                    formatVersion > LegacyNativeFormatVersion)
                {
                    throw CreateFormatException(
                        lineNumber,
                        $"Unsupported chart format version '{fields[1]}'.");
                }

                hasFormatVersion = true;
                break;

            case BpmHeader:
                EnsureUniqueHeader(hasBaseBpm, lineNumber, BpmHeader);
                EnsureHeaderFieldCount(fields, lineNumber, BpmHeader);
                baseBpm = ParseMetadataDouble(
                    fields[1],
                    lineNumber,
                    "BPM");

                if (baseBpm <= 0d)
                {
                    throw CreateFormatException(
                        lineNumber,
                        $"BPM must be greater than zero: '{fields[1]}'.");
                }

                hasBaseBpm = true;
                break;

            case MusicStartCorrectionHeader:
                EnsureUniqueHeader(
                    hasMusicStartCorrectionMs,
                    lineNumber,
                    MusicStartCorrectionHeader);
                EnsureHeaderFieldCount(
                    fields,
                    lineNumber,
                    MusicStartCorrectionHeader);
                musicStartCorrectionMs = ParseMetadataDouble(
                    fields[1],
                    lineNumber,
                    "music start correction ms");
                hasMusicStartCorrectionMs = true;
                break;
        }

        return true;
    }

    private static void AppendMainNotes(
        StringBuilder output,
        ChartHolder holder,
        bool[] openLongs)
    {
        for (int index = 0; index < ChartHolder.MainLineCount; index++)
        {
            NoteType noteType = holder.noteTypes[index];

            if (noteType == NoteType.Unknown)
            {
                output.Append("--");
                continue;
            }

            char handle = holder.noteHandles[index] switch
            {
                NoteHandleType.Left => 'L',
                NoteHandleType.Right => 'R',
                _ => throw new InvalidOperationException(
                    $"Main line {index + 1} requires a left or right handle.")
            };

            output.Append(handle);

            switch (noteType)
            {
                case NoteType.Tap:
                    output.Append('F');
                    break;
                case NoteType.LongTap:
                    output.Append(ToggleLong(openLongs, index));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"{noteType} cannot be stored in main line {index + 1}.");
            }
        }
    }

    private static void AppendScratchNotes(
        StringBuilder output,
        ChartHolder holder,
        bool[] openLongs)
    {
        for (int scratchIndex = 0;
             scratchIndex < ChartHolder.ScratchLineCount;
             scratchIndex++)
        {
            int index = ChartHolder.MainLineCount + scratchIndex;
            NoteType noteType = holder.noteTypes[index];

            if (noteType == NoteType.Unknown)
            {
                output.Append("----");
                continue;
            }

            if (!noteType.IsScratch())
            {
                throw new InvalidOperationException(
                    $"{noteType} cannot be stored in scratch line " +
                    $"{scratchIndex + 1}.");
            }

            ScratchMotionData motion = holder.scratchMotions[scratchIndex] ??
                ScratchMotionData.CreateDefault(noteType);
            ScratchPointType pointType =
                holder.scratchPointTypes[scratchIndex];
            motion = ScratchMotionRules.NormalizeMotion(
                noteType,
                pointType,
                motion);

            output.Append(motion.MotionType switch
            {
                ScratchMotionType.None => 'N',
                ScratchMotionType.Gradual => 'G',
                ScratchMotionType.Instant => 'I',
                ScratchMotionType.Release => 'R',
                _ => throw new InvalidOperationException(
                    $"Scratch line {scratchIndex + 1} has an unsupported " +
                    $"motion type: {motion.MotionType}.")
            });

            if (noteType == NoteType.Scratch &&
                pointType != ScratchPointType.Tap)
            {
                throw new InvalidOperationException(
                    $"Single Scratch on line {scratchIndex + 1} must use " +
                    "a Tap point.");
            }

            if (noteType == NoteType.LongScratch &&
                pointType == ScratchPointType.Tap)
            {
                throw new InvalidOperationException(
                    $"Long Scratch on line {scratchIndex + 1} must use " +
                    "a Start, Mid, or End point.");
            }

            ValidateScratchPointForSave(openLongs, index, pointType);
            output.Append(pointType switch
            {
                ScratchPointType.Tap => 'T',
                ScratchPointType.Start => 'S',
                ScratchPointType.Mid => 'M',
                ScratchPointType.End => 'E',
                _ => throw new InvalidOperationException(
                    $"Scratch line {scratchIndex + 1} has an unsupported " +
                    $"point type: {pointType}.")
            });
            output.Append(motion.MoveAmount.ToString("D2", Invariant));
        }
    }

    private static void AppendAirNotes(
        StringBuilder output,
        ChartHolder holder)
    {
        for (int i = 0; i < ChartHolder.AirNoteCount; i++)
        {
            int value = holder.airNoteValues[i];

            if (value < 0 || value > 99)
            {
                throw new InvalidOperationException(
                    $"Air note value must be between 00 and 99: {value}");
            }

            output.Append(value.ToString("D2", Invariant));
        }
    }

    private static void ParseMainNotes(
        string value,
        ChartHolder holder,
        bool[] openLongs,
        int lineNumber)
    {
        EnsureLength(
            value,
            MainNoteTextLength,
            lineNumber,
            "main note field");

        for (int index = 0; index < ChartHolder.MainLineCount; index++)
        {
            string token = value.Substring(index * 2, 2);

            if (token == "--")
            {
                continue;
            }

            holder.noteHandles[index] = token[0] switch
            {
                'L' => NoteHandleType.Left,
                'R' => NoteHandleType.Right,
                _ => throw CreateFormatException(
                    lineNumber,
                    $"Invalid handle '{token[0]}' in main line {index + 1}.")
            };

            switch (token[1])
            {
                case 'F':
                    holder.noteTypes[index] = NoteType.Tap;
                    break;
                case 'T':
                    // v1-v6의 Powered Tap은 일반 Tap으로 호환 로드합니다.
                    holder.noteTypes[index] = NoteType.Tap;
                    break;
                case 'S':
                    OpenLong(openLongs, index, lineNumber);
                    holder.noteTypes[index] = NoteType.LongTap;
                    break;
                case 'E':
                    CloseLong(openLongs, index, lineNumber);
                    holder.noteTypes[index] = NoteType.LongTap;
                    break;
                default:
                    throw CreateFormatException(
                        lineNumber,
                        $"Invalid note state '{token[1]}' in main line " +
                        $"{index + 1}.");
            }
        }
    }

    private static void ParseScratchNotes(
        string value,
        ChartHolder holder,
        bool[] openLongs,
        int lineNumber)
    {
        EnsureLength(
            value,
            ScratchNoteTextLength,
            lineNumber,
            "scratch note field");

        for (int scratchIndex = 0;
             scratchIndex < ChartHolder.ScratchLineCount;
             scratchIndex++)
        {
            int index = ChartHolder.MainLineCount + scratchIndex;
            string token = value.Substring(scratchIndex * 4, 4);

            if (token == "----")
            {
                continue;
            }

            ScratchMotionType motionType = token[0] switch
            {
                'N' => ScratchMotionType.None,
                'G' => ScratchMotionType.Gradual,
                'I' => ScratchMotionType.Instant,
                'R' => ScratchMotionType.Release,
                _ => throw CreateFormatException(
                    lineNumber,
                    $"Invalid motion type '{token[0]}' in scratch line " +
                    $"{scratchIndex + 1}.")
            };
            ScratchPointType pointType = token[1] switch
            {
                'T' => ScratchPointType.Tap,
                'S' => ScratchPointType.Start,
                'M' => ScratchPointType.Mid,
                'E' => ScratchPointType.End,
                _ => throw CreateFormatException(
                    lineNumber,
                    $"Invalid point type '{token[1]}' in scratch line " +
                    $"{scratchIndex + 1}.")
            };
            int moveAmount = ParseFixedDigits(
                token.Substring(2, 2),
                2,
                0,
                ScratchMotionData.MaximumMoveAmount,
                lineNumber,
                $"scratch line {scratchIndex + 1} move amount");

            if (pointType == ScratchPointType.Tap)
            {
                if (openLongs[index])
                {
                    throw CreateFormatException(
                        lineNumber,
                        $"Scratch Tap cannot appear inside an open Long " +
                        $"Scratch on scratch line {scratchIndex + 1}.");
                }

                holder.noteTypes[index] = NoteType.Scratch;
            }
            else
            {
                ValidateScratchPointForParse(
                    openLongs,
                    index,
                    pointType,
                    lineNumber);
                holder.noteTypes[index] = NoteType.LongScratch;
            }

            if (motionType == ScratchMotionType.Release &&
                !ScratchMotionRules.IsReleaseAllowed(
                    holder.noteTypes[index],
                    pointType))
            {
                throw CreateFormatException(
                    lineNumber,
                    $"Scratch Release is only valid on Long Scratch Mid " +
                    $"or End points on scratch line {scratchIndex + 1}.");
            }

            holder.scratchPointTypes[scratchIndex] = pointType;
            ScratchMotionData parsedMotion = new ScratchMotionData(
                moveAmount,
                motionType);
            holder.scratchMotions[scratchIndex] =
                ScratchMotionRules.NormalizeMotion(
                    holder.noteTypes[index],
                    pointType,
                    parsedMotion);
            holder.isPoweredNotes[index] =
                holder.scratchMotions[scratchIndex].MotionType !=
                ScratchMotionType.None;
        }
    }

    private static void ParseLegacyScratchNotes(
        string value,
        ChartHolder holder,
        bool[] openLongs,
        int lineNumber)
    {
        EnsureLength(
            value,
            LegacyScratchNoteTextLength,
            lineNumber,
            "legacy scratch note field");

        for (int scratchIndex = 0;
             scratchIndex < ChartHolder.ScratchLineCount;
             scratchIndex++)
        {
            int index = ChartHolder.MainLineCount + scratchIndex;
            string token = value.Substring(scratchIndex * 2, 2);

            if (token == "--")
            {
                continue;
            }

            holder.isPoweredNotes[index] = token[0] switch
            {
                'F' => false,
                'T' => true,
                _ => throw CreateFormatException(
                    lineNumber,
                    $"Invalid powered flag '{token[0]}' in scratch line " +
                    $"{scratchIndex + 1}.")
            };

            switch (token[1])
            {
                case 'F':
                    holder.noteTypes[index] = NoteType.Scratch;
                    holder.scratchPointTypes[scratchIndex] =
                        ScratchPointType.Tap;
                    break;
                case 'S':
                    OpenLong(openLongs, index, lineNumber);
                    holder.noteTypes[index] = NoteType.LongScratch;
                    holder.scratchPointTypes[scratchIndex] =
                        ScratchPointType.Start;
                    break;
                case 'E':
                    CloseLong(openLongs, index, lineNumber);
                    holder.noteTypes[index] = NoteType.LongScratch;
                    holder.scratchPointTypes[scratchIndex] =
                        ScratchPointType.End;
                    break;
                default:
                    throw CreateFormatException(
                        lineNumber,
                        $"Invalid note state '{token[1]}' in scratch line " +
                        $"{scratchIndex + 1}.");
            }
        }
    }

    private static void ParseAirNotes(
        string value,
        ChartHolder holder,
        int lineNumber)
    {
        EnsureLength(
            value,
            AirNoteTextLength,
            lineNumber,
            "air note field");

        for (int i = 0; i < ChartHolder.AirNoteCount; i++)
        {
            holder.airNoteValues[i] = ParseFixedDigits(
                value.Substring(i * 2, 2),
                2,
                0,
                99,
                lineNumber,
                $"air note {i + 1}");
        }
    }

    private static void ParseLegacyScratchMotions(
        string value,
        ChartHolder holder,
        int lineNumber)
    {
        string[] tokens = value.Split(';');

        if (tokens.Length != ChartHolder.ScratchLineCount)
        {
            throw CreateFormatException(
                lineNumber,
                $"Scratch motion field must contain exactly " +
                $"{ChartHolder.ScratchLineCount} tokens.");
        }

        for (int scratchIndex = 0;
             scratchIndex < ChartHolder.ScratchLineCount;
             scratchIndex++)
        {
            int noteIndex = ChartHolder.MainLineCount + scratchIndex;
            NoteType noteType = holder.noteTypes[noteIndex];
            string token = tokens[scratchIndex];

            if (!noteType.IsScratch())
            {
                if (token != "-")
                {
                    throw CreateFormatException(
                        lineNumber,
                        $"Scratch line {scratchIndex + 1} has motion data " +
                        "without a Scratch note.");
                }

                continue;
            }

            string[] values = token.Split(',');

            if (values.Length != 3 ||
                !int.TryParse(
                    values[0],
                    NumberStyles.Integer,
                    Invariant,
                    out int startOffsetUnits) ||
                !int.TryParse(
                    values[1],
                    NumberStyles.Integer,
                    Invariant,
                    out int endOffsetUnits))
            {
                throw CreateFormatException(
                    lineNumber,
                    $"Invalid motion data for scratch line " +
                    $"{scratchIndex + 1}: '{token}'.");
            }

            ScratchMotionType motionType = values[2] switch
            {
                "I" => ScratchMotionType.Instant,
                "G" => ScratchMotionType.Gradual,
                _ => throw CreateFormatException(
                    lineNumber,
                    $"Scratch motion type must be I or G: '{values[2]}'.")
            };

            ScratchMotionData legacyMotion = new ScratchMotionData(
                startOffsetUnits,
                endOffsetUnits,
                motionType);
            holder.scratchMotions[scratchIndex] =
                ScratchMotionRules.NormalizeMotion(
                    noteType,
                    holder.scratchPointTypes[scratchIndex],
                    legacyMotion);
        }
    }

    private static void NormalizeLegacyScratchData(
        ChartHolder holder,
        int formatVersion)
    {
        holder.EnsureStorage();

        for (int scratchIndex = 0;
             scratchIndex < ChartHolder.ScratchLineCount;
             scratchIndex++)
        {
            int noteIndex = ChartHolder.MainLineCount + scratchIndex;
            NoteType noteType = holder.noteTypes[noteIndex];

            if (!noteType.IsScratch())
            {
                continue;
            }

            ScratchMotionData legacyMotion =
                holder.scratchMotions[scratchIndex] ??
                ScratchMotionData.CreateDefault(noteType);
            ScratchMotionType motionType;

            if (!holder.isPoweredNotes[noteIndex])
            {
                motionType = ScratchMotionType.None;
            }
            else if (formatVersion < 2)
            {
                motionType = noteType == NoteType.LongScratch
                    ? ScratchMotionType.Gradual
                    : ScratchMotionType.Instant;
            }
            else
            {
                motionType = legacyMotion.MotionType;
            }

            ScratchMotionData requestedMotion =
                legacyMotion.WithMotionType(motionType);
            holder.scratchMotions[scratchIndex] =
                ScratchMotionRules.NormalizeMotion(
                    noteType,
                    holder.scratchPointTypes[scratchIndex],
                    requestedMotion);
            holder.isPoweredNotes[noteIndex] =
                holder.scratchMotions[scratchIndex].MotionType !=
                ScratchMotionType.None;
        }
    }

    private static void ValidateScratchPointForSave(
        bool[] openLongs,
        int index,
        ScratchPointType pointType)
    {
        switch (pointType)
        {
            case ScratchPointType.Tap:
                if (openLongs[index])
                {
                    throw new InvalidOperationException(
                        $"Scratch Tap cannot appear inside an open Long " +
                        $"Scratch on {GetLineName(index)}.");
                }

                break;
            case ScratchPointType.Start:
                if (openLongs[index])
                {
                    throw new InvalidOperationException(
                        $"Long Scratch starts twice on {GetLineName(index)}.");
                }

                openLongs[index] = true;
                break;
            case ScratchPointType.Mid:
                if (!openLongs[index])
                {
                    throw new InvalidOperationException(
                        $"Long Scratch Mid appears outside an open Long " +
                        $"Scratch on {GetLineName(index)}.");
                }

                break;
            case ScratchPointType.End:
                if (!openLongs[index])
                {
                    throw new InvalidOperationException(
                        $"Long Scratch ends before it starts on " +
                        $"{GetLineName(index)}.");
                }

                openLongs[index] = false;
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Scratch point type: {pointType}.");
        }
    }

    private static void ValidateScratchPointForParse(
        bool[] openLongs,
        int index,
        ScratchPointType pointType,
        int lineNumber)
    {
        switch (pointType)
        {
            case ScratchPointType.Start:
                OpenLong(openLongs, index, lineNumber);
                break;
            case ScratchPointType.Mid:
                if (!openLongs[index])
                {
                    throw CreateFormatException(
                        lineNumber,
                        $"Long Scratch Mid appears outside an open Long " +
                        $"Scratch on {GetLineName(index)}.");
                }

                break;
            case ScratchPointType.End:
                CloseLong(openLongs, index, lineNumber);
                break;
            default:
                throw CreateFormatException(
                    lineNumber,
                    $"Invalid Long Scratch point type '{pointType}'.");
        }
    }

    private static char ToggleLong(bool[] openLongs, int index)
    {
        bool isEnd = openLongs[index];
        openLongs[index] = !isEnd;
        return isEnd ? 'E' : 'S';
    }

    private static void OpenLong(
        bool[] openLongs,
        int index,
        int lineNumber)
    {
        if (openLongs[index])
        {
            throw CreateFormatException(
                lineNumber,
                $"Long note starts twice on {GetLineName(index)}.");
        }

        openLongs[index] = true;
    }

    private static void CloseLong(
        bool[] openLongs,
        int index,
        int lineNumber)
    {
        if (!openLongs[index])
        {
            throw CreateFormatException(
                lineNumber,
                $"Long note ends before it starts on {GetLineName(index)}.");
        }

        openLongs[index] = false;
    }

    private static void EnsureAllLongsClosed(bool[] openLongs, string prefix)
    {
        for (int i = 0; i < openLongs.Length; i++)
        {
            if (openLongs[i])
            {
                throw new FormatException(
                    $"{prefix}: Long note is not closed on {GetLineName(i)}.");
            }
        }
    }

    private static string GetLineName(int index)
    {
        return index < ChartHolder.MainLineCount
            ? $"main line {index + 1}"
            : $"scratch line {index - ChartHolder.MainLineCount + 1}";
    }

    private static void ValidateHolderPosition(ChartHolder holder)
    {
        if (holder.ChartNumber < 0 ||
            holder.ChartNumber > ChartHolder.MaximumMeasureNumber)
        {
            throw new InvalidOperationException(
                $"Measure number must be between 000 and 999: " +
                $"{holder.ChartNumber}");
        }

        if (holder.ChartPos < 0 ||
            holder.ChartPos >= ChartHolder.PositionUnitsPerMeasure)
        {
            throw new InvalidOperationException(
                $"Measure position must be between 0000 and " +
                $"{ChartHolder.PositionUnitsPerMeasure - 1:D4}: " +
                $"{holder.ChartPos}");
        }
    }

    private static string FormatBpm(float bpm)
    {
        if (bpm == -1f)
        {
            return "-1";
        }

        if (!IsFinite(bpm) || bpm <= 0f)
        {
            throw new InvalidOperationException(
                $"BPM must be -1 or greater than zero: {bpm}");
        }

        return bpm.ToString("R", Invariant);
    }

    private static float ParseBpm(string value, int lineNumber)
    {
        if (!float.TryParse(
                value,
                NumberStyles.Float,
                Invariant,
                out float bpm) ||
            !IsFinite(bpm) ||
            (bpm != -1f && bpm <= 0f))
        {
            throw CreateFormatException(
                lineNumber,
                $"BPM must be -1 or greater than zero: '{value}'.");
        }

        return bpm;
    }

    private static string FormatLineSpeed(ChartHolder holder)
    {
        if (!holder.hasLineSpeedChange)
        {
            return "-";
        }

        if (!IsFinite(holder.targetLineSpeed) ||
            holder.targetLineSpeed <= 0f)
        {
            throw new InvalidOperationException(
                $"Line Speed must be positive and finite: " +
                $"{holder.targetLineSpeed}");
        }

        return holder.targetLineSpeed.ToString("R", Invariant);
    }

    private static void ParseLineSpeed(
        string value,
        int lineNumber,
        out bool hasLineSpeedChange,
        out float lineSpeed)
    {
        if (value == "-")
        {
            hasLineSpeedChange = false;
            lineSpeed = 1f;
            return;
        }

        if (!float.TryParse(
                value,
                NumberStyles.Float,
                Invariant,
                out lineSpeed) ||
            !IsFinite(lineSpeed) ||
            lineSpeed <= 0f)
        {
            throw CreateFormatException(
                lineNumber,
                $"Line Speed must be '-' or a positive finite number: " +
                $"'{value}'.");
        }

        hasLineSpeedChange = true;
    }

    private static string FormatCamera(ChartHolder holder)
    {
        if (!holder.isCameraMove)
        {
            return "-";
        }

        if (!IsFinite(holder.cameraOffsetX))
        {
            throw new InvalidOperationException(
                $"Camera offset X must be finite: {holder.cameraOffsetX}");
        }

        char direction = holder.cameraSpinDirection switch
        {
            ChartCameraSpinDirection.Left => 'L',
            ChartCameraSpinDirection.None => 'N',
            ChartCameraSpinDirection.Right => 'R',
            _ => throw new InvalidOperationException(
                $"Unsupported Camera spin direction: " +
                $"{holder.cameraSpinDirection}")
        };
        return direction + ":" +
            holder.cameraOffsetX.ToString("R", Invariant);
    }

    private static void ParseCamera(
        string value,
        int lineNumber,
        ChartHolder holder)
    {
        if (value == "-")
        {
            holder.isCameraMove = false;
            holder.cameraOffsetX = 0f;
            holder.cameraSpinDirection =
                ChartCameraSpinDirection.None;
            return;
        }

        string[] tokens = value.Split(':');

        if (tokens.Length != 2 || tokens[0].Length != 1 ||
            !float.TryParse(
                tokens[1],
                NumberStyles.Float,
                Invariant,
                out float offsetX) ||
            !IsFinite(offsetX))
        {
            throw CreateFormatException(
                lineNumber,
                $"Camera must be '-' or '[L|N|R]:offsetX': '{value}'.");
        }

        ChartCameraSpinDirection spinDirection = tokens[0][0] switch
        {
            'L' => ChartCameraSpinDirection.Left,
            'N' => ChartCameraSpinDirection.None,
            'R' => ChartCameraSpinDirection.Right,
            _ => throw CreateFormatException(
                lineNumber,
                $"Camera direction must be L, N, or R: '{value}'.")
        };
        holder.isCameraMove = true;
        holder.cameraOffsetX = offsetX;
        holder.cameraSpinDirection = spinDirection;
    }

    private static bool ParseBoolean(
        string value,
        int lineNumber,
        string fieldName)
    {
        return value switch
        {
            "F" => false,
            "T" => true,
            _ => throw CreateFormatException(
                lineNumber,
                $"{fieldName} must be F or T: '{value}'.")
        };
    }

    private static double ParseMetadataDouble(
        string value,
        int lineNumber,
        string fieldName)
    {
        if (!double.TryParse(
                value,
                NumberStyles.Float,
                Invariant,
                out double result) ||
            !IsFinite(result))
        {
            throw CreateFormatException(
                lineNumber,
                $"{fieldName} must be a finite number: '{value}'.");
        }

        return result;
    }

    private static void EnsureHeaderFieldCount(
        string[] fields,
        int lineNumber,
        string header)
    {
        if (fields.Length != 2)
        {
            throw CreateFormatException(
                lineNumber,
                $"{header} must contain exactly one value.");
        }
    }

    private static void EnsureUniqueHeader(
        bool alreadyRead,
        int lineNumber,
        string header)
    {
        if (alreadyRead)
        {
            throw CreateFormatException(
                lineNumber,
                $"Duplicate metadata header: {header}.");
        }
    }

    private static int ParseFixedDigits(
        string value,
        int length,
        int minimum,
        int maximum,
        int lineNumber,
        string fieldName)
    {
        EnsureLength(value, length, lineNumber, fieldName);

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] < '0' || value[i] > '9')
            {
                throw CreateFormatException(
                    lineNumber,
                    $"{fieldName} must contain only digits: '{value}'.");
            }
        }

        int result = int.Parse(value, NumberStyles.None, Invariant);

        if (result < minimum || result > maximum)
        {
            throw CreateFormatException(
                lineNumber,
                $"{fieldName} must be between {minimum} and {maximum}: " +
                $"'{value}'.");
        }

        return result;
    }

    private static void EnsureLength(
        string value,
        int expectedLength,
        int lineNumber,
        string fieldName)
    {
        if (value == null || value.Length != expectedLength)
        {
            throw CreateFormatException(
                lineNumber,
                $"{fieldName} must be exactly {expectedLength} characters.");
        }
    }

    private static FormatException CreateFormatException(
        int lineNumber,
        string message)
    {
        return new FormatException($"Line {lineNumber}: {message}");
    }

    private static void ValidateBaseBpm(double value)
    {
        ValidateFinite(value, nameof(value));

        if (value <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Base BPM must be greater than zero.");
        }
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "A finite number is required.");
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    [Serializable]
    private sealed class JsonChartHeader
    {
        public string format = null;
        public int formatVersion = 0;
    }

    [Serializable]
    #pragma warning disable CS0649 // JsonUtility populates legacy v6 DTO fields.
    private sealed class JsonChartDocument
    {
        public string format;
        public int formatVersion;
        public double baseBpm;
        public double musicStartCorrectionMs;
        public JsonChartEvent[] events;
    }

    [Serializable]
    private sealed class JsonCurrentChartDocument
    {
        public string format;
        public int formatVersion;
        public string musicId;
        public string difficultyId;
        public string jacketFile;
        public string gimmickId;
        public double baseBpm;
        public double musicStartCorrectionMs;
        public string revision;
        public string[] notes;
        public JsonEffectDefinition[] eventDictionary;
    }

    [Serializable]
    private sealed class JsonCompactChartDocument
    {
        public string format;
        public int formatVersion;
        public double baseBpm;
        public double musicStartCorrectionMs;
        public string[] events;
        public string[] notes;
        public string musicId;
        public string difficultyId;
        public string jacketFile;
        public string gimmickId;
        public string revision;
        public bool hasEffectParameters;
        public JsonEffectDefinition[] effectDefinitions;
        public JsonEffectDefinition[] eventDictionary;
    }

    [Serializable]
    private sealed class JsonEffectDefinition
    {
        public int position;
        public string effectId;
        public string effectTypeId;
        public string commandId;
        public int order;
    }

    [Serializable]
    private sealed class JsonChartEvent
    {
        public int measure;
        public int position;
        public JsonMainNote[] mainNotes;
        public JsonScratchNote[] scratchNotes;
        public JsonAirNote[] airNotes;
        public JsonBpmChange bpmChange;
        public JsonLineSpeedChange lineSpeedChange;
        public bool effect;
        public JsonCameraEvent camera;
    }

    [Serializable]
    private sealed class JsonMainNote
    {
        public int line;
        public string type;
        public string hand;
        public string point;
    }

    [Serializable]
    private sealed class JsonScratchNote
    {
        public string side;
        public string type;
        public string point;
        public string motion;
        public int amount;
    }

    [Serializable]
    private sealed class JsonAirNote
    {
        public int line;
        public int value;
    }

    [Serializable]
    private sealed class JsonBpmChange
    {
        public bool enabled;
        public double bpm;
    }

    [Serializable]
    private sealed class JsonLineSpeedChange
    {
        public bool enabled;
        public double multiplier;
    }

    [Serializable]
    private sealed class JsonCameraEvent
    {
        public bool enabled;
        public double offsetX;
        public string spin;
    }
    #pragma warning restore CS0649
}

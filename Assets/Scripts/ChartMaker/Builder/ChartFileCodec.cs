using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using REmind.Data;

public static class ChartFileCodec
{
    public const int CurrentFormatVersion = 3;
    internal const int LegacyPositionUnitsPerMeasure = 1600;

    private const string FormatHeader = "#REmindChart";
    private const string BpmHeader = "#BPM";
    private const string MusicStartCorrectionHeader =
        "#MUSIC_START_CORRECTION_MS";
    private const int LegacyFieldCount = 8;
    private const int CurrentFieldCount = 9;
    private const int MainNoteTextLength = 8;
    private const int ScratchNoteTextLength = 4;
    private const int AirNoteTextLength = 8;

    private static readonly CultureInfo Invariant =
        CultureInfo.InvariantCulture;

    /// <summary>현재 채보와 편집용 타이밍 설정을 행 단위 텍스트 포맷으로 변환합니다.</summary>
    public static string Serialize(
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
                ordered.Add(holders[i]);
            }
        }

        ordered.Sort(
            (left, right) => left.AbsoluteChartPosition.CompareTo(
                right.AbsoluteChartPosition));

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
            output.Append(holder.isEffect ? 'T' : 'F');
            output.Append('|');
            output.Append(holder.isCameraMove ? 'T' : 'F');
            output.Append('|');
            AppendScratchMotions(output, holder);

            previousPosition = holder.AbsoluteChartPosition;
        }

        EnsureAllLongsClosed(openLongs, "Cannot save chart");
        return output.ToString();
    }

    /// <summary>텍스트 전체를 검증한 뒤 독립된 채보 데이터로 변환합니다.</summary>
    public static ChartFile Parse(string text)
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

            int expectedFieldCount = formatVersion >= 2
                ? CurrentFieldCount
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
            ParseMainNotes(fields[2], holder, openLongs, lineNumber);
            ParseScratchNotes(fields[3], holder, openLongs, lineNumber);
            ParseAirNotes(fields[4], holder, lineNumber);
            holder.targetBpm = ParseBpm(fields[5], lineNumber);
            holder.isEffect = ParseBoolean(fields[6], lineNumber, "effect");
            holder.isCameraMove = ParseBoolean(
                fields[7],
                lineNumber,
                "camera movement");

            if (formatVersion >= 2)
            {
                ParseScratchMotions(fields[8], holder, lineNumber);
            }
            else
            {
                holder.EnsureStorage();
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

    private static void AppendMetadata(
        StringBuilder output,
        double baseBpm,
        double musicStartCorrectionMs)
    {
        output.Append(FormatHeader);
        output.Append('|');
        output.Append(CurrentFormatVersion.ToString(Invariant));
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
                    formatVersion > CurrentFormatVersion)
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
                    output.Append(holder.isPoweredNotes[index] ? 'T' : 'F');
                    break;
                case NoteType.LongTap:
                    if (holder.isPoweredNotes[index])
                    {
                        throw new InvalidOperationException(
                            "Powered LongTap cannot be represented by this format.");
                    }

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
                output.Append("--");
                continue;
            }

            output.Append(holder.isPoweredNotes[index] ? 'T' : 'F');

            switch (noteType)
            {
                case NoteType.Scratch:
                    output.Append('F');
                    break;
                case NoteType.LongScratch:
                    output.Append(ToggleLong(openLongs, index));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"{noteType} cannot be stored in scratch line " +
                        $"{scratchIndex + 1}.");
            }
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

    private static void AppendScratchMotions(
        StringBuilder output,
        ChartHolder holder)
    {
        for (int scratchIndex = 0;
             scratchIndex < ChartHolder.ScratchLineCount;
             scratchIndex++)
        {
            if (scratchIndex > 0)
            {
                output.Append(';');
            }

            int noteIndex = ChartHolder.MainLineCount + scratchIndex;
            NoteType noteType = holder.noteTypes[noteIndex];

            if (!noteType.IsScratch())
            {
                output.Append('-');
                continue;
            }

            ScratchMotionData motion = holder.scratchMotions[scratchIndex] ??
                ScratchMotionData.CreateDefault(noteType);

            if (!Enum.IsDefined(typeof(ScratchMotionType), motion.MotionType))
            {
                throw new InvalidOperationException(
                    $"Scratch line {scratchIndex + 1} has an unsupported " +
                    $"motion type: {motion.MotionType}.");
            }

            if (noteType == NoteType.Scratch &&
                motion.MotionType != ScratchMotionType.Instant)
            {
                throw new InvalidOperationException(
                    $"Single Scratch on line {scratchIndex + 1} must use " +
                    "Instant motion.");
            }

            output.Append(motion.StartOffsetUnits.ToString(Invariant));
            output.Append(',');
            output.Append(motion.EndOffsetUnits.ToString(Invariant));
            output.Append(',');
            output.Append(
                motion.MotionType == ScratchMotionType.Instant ? 'I' : 'G');
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
                    holder.noteTypes[index] = NoteType.Tap;
                    holder.isPoweredNotes[index] = true;
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
                    break;
                case 'S':
                    OpenLong(openLongs, index, lineNumber);
                    holder.noteTypes[index] = NoteType.LongScratch;
                    break;
                case 'E':
                    CloseLong(openLongs, index, lineNumber);
                    holder.noteTypes[index] = NoteType.LongScratch;
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

    private static void ParseScratchMotions(
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

            if (noteType == NoteType.Scratch &&
                motionType != ScratchMotionType.Instant)
            {
                throw CreateFormatException(
                    lineNumber,
                    $"Single Scratch on line {scratchIndex + 1} must use " +
                    "Instant motion.");
            }

            holder.scratchMotions[scratchIndex] = new ScratchMotionData(
                startOffsetUnits,
                endOffsetUnits,
                motionType);
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
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using REmind.Data;

public static class ChartFileCodec
{
    private const int FieldCount = 8;
    private const int MainNoteTextLength = 8;
    private const int ScratchNoteTextLength = 4;
    private const int AirNoteTextLength = 8;

    private static readonly CultureInfo Invariant =
        CultureInfo.InvariantCulture;

    /// <summary>현재 채보 데이터를 한 행 단위 텍스트 포맷으로 변환합니다.</summary>
    public static string Serialize(IReadOnlyList<ChartHolder> holders)
    {
        if (holders == null)
        {
            throw new ArgumentNullException(nameof(holders));
        }

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

            if (output.Length > 0)
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
        int previousPosition = -1;
        int lineNumber = 0;

        using StringReader reader = new StringReader(text);
        string line;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            line = line.Trim();

            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            string[] fields = line.Split('|');

            if (fields.Length != FieldCount)
            {
                throw CreateFormatException(
                    lineNumber,
                    $"Expected {FieldCount} fields but found {fields.Length}.");
            }

            int chartNumber = ParseFixedDigits(
                fields[0],
                3,
                0,
                999,
                lineNumber,
                "measure number");
            int chartPosition = ParseFixedDigits(
                fields[1],
                4,
                0,
                ChartHolder.PositionUnitsPerMeasure,
                lineNumber,
                "measure position");
            int absolutePosition = checked(
                chartNumber * ChartHolder.PositionUnitsPerMeasure +
                chartPosition);

            if (absolutePosition <= previousPosition)
            {
                throw CreateFormatException(
                    lineNumber,
                    "Rows must be ordered by position without duplicates.");
            }

            ChartHolder holder = new ChartHolder(chartNumber, chartPosition);
            ParseMainNotes(fields[2], holder, openLongs, lineNumber);
            ParseScratchNotes(fields[3], holder, openLongs, lineNumber);
            ParseAirNotes(fields[4], holder, lineNumber);
            holder.targetBpm = ParseBpm(fields[5], lineNumber);
            holder.isEffect = ParseBoolean(fields[6], lineNumber, "effect");
            holder.isCameraMove = ParseBoolean(
                fields[7],
                lineNumber,
                "camera movement");

            holders.Add(holder);
            previousPosition = absolutePosition;
        }

        EnsureAllLongsClosed(openLongs, "End of file");
        return new ChartFile
        {
            chartDatas = holders.ToArray()
        };
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
        if (holder.ChartNumber < 0 || holder.ChartNumber > 999)
        {
            throw new InvalidOperationException(
                $"Measure number must be between 000 and 999: " +
                $"{holder.ChartNumber}");
        }

        if (holder.ChartPos < 0 ||
            holder.ChartPos > ChartHolder.PositionUnitsPerMeasure)
        {
            throw new InvalidOperationException(
                $"Measure position must be between 0000 and 1600: " +
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

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

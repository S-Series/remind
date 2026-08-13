using System;
using System.Collections.Generic;
using REmind.Data;
using UnityEngine;

public static class ChartManager
{
    private static readonly List<ChartHolder> ChartHolderList =
        new List<ChartHolder>();

    public static IReadOnlyList<ChartHolder> ChartHolders => ChartHolderList;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ChartHolderList.Clear();
    }

    /// <summary>
    /// 월드 Y 좌표를 마디와 0~1599 위치 단위로 변환해 채보 묶음을 반환합니다.
    /// </summary>
    public static ChartHolder GetOrCreateHolder(int worldPositionY)
    {
        int absolutePosition = Mathf.Max(
            0,
            Mathf.RoundToInt(
                worldPositionY * ChartHolder.PositionUnitsPerWorldUnit));
        int chartNumber = absolutePosition /
            ChartHolder.PositionUnitsPerMeasure;
        int chartPosition = absolutePosition %
            ChartHolder.PositionUnitsPerMeasure;

        return GetOrCreateHolder(chartNumber, chartPosition);
    }

    /// <summary>지정한 마디와 마디 내 위치의 채보 묶음을 반환합니다.</summary>
    public static ChartHolder GetOrCreateHolder(
        int chartNumber,
        int chartPosition)
    {
        NormalizePosition(ref chartNumber, ref chartPosition);
        int absolutePosition = checked(
            chartNumber * ChartHolder.PositionUnitsPerMeasure +
            chartPosition);
        int index = FindInsertionIndex(absolutePosition);

        if (index < ChartHolderList.Count &&
            ChartHolderList[index].AbsoluteChartPosition == absolutePosition)
        {
            return ChartHolderList[index];
        }

        ChartHolder holder = new ChartHolder(chartNumber, chartPosition);
        ChartHolderList.Insert(index, holder);
        return holder;
    }

    /// <summary>검증이 끝난 파일 데이터를 현재 편집 채보로 교체합니다.</summary>
    public static void ReplaceChartData(IReadOnlyList<ChartHolder> holders)
    {
        List<ChartHolder> replacements = new List<ChartHolder>();
        HashSet<int> positions = new HashSet<int>();

        if (holders != null)
        {
            for (int i = 0; i < holders.Count; i++)
            {
                ChartHolder source = holders[i];

                if (source == null)
                {
                    throw new ArgumentException(
                        $"Chart holder at index {i} is missing.",
                        nameof(holders));
                }

                ValidateHolderPosition(source, i);
                int absolutePosition = source.AbsoluteChartPosition;

                if (!positions.Add(absolutePosition))
                {
                    throw new ArgumentException(
                        $"Duplicate chart position at index {i}: " +
                        $"{source.ChartNumber:D3}|{source.ChartPos:D4}.",
                        nameof(holders));
                }

                replacements.Add(source.CloneData());
            }
        }

        replacements.Sort(
            (left, right) => left.AbsoluteChartPosition.CompareTo(
                right.AbsoluteChartPosition));

        ClearChart();
        ChartHolderList.AddRange(replacements);
    }

    /// <summary>
    /// 현재 편집 중인 채보 데이터를 비우고 필요하면 연결된 노트 오브젝트도 삭제합니다.
    /// </summary>
    /// <param name="destroyNoteObjects">
    /// false이면 씬 종료처럼 오브젝트가 별도로 파괴되는 상황에서 데이터만 비웁니다.
    /// </param>
    public static void ClearChart(bool destroyNoteObjects = true)
    {
        if (destroyNoteObjects)
        {
            for (int i = 0; i < ChartHolderList.Count; i++)
            {
                ChartHolderList[i].DestroyAllNoteObjects();
            }
        }

        ChartHolderList.Clear();
    }

    /// <summary>노트 표현 오브젝트를 찾아 해당 데이터와 연결 오브젝트를 함께 삭제합니다.</summary>
    public static bool DeleteNote(GameObject noteObject)
    {
        for (int i = 0; i < ChartHolderList.Count; i++)
        {
            ChartHolder holder = ChartHolderList[i];

            if (!holder.DeleteNote(noteObject))
            {
                continue;
            }

            if (!holder.HasChartData)
            {
                ChartHolderList.RemoveAt(i);
            }

            return true;
        }

        return false;
    }

    public static bool TryGetNotePosition(
        GameObject noteObject,
        out int absolutePosition)
    {
        for (int i = 0; i < ChartHolderList.Count; i++)
        {
            ChartHolder holder = ChartHolderList[i];

            if (holder.ContainsNoteObject(noteObject))
            {
                absolutePosition = holder.AbsoluteChartPosition;
                return true;
            }
        }

        absolutePosition = -1;
        return false;
    }

    internal static bool TryGetNoteData(
        GameObject noteObject,
        out ChartHolder holder,
        out int line,
        out NoteType noteType,
        out NoteHandleType handleType,
        out bool isPowered)
    {
        for (int i = 0; i < ChartHolderList.Count; i++)
        {
            ChartHolder candidate = ChartHolderList[i];

            if (!candidate.TryGetNoteData(
                    noteObject,
                    out line,
                    out noteType,
                    out handleType,
                    out isPowered))
            {
                continue;
            }

            holder = candidate;
            return true;
        }

        holder = null;
        line = 0;
        noteType = NoteType.Unknown;
        handleType = NoteHandleType.Unknown;
        isPowered = false;
        return false;
    }

    /// <summary>선택된 노트의 Powered 데이터를 반전합니다.</summary>
    internal static bool ToggleNotePowered(
        GameObject noteObject,
        out bool isPowered,
        out string error)
    {
        if (!TryGetNoteData(
                noteObject,
                out ChartHolder holder,
                out _,
                out NoteType noteType,
                out _,
                out bool currentPowered))
        {
            isPowered = false;
            error = "Selected note data could not be found.";
            return false;
        }

        if (noteType == NoteType.Air)
        {
            isPowered = false;
            error = "Air notes do not use Powered data.";
            return false;
        }

        if (noteType == NoteType.LongTap)
        {
            isPowered = currentPowered;
            error = "Powered Long Tap is not supported by the chart format.";
            return false;
        }

        isPowered = !currentPowered;

        if (!holder.TrySetPowered(noteObject, isPowered, out _))
        {
            isPowered = currentPowered;
            error = "Powered data could not be updated.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>선택된 노트의 공통 위치와 종류별 속성을 원자적으로 수정합니다.</summary>
    internal static bool EditNote(
        GameObject noteObject,
        int targetAbsolutePosition,
        int targetLine,
        NoteHandleType targetHandle,
        bool targetPowered,
        int targetAirValue,
        out string error)
    {
        if (!TryGetNoteData(
                noteObject,
                out ChartHolder sourceHolder,
                out int sourceLine,
                out NoteType sourceType,
                out _,
                out _))
        {
            error = "Selected note data could not be found.";
            return false;
        }

        if (targetAbsolutePosition < 0 ||
            targetAbsolutePosition >
            999 * ChartHolder.PositionUnitsPerMeasure +
            ChartHolder.PositionUnitsPerMeasure - 1)
        {
            error = "Note position is outside the supported chart range.";
            return false;
        }

        if (sourceType.IsScratch())
        {
            if (targetLine != -1 && targetLine != -2)
            {
                error = "Scratch notes require a left or right scratch line.";
                return false;
            }
        }
        else if (targetLine < 1 || targetLine > ChartHolder.MainLineCount)
        {
            error = "Note line must be between 1 and 4.";
            return false;
        }

        if (sourceType == NoteType.LongTap && targetPowered)
        {
            error = "Powered Long Tap is not supported by the chart format.";
            return false;
        }

        if (sourceType == NoteType.Air &&
            (targetAirValue < 1 || targetAirValue > 99))
        {
            error = "Air value must be between 1 and 99.";
            return false;
        }

        ChartHolder targetHolder = GetHolder(targetAbsolutePosition);

        bool usesSourceSlot =
            sourceHolder == targetHolder && sourceLine == targetLine;

        if (!usesSourceSlot &&
            targetHolder != null &&
            targetHolder.HasNote(targetLine, sourceType))
        {
            error = "Another note already occupies the target position and line.";
            return false;
        }

        if (!sourceHolder.TryDetachNote(
                noteObject,
                out int detachedLine,
                out NoteType detachedType,
                out GameObject[] noteObjects,
                out NoteHandleType detachedHandle,
                out bool detachedPowered,
                out int detachedAirValue))
        {
            error = "Selected note could not be detached from its source.";
            return false;
        }

        bool createdTargetHolder = targetHolder == null;
        targetHolder ??= GetOrCreateHolder(
            targetAbsolutePosition / ChartHolder.PositionUnitsPerMeasure,
            targetAbsolutePosition % ChartHolder.PositionUnitsPerMeasure);
        NoteHandleType? storedHandle = detachedType.IsScratch()
            ? null
            : targetHandle;
        bool powered = detachedType == NoteType.Air
            ? false
            : targetPowered;
        int airValue = detachedType == NoteType.Air
            ? targetAirValue
            : detachedAirValue;

        if (!targetHolder.AddNote(
                targetLine,
                detachedType,
                noteObjects,
                storedHandle,
                powered,
                airValue))
        {
            sourceHolder.AddNote(
                detachedLine,
                detachedType,
                noteObjects,
                detachedHandle,
                detachedPowered,
                detachedAirValue);

            if (createdTargetHolder && !targetHolder.HasChartData)
            {
                ChartHolderList.Remove(targetHolder);
            }

            error = "The target note slot could not be updated.";
            return false;
        }

        if (sourceHolder != targetHolder && !sourceHolder.HasChartData)
        {
            ChartHolderList.Remove(sourceHolder);
        }

        if (sourceType.IsLong())
        {
            RefreshLongNoteLengths(sourceLine);

            if (sourceLine != targetLine)
            {
                RefreshLongNoteLengths(targetLine);
            }
        }

        error = null;
        return true;
    }

    /// <summary>드래그 이동에서 현재 노트 속성을 보존한 채 위치만 수정합니다.</summary>
    internal static bool MoveNote(
        GameObject noteObject,
        int targetAbsolutePosition,
        int targetLine,
        NoteHandleType targetHandle)
    {
        if (!TryGetNoteData(
                noteObject,
                out ChartHolder holder,
                out int sourceLine,
                out NoteType noteType,
                out _,
                out bool isPowered))
        {
            return false;
        }

        int airValue = noteType == NoteType.Air
            ? holder.airNoteValues[sourceLine - 1]
            : 0;
        return EditNote(
            noteObject,
            targetAbsolutePosition,
            targetLine,
            targetHandle,
            isPowered,
            airValue,
            out _);
    }

    internal static ChartHolder GetHolder(int absolutePosition)
    {
        int index = FindInsertionIndex(absolutePosition);
        return index < ChartHolderList.Count &&
               ChartHolderList[index].AbsoluteChartPosition == absolutePosition
            ? ChartHolderList[index]
            : null;
    }

    internal static void RestoreHolder(
        int absolutePosition,
        ChartHolder replacement)
    {
        int index = FindInsertionIndex(absolutePosition);

        if (index < ChartHolderList.Count &&
            ChartHolderList[index].AbsoluteChartPosition == absolutePosition)
        {
            ChartHolderList[index].DestroyAllNoteObjects();
            ChartHolderList.RemoveAt(index);
        }

        if (replacement == null)
        {
            return;
        }

        replacement.EnsureStorage();

        if (replacement.AbsoluteChartPosition != absolutePosition)
        {
            throw new InvalidOperationException(
                "The history snapshot position does not match its change.");
        }

        ChartHolderList.Insert(index, replacement);
    }

    /// <summary>
    /// 같은 라인의 Long 노트를 아래부터 두 개씩 묶어 표시 길이를 다시 계산합니다.
    /// </summary>
    public static void RefreshLongNoteLengths(int line)
    {
        GameObject[] pendingStartObjects = null;
        int pendingStartPosition = 0;

        for (int i = 0; i < ChartHolderList.Count; i++)
        {
            ChartHolder holder = ChartHolderList[i];

            if (!holder.TryGetNote(
                    line,
                    out NoteType noteType,
                    out GameObject[] noteObjects) ||
                !noteType.IsLong())
            {
                continue;
            }

            // 짝수 번째와 짝이 없는 마지막 노트는 길이 0으로 유지합니다.
            SetNoteLength(noteObjects, 0f);

            if (pendingStartObjects == null)
            {
                pendingStartObjects = noteObjects;
                pendingStartPosition = holder.AbsoluteChartPosition;
                continue;
            }

            float length = Mathf.Max(
                0f,
                (holder.AbsoluteChartPosition - pendingStartPosition) /
                ChartHolder.PositionUnitsPerWorldUnit);
            SetNoteLength(pendingStartObjects, length);
            pendingStartObjects = null;
        }
    }

    private static void SetNoteLength(GameObject[] noteObjects, float length)
    {
        if (noteObjects == null)
        {
            return;
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            GameObject noteObject = noteObjects[i];

            if (noteObject &&
                noteObject.TryGetComponent(out NoteLength noteLength))
            {
                noteLength.SetLength(length);
            }
        }
    }

    private static int FindInsertionIndex(int absolutePosition)
    {
        int low = 0;
        int high = ChartHolderList.Count;

        while (low < high)
        {
            int middle = low + (high - low) / 2;

            if (ChartHolderList[middle].AbsoluteChartPosition < absolutePosition)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static void NormalizePosition(
        ref int chartNumber,
        ref int chartPosition)
    {
        if (chartNumber < 0 || chartPosition < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chartPosition),
                "Chart position cannot be negative.");
        }

        chartNumber = checked(
            chartNumber +
            chartPosition / ChartHolder.PositionUnitsPerMeasure);
        chartPosition %= ChartHolder.PositionUnitsPerMeasure;
    }

    private static void ValidateHolderPosition(ChartHolder holder, int index)
    {
        if (holder.ChartNumber < 0 ||
            holder.ChartNumber > 999 ||
            holder.ChartPos < 0 ||
            holder.ChartPos > ChartHolder.PositionUnitsPerMeasure)
        {
            throw new ArgumentOutOfRangeException(
                nameof(holder),
                $"Chart holder at index {index} has an invalid position: " +
                $"{holder.ChartNumber}|{holder.ChartPos}.");
        }
    }
}

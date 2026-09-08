using System;
using System.Collections.Generic;
using REmind.Charting;
using REmind.Data;
using UnityEngine;

public static class ChartManager
{
    private static readonly List<ChartHolder> ChartHolderList =
        new List<ChartHolder>();

    public static IReadOnlyList<ChartHolder> ChartHolders => ChartHolderList;
    public static event Action ChartChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ChartHolderList.Clear();
        ChartChanged = null;
    }

    /// <summary>
    /// 화면용 월드 Y를 240-pulse 타임라인 위치로 변환해 채보 묶음을 반환합니다.
    /// </summary>
    public static ChartHolder GetOrCreateHolder(float worldPositionY)
    {
        int absolutePosition = Mathf.Clamp(
            ChartHolder.WorldYToAbsolutePosition(worldPositionY),
            0,
            ChartHolder.MaximumAbsolutePosition);
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

        if (chartNumber > ChartHolder.MaximumMeasureNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chartNumber),
                chartNumber,
                $"Chart measure must be between 0 and " +
                $"{ChartHolder.MaximumMeasureNumber}.");
        }

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
        NotifyChartChanged();
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
        ChartEffectDocumentState.Reset();
        NotifyChartChanged();
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

            NotifyChartChanged();
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

    /// <summary>선택된 Tap과 Long Tap의 타입을 서로 전환합니다.</summary>
    internal static bool ChangeTapNoteType(
        GameObject noteObject,
        NoteType targetType,
        out ChartHolder holder,
        out int line,
        out NoteHandleType handleType,
        out GameObject[] previousNoteObjects,
        out string error)
    {
        if (!TryGetNoteData(
                noteObject,
                out holder,
                out line,
                out NoteType noteType,
                out handleType,
                out _) ||
            (noteType != NoteType.Tap &&
             noteType != NoteType.LongTap))
        {
            previousNoteObjects = null;
            error = "Selected Tap note data could not be found.";
            return false;
        }

        if ((targetType != NoteType.Tap &&
             targetType != NoteType.LongTap) ||
            targetType == noteType)
        {
            previousNoteObjects = null;
            error = "Tap target type must be the opposite Tap variant.";
            return false;
        }

        if (!holder.TryChangeTapNoteType(
                noteObject,
                targetType,
                out line,
                out handleType,
                out previousNoteObjects))
        {
            previousNoteObjects = null;
            error = "Tap note type could not be updated.";
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
        ScratchMotionData targetScratchMotion,
        ScratchPointType? targetScratchPointType,
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
            targetAbsolutePosition > ChartHolder.MaximumAbsolutePosition)
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

            ScratchPointType pointType = targetScratchPointType ??
                sourceHolder.GetScratchPointType(sourceLine);

            if ((sourceType == NoteType.Scratch &&
                 pointType != ScratchPointType.Tap) ||
                (sourceType == NoteType.LongScratch &&
                 pointType == ScratchPointType.Tap))
            {
                error = sourceType == NoteType.Scratch
                    ? "Single Scratch requires a Tap point."
                    : "Long Scratch requires a Start, Mid, or End point.";
                return false;
            }
        }
        else if (targetLine < 1 || targetLine > ChartHolder.MainLineCount)
        {
            error = "Note line must be between 1 and 4.";
            return false;
        }

        if (sourceType == NoteType.Air &&
            (targetAirValue < 1 || targetAirValue > 99))
        {
            error = "Air value must be between 1 and 99.";
            return false;
        }

        ChartHolder targetHolder = GetHolder(targetAbsolutePosition);
        ScratchPointType sourceScratchPointType = sourceType.IsScratch()
            ? sourceHolder.GetScratchPointType(sourceLine)
            : ScratchPointType.Tap;

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
                out int detachedAirValue,
                out ScratchMotionData detachedScratchMotion))
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
        bool powered = detachedType.IsScratch() && targetPowered;
        int airValue = detachedType == NoteType.Air
            ? targetAirValue
            : detachedAirValue;
        ScratchMotionData scratchMotion = detachedType.IsScratch()
            ? targetScratchMotion ?? detachedScratchMotion
            : null;
        ScratchPointType? scratchPointType = detachedType.IsScratch()
            ? targetScratchPointType ?? sourceScratchPointType
            : null;

        if (!targetHolder.AddNote(
                targetLine,
                detachedType,
                noteObjects,
                storedHandle,
                powered,
                airValue,
                scratchMotion,
                scratchPointType))
        {
            sourceHolder.AddNote(
                detachedLine,
                detachedType,
                noteObjects,
                detachedHandle,
                detachedPowered,
                detachedAirValue,
                detachedScratchMotion,
                sourceScratchPointType);

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

        NotifyChartChanged();
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

        if (noteType == NoteType.Camera)
        {
            return EditCameraNote(
                noteObject,
                targetAbsolutePosition,
                holder.cameraOffsetX,
                holder.cameraSpinDirection,
                out _);
        }

        if (noteType == NoteType.Effect)
        {
            return MoveEffectNote(noteObject, targetAbsolutePosition, out _);
        }

        if (noteType == NoteType.Marker)
        {
            return MoveMarkerNote(
                noteObject,
                targetAbsolutePosition,
                out _);
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
            null,
            null,
            out _);
    }

    /// <summary>Effect 이동은 고유 ID와 JSON 설정 연결을 그대로 보존합니다.</summary>
    internal static bool MoveEffectNote(
        GameObject noteObject,
        int targetAbsolutePosition,
        out string error)
    {
        if (!TryGetNoteData(noteObject, out ChartHolder sourceHolder,
                out _, out NoteType noteType, out _, out _) ||
            noteType != NoteType.Effect)
        {
            error = "Selected Effect note could not be found.";
            return false;
        }

        if (targetAbsolutePosition < 0 ||
            targetAbsolutePosition > ChartHolder.MaximumAbsolutePosition)
        {
            error = "Effect position is outside the supported chart range.";
            return false;
        }

        if (sourceHolder.AbsoluteChartPosition == targetAbsolutePosition)
        {
            error = null;
            return true;
        }

        ChartHolder targetHolder = GetHolder(targetAbsolutePosition);
        if (targetHolder != null && targetHolder.isEffect)
        {
            error = "Another Effect already exists at the target position.";
            return false;
        }

        ChartHolder sourceData = sourceHolder.CloneData();
        if (!sourceHolder.TryDetachEffectNote(noteObject, out GameObject[] objects))
        {
            error = "Selected Effect could not be detached.";
            return false;
        }

        bool createdTarget = targetHolder == null;
        targetHolder ??= GetOrCreateHolder(
            targetAbsolutePosition / ChartHolder.PositionUnitsPerMeasure,
            targetAbsolutePosition % ChartHolder.PositionUnitsPerMeasure);
        if (!targetHolder.AddEffectNote(objects))
        {
            sourceHolder.AddEffectNote(objects);
            CopyEffectDefinition(sourceData, sourceHolder, false);
            if (createdTarget && !targetHolder.HasChartData)
            {
                ChartHolderList.Remove(targetHolder);
            }
            error = "Effect target could not be updated.";
            return false;
        }

        CopyEffectDefinition(sourceData, targetHolder, false);
        if (!sourceHolder.HasChartData)
        {
            ChartHolderList.Remove(sourceHolder);
        }
        error = null;
        return true;
    }

    internal static void CopyEffectDefinition(
        ChartHolder source, ChartHolder target, bool assignNewIdentity)
    {
        target.effectId = assignNewIdentity
            ? Guid.NewGuid().ToString("N")
            : source.effectId;
        target.effectTypeId = source.effectTypeId;
        target.effectCommandId = source.effectCommandId;
        target.effectOrder = source.effectOrder;
        target.effectParametersJson = source.effectParametersJson;
    }

    /// <summary>Camera 이벤트의 위치, 기준 X 오프셋과 회전 방향을 원자적으로 수정합니다.</summary>
    internal static bool EditCameraNote(
        GameObject noteObject,
        int targetAbsolutePosition,
        float offsetX,
        ChartCameraSpinDirection spinDirection,
        out string error)
    {
        if (!TryGetNoteData(
                noteObject,
                out ChartHolder sourceHolder,
                out _,
                out NoteType noteType,
                out _,
                out _) ||
            noteType != NoteType.Camera)
        {
            error = "Selected Camera Note data could not be found.";
            return false;
        }

        if (targetAbsolutePosition < 0 ||
            targetAbsolutePosition > ChartHolder.MaximumAbsolutePosition)
        {
            error = "Camera Note position is outside the supported chart range.";
            return false;
        }

        if (!float.IsFinite(offsetX))
        {
            error = "Camera Offset X must be finite.";
            return false;
        }

        if (!Enum.IsDefined(
                typeof(ChartCameraSpinDirection),
                spinDirection))
        {
            error = $"Unsupported Camera spin direction: {spinDirection}.";
            return false;
        }

        if (sourceHolder.AbsoluteChartPosition == targetAbsolutePosition)
        {
            sourceHolder.cameraOffsetX = offsetX;
            sourceHolder.cameraSpinDirection = spinDirection;
            NotifyChartChanged();
            error = null;
            return true;
        }

        ChartHolder targetHolder = GetHolder(targetAbsolutePosition);

        if (targetHolder != null && targetHolder.isCameraMove)
        {
            error = "Another Camera Note already exists at the target position.";
            return false;
        }

        if (!sourceHolder.TryDetachCameraNote(
                noteObject,
                out GameObject[] noteObjects,
                out float sourceOffsetX,
                out ChartCameraSpinDirection sourceSpinDirection))
        {
            error = "Selected Camera Note could not be detached from its source.";
            return false;
        }

        bool createdTargetHolder = targetHolder == null;
        targetHolder ??= GetOrCreateHolder(
            targetAbsolutePosition / ChartHolder.PositionUnitsPerMeasure,
            targetAbsolutePosition % ChartHolder.PositionUnitsPerMeasure);

        if (!targetHolder.AddCameraNote(
                noteObjects,
                offsetX,
                spinDirection))
        {
            sourceHolder.AddCameraNote(
                noteObjects,
                sourceOffsetX,
                sourceSpinDirection);

            if (createdTargetHolder && !targetHolder.HasChartData)
            {
                ChartHolderList.Remove(targetHolder);
            }

            error = "The target Camera Note could not be updated.";
            return false;
        }

        if (!sourceHolder.HasChartData)
        {
            ChartHolderList.Remove(sourceHolder);
        }

        NotifyChartChanged();
        error = null;
        return true;
    }

    /// <summary>값 없는 Marker 이벤트의 위치를 원자적으로 이동합니다.</summary>
    internal static bool MoveMarkerNote(
        GameObject noteObject,
        int targetAbsolutePosition,
        out string error)
    {
        if (!TryGetNoteData(
                noteObject,
                out ChartHolder sourceHolder,
                out _,
                out NoteType noteType,
                out _,
                out _) ||
            noteType != NoteType.Marker)
        {
            error = "Selected Marker data could not be found.";
            return false;
        }

        if (targetAbsolutePosition < 0 ||
            targetAbsolutePosition > ChartHolder.MaximumAbsolutePosition)
        {
            error = "Marker position is outside the supported chart range.";
            return false;
        }

        if (sourceHolder.AbsoluteChartPosition == targetAbsolutePosition)
        {
            error = null;
            return true;
        }

        ChartHolder targetHolder = GetHolder(targetAbsolutePosition);

        if (targetHolder != null && targetHolder.isMarker)
        {
            error = "Another Marker already exists at the target position.";
            return false;
        }

        if (!sourceHolder.TryDetachMarkerNote(
                noteObject,
                out GameObject[] noteObjects))
        {
            error = "Selected Marker could not be detached from its source.";
            return false;
        }

        bool createdTargetHolder = targetHolder == null;
        targetHolder ??= GetOrCreateHolder(
            targetAbsolutePosition / ChartHolder.PositionUnitsPerMeasure,
            targetAbsolutePosition % ChartHolder.PositionUnitsPerMeasure);

        if (!targetHolder.AddMarkerNote(noteObjects))
        {
            sourceHolder.AddMarkerNote(noteObjects);

            if (createdTargetHolder && !targetHolder.HasChartData)
            {
                ChartHolderList.Remove(targetHolder);
            }

            error = "The target Marker could not be updated.";
            return false;
        }

        if (!sourceHolder.HasChartData)
        {
            ChartHolderList.Remove(sourceHolder);
        }

        NotifyChartChanged();
        error = null;
        return true;
    }

    internal static ScratchPointType GetSuggestedScratchPointType(
        int line,
        int targetAbsolutePosition)
    {
        bool isOpen = false;

        for (int i = 0; i < ChartHolderList.Count; i++)
        {
            ChartHolder holder = ChartHolderList[i];

            if (holder.AbsoluteChartPosition >= targetAbsolutePosition)
            {
                break;
            }

            int storageIndex = line == -1
                ? ChartHolder.MainLineCount
                : ChartHolder.MainLineCount + 1;

            if (holder.noteTypes[storageIndex] != NoteType.LongScratch)
            {
                continue;
            }

            switch (holder.GetScratchPointType(line))
            {
                case ScratchPointType.Start:
                    isOpen = true;
                    break;
                case ScratchPointType.End:
                    isOpen = false;
                    break;
            }
        }

        return isOpen ? ScratchPointType.End : ScratchPointType.Start;
    }

    /// <summary>
    /// 대상 위치보다 앞선 Long Tap을 두 개씩 짝지었을 때 같은 라인에 닫히지 않은
    /// 시작점이 남아 있는지 반환합니다.
    /// </summary>
    internal static bool HasOpenLongTap(
        int line,
        int targetAbsolutePosition)
    {
        if (line < 1 || line > ChartHolder.MainLineCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(line),
                line,
                $"Main line must be between 1 and " +
                $"{ChartHolder.MainLineCount}.");
        }

        bool isOpen = false;

        for (int i = 0; i < ChartHolderList.Count; i++)
        {
            ChartHolder holder = ChartHolderList[i];

            if (holder.AbsoluteChartPosition >= targetAbsolutePosition)
            {
                break;
            }

            if (holder.TryGetNote(
                    line,
                    out NoteType noteType,
                    out _) &&
                noteType == NoteType.LongTap)
            {
                isOpen = !isOpen;
            }
        }

        return isOpen;
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

    internal static void NotifyChartChanged()
    {
        ChartChanged?.Invoke();
    }

    /// <summary>
    /// 같은 라인의 Long 노트를 아래부터 두 개씩 묶어 표시 길이를 다시 계산합니다.
    /// </summary>
    public static void RefreshLongNoteLengths(int line)
    {
        if (line < 0)
        {
            RefreshScratchLongNoteLengths(line);
            return;
        }

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

            if (pendingStartObjects == null)
            {
                SetNoteLength(
                    noteObjects,
                    0f,
                    showUnlinkedPreview: true);
                pendingStartObjects = noteObjects;
                pendingStartPosition = holder.AbsoluteChartPosition;
                continue;
            }

            // End에는 Stub을 표시하지 않고, Start의 Stub은 실제 Ribbon으로
            // 교체합니다.
            SetNoteLength(noteObjects, 0f);

            float length = Mathf.Max(
                0f,
                ChartHolder.PositionDeltaToWorldLength(
                    holder.AbsoluteChartPosition - pendingStartPosition));
            SetNoteLength(pendingStartObjects, length);
            pendingStartObjects = null;
        }
    }

    private static void RefreshScratchLongNoteLengths(int line)
    {
        GameObject[] pendingSegmentObjects = null;
        int pendingSegmentPosition = 0;

        for (int i = 0; i < ChartHolderList.Count; i++)
        {
            ChartHolder holder = ChartHolderList[i];

            if (!holder.TryGetNote(
                    line,
                    out NoteType noteType,
                    out GameObject[] noteObjects) ||
                noteType != NoteType.LongScratch)
            {
                continue;
            }

            ScratchPointType pointType =
                holder.GetScratchPointType(line);

            if (pointType == ScratchPointType.Start)
            {
                SetNoteLength(
                    noteObjects,
                    0f,
                    showUnlinkedPreview: true);
                pendingSegmentObjects = noteObjects;
                pendingSegmentPosition = holder.AbsoluteChartPosition;
                continue;
            }

            SetNoteLength(noteObjects, 0f);

            if (pendingSegmentObjects != null)
            {
                float length = Mathf.Max(
                    0f,
                    ChartHolder.PositionDeltaToWorldLength(
                        holder.AbsoluteChartPosition -
                        pendingSegmentPosition));
                SetNoteLength(pendingSegmentObjects, length);
            }

            if (pointType == ScratchPointType.Mid)
            {
                SetNoteLength(
                    noteObjects,
                    0f,
                    showUnlinkedPreview: true);
                pendingSegmentObjects = noteObjects;
                pendingSegmentPosition = holder.AbsoluteChartPosition;
            }
            else
            {
                pendingSegmentObjects = null;
            }
        }
    }

    private static void SetNoteLength(
        GameObject[] noteObjects,
        float length,
        bool showUnlinkedPreview = false)
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
                noteLength.SetStraightLength(length);
                noteLength.SetUnlinkedPreviewVisible(
                    showUnlinkedPreview);
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
            holder.ChartNumber > ChartHolder.MaximumMeasureNumber ||
            holder.ChartPos < 0 ||
            holder.ChartPos >= ChartHolder.PositionUnitsPerMeasure)
        {
            throw new ArgumentOutOfRangeException(
                nameof(holder),
                $"Chart holder at index {index} has an invalid position: " +
                $"{holder.ChartNumber}|{holder.ChartPos}.");
        }
    }
}

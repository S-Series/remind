using System;
using UnityEngine;
using REmind.Charting;
using REmind.Data;

[Serializable]
public class ChartHolder
{
    public const int MainLineCount = 4;
    public const int ScratchLineCount = 2;
    public const int TotalLineCount = MainLineCount + ScratchLineCount;
    public const int AirNoteCount = 4;

    // Timeline resolution is based on 240 pulses so 1/3, 1/5, and 1/16
    // measure grids all land on exact integer positions. Twenty subunits per
    // pulse produce 4800 units per measure, which is also an exact multiple of
    // the legacy 1600-unit grid. Display geometry remains 160 units per measure.
    public const int PulsesPerMeasure = 240;
    public const int PositionUnitsPerPulse = 20;
    public const int PositionUnitsPerMeasure =
        PulsesPerMeasure * PositionUnitsPerPulse;
    public const int MeasureCount = 1000;
    public const int MaximumMeasureNumber = MeasureCount - 1;
    public const int MaximumAbsolutePosition =
        MeasureCount * PositionUnitsPerMeasure - 1;
    public const float WorldUnitsPerMeasure = 160f;
    public const float PositionUnitsPerWorldUnit =
        PositionUnitsPerMeasure / WorldUnitsPerMeasure;

    public int ChartNumber;
    public int ChartPos;
    // Main line handles correspond to lines 1-4.
    public NoteHandleType[] noteHandles;
    // 0-3: main lines 1-4, 4-5: left/right scratch lines.
    public NoteType[] noteTypes;
    // Main 0-3은 항상 false입니다. Scratch 4-5의 Motion 활성 상태를
    // 구버전 직렬화 데이터와 호환하기 위해 유지합니다.
    public bool[] isPoweredNotes;
    // Scratch motion data corresponds to left/right scratch lines.
    public ScratchMotionData[] scratchMotions;
    // Scratch point data corresponds to left/right scratch lines.
    public ScratchPointType[] scratchPointTypes;
    // Air note values correspond to main lines 1-4 and range from 00 to 99.
    public int[] airNoteValues;
    public float targetBpm = -1f; // -1 means that the BPM does not change.
    public bool hasLineSpeedChange;
    public float targetLineSpeed = 1f;
    public bool isEffect;
    public string effectId;
    public string effectTypeId;
    public string effectCommandId;
    public int effectOrder;
    // Attached editing state: written only to the difficulty's parameter JSON.
    public string effectParametersJson;
    public bool isMarker;
    public bool isCameraMove;
    public float cameraOffsetX;
    public ChartCameraSpinDirection cameraSpinDirection =
        ChartCameraSpinDirection.None;

    [NonSerialized] public GameObject[][] tapNoteObjectGroups;
    [NonSerialized] public GameObject[][] scratchNoteObjectGroups;
    [NonSerialized] public GameObject[][] airNoteObjectGroups;
    [NonSerialized] public GameObject[] actionNoteObjects;
    [NonSerialized] public GameObject[] speedNoteObjects;
    [NonSerialized] public GameObject[] cameraNoteObjects;
    [NonSerialized] public GameObject[] markerNoteObjects;
    [NonSerialized] public GameObject[] effectNoteObjects;

    public int AbsoluteChartPosition =>
        checked(ChartNumber * PositionUnitsPerMeasure + ChartPos);
    public float WorldY =>
        AbsolutePositionToWorldY(AbsoluteChartPosition);
    public bool HasChartData
    {
        get
        {
            EnsureStorage();

            for (int i = 0; i < noteTypes.Length; i++)
            {
                if (noteTypes[i] != NoteType.Unknown)
                {
                    return true;
                }
            }

            for (int i = 0; i < airNoteValues.Length; i++)
            {
                if (airNoteValues[i] != 0)
                {
                    return true;
                }
            }

            return targetBpm != -1f ||
                hasLineSpeedChange ||
                isEffect ||
                isCameraMove ||
                isMarker;
        }
    }

    public ChartHolder()
    {
        EnsureStorage();
    }

    public ChartHolder(int chartNumber, int chartPos)
        : this()
    {
        ChartNumber = chartNumber;
        ChartPos = chartPos;
    }

    public static int WorldYToAbsolutePosition(float worldY)
    {
        if (float.IsNaN(worldY) || float.IsInfinity(worldY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldY),
                worldY,
                "World Y must be finite.");
        }

        return checked((int)Math.Round(
            (double)worldY * PositionUnitsPerWorldUnit,
            MidpointRounding.AwayFromZero));
    }

    public static float AbsolutePositionToWorldY(int absolutePosition)
    {
        return absolutePosition / PositionUnitsPerWorldUnit;
    }

    public static float PositionDeltaToWorldLength(int positionDelta)
    {
        return positionDelta / PositionUnitsPerWorldUnit;
    }

    public static int GridIndexToAbsolutePosition(
        int gridIndex,
        int divisionsPerMeasure)
    {
        if (gridIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gridIndex));
        }

        if (divisionsPerMeasure <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisionsPerMeasure));
        }

        long numerator = checked(
            (long)gridIndex * PositionUnitsPerMeasure);
        long rounded = checked(
            (numerator + divisionsPerMeasure / 2L) /
            divisionsPerMeasure);
        return checked((int)rounded);
    }

    public static int ConvertAbsolutePosition(
        int sourceAbsolutePosition,
        int sourceUnitsPerMeasure)
    {
        if (sourceAbsolutePosition < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceAbsolutePosition));
        }

        if (sourceUnitsPerMeasure <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceUnitsPerMeasure));
        }

        long numerator = checked(
            (long)sourceAbsolutePosition * PositionUnitsPerMeasure);
        long rounded = checked(
            (numerator + sourceUnitsPerMeasure / 2L) /
            sourceUnitsPerMeasure);
        return checked((int)rounded);
    }

    /// <summary>지정한 라인에 이미 노트 데이터가 있는지 확인합니다.</summary>
    public bool HasNote(int line)
    {
        EnsureStorage();
        int index = GetLineIndex(line);
        return noteTypes[index] != NoteType.Unknown;
    }

    /// <summary>지정한 메인 라인에 Air 노트가 있는지 확인합니다.</summary>
    public bool HasAirNote(int line)
    {
        EnsureStorage();
        int index = GetMainLineIndex(line);
        return airNoteValues[index] > 0;
    }

    public bool HasNote(int line, NoteType noteType)
    {
        return noteType == NoteType.Air
            ? HasAirNote(line)
            : HasNote(line);
    }

    public ScratchMotionData GetScratchMotion(int line)
    {
        EnsureStorage();
        int scratchIndex = GetLineIndex(line) - MainLineCount;

        if (scratchIndex < 0 || scratchIndex >= ScratchLineCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(line),
                line,
                "Scratch motion requires line -1 or -2.");
        }

        NoteType noteType = noteTypes[MainLineCount + scratchIndex];
        return noteType.IsScratch()
            ? (scratchMotions[scratchIndex] ??
                ScratchMotionData.CreateDefault(noteType)).Clone()
            : null;
    }

    public ScratchPointType GetScratchPointType(int line)
    {
        EnsureStorage();
        int scratchIndex = GetLineIndex(line) - MainLineCount;

        if (scratchIndex < 0 || scratchIndex >= ScratchLineCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(line),
                line,
                "Scratch point type requires line -1 or -2.");
        }

        return scratchPointTypes[scratchIndex];
    }

    /// <summary>지정한 라인의 노트 종류와 모든 표현 오브젝트를 반환합니다.</summary>
    public bool TryGetNote(
        int line,
        out NoteType noteType,
        out GameObject[] noteObjects)
    {
        EnsureStorage();
        int index = GetLineIndex(line);
        noteType = noteTypes[index];
        noteObjects = index < MainLineCount
            ? tapNoteObjectGroups[index]
            : scratchNoteObjectGroups[index - MainLineCount];

        return noteType != NoteType.Unknown && noteObjects != null;
    }

    public bool TryGetAirNote(
        int line,
        out int value,
        out GameObject[] noteObjects)
    {
        EnsureStorage();
        int index = GetMainLineIndex(line);
        value = airNoteValues[index];
        noteObjects = airNoteObjectGroups[index];
        return value > 0 && noteObjects != null;
    }

    /// <summary>
    /// 노트 데이터와 같은 노트를 표현하는 모든 게임 오브젝트를 한 묶음으로 등록합니다.
    /// </summary>
    public bool AddNote(
        int line,
        NoteType noteType,
        GameObject[] noteObjects,
        NoteHandleType? handleType,
        bool isPowered = false,
        int airValue = 1,
        ScratchMotionData scratchMotion = null,
        ScratchPointType? scratchPointType = null)
    {
        EnsureStorage();

        if (noteType == NoteType.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(noteType),
                noteType,
                "Unknown cannot be registered as a chart note.");
        }

        if (noteObjects == null || noteObjects.Length == 0)
        {
            throw new ArgumentException(
                "A chart note requires at least one GameObject.",
                nameof(noteObjects));
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            if (!noteObjects[i])
            {
                throw new ArgumentException(
                    "A chart note cannot contain a missing GameObject.",
                    nameof(noteObjects));
            }
        }

        if (noteType == NoteType.Air)
        {
            return AddAirNote(line, airValue, noteObjects);
        }

        int index = GetLineIndex(line);

        if (noteTypes[index] != NoteType.Unknown)
        {
            return false;
        }

        bool isScratchLine = index >= MainLineCount;

        if (noteType.IsScratch() != isScratchLine)
        {
            throw new ArgumentException(
                "Scratch notes must use a Scratch line and Tap notes must " +
                "use a main line.",
                nameof(noteType));
        }

        ScratchMotionData storedScratchMotion = null;
        ScratchPointType storedScratchPointType = ScratchPointType.Tap;

        if (isScratchLine)
        {
            storedScratchMotion =
                (scratchMotion ??
                    ScratchMotionData.CreateDefault(noteType)).Clone();
            storedScratchPointType = scratchPointType ??
                (noteType == NoteType.Scratch
                    ? ScratchPointType.Tap
                    : ScratchPointType.Start);

            if (noteType == NoteType.Scratch &&
                storedScratchPointType != ScratchPointType.Tap)
            {
                throw new ArgumentException(
                    "Single Scratch notes require a Tap point.",
                    nameof(scratchPointType));
            }

            if (noteType == NoteType.LongScratch &&
                storedScratchPointType == ScratchPointType.Tap)
            {
                throw new ArgumentException(
                    "Long Scratch notes require a Start, Mid, or End point.",
                    nameof(scratchPointType));
            }

            storedScratchMotion = ScratchMotionRules.NormalizeMotion(
                noteType,
                storedScratchPointType,
                storedScratchMotion);
        }

        noteTypes[index] = noteType;
        isPoweredNotes[index] = isScratchLine &&
            storedScratchMotion.MotionType != ScratchMotionType.None;

        if (index < MainLineCount)
        {
            NoteHandleType defaultHandle =
                index < MainLineCount / 2
                    ? NoteHandleType.Left
                    : NoteHandleType.Right;

            noteHandles[index] = handleType ?? defaultHandle;
            tapNoteObjectGroups[index] = noteObjects;

            if (noteType.IsLong())
            {
                ChartManager.RefreshLongNoteLengths(line);
            }

            return true;
        }

        scratchNoteObjectGroups[index - MainLineCount] = noteObjects;
        scratchMotions[index - MainLineCount] = storedScratchMotion;
        scratchPointTypes[index - MainLineCount] =
            storedScratchPointType;
        RefreshScratchArrowPattern(index - MainLineCount);

        if (noteType.IsLong())
        {
            ChartManager.RefreshLongNoteLengths(line);
        }

        return true;
    }

    private bool AddAirNote(
        int line,
        int value,
        GameObject[] noteObjects)
    {
        int index = GetMainLineIndex(line);

        if (value < 1 || value > 99)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Air note value must be between 1 and 99.");
        }

        if (airNoteValues[index] > 0)
        {
            return false;
        }

        airNoteValues[index] = value;
        airNoteObjectGroups[index] = noteObjects;
        return true;
    }

    /// <summary>Camera 이벤트 데이터와 편집용 표현 오브젝트를 등록합니다.</summary>
    public bool AddCameraNote(
        GameObject[] noteObjects,
        float offsetX = 0f,
        ChartCameraSpinDirection spinDirection =
            ChartCameraSpinDirection.None)
    {
        EnsureStorage();

        if (isCameraMove ||
            !IsValidNoteObjects(noteObjects) ||
            !float.IsFinite(offsetX) ||
            !Enum.IsDefined(typeof(ChartCameraSpinDirection), spinDirection))
        {
            return false;
        }

        isCameraMove = true;
        cameraOffsetX = offsetX;
        cameraSpinDirection = spinDirection;
        cameraNoteObjects = noteObjects;
        return true;
    }

    public bool TryGetCameraNote(out GameObject[] noteObjects)
    {
        EnsureStorage();
        noteObjects = cameraNoteObjects;
        return isCameraMove;
    }

    public bool AddSpeedNote(GameObject[] noteObjects, float multiplier = 1f)
    {
        if (hasLineSpeedChange || !IsValidNoteObjects(noteObjects) ||
            !float.IsFinite(multiplier) || multiplier <= 0f)
        {
            return false;
        }

        hasLineSpeedChange = true;
        targetLineSpeed = multiplier;
        speedNoteObjects = noteObjects;
        return true;
    }

    public bool TryGetSpeedNote(out GameObject[] noteObjects)
    {
        noteObjects = speedNoteObjects;
        return hasLineSpeedChange;
    }

    public bool AttachSpeedNoteObjects(GameObject[] noteObjects)
    {
        if (!hasLineSpeedChange || speedNoteObjects != null ||
            !IsValidNoteObjects(noteObjects))
        {
            return false;
        }

        speedNoteObjects = noteObjects;
        return true;
    }

    internal bool TryDetachSpeedNote(GameObject noteObject,
        out GameObject[] noteObjects, out float multiplier)
    {
        noteObjects = null;
        multiplier = 1f;
        if (!hasLineSpeedChange ||
            !FindNoteObject(speedNoteObjects, noteObject))
        {
            return false;
        }

        noteObjects = speedNoteObjects;
        multiplier = targetLineSpeed;
        speedNoteObjects = null;
        hasLineSpeedChange = false;
        targetLineSpeed = 1f;
        return true;
    }

    public void EnsureEffectIdentity()
    {
        if (isEffect && string.IsNullOrWhiteSpace(effectId))
            effectId = "fx_" + Guid.NewGuid().ToString("N");
    }

    public void ClearEffectDefinition()
    {
        isEffect = false;
        effectId = effectTypeId = effectCommandId = effectParametersJson = null;
        effectOrder = 0;
    }

    public bool AddEffectNote(GameObject[] noteObjects)
    {
        if (isEffect || !IsValidNoteObjects(noteObjects)) return false;
        isEffect = true;
        EnsureEffectIdentity();
        effectNoteObjects = noteObjects;
        return true;
    }

    public bool TryGetEffectNote(out GameObject[] noteObjects)
    {
        noteObjects = effectNoteObjects;
        return isEffect;
    }

    public bool AttachEffectNoteObjects(GameObject[] noteObjects)
    {
        if (!isEffect || effectNoteObjects != null || !IsValidNoteObjects(noteObjects))
            return false;
        effectNoteObjects = noteObjects;
        return true;
    }

    internal bool TryDetachEffectNote(GameObject noteObject, out GameObject[] noteObjects)
    {
        noteObjects = null;
        if (!isEffect || !FindNoteObject(effectNoteObjects, noteObject)) return false;
        noteObjects = effectNoteObjects;
        effectNoteObjects = null;
        ClearEffectDefinition();
        return true;
    }

    /// <summary>값 없는 위치 Marker와 편집용 표현 오브젝트를 등록합니다.</summary>
    public bool AddMarkerNote(GameObject[] noteObjects)
    {
        EnsureStorage();

        if (isMarker || !IsValidNoteObjects(noteObjects))
        {
            return false;
        }

        isMarker = true;
        markerNoteObjects = noteObjects;
        return true;
    }

    public bool TryGetMarkerNote(out GameObject[] noteObjects)
    {
        EnsureStorage();
        noteObjects = markerNoteObjects;
        return isMarker;
    }

    /// <summary>
    /// 클릭한 표현 오브젝트와 같은 노트에 속한 복제 오브젝트를 모두 삭제합니다.
    /// </summary>
    public bool DeleteNote(GameObject noteObject)
    {
        if (!noteObject)
        {
            return false;
        }

        EnsureStorage();

        if (FindNoteObject(speedNoteObjects, noteObject))
        {
            DestroyNoteObjects(speedNoteObjects);
            speedNoteObjects = null;
            hasLineSpeedChange = false;
            targetLineSpeed = 1f;
            return true;
        }

        if (FindNoteObject(effectNoteObjects, noteObject))
        {
            DestroyNoteObjects(effectNoteObjects);
            effectNoteObjects = null;
            ClearEffectDefinition();
            return true;
        }

        if (FindNoteObject(markerNoteObjects, noteObject))
        {
            DeleteMarkerNote();
            return true;
        }

        if (FindNoteObject(cameraNoteObjects, noteObject))
        {
            DeleteCameraNote();
            return true;
        }

        int tapIndex = FindNoteGroup(tapNoteObjectGroups, noteObject);

        if (tapIndex >= 0)
        {
            bool wasLong = noteTypes[tapIndex].IsLong();
            DeleteTapNote(tapIndex);

            if (wasLong)
            {
                ChartManager.RefreshLongNoteLengths(tapIndex + 1);
            }

            return true;
        }

        int airIndex = FindNoteGroup(airNoteObjectGroups, noteObject);

        if (airIndex >= 0)
        {
            DeleteAirNote(airIndex);
            return true;
        }

        int scratchIndex = FindNoteGroup(scratchNoteObjectGroups, noteObject);

        if (scratchIndex >= 0)
        {
            bool wasLong = noteTypes[scratchIndex + MainLineCount].IsLong();
            DeleteScratchNote(scratchIndex);

            if (wasLong)
            {
                int line = scratchIndex == 0 ? -1 : -2;
                ChartManager.RefreshLongNoteLengths(line);
            }

            return true;
        }

        return false;
    }

    public bool ContainsNoteObject(GameObject noteObject)
    {
        if (!noteObject)
        {
            return false;
        }

        EnsureStorage();
        return FindNoteObject(speedNoteObjects, noteObject) ||
            FindNoteObject(effectNoteObjects, noteObject) ||
            FindNoteObject(markerNoteObjects, noteObject) ||
            FindNoteObject(cameraNoteObjects, noteObject) ||
            FindNoteGroup(tapNoteObjectGroups, noteObject) >= 0 ||
            FindNoteGroup(airNoteObjectGroups, noteObject) >= 0 ||
            FindNoteGroup(scratchNoteObjectGroups, noteObject) >= 0;
    }

    /// <summary>표현 오브젝트에 연결된 노트의 편집용 데이터를 반환합니다.</summary>
    internal bool TryGetNoteData(
        GameObject noteObject,
        out int line,
        out NoteType noteType,
        out NoteHandleType handleType,
        out bool isPowered)
    {
        EnsureStorage();

        if (FindNoteObject(speedNoteObjects, noteObject))
        {
            line = 0;
            noteType = NoteType.Speed;
            handleType = NoteHandleType.Unknown;
            isPowered = false;
            return hasLineSpeedChange;
        }

        if (FindNoteObject(effectNoteObjects, noteObject))
        {
            line = 0;
            noteType = NoteType.Effect;
            handleType = NoteHandleType.Unknown;
            isPowered = false;
            return isEffect;
        }

        if (FindNoteObject(markerNoteObjects, noteObject))
        {
            line = 0;
            noteType = NoteType.Marker;
            handleType = NoteHandleType.Unknown;
            isPowered = false;
            return isMarker;
        }

        if (FindNoteObject(cameraNoteObjects, noteObject))
        {
            line = 0;
            noteType = NoteType.Camera;
            handleType = NoteHandleType.Unknown;
            isPowered = false;
            return isCameraMove;
        }

        int tapIndex = FindNoteGroup(tapNoteObjectGroups, noteObject);

        if (tapIndex >= 0)
        {
            line = tapIndex + 1;
            noteType = noteTypes[tapIndex];
            handleType = noteHandles[tapIndex];
            isPowered = false;
            return noteType != NoteType.Unknown;
        }

        int airIndex = FindNoteGroup(airNoteObjectGroups, noteObject);

        if (airIndex >= 0)
        {
            line = airIndex + 1;
            noteType = NoteType.Air;
            handleType = airIndex < MainLineCount / 2
                ? NoteHandleType.Left
                : NoteHandleType.Right;
            isPowered = false;
            return airNoteValues[airIndex] > 0;
        }

        int scratchIndex = FindNoteGroup(
            scratchNoteObjectGroups,
            noteObject);

        if (scratchIndex >= 0)
        {
            int storageIndex = MainLineCount + scratchIndex;
            line = scratchIndex == 0 ? -1 : -2;
            noteType = noteTypes[storageIndex];
            handleType = NoteHandleType.Unknown;
            isPowered = isPoweredNotes[storageIndex];
            return noteType != NoteType.Unknown;
        }

        line = 0;
        noteType = NoteType.Unknown;
        handleType = NoteHandleType.Unknown;
        isPowered = false;
        return false;
    }

    /// <summary>Camera 이벤트를 파괴하지 않고 Holder에서 분리합니다.</summary>
    internal bool TryDetachCameraNote(
        GameObject noteObject,
        out GameObject[] noteObjects,
        out float offsetX,
        out ChartCameraSpinDirection spinDirection)
    {
        EnsureStorage();

        if (!isCameraMove ||
            !FindNoteObject(cameraNoteObjects, noteObject))
        {
            noteObjects = null;
            offsetX = 0f;
            spinDirection = ChartCameraSpinDirection.None;
            return false;
        }

        noteObjects = cameraNoteObjects;
        offsetX = cameraOffsetX;
        spinDirection = cameraSpinDirection;
        cameraNoteObjects = null;
        isCameraMove = false;
        cameraOffsetX = 0f;
        cameraSpinDirection = ChartCameraSpinDirection.None;
        return true;
    }

    /// <summary>Marker를 파괴하지 않고 Holder에서 분리합니다.</summary>
    internal bool TryDetachMarkerNote(
        GameObject noteObject,
        out GameObject[] noteObjects)
    {
        EnsureStorage();

        if (!isMarker || !FindNoteObject(markerNoteObjects, noteObject))
        {
            noteObjects = null;
            return false;
        }

        noteObjects = markerNoteObjects;
        markerNoteObjects = null;
        isMarker = false;
        return true;
    }

    /// <summary>
    /// 선택된 Tap 계열의 타입을 바꾸고 기존 표시 오브젝트를 분리합니다.
    /// </summary>
    internal bool TryChangeTapNoteType(
        GameObject noteObject,
        NoteType targetType,
        out int line,
        out NoteHandleType handleType,
        out GameObject[] previousNoteObjects)
    {
        EnsureStorage();
        int tapIndex = FindNoteGroup(tapNoteObjectGroups, noteObject);

        if (tapIndex >= 0 &&
            (noteTypes[tapIndex] == NoteType.Tap ||
             noteTypes[tapIndex] == NoteType.LongTap) &&
            (targetType == NoteType.Tap ||
             targetType == NoteType.LongTap))
        {
            line = tapIndex + 1;
            handleType = noteHandles[tapIndex];
            previousNoteObjects = tapNoteObjectGroups[tapIndex];
            tapNoteObjectGroups[tapIndex] = null;
            noteTypes[tapIndex] = targetType;
            isPoweredNotes[tapIndex] = false;
            return true;
        }

        line = 0;
        handleType = NoteHandleType.Unknown;
        previousNoteObjects = null;
        return false;
    }

    /// <summary>
    /// 오브젝트를 파괴하지 않고 이 Holder에서 노트 데이터와 표현 묶음을 분리합니다.
    /// </summary>
    internal bool TryDetachNote(
        GameObject noteObject,
        out int line,
        out NoteType noteType,
        out GameObject[] noteObjects,
        out NoteHandleType handleType,
        out bool isPowered,
        out int airValue,
        out ScratchMotionData scratchMotion)
    {
        EnsureStorage();
        int tapIndex = FindNoteGroup(tapNoteObjectGroups, noteObject);

        if (tapIndex >= 0)
        {
            line = tapIndex + 1;
            noteType = noteTypes[tapIndex];
            noteObjects = tapNoteObjectGroups[tapIndex];
            handleType = noteHandles[tapIndex];
            isPowered = false;
            airValue = 0;
            scratchMotion = null;
            tapNoteObjectGroups[tapIndex] = null;
            noteTypes[tapIndex] = NoteType.Unknown;
            noteHandles[tapIndex] = NoteHandleType.Unknown;
            isPoweredNotes[tapIndex] = false;
            return true;
        }

        int airIndex = FindNoteGroup(airNoteObjectGroups, noteObject);

        if (airIndex >= 0)
        {
            line = airIndex + 1;
            noteType = NoteType.Air;
            noteObjects = airNoteObjectGroups[airIndex];
            handleType = airIndex < MainLineCount / 2
                ? NoteHandleType.Left
                : NoteHandleType.Right;
            isPowered = false;
            airValue = airNoteValues[airIndex];
            scratchMotion = null;
            airNoteObjectGroups[airIndex] = null;
            airNoteValues[airIndex] = 0;
            return true;
        }

        int scratchIndex = FindNoteGroup(
            scratchNoteObjectGroups,
            noteObject);

        if (scratchIndex >= 0)
        {
            int storageIndex = MainLineCount + scratchIndex;
            line = scratchIndex == 0 ? -1 : -2;
            noteType = noteTypes[storageIndex];
            noteObjects = scratchNoteObjectGroups[scratchIndex];
            handleType = NoteHandleType.Unknown;
            isPowered = isPoweredNotes[storageIndex];
            airValue = 0;
            scratchMotion = scratchMotions[scratchIndex]?.Clone() ??
                ScratchMotionData.CreateDefault(noteType);
            scratchNoteObjectGroups[scratchIndex] = null;
            noteTypes[storageIndex] = NoteType.Unknown;
            isPoweredNotes[storageIndex] = false;
            scratchMotions[scratchIndex] = null;
            scratchPointTypes[scratchIndex] = ScratchPointType.Tap;
            return true;
        }

        line = 0;
        noteType = NoteType.Unknown;
        noteObjects = null;
        handleType = NoteHandleType.Unknown;
        isPowered = false;
        airValue = 0;
        scratchMotion = null;
        return false;
    }

    /// <summary>이 홀더가 관리하는 런타임 노트 오브젝트를 모두 제거합니다.</summary>
    public void DestroyAllNoteObjects()
    {
        EnsureStorage();

        for (int i = 0; i < tapNoteObjectGroups.Length; i++)
        {
            DestroyNoteObjects(tapNoteObjectGroups[i]);
            tapNoteObjectGroups[i] = null;
        }

        for (int i = 0; i < scratchNoteObjectGroups.Length; i++)
        {
            DestroyNoteObjects(scratchNoteObjectGroups[i]);
            scratchNoteObjectGroups[i] = null;
        }

        for (int i = 0; i < airNoteObjectGroups.Length; i++)
        {
            DestroyNoteObjects(airNoteObjectGroups[i]);
            airNoteObjectGroups[i] = null;
        }

        DestroyNoteObjects(cameraNoteObjects);
        cameraNoteObjects = null;
        DestroyNoteObjects(speedNoteObjects);
        speedNoteObjects = null;
        DestroyNoteObjects(markerNoteObjects);
        markerNoteObjects = null;
        DestroyNoteObjects(effectNoteObjects);
        effectNoteObjects = null;
    }

    /// <summary>파일에서 먼저 복원한 노트 데이터에 표시 오브젝트를 연결합니다.</summary>
    public bool AttachNoteObjects(int line, GameObject[] noteObjects)
    {
        EnsureStorage();
        int index = GetLineIndex(line);

        if (noteTypes[index] == NoteType.Unknown ||
            noteObjects == null ||
            noteObjects.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            if (!noteObjects[i])
            {
                return false;
            }
        }

        GameObject[][] groups = index < MainLineCount
            ? tapNoteObjectGroups
            : scratchNoteObjectGroups;
        int groupIndex = index < MainLineCount
            ? index
            : index - MainLineCount;

        if (groups[groupIndex] != null)
        {
            return false;
        }

        groups[groupIndex] = noteObjects;

        if (index >= MainLineCount)
        {
            RefreshScratchArrowPattern(groupIndex);
        }

        return true;
    }

    public bool AttachAirNoteObjects(int line, GameObject[] noteObjects)
    {
        EnsureStorage();
        int index = GetMainLineIndex(line);

        if (airNoteValues[index] <= 0 ||
            noteObjects == null ||
            noteObjects.Length == 0 ||
            airNoteObjectGroups[index] != null)
        {
            return false;
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            if (!noteObjects[i])
            {
                return false;
            }
        }

        airNoteObjectGroups[index] = noteObjects;
        return true;
    }

    public bool AttachCameraNoteObjects(GameObject[] noteObjects)
    {
        EnsureStorage();

        if (!isCameraMove || cameraNoteObjects != null ||
            !IsValidNoteObjects(noteObjects))
        {
            return false;
        }

        cameraNoteObjects = noteObjects;
        return true;
    }

    public bool AttachMarkerNoteObjects(GameObject[] noteObjects)
    {
        EnsureStorage();

        if (!isMarker || markerNoteObjects != null ||
            !IsValidNoteObjects(noteObjects))
        {
            return false;
        }

        markerNoteObjects = noteObjects;
        return true;
    }

    private void DeleteTapNote(int index)
    {
        DestroyNoteObjects(tapNoteObjectGroups[index]);
        tapNoteObjectGroups[index] = null;
        noteTypes[index] = NoteType.Unknown;
        noteHandles[index] = NoteHandleType.Unknown;
        isPoweredNotes[index] = false;
    }

    private void DeleteScratchNote(int index)
    {
        DestroyNoteObjects(scratchNoteObjectGroups[index]);
        scratchNoteObjectGroups[index] = null;
        noteTypes[index + MainLineCount] = NoteType.Unknown;
        isPoweredNotes[index + MainLineCount] = false;
        scratchMotions[index] = null;
        scratchPointTypes[index] = ScratchPointType.Tap;
    }

    private void DeleteAirNote(int index)
    {
        DestroyNoteObjects(airNoteObjectGroups[index]);
        airNoteObjectGroups[index] = null;
        airNoteValues[index] = 0;
    }

    private void DeleteCameraNote()
    {
        DestroyNoteObjects(cameraNoteObjects);
        cameraNoteObjects = null;
        isCameraMove = false;
        cameraOffsetX = 0f;
        cameraSpinDirection = ChartCameraSpinDirection.None;
    }

    private void DeleteMarkerNote()
    {
        DestroyNoteObjects(markerNoteObjects);
        markerNoteObjects = null;
        isMarker = false;
    }

    private static bool FindNoteObject(
        GameObject[] noteObjects,
        GameObject noteObject)
    {
        return noteObjects != null &&
            Array.IndexOf(noteObjects, noteObject) >= 0;
    }

    private static bool IsValidNoteObjects(GameObject[] noteObjects)
    {
        if (noteObjects == null || noteObjects.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            if (!noteObjects[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int FindNoteGroup(
        GameObject[][] noteObjectGroups,
        GameObject noteObject)
    {
        for (int i = 0; i < noteObjectGroups.Length; i++)
        {
            GameObject[] noteObjects = noteObjectGroups[i];

            if (noteObjects != null && Array.IndexOf(noteObjects, noteObject) >= 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetLineIndex(int line)
    {
        return line switch
        {
            1 => 0,
            2 => 1,
            3 => 2,
            4 => 3,
            -1 => 4,
            -2 => 5,
            _ => throw new ArgumentOutOfRangeException(
                nameof(line),
                line,
                "Chart line must be 1-4, -1, or -2.")
        };
    }

    private static int GetMainLineIndex(int line)
    {
        if (line < 1 || line > MainLineCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(line),
                line,
                "Air note line must be between 1 and 4.");
        }

        return line - 1;
    }

    private static void DestroyNoteObject(GameObject noteObject)
    {
        if (noteObject)
        {
            UnityEngine.Object.Destroy(noteObject);
        }
    }

    private static void DestroyNoteObjects(GameObject[] noteObjects)
    {
        if (noteObjects == null)
        {
            return;
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            DestroyNoteObject(noteObjects[i]);
        }
    }

    private void RefreshScratchArrowPattern(int scratchIndex)
    {
        GameObject[] noteObjects = scratchNoteObjectGroups[scratchIndex];

        if (noteObjects == null)
        {
            return;
        }

        ScratchMotionData motion = scratchMotions[scratchIndex] ??
            ScratchMotionData.CreateDefault(
                noteTypes[MainLineCount + scratchIndex]);
        NoteHandleType side = scratchIndex == 0
            ? NoteHandleType.Left
            : NoteHandleType.Right;

        for (int i = 0; i < noteObjects.Length; i++)
        {
            GameObject noteObject = noteObjects[i];

            if (noteObject &&
                noteObject.TryGetComponent(
                    out ScratchArrowPattern arrowPattern))
            {
                arrowPattern.Configure(side, motion.MotionType);
            }
        }
    }

    internal void EnsureStorage()
    {
        // JsonUtility can bypass constructors, so every public operation repairs
        // serialized arrays before indexing them.
        Resize(ref noteHandles, MainLineCount);
        Resize(ref noteTypes, TotalLineCount);
        Resize(ref isPoweredNotes, TotalLineCount);
        Resize(ref scratchMotions, ScratchLineCount);
        Resize(ref scratchPointTypes, ScratchLineCount);
        Resize(ref airNoteValues, AirNoteCount);
        Resize(ref tapNoteObjectGroups, MainLineCount);
        Resize(ref scratchNoteObjectGroups, ScratchLineCount);
        Resize(ref airNoteObjectGroups, AirNoteCount);
        Resize(ref actionNoteObjects, MainLineCount);

        for (int mainIndex = 0; mainIndex < MainLineCount; mainIndex++)
        {
            isPoweredNotes[mainIndex] = false;
        }

        for (int scratchIndex = 0;
             scratchIndex < ScratchLineCount;
             scratchIndex++)
        {
            NoteType noteType = noteTypes[MainLineCount + scratchIndex];

            if (noteType.IsScratch())
            {
                ScratchMotionData motion =
                    scratchMotions[scratchIndex] ??
                    ScratchMotionData.CreateDefault(noteType);
                scratchMotions[scratchIndex] =
                    ScratchMotionRules.NormalizeMotion(
                        noteType,
                        scratchPointTypes[scratchIndex],
                        motion);
                isPoweredNotes[MainLineCount + scratchIndex] =
                    scratchMotions[scratchIndex].MotionType !=
                    ScratchMotionType.None;
            }
            else
            {
                scratchMotions[scratchIndex] = null;
                scratchPointTypes[scratchIndex] = ScratchPointType.Tap;
            }
        }
    }

    /// <summary>런타임 오브젝트 참조를 제외한 채보 데이터 복사본을 만듭니다.</summary>
    internal ChartHolder CloneData()
    {
        EnsureStorage();
        ChartHolder clone = new ChartHolder(ChartNumber, ChartPos)
        {
            targetBpm = targetBpm,
            hasLineSpeedChange = hasLineSpeedChange,
            targetLineSpeed = targetLineSpeed,
            isEffect = isEffect,
            effectId = effectId,
            effectTypeId = effectTypeId,
            effectCommandId = effectCommandId,
            effectOrder = effectOrder,
            effectParametersJson = effectParametersJson,
            isMarker = isMarker,
            isCameraMove = isCameraMove,
            cameraOffsetX = cameraOffsetX,
            cameraSpinDirection = cameraSpinDirection
        };

        Array.Copy(noteHandles, clone.noteHandles, MainLineCount);
        Array.Copy(noteTypes, clone.noteTypes, TotalLineCount);
        Array.Copy(isPoweredNotes, clone.isPoweredNotes, TotalLineCount);
        Array.Copy(airNoteValues, clone.airNoteValues, AirNoteCount);

        for (int i = 0; i < ScratchLineCount; i++)
        {
            clone.scratchMotions[i] = scratchMotions[i]?.Clone();
        }

        Array.Copy(
            scratchPointTypes,
            clone.scratchPointTypes,
            ScratchLineCount);

        return clone;
    }

    private static void Resize<T>(ref T[] array, int length)
    {
        if (array == null || array.Length != length)
        {
            Array.Resize(ref array, length);
        }
    }
}

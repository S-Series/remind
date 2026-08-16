using System;
using UnityEngine;
using REmind.Data;

[Serializable]
public class ChartHolder
{
    public const int MainLineCount = 4;
    public const int ScratchLineCount = 2;
    public const int TotalLineCount = MainLineCount + ScratchLineCount;
    public const int AirNoteCount = 4;
    public const int PositionUnitsPerMeasure = 1600;
    public const float PositionUnitsPerWorldUnit = 10f;

    public int ChartNumber;
    public int ChartPos;
    // Main line handles correspond to lines 1-4.
    public NoteHandleType[] noteHandles;
    // 0-3: main lines 1-4, 4-5: left/right scratch lines.
    public NoteType[] noteTypes;
    public bool[] isPoweredNotes;
    // Scratch motion data corresponds to left/right scratch lines.
    public ScratchMotionData[] scratchMotions;
    // Air note values correspond to main lines 1-4 and range from 00 to 99.
    public int[] airNoteValues;
    public float targetBpm = -1f; // -1 means that the BPM does not change.
    public bool isEffect; // Reserved for chart effects.
    public bool isCameraMove; // Reserved for camera events.

    [NonSerialized] public GameObject[][] tapNoteObjectGroups;
    [NonSerialized] public GameObject[][] scratchNoteObjectGroups;
    [NonSerialized] public GameObject[][] airNoteObjectGroups;
    [NonSerialized] public GameObject[] actionNoteObjects;

    public int AbsoluteChartPosition =>
        checked(ChartNumber * PositionUnitsPerMeasure + ChartPos);
    public float WorldY =>
        AbsoluteChartPosition / PositionUnitsPerWorldUnit;
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

            return targetBpm != -1f || isEffect || isCameraMove;
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
        ScratchMotionData scratchMotion = null)
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

        if (isScratchLine)
        {
            storedScratchMotion =
                (scratchMotion ??
                    ScratchMotionData.CreateDefault(noteType)).Clone();

            if (noteType == NoteType.Scratch &&
                storedScratchMotion.MotionType != ScratchMotionType.Instant)
            {
                throw new ArgumentException(
                    "Single Scratch notes must use Instant motion.",
                    nameof(scratchMotion));
            }
        }

        noteTypes[index] = noteType;
        isPoweredNotes[index] = isPowered;

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
        return FindNoteGroup(tapNoteObjectGroups, noteObject) >= 0 ||
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
        int tapIndex = FindNoteGroup(tapNoteObjectGroups, noteObject);

        if (tapIndex >= 0)
        {
            line = tapIndex + 1;
            noteType = noteTypes[tapIndex];
            handleType = noteHandles[tapIndex];
            isPowered = isPoweredNotes[tapIndex];
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

    /// <summary>표현 오브젝트가 가리키는 Tap/Scratch 데이터의 Powered 값을 변경합니다.</summary>
    internal bool TrySetPowered(
        GameObject noteObject,
        bool isPowered,
        out NoteType noteType)
    {
        EnsureStorage();
        int tapIndex = FindNoteGroup(tapNoteObjectGroups, noteObject);

        if (tapIndex >= 0)
        {
            noteType = noteTypes[tapIndex];
            isPoweredNotes[tapIndex] = isPowered;
            return noteType != NoteType.Unknown;
        }

        int scratchIndex = FindNoteGroup(
            scratchNoteObjectGroups,
            noteObject);

        if (scratchIndex >= 0)
        {
            int storageIndex = MainLineCount + scratchIndex;
            noteType = noteTypes[storageIndex];
            isPoweredNotes[storageIndex] = isPowered;
            return noteType != NoteType.Unknown;
        }

        noteType = NoteType.Unknown;
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
            isPowered = isPoweredNotes[tapIndex];
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
    }

    private void DeleteAirNote(int index)
    {
        DestroyNoteObjects(airNoteObjectGroups[index]);
        airNoteObjectGroups[index] = null;
        airNoteValues[index] = 0;
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

    internal void EnsureStorage()
    {
        // JsonUtility can bypass constructors, so every public operation repairs
        // serialized arrays before indexing them.
        Resize(ref noteHandles, MainLineCount);
        Resize(ref noteTypes, TotalLineCount);
        Resize(ref isPoweredNotes, TotalLineCount);
        Resize(ref scratchMotions, ScratchLineCount);
        Resize(ref airNoteValues, AirNoteCount);
        Resize(ref tapNoteObjectGroups, MainLineCount);
        Resize(ref scratchNoteObjectGroups, ScratchLineCount);
        Resize(ref airNoteObjectGroups, AirNoteCount);
        Resize(ref actionNoteObjects, MainLineCount);

        for (int scratchIndex = 0;
             scratchIndex < ScratchLineCount;
             scratchIndex++)
        {
            NoteType noteType = noteTypes[MainLineCount + scratchIndex];

            if (noteType.IsScratch() && scratchMotions[scratchIndex] == null)
            {
                scratchMotions[scratchIndex] =
                    ScratchMotionData.CreateDefault(noteType);
            }
            else if (!noteType.IsScratch())
            {
                scratchMotions[scratchIndex] = null;
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
            isEffect = isEffect,
            isCameraMove = isCameraMove
        };

        Array.Copy(noteHandles, clone.noteHandles, MainLineCount);
        Array.Copy(noteTypes, clone.noteTypes, TotalLineCount);
        Array.Copy(isPoweredNotes, clone.isPoweredNotes, TotalLineCount);
        Array.Copy(airNoteValues, clone.airNoteValues, AirNoteCount);

        for (int i = 0; i < ScratchLineCount; i++)
        {
            clone.scratchMotions[i] = scratchMotions[i]?.Clone();
        }

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

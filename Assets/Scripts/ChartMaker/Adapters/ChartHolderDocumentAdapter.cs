using System;
using System.Collections.Generic;
using REmind.Charting;
using REmind.Data;
using UnityEngine;

/// <summary>
/// 현재 ChartHolder 저장 구조를 Unity 비의존 ChartDocument로 투영합니다.
/// 이 어댑터만 ChartHolder 배열과 표시 GameObject의 배치를 알고 있습니다.
/// </summary>
public static class ChartHolderDocumentAdapter
{
    public static ChartHolderDocumentBuildResult Build(
        IReadOnlyList<ChartHolder> holders,
        double baseBpm,
        int beatsPerMeasure)
    {
        ChartDocument document = new ChartDocument(
            ChartHolder.PositionUnitsPerMeasure,
            beatsPerMeasure,
            baseBpm);
        Dictionary<string, ChartNoteViewBinding> viewBindings =
            new Dictionary<string, ChartNoteViewBinding>(
                StringComparer.Ordinal);
        List<CompileIssue> issues = new List<CompileIssue>();
        PendingLongNote[] pendingMain =
            new PendingLongNote[ChartHolder.MainLineCount];
        PendingLongNote[] pendingScratch =
            new PendingLongNote[ChartHolder.ScratchLineCount];

        if (holders == null)
        {
            issues.Add(new CompileIssue(
                "ADAPTER_HOLDERS_NULL",
                "ChartHolder collection is missing."));
            return new ChartHolderDocumentBuildResult(
                document,
                viewBindings,
                issues);
        }

        for (int holderIndex = 0; holderIndex < holders.Count; holderIndex++)
        {
            ChartHolder holder = holders[holderIndex];

            if (holder == null)
            {
                issues.Add(new CompileIssue(
                    "ADAPTER_HOLDER_NULL",
                    $"ChartHolder {holderIndex} is missing."));
                continue;
            }

            holder.EnsureStorage();
            int position = holder.AbsoluteChartPosition;
            AddTimingEvent(holder, position, document, issues);
            AddLineSpeedEvent(holder, position, document, issues);
            AddCameraEvent(holder, position, document, issues);
            if (holder.isEffect)
            {
                document.EffectEvents.Add(new ChartEffectEvent(
                    position, holder.effectId, holder.effectTypeId,
                    holder.effectCommandId, holder.effectOrder));
            }
            AddMainNotes(
                holder,
                position,
                document,
                viewBindings,
                pendingMain,
                issues);
            AddAirNotes(holder, position, document, viewBindings);
            AddScratchNotes(
                holder,
                position,
                document,
                viewBindings,
                pendingScratch,
                issues);
        }

        AddUnclosedIssues(pendingMain, "Main Long Note", issues);
        AddUnclosedIssues(pendingScratch, "Long Scratch", issues);

        return new ChartHolderDocumentBuildResult(
            document,
            viewBindings,
            issues);
    }

    private static void AddTimingEvent(
        ChartHolder holder,
        int position,
        ChartDocument document,
        List<CompileIssue> issues)
    {
        if (holder.targetBpm == -1f)
        {
            return;
        }

        if (float.IsNaN(holder.targetBpm) ||
            float.IsInfinity(holder.targetBpm) ||
            holder.targetBpm <= 0f)
        {
            issues.Add(new CompileIssue(
                "ADAPTER_TIMING_BPM",
                $"BPM event at position {position} must be positive and " +
                "finite."));
            return;
        }

        document.TimingEvents.Add(new ChartTimingEvent(
            position,
            holder.targetBpm));
    }

    private static void AddLineSpeedEvent(
        ChartHolder holder,
        int position,
        ChartDocument document,
        List<CompileIssue> issues)
    {
        if (!holder.hasLineSpeedChange)
        {
            return;
        }

        if (float.IsNaN(holder.targetLineSpeed) ||
            float.IsInfinity(holder.targetLineSpeed) ||
            holder.targetLineSpeed <= 0f)
        {
            issues.Add(new CompileIssue(
                "ADAPTER_LINE_SPEED",
                $"Line Speed event at position {position} must be positive " +
                "and finite."));
            return;
        }

        document.LineSpeedEvents.Add(new ChartLineSpeedEvent(
            position,
            holder.targetLineSpeed));
    }

    private static void AddCameraEvent(
        ChartHolder holder,
        int position,
        ChartDocument document,
        List<CompileIssue> issues)
    {
        if (!holder.isCameraMove)
        {
            return;
        }

        if (float.IsNaN(holder.cameraOffsetX) ||
            float.IsInfinity(holder.cameraOffsetX))
        {
            issues.Add(new CompileIssue(
                "ADAPTER_CAMERA_OFFSET",
                $"Camera event at position {position} requires a finite " +
                "offset X."));
            return;
        }

        if (!Enum.IsDefined(
                typeof(ChartCameraSpinDirection),
                holder.cameraSpinDirection))
        {
            issues.Add(new CompileIssue(
                "ADAPTER_CAMERA_SPIN",
                $"Camera event at position {position} has an unsupported " +
                $"spin direction: {holder.cameraSpinDirection}."));
            return;
        }

        document.CameraEvents.Add(new ChartCameraEvent(
            position,
            holder.cameraOffsetX,
            holder.cameraSpinDirection));
    }

    private static void AddMainNotes(
        ChartHolder holder,
        int position,
        ChartDocument document,
        Dictionary<string, ChartNoteViewBinding> viewBindings,
        PendingLongNote[] pendingMain,
        List<CompileIssue> issues)
    {
        for (int laneIndex = 0;
             laneIndex < ChartHolder.MainLineCount;
             laneIndex++)
        {
            NoteType noteType = holder.noteTypes[laneIndex];

            switch (noteType)
            {
                case NoteType.Unknown:
                    break;
                case NoteType.Tap:
                    AddSingleNote(
                        document,
                        viewBindings,
                        ChartNoteKind.Tap,
                        laneIndex,
                        position,
                        holder.tapNoteObjectGroups[laneIndex],
                        "main");
                    break;
                case NoteType.LongTap:
                    PendingLongNote pending = pendingMain[laneIndex];

                    if (pending == null)
                    {
                        pendingMain[laneIndex] = new PendingLongNote(
                            laneIndex,
                            position,
                            holder.tapNoteObjectGroups[laneIndex]);
                    }
                    else
                    {
                        AddLongNote(
                            document,
                            viewBindings,
                            ChartNoteKind.Hold,
                            pending,
                            position,
                            holder.tapNoteObjectGroups[laneIndex],
                            "hold");
                        pendingMain[laneIndex] = null;
                    }

                    break;
                default:
                    issues.Add(new CompileIssue(
                        "ADAPTER_MAIN_NOTE_KIND",
                        $"Unsupported {noteType} data exists in Main lane " +
                        $"{laneIndex + 1} at position {position}."));
                    break;
            }
        }
    }

    private static void AddAirNotes(
        ChartHolder holder,
        int position,
        ChartDocument document,
        Dictionary<string, ChartNoteViewBinding> viewBindings)
    {
        for (int airIndex = 0;
             airIndex < ChartHolder.AirNoteCount;
             airIndex++)
        {
            if (holder.airNoteValues[airIndex] <= 0)
            {
                continue;
            }

            AddSingleNote(
                document,
                viewBindings,
                ChartNoteKind.Air,
                (int)ChartLane.AirMain1 + airIndex,
                position,
                holder.airNoteObjectGroups[airIndex],
                "air");
        }
    }

    private static void AddScratchNotes(
        ChartHolder holder,
        int position,
        ChartDocument document,
        Dictionary<string, ChartNoteViewBinding> viewBindings,
        PendingLongNote[] pendingScratch,
        List<CompileIssue> issues)
    {
        for (int scratchIndex = 0;
             scratchIndex < ChartHolder.ScratchLineCount;
             scratchIndex++)
        {
            int storageIndex =
                ChartHolder.MainLineCount + scratchIndex;
            NoteType noteType = holder.noteTypes[storageIndex];
            int lane = (int)ChartLane.GroundLeft + scratchIndex;
            GameObject[] noteObjects =
                holder.scratchNoteObjectGroups[scratchIndex];

            if (noteType == NoteType.Unknown)
            {
                continue;
            }

            if (noteType == NoteType.Scratch)
            {
                AddScratchCameraTiltEvent(
                    holder,
                    scratchIndex,
                    noteType,
                    ScratchPointType.Tap,
                    position,
                    lane,
                    document);
                AddSingleNote(
                    document,
                    viewBindings,
                    ChartNoteKind.Scratch,
                    lane,
                    position,
                    noteObjects,
                    "scratch");
                continue;
            }

            if (noteType != NoteType.LongScratch)
            {
                issues.Add(new CompileIssue(
                    "ADAPTER_SCRATCH_NOTE_KIND",
                    $"Unsupported {noteType} data exists in Ground Side " +
                    $"lane {scratchIndex + 1} at position {position}."));
                continue;
            }

            ScratchPointType pointType =
                holder.scratchPointTypes[scratchIndex];
            AddScratchCameraTiltEvent(
                holder,
                scratchIndex,
                noteType,
                pointType,
                position,
                lane,
                document);
            PendingLongNote pending = pendingScratch[scratchIndex];

            switch (pointType)
            {
                case ScratchPointType.Start:
                    if (pending != null)
                    {
                        issues.Add(new CompileIssue(
                            "ADAPTER_SCRATCH_NESTED_START",
                            $"Long Scratch in lane {lane} starts again at " +
                            $"position {position} before the previous segment " +
                            "ended."));
                    }

                    pendingScratch[scratchIndex] = new PendingLongNote(
                        lane,
                        position,
                        noteObjects);
                    break;
                case ScratchPointType.Mid:
                    if (pending == null)
                    {
                        issues.Add(new CompileIssue(
                            "ADAPTER_SCRATCH_MID_WITHOUT_START",
                            $"Long Scratch Mid in lane {lane} at position " +
                            $"{position} has no Start."));
                    }
                    else
                    {
                        pending.Append(position, noteObjects);
                    }

                    break;
                case ScratchPointType.End:
                    if (pending == null)
                    {
                        issues.Add(new CompileIssue(
                            "ADAPTER_SCRATCH_END_WITHOUT_START",
                            $"Long Scratch End in lane {lane} at position " +
                            $"{position} has no Start."));
                        break;
                    }

                    AddLongNote(
                        document,
                        viewBindings,
                        ChartNoteKind.LongScratch,
                        pending,
                        position,
                        noteObjects,
                        "long-scratch");
                    pendingScratch[scratchIndex] = null;
                    break;
                default:
                    issues.Add(new CompileIssue(
                        "ADAPTER_SCRATCH_POINT_KIND",
                        $"Long Scratch in lane {lane} at position {position} " +
                        "has an invalid point kind."));
                    break;
            }
        }
    }

    private static void AddScratchCameraTiltEvent(
        ChartHolder holder,
        int scratchIndex,
        NoteType noteType,
        ScratchPointType pointType,
        int position,
        int lane,
        ChartDocument document)
    {
        ScratchMotionData motion = ScratchMotionRules.NormalizeMotion(
            noteType,
            pointType,
            holder.scratchMotions[scratchIndex]);

        if (noteType == NoteType.Scratch)
        {
            if (motion.MotionType == ScratchMotionType.Instant &&
                motion.MoveAmount > 0)
            {
                document.ScratchCameraTiltEvents.Add(
                    new ChartScratchCameraTiltEvent(
                        position,
                        (ChartLane)lane,
                        ChartScratchCameraTiltEventType.Instant));
            }

            return;
        }

        ChartScratchCameraTiltEventType eventType;

        if (motion.MotionType == ScratchMotionType.Release &&
            motion.MoveAmount > 0)
        {
            eventType = ChartScratchCameraTiltEventType.ReverseInstant;
        }
        else if (pointType == ScratchPointType.End ||
                 motion.MotionType == ScratchMotionType.None ||
                 motion.MoveAmount <= 0)
        {
            eventType = ChartScratchCameraTiltEventType.Release;
        }
        else
        {
            eventType = motion.MotionType == ScratchMotionType.Gradual
                ? ChartScratchCameraTiltEventType.Gradual
                : ChartScratchCameraTiltEventType.Instant;
        }
        document.ScratchCameraTiltEvents.Add(
            new ChartScratchCameraTiltEvent(
                position,
                (ChartLane)lane,
                eventType));
    }

    private static void AddSingleNote(
        ChartDocument document,
        Dictionary<string, ChartNoteViewBinding> viewBindings,
        ChartNoteKind kind,
        int lane,
        int position,
        GameObject[] noteObjects,
        string idPrefix)
    {
        string id = CreateNoteId(idPrefix, lane, position);
        document.Notes.Add(new ChartDocumentNote(
            id,
            kind,
            lane,
            position));
        viewBindings.Add(
            id,
            new ChartNoteViewBinding(
                noteObjects,
                noteObjects,
                new[]
                {
                    new ChartNoteViewPointBinding(position, noteObjects)
                }));
    }

    private static void AddLongNote(
        ChartDocument document,
        Dictionary<string, ChartNoteViewBinding> viewBindings,
        ChartNoteKind kind,
        PendingLongNote pending,
        int endPosition,
        GameObject[] endObjects,
        string idPrefix)
    {
        string id = CreateNoteId(
            idPrefix,
            pending.Lane,
            pending.StartPosition);
        document.Notes.Add(new ChartDocumentNote(
            id,
            kind,
            pending.Lane,
            pending.StartPosition,
            endPosition));
        viewBindings.Add(
            id,
            new ChartNoteViewBinding(
                pending.StartObjects,
                CombineObjects(pending.AllObjects, endObjects),
                AppendPoint(
                    pending.Points,
                    new ChartNoteViewPointBinding(
                        endPosition,
                        endObjects))));
    }

    private static void AddUnclosedIssues(
        IReadOnlyList<PendingLongNote> pendingNotes,
        string displayName,
        List<CompileIssue> issues)
    {
        for (int i = 0; i < pendingNotes.Count; i++)
        {
            PendingLongNote pending = pendingNotes[i];

            if (pending == null)
            {
                continue;
            }

            issues.Add(new CompileIssue(
                "ADAPTER_LONG_UNCLOSED",
                $"{displayName} in lane {pending.Lane} at position " +
                $"{pending.StartPosition} has no End."));
        }
    }

    private static string CreateNoteId(
        string prefix,
        int lane,
        int position)
    {
        return $"{prefix}-{lane:D2}-{position:D7}";
    }

    private static GameObject[] CombineObjects(
        GameObject[] first,
        GameObject[] second)
    {
        int firstCount = first?.Length ?? 0;
        int secondCount = second?.Length ?? 0;
        GameObject[] combined =
            new GameObject[firstCount + secondCount];

        if (firstCount > 0)
        {
            Array.Copy(first, 0, combined, 0, firstCount);
        }

        if (secondCount > 0)
        {
            Array.Copy(second, 0, combined, firstCount, secondCount);
        }

        return combined;
    }

    private static ChartNoteViewPointBinding[] AppendPoint(
        IReadOnlyList<ChartNoteViewPointBinding> points,
        ChartNoteViewPointBinding point)
    {
        ChartNoteViewPointBinding[] result =
            new ChartNoteViewPointBinding[points.Count + 1];

        for (int i = 0; i < points.Count; i++)
        {
            result[i] = points[i];
        }

        result[result.Length - 1] = point;
        return result;
    }

    private sealed class PendingLongNote
    {
        public PendingLongNote(
            int lane,
            int startPosition,
            GameObject[] startObjects)
        {
            Lane = lane;
            StartPosition = startPosition;
            StartObjects = CloneObjects(startObjects);
            AllObjects = CloneObjects(startObjects);
            Points = new List<ChartNoteViewPointBinding>
            {
                new ChartNoteViewPointBinding(
                    startPosition,
                    startObjects)
            };
        }

        public int Lane { get; }
        public int StartPosition { get; }
        public GameObject[] StartObjects { get; }
        public GameObject[] AllObjects { get; private set; }
        public List<ChartNoteViewPointBinding> Points { get; }

        public void Append(int position, GameObject[] noteObjects)
        {
            AllObjects = CombineObjects(AllObjects, noteObjects);
            Points.Add(new ChartNoteViewPointBinding(
                position,
                noteObjects));
        }
    }

    private static GameObject[] CloneObjects(GameObject[] noteObjects)
    {
        return noteObjects != null
            ? (GameObject[])noteObjects.Clone()
            : Array.Empty<GameObject>();
    }
}

public sealed class ChartHolderDocumentBuildResult
{
    internal ChartHolderDocumentBuildResult(
        ChartDocument document,
        Dictionary<string, ChartNoteViewBinding> viewBindings,
        List<CompileIssue> issues)
    {
        Document = document;
        ViewBindings = viewBindings;
        Issues = issues.AsReadOnly();
    }

    public ChartDocument Document { get; }
    public IReadOnlyDictionary<string, ChartNoteViewBinding> ViewBindings
    {
        get;
    }
    public IReadOnlyList<CompileIssue> Issues { get; }

    public bool Succeeded
    {
        get
        {
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Severity == CompileIssueSeverity.Error)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

public sealed class ChartNoteViewBinding
{
    internal ChartNoteViewBinding(
        GameObject[] startObjects,
        GameObject[] allObjects,
        IReadOnlyList<ChartNoteViewPointBinding> points = null)
    {
        StartObjects = startObjects != null
            ? (GameObject[])startObjects.Clone()
            : Array.Empty<GameObject>();
        AllObjects = allObjects != null
            ? (GameObject[])allObjects.Clone()
            : Array.Empty<GameObject>();
        Points = points != null
            ? Array.AsReadOnly(CopyPoints(points))
            : Array.AsReadOnly(new[]
            {
                new ChartNoteViewPointBinding(0, startObjects)
            });
    }

    public GameObject[] StartObjects { get; }
    public GameObject[] AllObjects { get; }
    public IReadOnlyList<ChartNoteViewPointBinding> Points { get; }

    private static ChartNoteViewPointBinding[] CopyPoints(
        IReadOnlyList<ChartNoteViewPointBinding> source)
    {
        ChartNoteViewPointBinding[] result =
            new ChartNoteViewPointBinding[source.Count];

        for (int i = 0; i < source.Count; i++)
        {
            result[i] = source[i];
        }

        return result;
    }
}

public sealed class ChartNoteViewPointBinding
{
    internal ChartNoteViewPointBinding(
        int position,
        GameObject[] noteObjects)
    {
        Position = position;
        NoteObjects = noteObjects != null
            ? (GameObject[])noteObjects.Clone()
            : Array.Empty<GameObject>();
    }

    public int Position { get; }
    public GameObject[] NoteObjects { get; }
}

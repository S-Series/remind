using System;
using System.Collections.Generic;
using REmind.Data;

/// <summary>
/// 저장할 수 없는 미완성 Long Note만 단일 노트로 바꾸는 정규화 계획을 만듭니다.
/// 정상적으로 닫힌 Long Note 데이터는 변경하지 않습니다.
/// </summary>
internal static class ChartLongNoteSaveNormalizer
{
    public static ChartLongNoteSaveNormalizationPlan CreatePlan(
        IReadOnlyList<ChartHolder> holders)
    {
        List<ChartHolder> ordered = CreateOrderedHolders(holders);
        List<LongNoteNormalizationAction> actions =
            new List<LongNoteNormalizationAction>();
        CollectUnpairedLongTaps(ordered, actions);
        CollectInvalidLongScratches(ordered, actions);
        return new ChartLongNoteSaveNormalizationPlan(actions);
    }

    private static List<ChartHolder> CreateOrderedHolders(
        IReadOnlyList<ChartHolder> holders)
    {
        List<ChartHolder> ordered = new List<ChartHolder>();

        if (holders == null)
        {
            return ordered;
        }

        for (int i = 0; i < holders.Count; i++)
        {
            ChartHolder holder = holders[i];

            if (holder == null)
            {
                continue;
            }

            holder.EnsureStorage();
            ordered.Add(holder);
        }

        ordered.Sort(
            (left, right) => left.AbsoluteChartPosition.CompareTo(
                right.AbsoluteChartPosition));
        return ordered;
    }

    private static void CollectUnpairedLongTaps(
        IReadOnlyList<ChartHolder> holders,
        List<LongNoteNormalizationAction> actions)
    {
        for (int laneIndex = 0;
             laneIndex < ChartHolder.MainLineCount;
             laneIndex++)
        {
            ChartHolder pending = null;

            for (int holderIndex = 0;
                 holderIndex < holders.Count;
                 holderIndex++)
            {
                ChartHolder holder = holders[holderIndex];

                if (holder.noteTypes[laneIndex] != NoteType.LongTap)
                {
                    continue;
                }

                pending = pending == null ? holder : null;
            }

            if (pending != null)
            {
                actions.Add(LongNoteNormalizationAction.ForLongTap(
                    pending,
                    laneIndex));
            }
        }
    }

    private static void CollectInvalidLongScratches(
        IReadOnlyList<ChartHolder> holders,
        List<LongNoteNormalizationAction> actions)
    {
        for (int scratchIndex = 0;
             scratchIndex < ChartHolder.ScratchLineCount;
             scratchIndex++)
        {
            List<LongNoteNormalizationAction> openSegment = null;
            int storageIndex = ChartHolder.MainLineCount + scratchIndex;

            for (int holderIndex = 0;
                 holderIndex < holders.Count;
                 holderIndex++)
            {
                ChartHolder holder = holders[holderIndex];

                if (holder.noteTypes[storageIndex] != NoteType.LongScratch)
                {
                    continue;
                }

                LongNoteNormalizationAction current =
                    LongNoteNormalizationAction.ForLongScratch(
                        holder,
                        storageIndex,
                        scratchIndex);

                switch (holder.scratchPointTypes[scratchIndex])
                {
                    case ScratchPointType.Start:
                        if (openSegment != null)
                        {
                            actions.AddRange(openSegment);
                        }

                        openSegment = new List<LongNoteNormalizationAction>
                        {
                            current
                        };
                        break;
                    case ScratchPointType.Mid:
                        if (openSegment == null)
                        {
                            actions.Add(current);
                        }
                        else
                        {
                            openSegment.Add(current);
                        }

                        break;
                    case ScratchPointType.End:
                        if (openSegment == null)
                        {
                            actions.Add(current);
                        }
                        else
                        {
                            // Start ... End가 완성됐으므로 이 구간은 유지합니다.
                            openSegment = null;
                        }

                        break;
                    default:
                        actions.Add(current);
                        break;
                }
            }

            if (openSegment != null)
            {
                actions.AddRange(openSegment);
            }
        }
    }
}

internal sealed class ChartLongNoteSaveNormalizationPlan
{
    private readonly LongNoteNormalizationAction[] actions;
    private readonly int[] affectedPositions;

    internal ChartLongNoteSaveNormalizationPlan(
        IReadOnlyList<LongNoteNormalizationAction> sourceActions)
    {
        actions = new LongNoteNormalizationAction[sourceActions.Count];
        HashSet<int> positions = new HashSet<int>();

        for (int i = 0; i < sourceActions.Count; i++)
        {
            LongNoteNormalizationAction action = sourceActions[i];
            actions[i] = action;
            positions.Add(action.AbsolutePosition);

            if (action.IsScratch)
            {
                ConvertedLongScratchPointCount++;
            }
            else
            {
                ConvertedLongTapCount++;
            }
        }

        affectedPositions = new int[positions.Count];
        positions.CopyTo(affectedPositions);
        Array.Sort(affectedPositions);
    }

    public bool HasChanges => actions.Length > 0;
    public int ConvertedLongTapCount { get; }
    public int ConvertedLongScratchPointCount { get; }
    public int[] AffectedPositions =>
        (int[])affectedPositions.Clone();

    public void Apply()
    {
        for (int i = 0; i < actions.Length; i++)
        {
            actions[i].Apply();
        }
    }
}

internal readonly struct LongNoteNormalizationAction
{
    private readonly ChartHolder holder;
    private readonly int storageIndex;
    private readonly int scratchIndex;

    private LongNoteNormalizationAction(
        ChartHolder holder,
        int storageIndex,
        int scratchIndex)
    {
        this.holder = holder;
        this.storageIndex = storageIndex;
        this.scratchIndex = scratchIndex;
    }

    public int AbsolutePosition => holder.AbsoluteChartPosition;
    public bool IsScratch => scratchIndex >= 0;

    public static LongNoteNormalizationAction ForLongTap(
        ChartHolder holder,
        int storageIndex)
    {
        return new LongNoteNormalizationAction(
            holder,
            storageIndex,
            -1);
    }

    public static LongNoteNormalizationAction ForLongScratch(
        ChartHolder holder,
        int storageIndex,
        int scratchIndex)
    {
        return new LongNoteNormalizationAction(
            holder,
            storageIndex,
            scratchIndex);
    }

    public void Apply()
    {
        holder.EnsureStorage();

        if (!IsScratch)
        {
            if (holder.noteTypes[storageIndex] == NoteType.LongTap)
            {
                holder.noteTypes[storageIndex] = NoteType.Tap;
                holder.isPoweredNotes[storageIndex] = false;
            }

            return;
        }

        if (holder.noteTypes[storageIndex] != NoteType.LongScratch)
        {
            return;
        }

        ScratchMotionData sourceMotion =
            holder.scratchMotions[scratchIndex] ??
            ScratchMotionData.CreateDefault(NoteType.Scratch);
        holder.noteTypes[storageIndex] = NoteType.Scratch;
        holder.scratchPointTypes[scratchIndex] = ScratchPointType.Tap;
        holder.scratchMotions[scratchIndex] =
            ScratchMotionRules.NormalizeMotion(
                NoteType.Scratch,
                ScratchPointType.Tap,
                sourceMotion);
        holder.isPoweredNotes[storageIndex] =
            holder.scratchMotions[scratchIndex].MotionType !=
            ScratchMotionType.None;
    }
}

using System.Collections.Generic;

internal static class ChartEditHistory
{
    private const int MaxHistoryCount = 100;

    private static readonly List<ChartEditChange> UndoChanges =
        new List<ChartEditChange>();
    private static readonly List<ChartEditChange> RedoChanges =
        new List<ChartEditChange>();

    public static bool CanUndo => UndoChanges.Count > 0;
    public static bool CanRedo => RedoChanges.Count > 0;

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Clear();
    }

    /// <summary>수정할 모든 위치의 변경 전 데이터를 복제해 하나의 변경 토큰을 만듭니다.</summary>
    public static ChartEditTransaction BeginChange(params int[] absolutePositions)
    {
        if (absolutePositions == null || absolutePositions.Length == 0)
        {
            return default;
        }

        List<ChartEditState> states = new List<ChartEditState>();
        HashSet<int> capturedPositions = new HashSet<int>();

        for (int i = 0; i < absolutePositions.Length; i++)
        {
            int absolutePosition = absolutePositions[i];

            if (absolutePosition < 0 ||
                !capturedPositions.Add(absolutePosition))
            {
                continue;
            }

            states.Add(CaptureState(absolutePosition));
        }

        return states.Count > 0
            ? new ChartEditTransaction(states.ToArray())
            : default;
    }

    /// <summary>수정 후 상태를 기록해 하나의 Undo 단위로 확정합니다.</summary>
    public static void CommitChange(ChartEditTransaction transaction)
    {
        if (!transaction.IsValid)
        {
            return;
        }

        ChartEditChange change = new ChartEditChange(
            transaction.BeforeStates,
            CaptureStates(transaction.BeforeStates));
        PushChange(UndoChanges, change);
        RedoChanges.Clear();
    }

    public static bool Undo(ChartPlacementController placementController)
    {
        return RestoreFrom(
            UndoChanges,
            RedoChanges,
            placementController,
            useBeforeState: true);
    }

    public static bool Redo(ChartPlacementController placementController)
    {
        return RestoreFrom(
            RedoChanges,
            UndoChanges,
            placementController,
            useBeforeState: false);
    }

    public static void Clear()
    {
        UndoChanges.Clear();
        RedoChanges.Clear();
    }

    private static bool RestoreFrom(
        List<ChartEditChange> source,
        List<ChartEditChange> destination,
        ChartPlacementController placementController,
        bool useBeforeState)
    {
        if (source.Count == 0 || !placementController)
        {
            return false;
        }

        int index = source.Count - 1;
        ChartEditChange change = source[index];
        source.RemoveAt(index);

        ChartEditState[] states = useBeforeState
            ? change.Before
            : change.After;

        for (int i = 0; i < states.Length; i++)
        {
            ChartEditState state = states[i];
            ChartManager.RestoreHolder(
                state.AbsolutePosition,
                state.Holder != null
                    ? state.Holder.CloneData()
                    : null);
        }

        ChartManager.NotifyChartChanged();
        placementController.RebuildChartViews();
        PushChange(destination, change);
        return true;
    }

    private static void PushChange(
        List<ChartEditChange> changes,
        ChartEditChange change)
    {
        changes.Add(change);

        if (changes.Count > MaxHistoryCount)
        {
            changes.RemoveAt(0);
        }
    }

    private static ChartEditState CaptureState(int absolutePosition)
    {
        ChartHolder holder = ChartManager.GetHolder(absolutePosition);
        return new ChartEditState(
            absolutePosition,
            holder != null && holder.HasChartData
                ? holder.CloneData()
                : null);
    }

    private static ChartEditState[] CaptureStates(
        ChartEditState[] sourceStates)
    {
        ChartEditState[] states =
            new ChartEditState[sourceStates.Length];

        for (int i = 0; i < sourceStates.Length; i++)
        {
            states[i] = CaptureState(sourceStates[i].AbsolutePosition);
        }

        return states;
    }

    internal readonly struct ChartEditTransaction
    {
        public ChartEditState[] BeforeStates { get; }
        public bool IsValid => BeforeStates != null && BeforeStates.Length > 0;

        public ChartEditTransaction(ChartEditState[] beforeStates)
        {
            BeforeStates = beforeStates;
        }
    }

    private readonly struct ChartEditChange
    {
        public ChartEditState[] Before { get; }
        public ChartEditState[] After { get; }

        public ChartEditChange(
            ChartEditState[] before,
            ChartEditState[] after)
        {
            Before = before;
            After = after;
        }
    }

    internal readonly struct ChartEditState
    {
        public int AbsolutePosition { get; }
        public ChartHolder Holder { get; }

        public ChartEditState(
            int absolutePosition,
            ChartHolder holder)
        {
            AbsolutePosition = absolutePosition;
            Holder = holder;
        }
    }
}

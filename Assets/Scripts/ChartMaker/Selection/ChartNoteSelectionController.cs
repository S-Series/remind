using System;
using System.Collections.Generic;
using REmind.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChartNoteSelectionController : MonoBehaviour
{
    private const int SelectionCategoryCount = 4;
    private const int MeasureCount = ChartHolder.MeasureCount;

    [SerializeField] private ChartMakerInputRouter inputRouter;
    [SerializeField] private ChartPlacementController placementController;
    [SerializeField] private Transform leftNoteField;
    [SerializeField] private Transform middleNoteField;
    [SerializeField] private Transform rightNoteField;
    [SerializeField] private LayerMask noteLayerMask = ~0;

    private readonly List<GameObject> selectedNoteObjects =
        new List<GameObject>();
    private readonly List<ChartNoteSelectable> selectedTargets =
        new List<ChartNoteSelectable>();
    private readonly List<SelectionCandidate> clickCandidates =
        new List<SelectionCandidate>();

    public IReadOnlyList<GameObject> SelectedNoteObjects => selectedNoteObjects;
    public event Action<IReadOnlyList<GameObject>> SelectionChanged;

    private void Awake()
    {
        if (!inputRouter)
        {
            inputRouter = GetComponent<ChartMakerInputRouter>();
        }

        if (!placementController)
        {
            placementController = GetComponent<ChartPlacementController>();
        }

        if (!leftNoteField || !middleNoteField || !rightNoteField)
        {
            Debug.LogError(
                "ChartNoteSelectionController requires all three Note Fields.",
                this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (inputRouter)
        {
            inputRouter.CancelRequested += ClearSelection;
            inputRouter.DeleteRequested += DeleteSelection;
            inputRouter.TogglePoweredRequested += ToggleSelectionPowered;
            inputRouter.MoveSelectionRequested += MoveSelection;
        }
    }

    private void OnDisable()
    {
        if (inputRouter)
        {
            inputRouter.CancelRequested -= ClearSelection;
            inputRouter.DeleteRequested -= DeleteSelection;
            inputRouter.TogglePoweredRequested -= ToggleSelectionPowered;
            inputRouter.MoveSelectionRequested -= MoveSelection;
        }

        ClearSelection();
    }

    /// <summary>
    /// 입력 영역의 정규화 좌표를 실제 Note Field 좌표로 변환해 노트를 선택합니다.
    /// </summary>
    public bool TrySelectAt(
        Vector2 normalizedPosition,
        bool? positionCorrection)
    {
        if (!enabled)
        {
            return false;
        }

        Transform noteField = GetNoteField(positionCorrection);
        Vector2 localPosition =
            ChartPlacementController.NormalizedToNoteFieldPosition(
                normalizedPosition);
        Vector2 worldPosition = noteField.TransformPoint(localPosition);
        Physics2D.SyncTransforms();
        Collider2D[] hits = Physics2D.OverlapPointAll(
            worldPosition,
            noteLayerMask);
        clickCandidates.Clear();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            NoteView noteView = hit.GetComponentInParent<NoteView>();

            if (!noteView ||
                !noteView.transform.IsChildOf(noteField) ||
                !noteView.TryGetClickPriority(
                    hit,
                    out int clickPriority) ||
                !noteView.TryGetComponent(
                    out ChartNoteSelectable selectable) ||
                GetSelectionRank(selectable.NoteType) < 0)
            {
                continue;
            }

            AddOrUpdateCandidate(
                selectable,
                hit,
                clickPriority,
                worldPosition);
        }

        ChartNoteSelectable selectedCandidate = GetBestCandidate();
        clickCandidates.Clear();

        if (selectedCandidate)
        {
            Select(selectedCandidate);
            return true;
        }

        ClearSelection();
        return false;
    }

    private void AddOrUpdateCandidate(
        ChartNoteSelectable target,
        Collider2D hit,
        int clickPriority,
        Vector2 clickPosition)
    {
        SpriteRenderer renderer = hit.GetComponent<SpriteRenderer>();

        if (!renderer)
        {
            renderer = target.GetComponentInChildren<SpriteRenderer>();
        }

        SelectionCandidate candidate = new SelectionCandidate(
            target,
            clickPriority,
            renderer
                ? SortingLayer.GetLayerValueFromID(renderer.sortingLayerID)
                : 0,
            renderer ? renderer.sortingOrder : 0,
            ((Vector2)hit.bounds.center - clickPosition).sqrMagnitude);

        for (int i = 0; i < clickCandidates.Count; i++)
        {
            if (clickCandidates[i].Target != target)
            {
                continue;
            }

            if (IsBetterHit(candidate, clickCandidates[i]))
            {
                clickCandidates[i] = candidate;
            }

            return;
        }

        clickCandidates.Add(candidate);
    }

    /// <summary>
    /// 기본 타입 순서 또는 현재 선택 타입의 다음 순서에서 가장 가까운 후보를 고릅니다.
    /// </summary>
    private ChartNoteSelectable GetBestCandidate()
    {
        if (clickCandidates.Count == 0)
        {
            return null;
        }

        ChartNoteSelectable currentCandidate = null;

        for (int i = 0; i < clickCandidates.Count; i++)
        {
            if (clickCandidates[i].Target.IsSelected)
            {
                currentCandidate = clickCandidates[i].Target;
                break;
            }
        }

        int startRank = 0;

        if (currentCandidate)
        {
            int currentRank = GetSelectionRank(currentCandidate.NoteType);

            if (currentRank >= 0)
            {
                startRank = (currentRank + 1) % SelectionCategoryCount;
            }
        }

        SelectionCandidate best = clickCandidates[0];

        for (int i = 1; i < clickCandidates.Count; i++)
        {
            SelectionCandidate candidate = clickCandidates[i];

            if (IsBetterCandidate(
                    candidate,
                    best,
                    startRank,
                    currentCandidate))
            {
                best = candidate;
            }
        }

        return best.Target;
    }

    private static bool IsBetterCandidate(
        SelectionCandidate candidate,
        SelectionCandidate current,
        int startRank,
        ChartNoteSelectable selectedTarget)
    {
        int candidateOffset = GetCyclicOffset(
            GetSelectionRank(candidate.Target.NoteType),
            startRank);
        int currentOffset = GetCyclicOffset(
            GetSelectionRank(current.Target.NoteType),
            startRank);

        if (candidateOffset != currentOffset)
        {
            return candidateOffset < currentOffset;
        }

        bool candidateWasSelected = candidate.Target == selectedTarget;
        bool currentWasSelected = current.Target == selectedTarget;

        if (candidateWasSelected != currentWasSelected)
        {
            return !candidateWasSelected;
        }

        if (IsBetterHit(candidate, current))
        {
            return true;
        }

        if (IsBetterHit(current, candidate))
        {
            return false;
        }

        return candidate.Target.GetInstanceID() <
            current.Target.GetInstanceID();
    }

    private static bool IsBetterHit(
        SelectionCandidate candidate,
        SelectionCandidate current)
    {
        if (candidate.ClickPriority != current.ClickPriority)
        {
            return candidate.ClickPriority > current.ClickPriority;
        }

        if (candidate.SortingLayerValue != current.SortingLayerValue)
        {
            return candidate.SortingLayerValue > current.SortingLayerValue;
        }

        if (candidate.SortingOrder != current.SortingOrder)
        {
            return candidate.SortingOrder > current.SortingOrder;
        }

        return candidate.DistanceSqr < current.DistanceSqr;
    }

    private static int GetCyclicOffset(int rank, int startRank)
    {
        return (rank - startRank + SelectionCategoryCount) %
            SelectionCategoryCount;
    }

    private static int GetSelectionRank(NoteType noteType)
    {
        return noteType switch
        {
            NoteType.Tap => 0,
            NoteType.Air => 1,
            NoteType.LongTap => 2,
            NoteType.Scratch => 3,
            NoteType.LongScratch => 3,
            _ => -1
        };
    }

    /// <summary>클릭된 노트 뷰를 받아 연결된 중앙·손 필드 노트를 함께 선택합니다.</summary>
    public bool OnNoteClicked(NoteView clickedNote)
    {
        if (!clickedNote ||
            !clickedNote.TryGetComponent(
                out ChartNoteSelectable selectable))
        {
            return false;
        }

        Select(selectable);
        return true;
    }

    public void ClearSelection()
    {
        if (!ClearSelectionVisuals())
        {
            return;
        }

        SelectionChanged?.Invoke(selectedNoteObjects);
    }

    /// <summary>이동 등으로 선택된 노트 데이터가 바뀐 사실을 UI에 다시 알립니다.</summary>
    internal void NotifySelectionChanged()
    {
        SelectionChanged?.Invoke(selectedNoteObjects);
    }

    /// <summary>Tab 단축키로 선택된 노트의 Powered 데이터를 즉시 반전합니다.</summary>
    public void ToggleSelectionPowered()
    {
        if (selectedNoteObjects.Count == 0 || !selectedNoteObjects[0])
        {
            return;
        }

        GameObject selectedObject = selectedNoteObjects[0];

        if (!ChartManager.TryGetNotePosition(
                selectedObject,
                out int absolutePosition))
        {
            return;
        }

        ChartEditHistory.ChartEditTransaction editTransaction =
            ChartEditHistory.BeginChange(absolutePosition);

        if (!ChartManager.ToggleNotePowered(
                selectedObject,
                out _,
                out string error))
        {
            Debug.LogWarning(error, this);
            return;
        }

        ChartEditHistory.CommitChange(editTransaction);
        NotifySelectionChanged();
    }

    /// <summary>
    /// 방향키 입력으로 선택된 노트의 라인 또는 채보 위치를 한 단계 이동합니다.
    /// </summary>
    public void MoveSelection(Vector2Int direction, bool moveByPage)
    {
        if (!placementController ||
            selectedNoteObjects.Count == 0 ||
            !selectedNoteObjects[0] ||
            Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
        {
            return;
        }

        GameObject selectedObject = selectedNoteObjects[0];

        if (!ChartManager.TryGetNoteData(
                selectedObject,
                out ChartHolder holder,
                out int sourceLine,
                out NoteType noteType,
                out NoteHandleType sourceHandle,
                out bool isPowered))
        {
            return;
        }

        int targetLine = sourceLine;
        int targetAbsolutePosition = holder.AbsoluteChartPosition;
        NoteHandleType targetHandle = sourceHandle;

        if (direction.x != 0)
        {
            if (!TryGetAdjacentLine(
                    noteType,
                    sourceLine,
                    direction.x,
                    out targetLine))
            {
                return;
            }

            targetHandle = targetLine switch
            {
                -1 => NoteHandleType.Left,
                -2 => NoteHandleType.Right,
                <= 2 => NoteHandleType.Left,
                _ => NoteHandleType.Right
            };
        }
        else
        {
            targetAbsolutePosition = GetMovedAbsolutePosition(
                targetAbsolutePosition,
                direction.y,
                moveByPage);

            if (targetAbsolutePosition == holder.AbsoluteChartPosition)
            {
                return;
            }
        }

        int targetMeasure =
            targetAbsolutePosition / ChartHolder.PositionUnitsPerMeasure;
        int targetPosition =
            targetAbsolutePosition % ChartHolder.PositionUnitsPerMeasure;
        bool moved;
        string error;

        switch (noteType)
        {
            case NoteType.Tap:
            case NoteType.LongTap:
                moved = placementController.TryEditTapNote(
                    selectedObject,
                    targetMeasure,
                    targetPosition,
                    targetLine,
                    targetHandle,
                    isPowered,
                    out error);
                break;
            case NoteType.Scratch:
            case NoteType.LongScratch:
                ScratchMotionData scratchMotion =
                    holder.GetScratchMotion(sourceLine);
                moved = placementController.TryEditScratchNote(
                    selectedObject,
                    targetMeasure,
                    targetPosition,
                    targetHandle,
                    isPowered,
                    scratchMotion.StartOffsetUnits,
                    scratchMotion.EndOffsetUnits,
                    scratchMotion.MotionType,
                    out error);
                break;
            case NoteType.Air:
                moved = placementController.TryEditAirNote(
                    selectedObject,
                    targetMeasure,
                    targetPosition,
                    targetLine,
                    holder.airNoteValues[sourceLine - 1],
                    out error);
                break;
            default:
                moved = false;
                error = $"{noteType} does not support keyboard movement.";
                break;
        }

        if (!moved)
        {
            Debug.LogWarning(
                error ??
                "The selected note could not be moved to the requested slot.",
                this);
        }
    }

    private static bool TryGetAdjacentLine(
        NoteType noteType,
        int sourceLine,
        int direction,
        out int targetLine)
    {
        if (noteType.IsScratch())
        {
            targetLine = direction < 0 ? -1 : -2;
            return targetLine != sourceLine;
        }

        targetLine = sourceLine + direction;
        return targetLine >= 1 && targetLine <= ChartHolder.MainLineCount;
    }

    private static int GetMovedAbsolutePosition(
        int sourcePosition,
        int direction,
        bool moveByPage)
    {
        int maximumPosition = ChartHolder.MaximumAbsolutePosition;
        int targetPosition;

        if (moveByPage)
        {
            targetPosition = sourcePosition +
                direction * ChartHolder.PositionUnitsPerMeasure;
        }
        else if (ChartPlacementController.UseYClamp)
        {
            targetPosition = GetAdjacentGuidePosition(
                sourcePosition,
                direction);
        }
        else
        {
            targetPosition = sourcePosition + direction;
        }

        return targetPosition >= 0 && targetPosition <= maximumPosition
            ? targetPosition
            : sourcePosition;
    }

    /// <summary>현재 위치에서 입력 방향에 있는 가장 가까운 가이드 Pos를 찾습니다.</summary>
    private static int GetAdjacentGuidePosition(
        int sourcePosition,
        int direction)
    {
        int guideCount = Mathf.Max(1, ChartPlacementController.YGuideCount);
        int guideIndex = Mathf.RoundToInt(
            (float)sourcePosition * guideCount /
            ChartHolder.PositionUnitsPerMeasure);
        int maximumGuideIndex = MeasureCount * guideCount;

        while (guideIndex >= 0 && guideIndex <= maximumGuideIndex)
        {
            int guidePosition = GetGuidePosition(guideIndex, guideCount);

            if ((direction > 0 && guidePosition > sourcePosition) ||
                (direction < 0 && guidePosition < sourcePosition))
            {
                return guidePosition;
            }

            guideIndex += direction;
        }

        return sourcePosition;
    }

    private static int GetGuidePosition(int guideIndex, int guideCount)
    {
        return ChartHolder.GridIndexToAbsolutePosition(
            guideIndex,
            guideCount);
    }

    /// <summary>선택된 중앙·손 필드 노트 묶음을 채보 데이터와 함께 삭제합니다.</summary>
    public void DeleteSelection()
    {
        if (selectedNoteObjects.Count == 0)
        {
            return;
        }

        GameObject selectedObject = selectedNoteObjects[0];

        if (!selectedObject)
        {
            ClearSelection();
            return;
        }

        if (!ChartManager.TryGetNotePosition(
                selectedObject,
                out int absolutePosition))
        {
            ClearSelection();
            return;
        }

        ChartEditHistory.ChartEditTransaction editTransaction =
            ChartEditHistory.BeginChange(absolutePosition);

        if (!ChartManager.DeleteNote(selectedObject))
        {
            return;
        }

        ChartEditHistory.CommitChange(editTransaction);
        ClearSelection();
    }

    private bool ClearSelectionVisuals()
    {
        if (selectedTargets.Count == 0 && selectedNoteObjects.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < selectedTargets.Count; i++)
        {
            if (selectedTargets[i])
            {
                selectedTargets[i].SetSelected(false);
            }
        }

        selectedTargets.Clear();
        selectedNoteObjects.Clear();
        return true;
    }

    private void Select(ChartNoteSelectable clickedTarget)
    {
        ClearSelectionVisuals();

        GameObject[] linkedObjects = clickedTarget.LinkedNoteObjects;
        if (linkedObjects == null || linkedObjects.Length == 0)
        {
            AddSelectedObject(clickedTarget.gameObject);
        }
        else
        {
            for (int i = 0; i < linkedObjects.Length; i++)
            {
                AddSelectedObject(linkedObjects[i]);
            }
        }

        SelectionChanged?.Invoke(selectedNoteObjects);
    }

    private void AddSelectedObject(GameObject noteObject)
    {
        if (!noteObject || selectedNoteObjects.Contains(noteObject))
        {
            return;
        }

        selectedNoteObjects.Add(noteObject);

        if (noteObject.TryGetComponent(
                out ChartNoteSelectable selectable))
        {
            selectable.SetSelected(true);
            selectedTargets.Add(selectable);
        }
    }

    private Transform GetNoteField(bool? positionCorrection)
    {
        return positionCorrection switch
        {
            false => leftNoteField,
            true => rightNoteField,
            null => middleNoteField
        };
    }

    private readonly struct SelectionCandidate
    {
        public ChartNoteSelectable Target { get; }
        public int ClickPriority { get; }
        public int SortingLayerValue { get; }
        public int SortingOrder { get; }
        public float DistanceSqr { get; }

        public SelectionCandidate(
            ChartNoteSelectable target,
            int clickPriority,
            int sortingLayerValue,
            int sortingOrder,
            float distanceSqr)
        {
            Target = target;
            ClickPriority = clickPriority;
            SortingLayerValue = sortingLayerValue;
            SortingOrder = sortingOrder;
            DistanceSqr = distanceSqr;
        }
    }
}

using System.Collections.Generic;
using REmind.Charting;
using REmind.Data;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class ChartPlacementController : MonoBehaviour
{
    private const float PreviewMinX = -15f;
    private const float PreviewMaxX = 15f;
    private const float NormalizedMinX = -1f;
    private const float NormalizedMaxX = 1f;
    private const float PreviewMaxNormalizedY = 0.8f;
    private const float PreviewReferenceY =
        ChartHolder.WorldUnitsPerMeasure;
    private const float SidePositionCorrection = 40f;

    private static readonly float[] TapXClampValues = { -0.75f, -0.25f, 0.25f, 0.75f };
    private static readonly float[] ScratchClampValues = { -0.5f, 0.5f };
    private static readonly float[] SingleClampValues = { 0f };
    private static readonly NoteType[] SpecialNoteCycle =
    {
        NoteType.Speed,
        NoteType.Effect,
        NoteType.Camera,
        NoteType.Marker
    };

    private static readonly Dictionary<NoteType, float[]> XClampValues =
        new Dictionary<NoteType, float[]>
        {
            { NoteType.Unknown, SingleClampValues },
            //======================================//
            { NoteType.Tap, TapXClampValues },
            { NoteType.LongTap, TapXClampValues },
            { NoteType.Air, TapXClampValues },
            //======================================//
            { NoteType.Scratch, ScratchClampValues },
            { NoteType.LongScratch, ScratchClampValues },
            //======================================//
            { NoteType.Speed, SingleClampValues },
            { NoteType.Effect, SingleClampValues },
            { NoteType.Camera, SingleClampValues },
            { NoteType.Marker, SingleClampValues }
        };

    public static bool UseYClamp { get; set; } = true;
    public static int YGuideCount { get; private set; } = 4;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticSettings()
    {
        UseYClamp = true;
        YGuideCount = 4;
        XClampValues.Clear();
        XClampValues.Add(NoteType.Unknown, SingleClampValues);
        XClampValues.Add(NoteType.Tap, TapXClampValues);
        XClampValues.Add(NoteType.LongTap, TapXClampValues);
        XClampValues.Add(NoteType.Air, TapXClampValues);
        XClampValues.Add(NoteType.Scratch, ScratchClampValues);
        XClampValues.Add(NoteType.LongScratch, ScratchClampValues);
        XClampValues.Add(NoteType.Speed, SingleClampValues);
        XClampValues.Add(NoteType.Effect, SingleClampValues);
        XClampValues.Add(NoteType.Camera, SingleClampValues);
        XClampValues.Add(NoteType.Marker, SingleClampValues);
    }

    [Header("Settings")]
    [FormerlySerializedAs("PreviewField")]
    [SerializeField] private Transform previewField;

    [Header("Chart Fields")]
    [SerializeField] private Transform leftNoteField;
    [SerializeField] private Transform middleNoteField;
    [SerializeField] private Transform rightNoteField;
    [SerializeField] private Transform specialNoteField;
    [SerializeField] private Transform chartPreviewNoteField;

    [Header("InGame / Making Prefabs")]
    [FormerlySerializedAs("TapNotePrefab")]
    [SerializeField] private GameObject tapNotePrefab;
    [FormerlySerializedAs("HoldNotePrefab")]
    [FormerlySerializedAs("holdNotePrefab")]
    [SerializeField] private GameObject longTapNotePrefab;
    [SerializeField] private GameObject scratchNotePrefab;
    [SerializeField] private GameObject longScratchNotePrefab;
    [FormerlySerializedAs("AirNotePrefab")]
    [SerializeField] private GameObject airNotePrefab;

    [Header("Special Preview Prefabs")]
    [FormerlySerializedAs("FlickNotePrefab")]
    [FormerlySerializedAs("flickNotePrefab")]
    [FormerlySerializedAs("effectNotePrefab")]
    [SerializeField] private GameObject chartPreviewEffectNotePrefab;
    [FormerlySerializedAs("SpeedNotePrefab")]
    [FormerlySerializedAs("speedNotePrefab")]
    [SerializeField] private GameObject chartPreviewSpeedNotePrefab;
    [FormerlySerializedAs("ActionNotePrefab")]
    [FormerlySerializedAs("actionNotePrefab")]
    [FormerlySerializedAs("cameraNotePrefab")]
    [SerializeField] private GameObject chartPreviewCameraNotePrefab;
    [SerializeField] private GameObject chartPreviewMarkerNotePrefab;
    [SerializeField] private GameObject chartPreviewIdleNotePrefab;

    [Header("Chart Preview Prefabs")]
    [SerializeField] private GameObject chartPreviewTapNotePrefab;
    [SerializeField] private GameObject chartPreviewLongTapNotePrefab;
    [SerializeField] private GameObject chartPreviewScratchNotePrefab;
    [SerializeField] private GameObject chartPreviewLongScratchNotePrefab;
    [SerializeField] private GameObject chartPreviewAirNotePrefab;

    [Header("Input Areas")]
    [SerializeField] private ChartMakerInputRouter inputRouter;
    [SerializeField] private ChartAction[] chartActions;

    [Header("Selection")]
    [SerializeField] private ChartNoteSelectionController selectionController;

    [Header("Editing State")]
    [SerializeField] private ChartCore chartCore;

    [FormerlySerializedAs("logHoverNormalizedPosition")]
    [SerializeField] private bool logPlacedPosition = true;

    [SerializeField] private bool isPreviewing;
    [SerializeField] private bool isHovering;
    private NoteType currentNoteType = NoteType.Unknown;
    private bool canPlaceCurrentPreview;
    private bool isDraggingNote;
    private GameObject dragAnchor;
    private NoteType draggedNoteType;
    private int dragSourceAbsolutePosition;
    private int dragSourceLine;
    private NoteHandleType dragSourceHandle;
    private int dragTargetLine;
    private NoteHandleType dragTargetHandle;
    private Vector3 dragTargetPosition;
    private Transform[] dragOriginalParents;
    private Vector3[] dragOriginalPositions;
    private GameObject[] dragObjects;

    private GameObject previewNote;

    public Vector2 NormalizedPosition { get; private set; }
    public bool? PositionCorrection { get; private set; }
    public ChartToolType CurrentTool { get; private set; }
    public bool IsPreviewing => isPreviewing;
    public bool IsHovering => isHovering;
    public bool CanEditChart
    {
        get
        {
            ResolveChartCore();
            return !chartCore || !chartCore.IsTestPlaying;
        }
    }

    private void OnEnable()
    {
        SetInputRouterSubscription(true);
        SetActionSubscriptions(true);
        GuideGenerate.ReferenceYChanged += HandleScrollReferenceYChanged;
        GuideGenerate.VerticalDisplayScaleChanged +=
            HandleVerticalDisplayScaleChanged;
    }

    private void OnDisable()
    {
        CancelNoteDrag();
        SetInputRouterSubscription(false);
        SetActionSubscriptions(false);
        GuideGenerate.ReferenceYChanged -= HandleScrollReferenceYChanged;
        GuideGenerate.VerticalDisplayScaleChanged -=
            HandleVerticalDisplayScaleChanged;
        SetHovering(false);
    }

    private void Start()
    {
        ResolveChartCore();

        if (!selectionController)
        {
            selectionController = GetComponent<ChartNoteSelectionController>();
        }

        if (!inputRouter ||
            !tapNotePrefab ||
            !previewField ||
            !leftNoteField ||
            !middleNoteField ||
            !rightNoteField ||
            !specialNoteField ||
            !chartPreviewNoteField ||
            !chartPreviewTapNotePrefab ||
            !chartPreviewLongTapNotePrefab ||
            !chartPreviewScratchNotePrefab ||
            !chartPreviewLongScratchNotePrefab ||
            !chartPreviewAirNotePrefab ||
            !chartPreviewSpeedNotePrefab ||
            !chartPreviewEffectNotePrefab ||
            !chartPreviewCameraNotePrefab ||
            !chartPreviewIdleNotePrefab)
        {
            Debug.LogError(
                "ChartPlacementController requires an Input Router, a Tap Note prefab, " +
                "Preview Field, all four editing Note Fields, and every chart " +
                "Preview prefab.",
                this);
            enabled = false;
            return;
        }

        if (CurrentTool != ChartToolType.None)
        {
            SetCurrentTool(ChartToolType.None);
        }
    }

    /// <summary>선택한 도구의 프리뷰 사용 상태를 바꿉니다.</summary>
    public void SetPreviewing(bool previewing)
    {
        isPreviewing = previewing;
        RefreshPreviewVisibility();
    }

    private void SetHovering(bool hovering)
    {
        isHovering = hovering;
        RefreshPreviewVisibility();
    }

    private void RefreshPreviewVisibility()
    {
        if (previewNote)
        {
            previewNote.SetActive(
                isPreviewing && isHovering && !isDraggingNote);
        }
    }

    private void HandlePointerEntered(ChartAction _)
    {
        SetHovering(true);
    }

    private void HandlePointerExited(ChartAction _)
    {
        SetHovering(false);
    }

    private void HandleNormalizedPositionChanged(
        ChartAction _,
        Vector2 normalizedPosition,
        bool? positionCorrection)
    {
        NormalizedPosition = normalizedPosition;
        PositionCorrection = positionCorrection;
        RefreshPreviewPosition();
    }

    private void HandlePositionClicked(
        ChartAction source,
        Vector2 normalizedPosition,
        bool? positionCorrection)
    {
        if (!CanEditChart)
        {
            return;
        }

        NormalizedPosition = normalizedPosition;
        PositionCorrection = positionCorrection;
        RefreshPreviewPosition();

        // 배치 도구가 활성화된 동안 기존 노트가 클릭되어도 선택 모드로
        // 전환하지 않습니다. 이미 점유된 슬롯이면 배치만 실패하고 도구는
        // 그대로 유지됩니다.
        if (canPlaceCurrentPreview)
        {
            if (source &&
                source.IsSpecialField != currentNoteType.IsChartEvent())
            {
                return;
            }

            TryPlaceCurrentNote();
            return;
        }

        if (selectionController &&
            selectionController.TrySelectAt(
                normalizedPosition,
                positionCorrection,
                source && source.IsSpecialField))
        {
            if (CurrentTool == ChartToolType.Eraser)
            {
                selectionController.DeleteSelection();
            }

            return;
        }

        TryPlaceCurrentNote();
    }

    private void HandleDragStarted(
        ChartAction source,
        Vector2 normalizedPosition,
        bool? positionCorrection)
    {
        if (!CanEditChart ||
            canPlaceCurrentPreview ||
            CurrentTool == ChartToolType.Eraser)
        {
            return;
        }

        NormalizedPosition = normalizedPosition;
        PositionCorrection = positionCorrection;

        if (!selectionController ||
            !selectionController.TrySelectAt(
                normalizedPosition,
                positionCorrection,
                source && source.IsSpecialField) ||
            selectionController.SelectedNoteObjects.Count == 0)
        {
            return;
        }

        dragAnchor = selectionController.SelectedNoteObjects[0];

        if (!dragAnchor ||
            !ChartManager.TryGetNoteData(
                dragAnchor,
                out ChartHolder sourceHolder,
                out dragSourceLine,
                out draggedNoteType,
                out dragSourceHandle,
                out _))
        {
            dragAnchor = null;
            return;
        }

        dragSourceAbsolutePosition = sourceHolder.AbsoluteChartPosition;
        GameObject[] linkedNoteObjects;

        if (draggedNoteType == NoteType.Marker)
        {
            sourceHolder.TryGetMarkerNote(out linkedNoteObjects);
        }
        else if (draggedNoteType == NoteType.Camera)
        {
            sourceHolder.TryGetCameraNote(out linkedNoteObjects);
        }
        else if (draggedNoteType == NoteType.Effect)
        {
            sourceHolder.TryGetEffectNote(out linkedNoteObjects);
        }
        else if (draggedNoteType == NoteType.Air)
        {
            sourceHolder.TryGetAirNote(
                dragSourceLine,
                out _,
                out linkedNoteObjects);
        }
        else
        {
            sourceHolder.TryGetNote(
                dragSourceLine,
                out _,
                out linkedNoteObjects);
        }

        IReadOnlyList<GameObject> dragTargets = linkedNoteObjects;

        if (dragTargets == null)
        {
            dragTargets = selectionController.SelectedNoteObjects;
        }

        CaptureDragTransforms(dragTargets);
        isDraggingNote = true;
        RefreshPreviewVisibility();
        UpdateNoteDrag(normalizedPosition, positionCorrection);
    }

    private void HandlePositionDragged(
        ChartAction _,
        Vector2 normalizedPosition,
        bool? positionCorrection)
    {
        if (!isDraggingNote)
        {
            return;
        }

        UpdateNoteDrag(normalizedPosition, positionCorrection);
    }

    private void HandleDragEnded(
        ChartAction _,
        Vector2 normalizedPosition,
        bool? positionCorrection)
    {
        if (!isDraggingNote)
        {
            return;
        }

        UpdateNoteDrag(normalizedPosition, positionCorrection);
        CommitNoteDrag();
    }

    private void HandleScrollReferenceYChanged(float _)
    {
        RefreshPreviewPosition();
    }

    private void HandleVerticalDisplayScaleChanged(float scale)
    {
        RefreshPreviewPosition();
        ApplyEditingVisualDisplayScale(scale);
    }

    private void HandleToolSelected(ChartToolType toolType)
    {
        SetCurrentTool(toolType);
    }

    private void HandleToolShortcutRequested(ChartToolShortcut shortcut)
    {
        if (!CanEditChart)
        {
            return;
        }

        switch (shortcut)
        {
            case ChartToolShortcut.Tap:
                SetCurrentNoteType(currentNoteType == NoteType.Tap
                    ? NoteType.LongTap
                    : NoteType.Tap);
                break;
            case ChartToolShortcut.Scratch:
                SetCurrentNoteType(currentNoteType == NoteType.Scratch
                    ? NoteType.LongScratch
                    : NoteType.Scratch);
                break;
            case ChartToolShortcut.Eraser:
                SetCurrentTool(ChartToolType.Eraser);
                break;
            case ChartToolShortcut.Air:
                SetCurrentNoteType(NoteType.Air);
                break;
            case ChartToolShortcut.Specials:
                SelectNextSpecialNote();
                break;
            default:
                Debug.LogWarning(
                    $"Unsupported chart tool shortcut: {shortcut}",
                    this);
                break;
        }
    }

    private void SelectNextSpecialNote()
    {
        int currentIndex = System.Array.IndexOf(
            SpecialNoteCycle,
            currentNoteType);
        int nextIndex = (currentIndex + 1) % SpecialNoteCycle.Length;
        SetCurrentNoteType(SpecialNoteCycle[nextIndex]);
    }

    private void SetInputRouterSubscription(bool subscribe)
    {
        if (!inputRouter)
        {
            return;
        }

        if (subscribe)
        {
            inputRouter.ToolSelected += HandleToolSelected;
            inputRouter.ToolShortcutRequested +=
                HandleToolShortcutRequested;
        }
        else
        {
            inputRouter.ToolSelected -= HandleToolSelected;
            inputRouter.ToolShortcutRequested -=
                HandleToolShortcutRequested;
        }
    }

    private void RefreshPreviewPosition()
    {
        if (previewNote)
        {
            previewNote.transform.localPosition =
                GetCurrentPreviewPosition();
        }
    }

    private void SetActionSubscriptions(bool subscribe)
    {
        if (chartActions == null)
        {
            return;
        }

        foreach (ChartAction chartAction in chartActions)
        {
            if (!chartAction)
            {
                continue;
            }

            if (subscribe)
            {
                chartAction.PointerEntered += HandlePointerEntered;
                chartAction.PointerExited += HandlePointerExited;
                chartAction.NormalizedPositionChanged +=
                    HandleNormalizedPositionChanged;
                chartAction.PositionClicked += HandlePositionClicked;
                chartAction.DragStarted += HandleDragStarted;
                chartAction.PositionDragged += HandlePositionDragged;
                chartAction.DragEnded += HandleDragEnded;
            }
            else
            {
                chartAction.PointerEntered -= HandlePointerEntered;
                chartAction.PointerExited -= HandlePointerExited;
                chartAction.NormalizedPositionChanged -=
                    HandleNormalizedPositionChanged;
                chartAction.PositionClicked -= HandlePositionClicked;
                chartAction.DragStarted -= HandleDragStarted;
                chartAction.PositionDragged -= HandlePositionDragged;
                chartAction.DragEnded -= HandleDragEnded;
            }
        }
    }

    private void CaptureDragTransforms(
        IReadOnlyList<GameObject> noteObjects)
    {
        dragObjects = new GameObject[noteObjects.Count];
        dragOriginalParents = new Transform[noteObjects.Count];
        dragOriginalPositions = new Vector3[noteObjects.Count];

        for (int i = 0; i < noteObjects.Count; i++)
        {
            GameObject noteObject = noteObjects[i];

            if (!noteObject)
            {
                continue;
            }

            dragObjects[i] = noteObject;
            dragOriginalParents[i] = noteObject.transform.parent;
            dragOriginalPositions[i] = noteObject.transform.localPosition;
        }
    }

    /// <summary>드래그 좌표를 노트 종류에 맞게 스냅하고 복제 오브젝트에 미리 적용합니다.</summary>
    private void UpdateNoteDrag(
        Vector2 normalizedPosition,
        bool? positionCorrection)
    {
        GetNotePlacement(
            normalizedPosition,
            draggedNoteType,
            positionCorrection,
            out dragTargetPosition,
            out dragTargetLine,
            out dragTargetHandle);

        Transform handField = dragTargetHandle == NoteHandleType.Left
            ? leftNoteField
            : rightNoteField;
        GameObject[] noteObjects = dragObjects;

        if (noteObjects == null)
        {
            return;
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            GameObject noteObject = noteObjects[i];

            if (!noteObject)
            {
                continue;
            }

            Transform targetParent = GetTargetNoteField(
                noteObject.transform.parent,
                handField);
            noteObject.transform.SetParent(targetParent, false);
            noteObject.transform.localPosition = dragTargetPosition;
        }
    }

    /// <summary>드래그 결과를 두 ChartHolder 사이의 단일 편집 작업으로 확정합니다.</summary>
    private void CommitNoteDrag()
    {
        if (!CanEditChart)
        {
            RestoreDragTransforms();
            FinishNoteDrag();
            return;
        }

        int targetAbsolutePosition = Mathf.Max(
            0,
            ChartHolder.WorldYToAbsolutePosition(dragTargetPosition.y));
        bool positionChanged =
            targetAbsolutePosition != dragSourceAbsolutePosition ||
            (!draggedNoteType.IsChartEvent() &&
             (dragTargetLine != dragSourceLine ||
              (!draggedNoteType.IsScratch() &&
               dragTargetHandle != dragSourceHandle)));

        if (!positionChanged)
        {
            FinishNoteDrag();
            return;
        }

        ChartEditHistory.ChartEditTransaction editTransaction =
            ChartEditHistory.BeginChange(
                dragSourceAbsolutePosition,
                targetAbsolutePosition);

        if (!ChartManager.MoveNote(
                dragAnchor,
                targetAbsolutePosition,
                dragTargetLine,
                dragTargetHandle))
        {
            RestoreDragTransforms();
            FinishNoteDrag();
            return;
        }

        ChartEditHistory.CommitChange(editTransaction);
        selectionController.NotifySelectionChanged();
        FinishNoteDrag();
    }

    private void CancelNoteDrag()
    {
        if (!isDraggingNote)
        {
            return;
        }

        RestoreDragTransforms();
        FinishNoteDrag();
    }

    private void RestoreDragTransforms()
    {
        if (dragOriginalParents == null || dragOriginalPositions == null)
        {
            return;
        }

        GameObject[] noteObjects = dragObjects;

        if (noteObjects == null)
        {
            return;
        }

        int count = Mathf.Min(
            noteObjects.Length,
            dragOriginalParents.Length);

        for (int i = 0; i < count; i++)
        {
            GameObject noteObject = noteObjects[i];

            if (!noteObject || !dragOriginalParents[i])
            {
                continue;
            }

            noteObject.transform.SetParent(dragOriginalParents[i], false);
            noteObject.transform.localPosition = dragOriginalPositions[i];
        }
    }

    private void FinishNoteDrag()
    {
        isDraggingNote = false;
        dragAnchor = null;
        draggedNoteType = NoteType.Unknown;
        dragSourceAbsolutePosition = 0;
        dragSourceLine = 0;
        dragSourceHandle = NoteHandleType.Unknown;
        dragTargetLine = 0;
        dragObjects = null;
        dragOriginalParents = null;
        dragOriginalPositions = null;
        RefreshPreviewVisibility();
    }

    private void TryPlaceCurrentNote()
    {
        if (!CanEditChart || !canPlaceCurrentPreview)
        {
            return;
        }

        GetNotePlacement(
            NormalizedPosition,
            currentNoteType,
            PositionCorrection,
            out Vector3 notePosition,
            out int line,
            out NoteHandleType handleType);
        ChartHolder holder = ChartManager.GetOrCreateHolder(notePosition.y);
        notePosition.y = holder.WorldY;
        bool closesOpenLongTap =
            currentNoteType == NoteType.Tap &&
            ChartManager.HasOpenLongTap(
                line,
                holder.AbsoluteChartPosition);
        bool closesOpenLongScratch =
            currentNoteType == NoteType.Scratch &&
            ChartManager.GetSuggestedScratchPointType(
                line,
                holder.AbsoluteChartPosition) == ScratchPointType.End;
        NoteType placementNoteType = closesOpenLongTap
            ? NoteType.LongTap
            : closesOpenLongScratch
                ? NoteType.LongScratch
                : currentNoteType;
        GameObject prefab = GetNotePrefab(placementNoteType);

        if (!prefab)
        {
            Debug.LogWarning(
                $"Cannot place {placementNoteType}: prefab is missing.",
                this);
            return;
        }

        if (placementNoteType == NoteType.Effect)
        {
            if (holder.isEffect)
            {
                return;
            }

            ChartEditHistory.ChartEditTransaction effectTransaction =
                ChartEditHistory.BeginChange(holder.AbsoluteChartPosition);
            GameObject[] effectObjects = CreateNoteObjects(
                prefab, notePosition, NoteHandleType.Left, NoteType.Effect);
            if (!holder.AddEffectNote(effectObjects))
            {
                DestroyNoteObjects(effectObjects);
                return;
            }

            // A visible new note starts unresolved until its user-selected type is applied.
            // Legacy flags are never guessed into an executable effect.
            holder.EnsureEffectIdentity();
            ChartManager.NotifyChartChanged();
            ChartEditHistory.CommitChange(effectTransaction);
            // An Effect is intentionally unresolved when first placed. Move
            // directly into selection/edit mode so the required type and
            // settings are visible instead of requiring an undocumented Esc
            // and second click sequence.
            SetCurrentTool(ChartToolType.None);
            selectionController?.SelectNoteObject(effectObjects[0]);
            return;
        }

        if (placementNoteType == NoteType.Camera)
        {
            if (holder.isCameraMove)
            {
                return;
            }

            ChartEditHistory.ChartEditTransaction cameraTransaction =
                ChartEditHistory.BeginChange(holder.AbsoluteChartPosition);
            GameObject[] cameraObjects = CreateNoteObjects(
                prefab,
                notePosition,
                NoteHandleType.Left,
                NoteType.Camera);

            if (!holder.AddCameraNote(cameraObjects))
            {
                DestroyNoteObjects(cameraObjects);
                return;
            }

            ChartManager.NotifyChartChanged();
            ChartEditHistory.CommitChange(cameraTransaction);
            return;
        }

        if (placementNoteType == NoteType.Marker)
        {
            if (holder.isMarker)
            {
                return;
            }

            ChartEditHistory.ChartEditTransaction markerTransaction =
                ChartEditHistory.BeginChange(holder.AbsoluteChartPosition);
            GameObject[] markerObjects = CreateNoteObjects(
                prefab,
                notePosition,
                NoteHandleType.Left,
                NoteType.Marker);

            if (!holder.AddMarkerNote(markerObjects))
            {
                DestroyNoteObjects(markerObjects);
                return;
            }

            ChartManager.NotifyChartChanged();
            ChartEditHistory.CommitChange(markerTransaction);
            return;
        }

        if (holder.HasNote(line, placementNoteType))
        {
            return;
        }

        ChartEditHistory.ChartEditTransaction editTransaction =
            ChartEditHistory.BeginChange(holder.AbsoluteChartPosition);

        if (logPlacedPosition)
        {
            Debug.Log(
                $"Place {CurrentTool}: normalized={NormalizedPosition}, " +
                $"line={line}, chartY={notePosition.y}, " +
                $"position={holder.AbsoluteChartPosition}",
                this);
        }

        GameObject[] noteObjects = CreateNoteObjects(
            prefab,
            notePosition,
            handleType,
            placementNoteType);
        ScratchPointType? scratchPointType = placementNoteType switch
        {
            NoteType.Scratch => ScratchPointType.Tap,
            NoteType.LongScratch =>
                ChartManager.GetSuggestedScratchPointType(
                    line,
                    holder.AbsoluteChartPosition),
            _ => null
        };

        if (!holder.AddNote(
                line,
                placementNoteType,
                noteObjects,
                handleType,
                airValue: 1,
                scratchPointType: scratchPointType))
        {
            DestroyNoteObjects(noteObjects);
            return;
        }

        ChartManager.NotifyChartChanged();
        ChartEditHistory.CommitChange(editTransaction);
    }

    /// <summary>Tap/Long Tap의 위치, 라인과 손 방향을 수정합니다.</summary>
    public bool TryEditTapNote(
        GameObject noteObject,
        int measure,
        int measurePosition,
        int line,
        NoteHandleType handleType,
        out string error)
    {
        if (!TryRequireNoteType(
                noteObject,
                out NoteType noteType,
                out error,
                NoteType.Tap,
                NoteType.LongTap))
        {
            return false;
        }

        return TryApplyNoteEdit(
            noteObject,
            noteType,
            measure,
            measurePosition,
            line,
            handleType,
            false,
            0,
            null,
            null,
            out error);
    }

    /// <summary>선택된 Tap과 Long Tap을 서로 전환하고 해당 뷰만 교체합니다.</summary>
    public bool TryToggleTapLongNote(
        GameObject noteObject,
        out string error)
    {
        if (!TryRequireEditingAvailable(out error) ||
            !TryRequireNoteType(
                noteObject,
                out NoteType sourceType,
                out error,
                NoteType.Tap,
                NoteType.LongTap) ||
            !ChartManager.TryGetNoteData(
                noteObject,
                out ChartHolder sourceHolder,
                out int sourceLine,
                out _,
                out NoteHandleType sourceHandle,
                out _))
        {
            error ??= "Selected Tap note data could not be found.";
            return false;
        }

        NoteType targetType = sourceType == NoteType.Tap
            ? NoteType.LongTap
            : NoteType.Tap;
        GameObject targetPrefab = GetNotePrefab(targetType);

        if (!targetPrefab)
        {
            error = $"No prefab is configured for {targetType}.";
            return false;
        }

        Vector3 notePosition = new Vector3(
            GetStoredLineX(sourceLine),
            sourceHolder.WorldY,
            0f);
        GameObject[] replacementObjects = CreateNoteObjects(
            targetPrefab,
            notePosition,
            sourceHandle,
            targetType);
        ChartEditHistory.ChartEditTransaction editTransaction =
            ChartEditHistory.BeginChange(
                sourceHolder.AbsoluteChartPosition);

        if (!ChartManager.ChangeTapNoteType(
                noteObject,
                targetType,
                out ChartHolder changedHolder,
                out int changedLine,
                out _,
                out GameObject[] previousObjects,
                out error))
        {
            DestroyNoteObjects(replacementObjects);
            return false;
        }

        if (!changedHolder.AttachNoteObjects(
                changedLine,
                replacementObjects))
        {
            changedHolder.noteTypes[changedLine - 1] = sourceType;
            changedHolder.isPoweredNotes[changedLine - 1] = false;
            changedHolder.AttachNoteObjects(changedLine, previousObjects);
            DestroyNoteObjects(replacementObjects);
            error = "The replacement Tap note view could not be attached.";
            return false;
        }

        selectionController?.ClearSelection();
        DestroyNoteObjects(previousObjects);
        ChartManager.RefreshLongNoteLengths(changedLine);
        ChartManager.NotifyChartChanged();
        ChartEditHistory.CommitChange(editTransaction);
        selectionController?.SelectNoteObject(replacementObjects[0]);
        error = null;
        return true;
    }

    /// <summary>선택된 Scratch의 Powered 상태를 전환합니다.</summary>
    public bool TryToggleScratchPowered(
        GameObject noteObject,
        out string error)
    {
        if (!TryRequireNoteType(
                noteObject,
                out NoteType noteType,
                out error,
                NoteType.Scratch,
                NoteType.LongScratch) ||
            !ChartManager.TryGetNoteData(
                noteObject,
                out ChartHolder holder,
                out int line,
                out _,
                out _,
                out _))
        {
            error ??= "Selected Scratch note data could not be found.";
            return false;
        }

        ScratchPointType pointType = holder.GetScratchPointType(line);
        ScratchMotionData sourceMotion = holder.GetScratchMotion(line);
        ScratchMotionType targetMotionType =
            sourceMotion.MotionType == ScratchMotionType.None
                ? GetDefaultPoweredScratchMotion(noteType, pointType)
                : ScratchMotionType.None;
        int moveAmount = targetMotionType != ScratchMotionType.None &&
            sourceMotion.MoveAmount <= 0
                ? ScratchMotionData.DefaultMoveAmount
                : sourceMotion.MoveAmount;
        NoteHandleType side = line == -2
            ? NoteHandleType.Right
            : NoteHandleType.Left;
        return TryEditScratchNote(
            noteObject,
            holder.ChartNumber,
            holder.ChartPos,
            side,
            pointType,
            moveAmount,
            targetMotionType,
            out error);
    }

    /// <summary>선택된 Scratch와 Long Scratch를 서로 전환합니다.</summary>
    public bool TryToggleScratchLongNote(
        GameObject noteObject,
        out string error)
    {
        if (!TryRequireEditingAvailable(out error) ||
            !TryRequireNoteType(
                noteObject,
                out NoteType sourceType,
                out error,
                NoteType.Scratch,
                NoteType.LongScratch) ||
            !ChartManager.TryGetNoteData(
                noteObject,
                out ChartHolder sourceHolder,
                out int sourceLine,
                out _,
                out _,
                out _))
        {
            error ??= "Selected Scratch note data could not be found.";
            return false;
        }

        NoteType targetType = sourceType == NoteType.Scratch
            ? NoteType.LongScratch
            : NoteType.Scratch;
        ScratchPointType sourcePointType =
            sourceHolder.GetScratchPointType(sourceLine);
        ScratchPointType targetPointType = targetType == NoteType.Scratch
            ? ScratchPointType.Tap
            : ChartManager.GetSuggestedScratchPointType(
                sourceLine,
                sourceHolder.AbsoluteChartPosition);
        ScratchMotionData sourceMotion =
            sourceHolder.GetScratchMotion(sourceLine);
        ScratchMotionType targetMotionType;

        if (targetType == NoteType.Scratch)
        {
            targetMotionType = sourceMotion.MotionType ==
                ScratchMotionType.None
                    ? ScratchMotionType.None
                    : ScratchMotionType.Instant;
        }
        else if (targetPointType == ScratchPointType.End &&
                 sourceMotion.MotionType != ScratchMotionType.None)
        {
            targetMotionType = ScratchMotionType.Release;
        }
        else
        {
            targetMotionType = sourceMotion.MotionType ==
                ScratchMotionType.Release
                    ? ScratchMotionType.Gradual
                    : sourceMotion.MotionType;
        }

        int moveAmount = targetMotionType != ScratchMotionType.None &&
            sourceMotion.MoveAmount <= 0
                ? ScratchMotionData.DefaultMoveAmount
                : sourceMotion.MoveAmount;
        ScratchMotionData targetMotion =
            ScratchMotionRules.NormalizeMotion(
                targetType,
                targetPointType,
                new ScratchMotionData(moveAmount, targetMotionType));
        GameObject targetPrefab = GetNotePrefab(targetType);

        if (!targetPrefab)
        {
            error = $"No prefab is configured for {targetType}.";
            return false;
        }

        Vector3 notePosition = new Vector3(
            GetStoredLineX(sourceLine),
            sourceHolder.WorldY,
            0f);
        NoteHandleType side = sourceLine == -2
            ? NoteHandleType.Right
            : NoteHandleType.Left;
        GameObject[] replacementObjects = CreateNoteObjects(
            targetPrefab,
            notePosition,
            side,
            targetType);
        ChartEditHistory.ChartEditTransaction editTransaction =
            ChartEditHistory.BeginChange(
                sourceHolder.AbsoluteChartPosition);

        if (!sourceHolder.TryDetachNote(
                noteObject,
                out int detachedLine,
                out NoteType detachedType,
                out GameObject[] previousObjects,
                out NoteHandleType detachedHandle,
                out bool detachedPowered,
                out int detachedAirValue,
                out ScratchMotionData detachedMotion))
        {
            DestroyNoteObjects(replacementObjects);
            error = "Selected Scratch note could not be detached.";
            return false;
        }

        if (!sourceHolder.AddNote(
                sourceLine,
                targetType,
                replacementObjects,
                null,
                targetMotion.MotionType != ScratchMotionType.None,
                scratchMotion: targetMotion,
                scratchPointType: targetPointType))
        {
            sourceHolder.AddNote(
                detachedLine,
                detachedType,
                previousObjects,
                detachedHandle,
                detachedPowered,
                detachedAirValue,
                detachedMotion,
                sourcePointType);
            DestroyNoteObjects(replacementObjects);
            error = "Scratch note type could not be updated.";
            return false;
        }

        selectionController?.ClearSelection();
        DestroyNoteObjects(previousObjects);
        ChartManager.RefreshLongNoteLengths(sourceLine);
        ChartManager.NotifyChartChanged();
        ChartEditHistory.CommitChange(editTransaction);
        selectionController?.SelectNoteObject(replacementObjects[0]);
        error = null;
        return true;
    }

    private static ScratchMotionType GetDefaultPoweredScratchMotion(
        NoteType noteType,
        ScratchPointType pointType)
    {
        if (noteType == NoteType.Scratch)
        {
            return ScratchMotionType.Instant;
        }

        return pointType == ScratchPointType.End
            ? ScratchMotionType.Release
            : ScratchMotionType.Gradual;
    }

    /// <summary>Scratch의 위치, 방향, 지점과 이동 명령을 수정합니다.</summary>
    public bool TryEditScratchNote(
        GameObject noteObject,
        int measure,
        int measurePosition,
        NoteHandleType side,
        ScratchPointType pointType,
        int moveAmount,
        ScratchMotionType motionType,
        out string error)
    {
        if (!TryRequireNoteType(
                noteObject,
                out NoteType noteType,
                out error,
                NoteType.Scratch,
                NoteType.LongScratch))
        {
            return false;
        }

        if (!System.Enum.IsDefined(typeof(ScratchMotionType), motionType))
        {
            error = $"Unsupported Scratch motion type: {motionType}.";
            return false;
        }

        if (moveAmount < 0 ||
            moveAmount > ScratchMotionData.MaximumMoveAmount)
        {
            error = $"Scratch move amount must be between 0 and " +
                $"{ScratchMotionData.MaximumMoveAmount}.";
            return false;
        }

        if ((noteType == NoteType.Scratch &&
             pointType != ScratchPointType.Tap) ||
            (noteType == NoteType.LongScratch &&
             pointType == ScratchPointType.Tap))
        {
            error = noteType == NoteType.Scratch
                ? "Single Scratch requires a Tap point."
                : "Long Scratch requires a Start, Mid, or End point.";
            return false;
        }

        if (motionType == ScratchMotionType.Release &&
            !ScratchMotionRules.IsReleaseAllowed(noteType, pointType))
        {
            error = "Release is only available on Long Scratch Mid or End.";
            return false;
        }

        int line = side == NoteHandleType.Right ? -2 : -1;
        ScratchMotionData effectiveMotion =
            ScratchMotionRules.NormalizeMotion(
                noteType,
                pointType,
                new ScratchMotionData(moveAmount, motionType));
        return TryApplyNoteEdit(
            noteObject,
            noteType,
            measure,
            measurePosition,
            line,
            side,
            effectiveMotion.MotionType != ScratchMotionType.None,
            0,
            effectiveMotion,
            pointType,
            out error);
    }

    /// <summary>Air의 위치, 메인 라인과 1~99 값을 수정합니다.</summary>
    public bool TryEditAirNote(
        GameObject noteObject,
        int measure,
        int measurePosition,
        int line,
        int airValue,
        out string error)
    {
        if (!TryRequireNoteType(
                noteObject,
                out NoteType noteType,
                out error,
                NoteType.Air))
        {
            return false;
        }

        NoteHandleType handleType = line <= 2
            ? NoteHandleType.Left
            : NoteHandleType.Right;
        return TryApplyNoteEdit(
            noteObject,
            noteType,
            measure,
            measurePosition,
            line,
            handleType,
            false,
            airValue,
            null,
            null,
            out error);
    }

    /// <summary>Camera Note의 위치, X 오프셋과 L/N/R 회전을 수정합니다.</summary>
    public bool TryEditCameraNote(
        GameObject noteObject,
        int measure,
        int measurePosition,
        float offsetX,
        ChartCameraSpinDirection spinDirection,
        out string error)
    {
        if (!TryRequireEditingAvailable(out error) ||
            !TryRequireNoteType(
                noteObject,
                out _,
                out error,
                NoteType.Camera) ||
            !TryGetAbsolutePosition(
                measure,
                measurePosition,
                out int targetAbsolutePosition,
                out error) ||
            !ChartManager.TryGetNoteData(
                noteObject,
                out ChartHolder sourceHolder,
                out _,
                out _,
                out _,
                out _))
        {
            error ??= "Selected Camera Note data could not be found.";
            return false;
        }

        if (!float.IsFinite(offsetX))
        {
            error = "Camera Offset X must be finite.";
            return false;
        }

        if (!System.Enum.IsDefined(
                typeof(ChartCameraSpinDirection),
                spinDirection))
        {
            error = $"Unsupported Camera spin direction: {spinDirection}.";
            return false;
        }

        bool isUnchanged =
            sourceHolder.AbsoluteChartPosition == targetAbsolutePosition &&
            sourceHolder.cameraOffsetX == offsetX &&
            sourceHolder.cameraSpinDirection == spinDirection;

        if (isUnchanged)
        {
            error = null;
            return true;
        }

        ChartEditHistory.ChartEditTransaction editTransaction =
            ChartEditHistory.BeginChange(
                sourceHolder.AbsoluteChartPosition,
                targetAbsolutePosition);

        if (!ChartManager.EditCameraNote(
                noteObject,
                targetAbsolutePosition,
                offsetX,
                spinDirection,
                out error))
        {
            return false;
        }

        UpdateEditedNoteObjects(
            noteObject,
            targetAbsolutePosition,
            0,
            NoteHandleType.Left);
        ChartEditHistory.CommitChange(editTransaction);
        selectionController?.NotifySelectionChanged();
        return true;
    }

    /// <summary>Effect definition, parameters and document metadata form one undoable edit.</summary>
    public bool TryEditEffectNote(
        GameObject noteObject, int measure, int measurePosition,
        string effectTypeId, string commandId, int order, string parametersJson,
        string musicId, string difficultyId, string gimmickId,
        out string error)
    {
        if (!TryRequireEditingAvailable(out error) ||
            !TryGetAbsolutePosition(measure, measurePosition,
                out int targetPosition, out error) ||
            !ChartManager.TryGetNoteData(noteObject, out ChartHolder source,
                out _, out NoteType noteType, out _, out _) ||
            noteType != NoteType.Effect)
        {
            error ??= "Selected Effect note could not be found.";
            return false;
        }

        string normalizedMusicId = musicId?.Trim();
        string normalizedDifficultyId = difficultyId?.Trim();
        try
        {
            ChartEffectFileStore.ValidateFileId(normalizedMusicId,
                nameof(musicId));
            ChartEffectFileStore.ValidateFileId(normalizedDifficultyId,
                nameof(difficultyId));
        }
        catch (System.FormatException exception)
        {
            error = exception.Message;
            return false;
        }

        if (order < 0)
        {
            error = "Effect order must be zero or greater.";
            return false;
        }

        var effectRegistry = EffectRegistry.CreateDefault();
        if (!ChartEffectJsonCodec.TryDecode(effectTypeId, commandId, gimmickId,
                parametersJson, effectRegistry, out _, out error))
        {
            return false;
        }

        if (!string.Equals(gimmickId ?? string.Empty,
                ChartEffectDocumentState.GimmickId ?? string.Empty,
                System.StringComparison.Ordinal))
        {
            foreach (ChartHolder holder in ChartManager.ChartHolders)
            {
                if (holder == null || holder == source || !holder.isEffect ||
                    holder.effectTypeId != CallMusicGimmickEffect.TypeId)
                    continue;
                if (!ChartEffectJsonCodec.TryDecode(holder.effectTypeId,
                        holder.effectCommandId, gimmickId,
                        holder.effectParametersJson, effectRegistry,
                        out _, out string relatedError))
                {
                    error = $"Gimmick change would invalidate Effect " +
                        $"'{holder.effectId}': {relatedError}";
                    return false;
                }
            }
        }

        ChartEditHistory.ChartEditTransaction transaction =
            ChartEditHistory.BeginChange(source.AbsoluteChartPosition, targetPosition);
        if (!ChartManager.MoveEffectNote(noteObject, targetPosition, out error))
        {
            return false;
        }

        ChartHolder target = ChartManager.GetHolder(targetPosition);
        target.EnsureEffectIdentity();
        target.effectTypeId = effectTypeId;
        target.effectCommandId = commandId ?? string.Empty;
        target.effectOrder = order;
        target.effectParametersJson = parametersJson;
        ChartEffectDocumentState.MusicId = normalizedMusicId;
        ChartEffectDocumentState.DifficultyId = normalizedDifficultyId;
        ChartEffectDocumentState.GimmickId = gimmickId ?? string.Empty;
        UpdateEditedNoteObjects(noteObject, targetPosition, 0, NoteHandleType.Left);
        ChartManager.NotifyChartChanged();
        ChartEditHistory.CommitChange(transaction);
        selectionController?.NotifySelectionChanged();
        error = null;
        return true;
    }

    /// <summary>Copy the last applied Effect into an empty time slot with a fresh ID.</summary>
    public bool TryDuplicateEffectNote(
        GameObject noteObject, int measure, int measurePosition, out string error)
    {
        if (!TryRequireEditingAvailable(out error) ||
            !TryGetAbsolutePosition(measure, measurePosition,
                out int targetPosition, out error) ||
            !ChartManager.TryGetNoteData(noteObject, out ChartHolder source,
                out _, out NoteType noteType, out _, out _) ||
            noteType != NoteType.Effect)
        {
            error ??= "Selected Effect note could not be found.";
            return false;
        }

        ChartHolder target = ChartManager.GetHolder(targetPosition);
        if (target != null && target.isEffect)
        {
            error = "Set Measure / Pos to an empty Effect position before copying.";
            return false;
        }

        GameObject prefab = GetNotePrefab(NoteType.Effect);
        if (!prefab)
        {
            error = "Effect preview prefab is missing.";
            return false;
        }

        ChartEditHistory.ChartEditTransaction transaction =
            ChartEditHistory.BeginChange(targetPosition);
        target ??= ChartManager.GetOrCreateHolder(measure, measurePosition);
        GameObject[] objects = CreateNoteObjects(prefab,
            new Vector3(0f, target.WorldY, 0f), NoteHandleType.Left, NoteType.Effect);
        if (!target.AddEffectNote(objects))
        {
            DestroyNoteObjects(objects);
            error = "Effect copy could not be placed.";
            return false;
        }

        ChartManager.CopyEffectDefinition(source, target, true);
        ChartManager.NotifyChartChanged();
        ChartEditHistory.CommitChange(transaction);
        error = null;
        return true;
    }

    private bool TryApplyNoteEdit(
        GameObject noteObject,
        NoteType noteType,
        int measure,
        int measurePosition,
        int line,
        NoteHandleType handleType,
        bool isPowered,
        int airValue,
        ScratchMotionData scratchMotion,
        ScratchPointType? scratchPointType,
        out string error)
    {
        if (!TryRequireEditingAvailable(out error) ||
            !TryGetAbsolutePosition(
                measure,
                measurePosition,
                out int targetAbsolutePosition,
                out error) ||
            !ChartManager.TryGetNoteData(
                noteObject,
                out ChartHolder sourceHolder,
                out int sourceLine,
                out _,
                out NoteHandleType sourceHandle,
                out bool sourcePowered))
        {
            error ??= "Selected note data could not be found.";
            return false;
        }

        int sourceAirValue = noteType == NoteType.Air
            ? sourceHolder.airNoteValues[sourceLine - 1]
            : 0;
        ScratchMotionData sourceScratchMotion = noteType.IsScratch()
            ? sourceHolder.GetScratchMotion(sourceLine)
            : null;
        ScratchPointType sourceScratchPointType = noteType.IsScratch()
            ? sourceHolder.GetScratchPointType(sourceLine)
            : ScratchPointType.Tap;
        bool isUnchanged =
            sourceHolder.AbsoluteChartPosition == targetAbsolutePosition &&
            sourceLine == line &&
            (noteType.IsScratch() || sourceHandle == handleType) &&
            (noteType.IsScratch() || sourcePowered == isPowered) &&
            (noteType != NoteType.Air || sourceAirValue == airValue) &&
            (!noteType.IsScratch() ||
             (sourceScratchMotion.Equals(scratchMotion) &&
              sourceScratchPointType == scratchPointType));

        if (isUnchanged)
        {
            error = null;
            return true;
        }

        ChartEditHistory.ChartEditTransaction editTransaction =
            ChartEditHistory.BeginChange(
                sourceHolder.AbsoluteChartPosition,
                targetAbsolutePosition);

        if (!ChartManager.EditNote(
                noteObject,
                targetAbsolutePosition,
                line,
                handleType,
                isPowered,
                airValue,
                scratchMotion,
                scratchPointType,
                out error))
        {
            return false;
        }

        UpdateEditedNoteObjects(
            noteObject,
            targetAbsolutePosition,
            line,
            handleType);
        ChartManager.NotifyChartChanged();
        ChartEditHistory.CommitChange(editTransaction);
        selectionController?.NotifySelectionChanged();
        return true;
    }

    private void UpdateEditedNoteObjects(
        GameObject noteObject,
        int absolutePosition,
        int line,
        NoteHandleType handleType)
    {
        if (!ChartManager.TryGetNoteData(
                noteObject,
                out ChartHolder holder,
                out _,
                out NoteType noteType,
                out _,
                out _))
        {
            return;
        }

        GameObject[] noteObjects;

        if (noteType == NoteType.Camera)
        {
            holder.TryGetCameraNote(out noteObjects);
        }
        else if (noteType == NoteType.Effect)
        {
            holder.TryGetEffectNote(out noteObjects);
        }
        else if (noteType == NoteType.Air)
        {
            holder.TryGetAirNote(line, out _, out noteObjects);
        }
        else
        {
            holder.TryGetNote(line, out _, out noteObjects);
        }

        if (noteObjects == null)
        {
            return;
        }

        Vector3 localPosition = new Vector3(
            noteType.IsChartEvent() ? 0f : GetStoredLineX(line),
            ChartHolder.AbsolutePositionToWorldY(absolutePosition),
            0f);
        Transform handField = handleType == NoteHandleType.Right
            ? rightNoteField
            : leftNoteField;

        for (int i = 0; i < noteObjects.Length; i++)
        {
            GameObject current = noteObjects[i];

            if (!current)
            {
                continue;
            }

            current.transform.SetParent(
                GetTargetNoteField(current.transform.parent, handField),
                false);
            current.transform.localPosition = localPosition;
        }
    }

    private static bool TryRequireNoteType(
        GameObject noteObject,
        out NoteType noteType,
        out string error,
        params NoteType[] allowedTypes)
    {
        if (!ChartManager.TryGetNoteData(
                noteObject,
                out _,
                out _,
                out noteType,
                out _,
                out _))
        {
            error = "Selected note data could not be found.";
            return false;
        }

        for (int i = 0; i < allowedTypes.Length; i++)
        {
            if (noteType == allowedTypes[i])
            {
                error = null;
                return true;
            }
        }

        error = $"{noteType} cannot be edited with this note editor.";
        return false;
    }

    private static bool TryGetAbsolutePosition(
        int measure,
        int measurePosition,
        out int absolutePosition,
        out string error)
    {
        absolutePosition = 0;

        if (measure < 0 ||
            measure > ChartHolder.MaximumMeasureNumber)
        {
            error = "Measure must be between 0 and 999.";
            return false;
        }

        if (measurePosition < 0 ||
            measurePosition >= ChartHolder.PositionUnitsPerMeasure)
        {
            error =
                $"Position must be between 0 and " +
                $"{ChartHolder.PositionUnitsPerMeasure - 1}.";
            return false;
        }

        long calculatedPosition =
            (long)measure * ChartHolder.PositionUnitsPerMeasure +
            measurePosition;
        long maximumPosition =
            ChartHolder.MaximumAbsolutePosition;

        if (calculatedPosition > maximumPosition)
        {
            error =
                $"The normalized measure would exceed " +
                $"{ChartHolder.MaximumMeasureNumber}.";
            return false;
        }

        absolutePosition = (int)calculatedPosition;
        error = null;
        return true;
    }

    /// <summary>파일에서 복원한 채보 데이터에 중앙·손 필드 노트 뷰를 생성합니다.</summary>
    public void RebuildChartViews(bool recreateExisting = false)
    {
        if (selectionController)
        {
            selectionController.ClearSelection();
        }

        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;

        if (recreateExisting)
        {
            for (int holderIndex = 0;
                 holderIndex < holders.Count;
                 holderIndex++)
            {
                holders[holderIndex].DestroyAllNoteObjects();
            }
        }

        for (int holderIndex = 0; holderIndex < holders.Count; holderIndex++)
        {
            ChartHolder holder = holders[holderIndex];
            holder.EnsureStorage();

            for (int noteIndex = 0;
                 noteIndex < ChartHolder.TotalLineCount;
                 noteIndex++)
            {
                NoteType noteType = holder.noteTypes[noteIndex];

                if (noteType == NoteType.Unknown)
                {
                    continue;
                }

                int line = GetLineFromStorageIndex(noteIndex);

                if (holder.TryGetNote(
                        line,
                        out _,
                        out GameObject[] existingObjects) &&
                    existingObjects != null)
                {
                    continue;
                }

                GameObject prefab = GetNotePrefab(noteType);

                if (!prefab)
                {
                    Debug.LogWarning(
                        $"Cannot rebuild {noteType}: prefab is missing.",
                        this);
                    continue;
                }

                NoteHandleType handleType = GetStoredHandleType(
                    holder,
                    noteIndex);
                Vector3 notePosition = new Vector3(
                    GetStoredLineX(line),
                    holder.WorldY,
                    0f);
                GameObject[] noteObjects = CreateNoteObjects(
                    prefab,
                    notePosition,
                    handleType,
                    noteType);

                if (!holder.AttachNoteObjects(line, noteObjects))
                {
                    DestroyNoteObjects(noteObjects);
                }
            }

            for (int airIndex = 0;
                 airIndex < ChartHolder.AirNoteCount;
                 airIndex++)
            {
                int line = airIndex + 1;

                if (holder.airNoteValues[airIndex] <= 0 ||
                    (holder.TryGetAirNote(
                        line,
                        out _,
                        out GameObject[] existingAirObjects) &&
                     existingAirObjects != null))
                {
                    continue;
                }

                if (!airNotePrefab)
                {
                    Debug.LogWarning(
                        "Cannot rebuild Air: prefab is missing.",
                        this);
                    continue;
                }

                NoteHandleType handleType = line <= 2
                    ? NoteHandleType.Left
                    : NoteHandleType.Right;
                Vector3 notePosition = new Vector3(
                    GetStoredLineX(line),
                    holder.WorldY,
                    0f);
                GameObject[] noteObjects = CreateNoteObjects(
                    airNotePrefab,
                    notePosition,
                    handleType,
                    NoteType.Air);

                if (!holder.AttachAirNoteObjects(line, noteObjects))
                {
                    DestroyNoteObjects(noteObjects);
                }
            }

            if (holder.isEffect &&
                (!holder.TryGetEffectNote(out GameObject[] existingEffectObjects) ||
                 existingEffectObjects == null))
            {
                GameObject effectPrefab = GetNotePrefab(NoteType.Effect);
                if (effectPrefab)
                {
                    GameObject[] effectObjects = CreateNoteObjects(
                        effectPrefab, new Vector3(0f, holder.WorldY, 0f),
                        NoteHandleType.Left, NoteType.Effect);
                    if (!holder.AttachEffectNoteObjects(effectObjects))
                    {
                        DestroyNoteObjects(effectObjects);
                    }
                }
            }

            if (holder.isCameraMove &&
                (!holder.TryGetCameraNote(
                    out GameObject[] existingCameraObjects) ||
                 existingCameraObjects == null))
            {
                GameObject cameraPrefab = GetNotePrefab(NoteType.Camera);

                if (!cameraPrefab)
                {
                    Debug.LogWarning(
                        "Cannot rebuild Camera Note: prefab is missing.",
                        this);
                    continue;
                }

                Vector3 cameraPosition = new Vector3(
                    0f,
                    holder.WorldY,
                    0f);
                GameObject[] cameraObjects = CreateNoteObjects(
                    cameraPrefab,
                    cameraPosition,
                    NoteHandleType.Left,
                    NoteType.Camera);

                if (!holder.AttachCameraNoteObjects(cameraObjects))
                {
                    DestroyNoteObjects(cameraObjects);
                }
            }

            if (holder.isMarker &&
                (!holder.TryGetMarkerNote(
                    out GameObject[] existingMarkerObjects) ||
                 existingMarkerObjects == null))
            {
                GameObject markerPrefab = GetNotePrefab(NoteType.Marker);

                if (!markerPrefab)
                {
                    Debug.LogWarning(
                        "Cannot rebuild Marker: prefab is missing.",
                        this);
                    continue;
                }

                Vector3 markerPosition = new Vector3(
                    0f,
                    holder.WorldY,
                    0f);
                GameObject[] markerObjects = CreateNoteObjects(
                    markerPrefab,
                    markerPosition,
                    NoteHandleType.Left,
                    NoteType.Marker);

                if (!holder.AttachMarkerNoteObjects(markerObjects))
                {
                    DestroyNoteObjects(markerObjects);
                }
            }
        }

        for (int line = 1; line <= ChartHolder.MainLineCount; line++)
        {
            ChartManager.RefreshLongNoteLengths(line);
        }

        ChartManager.RefreshLongNoteLengths(-1);
        ChartManager.RefreshLongNoteLengths(-2);
        ChartManager.NotifyChartChanged();
    }

    private GameObject[] CreateNoteObjects(
        GameObject prefab,
        Vector3 notePosition,
        NoteHandleType handleType,
        NoteType noteType)
    {
        if (noteType.IsChartEvent())
        {
            GameObject specialPrefab =
                GetChartPreviewNotePrefab(noteType) ??
                chartPreviewIdleNotePrefab ??
                prefab;
            GameObject specialNote = Instantiate(
                specialPrefab,
                specialNoteField,
                false);
            specialNote.transform.localPosition = new Vector3(
                0f,
                notePosition.y,
                notePosition.z);

            GameObject[] specialNoteObjects = { specialNote };
            InitializeSelectionTargets(specialNoteObjects, noteType);
            return specialNoteObjects;
        }

        // 편집 필드는 InGame 프리팹, 우측 Preview는 표시 전용 프리팹을 사용합니다.
        GameObject middleNote = Instantiate(prefab, middleNoteField, false);
        middleNote.transform.localPosition = notePosition;

        Transform handField = handleType == NoteHandleType.Left
            ? leftNoteField
            : rightNoteField;
        GameObject handNote = Instantiate(prefab, handField, false);
        handNote.transform.localPosition = notePosition;

        GameObject chartPreviewPrefab =
            GetChartPreviewNotePrefab(noteType) ?? prefab;
        GameObject chartPreviewNote = Instantiate(
            chartPreviewPrefab,
            chartPreviewNoteField,
            false);
        chartPreviewNote.transform.localPosition = notePosition;
        PrepareDisplayOnlyNote(chartPreviewNote);

        GameObject[] noteObjects =
            { middleNote, handNote, chartPreviewNote };
        GameObject[] selectableNoteObjects = { middleNote, handNote };
        InitializeSelectionTargets(selectableNoteObjects, noteType);
        return noteObjects;
    }

    private void InitializeSelectionTargets(
        GameObject[] noteObjects,
        NoteType noteType)
    {
        for (int i = 0; i < noteObjects.Length; i++)
        {
            GameObject noteObject = noteObjects[i];
            ChartNoteSelectable selectable =
                noteObject.GetComponent<ChartNoteSelectable>();

            if (!selectable)
            {
                selectable = noteObject.AddComponent<ChartNoteSelectable>();
            }

            NoteView noteView = noteObject.GetComponent<NoteView>();

            if (!noteView)
            {
                noteView = noteObject.AddComponent<NoteView>();
            }

            noteView.RefreshClickColliders();
            selectable.Configure(noteType, noteObjects);
            ApplyEditingVisualDisplayScale(noteObject);
        }
    }

    private void ApplyEditingVisualDisplayScale(float scale)
    {
        ApplyEditingVisualDisplayScale(previewNote, scale);

        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;

        for (int i = 0; i < holders.Count; i++)
        {
            ChartHolder holder = holders[i];
            holder.EnsureStorage();
            ApplyEditingVisualDisplayScale(
                holder.tapNoteObjectGroups,
                scale);
            ApplyEditingVisualDisplayScale(
                holder.scratchNoteObjectGroups,
                scale);
            ApplyEditingVisualDisplayScale(
                holder.airNoteObjectGroups,
                scale);
            ApplyEditingVisualDisplayScale(holder.actionNoteObjects, scale);
            ApplyEditingVisualDisplayScale(holder.cameraNoteObjects, scale);
            ApplyEditingVisualDisplayScale(holder.markerNoteObjects, scale);
        }
    }

    private void ApplyEditingVisualDisplayScale(GameObject noteObject)
    {
        ApplyEditingVisualDisplayScale(
            noteObject,
            GetVerticalDisplayScale());
    }

    private void ApplyEditingVisualDisplayScale(
        GameObject[][] noteObjectGroups,
        float scale)
    {
        if (noteObjectGroups == null)
        {
            return;
        }

        for (int i = 0; i < noteObjectGroups.Length; i++)
        {
            ApplyEditingVisualDisplayScale(noteObjectGroups[i], scale);
        }
    }

    private void ApplyEditingVisualDisplayScale(
        GameObject[] noteObjects,
        float scale)
    {
        if (noteObjects == null)
        {
            return;
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            ApplyEditingVisualDisplayScale(noteObjects[i], scale);
        }
    }

    private void ApplyEditingVisualDisplayScale(
        GameObject noteObject,
        float scale)
    {
        if (!noteObject || !IsEditingVisual(noteObject.transform))
        {
            return;
        }

        NoteView noteView = noteObject.GetComponent<NoteView>();

        if (!noteView)
        {
            noteView = noteObject.AddComponent<NoteView>();
        }

        noteView.SetVerticalDisplayScale(scale);
    }

    private bool IsEditingVisual(Transform target)
    {
        if (!target)
        {
            return false;
        }

        Transform parent = target.parent;
        return parent == previewField ||
            parent == leftNoteField ||
            parent == middleNoteField ||
            parent == rightNoteField ||
            parent == specialNoteField;
    }

    private void PrepareDisplayOnlyNote(GameObject noteObject)
    {
        if (noteObject.TryGetComponent(out NoteLength noteLength))
        {
            noteLength.SetRibbonColliderEnabled(false);
        }

        Transform[] childTransforms =
            noteObject.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < childTransforms.Length; i++)
        {
            childTransforms[i].gameObject.layer =
                chartPreviewNoteField.gameObject.layer;
        }

        Collider2D[] colliders2D =
            noteObject.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders2D.Length; i++)
        {
            colliders2D[i].enabled = false;
        }

        Collider[] colliders =
            noteObject.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        UnityEngine.UI.Graphic[] graphics =
            noteObject.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }

    private Transform GetTargetNoteField(
        Transform currentParent,
        Transform handField)
    {
        if (currentParent == middleNoteField)
        {
            return middleNoteField;
        }

        if (currentParent == specialNoteField)
        {
            return specialNoteField;
        }

        return currentParent == chartPreviewNoteField
            ? chartPreviewNoteField
            : handField;
    }

    private static void DestroyNoteObjects(GameObject[] noteObjects)
    {
        if (noteObjects == null)
        {
            return;
        }

        for (int i = 0; i < noteObjects.Length; i++)
        {
            if (noteObjects[i])
            {
                Destroy(noteObjects[i]);
            }
        }
    }

    private static int GetLineFromStorageIndex(int noteIndex)
    {
        return noteIndex < ChartHolder.MainLineCount
            ? noteIndex + 1
            : noteIndex == ChartHolder.MainLineCount
                ? -1
                : -2;
    }

    private static NoteHandleType GetStoredHandleType(
        ChartHolder holder,
        int noteIndex)
    {
        if (noteIndex >= ChartHolder.MainLineCount)
        {
            return noteIndex == ChartHolder.MainLineCount
                ? NoteHandleType.Left
                : NoteHandleType.Right;
        }

        NoteHandleType storedHandle = holder.noteHandles[noteIndex];
        return storedHandle != NoteHandleType.Unknown
            ? storedHandle
            : noteIndex < ChartHolder.MainLineCount / 2
                ? NoteHandleType.Left
                : NoteHandleType.Right;
    }

    internal static float GetStoredLineX(int line)
    {
        float normalizedX = line switch
        {
            >= 1 and <= 4 => TapXClampValues[line - 1],
            -1 => ScratchClampValues[0],
            -2 => ScratchClampValues[1],
            _ => 0f
        };

        return Mathf.Lerp(
            PreviewMinX,
            PreviewMaxX,
            Mathf.InverseLerp(
                NormalizedMinX,
                NormalizedMaxX,
                normalizedX));
    }

    private static Vector3 ToNoteFieldPosition(
        Vector3 previewPosition,
        bool? positionCorrection)
    {
        float x = positionCorrection switch
        {
            false => previewPosition.x + SidePositionCorrection,
            true => previewPosition.x - SidePositionCorrection,
            null => previewPosition.x
        };

        return new Vector3(x, previewPosition.y, previewPosition.z);
    }

    private static int GetLine(float x)
    {
        float normalizedX = Mathf.Lerp(
            NormalizedMinX,
            NormalizedMaxX,
            Mathf.InverseLerp(PreviewMinX, PreviewMaxX, x));
        int lineIndex = FindNearestClampIndex(
            normalizedX,
            TapXClampValues);

        return lineIndex + 1;
    }

    private static NoteHandleType GetHandleType(
        bool? positionCorrection,
        int line)
    {
        return positionCorrection switch
        {
            false => NoteHandleType.Left,
            true => NoteHandleType.Right,
            null => line <= 2
                ? NoteHandleType.Left
                : NoteHandleType.Right
        };
    }

    /// <summary>Scratch는 손 방향별 전용 슬롯 -1/-2에 저장합니다.</summary>
    private static int GetStorageLine(
        NoteType noteType,
        int mainLine,
        NoteHandleType handleType)
    {
        if (!noteType.IsScratch())
        {
            return mainLine;
        }

        return handleType switch
        {
            NoteHandleType.Left => -1,
            NoteHandleType.Right => -2,
            _ => throw new System.ArgumentOutOfRangeException(
                nameof(handleType),
                handleType,
                "Scratch placement requires a left or right handle.")
        };
    }

    /// <summary>현재 배치할 노트 종류와 미리보기 프리팹을 함께 변경합니다.</summary>
    public void SetCurrentNoteType(NoteType noteType)
    {
        if (!CanEditChart)
        {
            Debug.LogWarning(
                "Stop test playback before selecting a placement tool.",
                this);
            return;
        }

        GameObject prefab = GetNotePrefab(noteType);

        if (!prefab || !previewField)
        {
            Debug.LogWarning($"No preview prefab for note type {noteType}.", this);
            return;
        }

        CurrentTool = ToChartToolType(noteType);
        SetPreviewing(true);
        SetPreviewPrefab(
            prefab,
            noteType,
            IsPlacementImplemented(noteType));
    }

    /// <summary>Speed 버튼에서 특수 프리뷰를 선택합니다.</summary>
    public void SelectSpeedNote()
    {
        SetCurrentNoteType(NoteType.Speed);
    }

    /// <summary>Effect 버튼에서 특수 프리뷰를 선택합니다.</summary>
    public void SelectEffectNote()
    {
        SetCurrentNoteType(NoteType.Effect);
    }

    /// <summary>Camera 버튼에서 배치 가능한 특수 노트를 선택합니다.</summary>
    public void SelectCameraNote()
    {
        SetCurrentNoteType(NoteType.Camera);
    }

    /// <summary>Marker 버튼에서 값 없는 위치 이벤트를 선택합니다.</summary>
    public void SelectMarkerNote()
    {
        SetCurrentNoteType(NoteType.Marker);
    }

    /// <summary>선택한 ChartMaker 도구에 맞는 프리뷰를 표시합니다.</summary>
    public void SetCurrentTool(ChartToolType toolType)
    {
        if (toolType != ChartToolType.None && !CanEditChart)
        {
            Debug.LogWarning(
                "Stop test playback before selecting an editing tool.",
                this);
            return;
        }

        CancelNoteDrag();
        CurrentTool = toolType;

        switch (toolType)
        {
            case ChartToolType.SingleTap:
                SetPreviewing(true);
                SetPreviewPrefab(tapNotePrefab, NoteType.Tap, true);
                break;
            case ChartToolType.LongTap:
                SetPreviewing(true);
                SetPreviewPrefab(longTapNotePrefab, NoteType.LongTap, true);
                break;
            case ChartToolType.SingleScratch:
                SetPreviewing(true);
                SetPreviewPrefab(scratchNotePrefab, NoteType.Scratch, true);
                break;
            case ChartToolType.LongScratch:
                SetPreviewing(true);
                SetPreviewPrefab(
                    longScratchNotePrefab,
                    NoteType.LongScratch,
                    true);
                break;
            case ChartToolType.SingleAir:
                SetPreviewing(true);
                SetPreviewPrefab(airNotePrefab, NoteType.Air, true);
                break;
            case ChartToolType.Eraser:
                SetPreviewing(true);
                ClearPreview();
                break;
            case ChartToolType.None:
                if (selectionController)
                {
                    selectionController.ClearSelection();
                }

                SetPreviewing(false);
                ClearPreview();
                break;
            default:
                Debug.LogWarning($"Unsupported chart tool: {toolType}", this);
                SetPreviewing(false);
                ClearPreview();
                break;
        }
    }

    private void SetPreviewPrefab(
        GameObject prefab,
        NoteType noteType,
        bool canPlace)
    {
        ClearPreview();
        currentNoteType = noteType;
        canPlaceCurrentPreview = canPlace;

        if (canPlace && selectionController)
        {
            selectionController.ClearSelection();
        }

        Transform targetPreviewField = noteType.IsChartEvent()
            ? specialNoteField
            : previewField;

        if (!prefab || !targetPreviewField)
        {
            Debug.LogWarning($"No preview prefab for chart tool {CurrentTool}.", this);
            return;
        }

        previewNote = Instantiate(prefab, targetPreviewField, false);
        previewNote.transform.localPosition = GetCurrentPreviewPosition();
        ApplyEditingVisualDisplayScale(previewNote);

        if (noteType.IsLong() &&
            previewNote.TryGetComponent(out NoteLength noteLength))
        {
            noteLength.SetLength(0f);
            noteLength.SetUnlinkedPreviewVisible(true);
        }

        RefreshPreviewVisibility();
    }

    private void ClearPreview()
    {
        if (previewNote)
        {
            Destroy(previewNote);
        }

        previewNote = null;
        currentNoteType = NoteType.Unknown;
        canPlaceCurrentPreview = false;
    }

    private static ChartToolType ToChartToolType(NoteType noteType)
    {
        return noteType switch
        {
            NoteType.Tap => ChartToolType.SingleTap,
            NoteType.LongTap => ChartToolType.LongTap,
            NoteType.Scratch => ChartToolType.SingleScratch,
            NoteType.LongScratch => ChartToolType.LongScratch,
            NoteType.Air => ChartToolType.SingleAir,
            _ => ChartToolType.None
        };
    }

    private static bool IsPlacementImplemented(NoteType noteType)
    {
        return noteType == NoteType.Tap ||
            noteType == NoteType.LongTap ||
            noteType.IsScratch() ||
            noteType == NoteType.Air ||
            noteType == NoteType.Effect ||
            noteType == NoteType.Camera ||
            noteType == NoteType.Marker;
    }

    private bool TryRequireEditingAvailable(out string error)
    {
        if (CanEditChart)
        {
            error = null;
            return true;
        }

        error = "Stop test playback before editing the chart.";
        return false;
    }

    private void ResolveChartCore()
    {
        if (!chartCore)
        {
            chartCore = ChartCore.Instance != null
                ? ChartCore.Instance
                : FindFirstObjectByType<ChartCore>();
        }
    }

    /// <summary>노트 종류별 X축 스냅 좌표를 -1~1 범위로 설정합니다.</summary>
    public static void SetXClampValues(
        NoteType noteType,
        params float[] values)
    {
        if (values == null || values.Length == 0)
        {
            return;
        }

        float[] copiedValues = new float[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            if (!IsValidClampValue(values[i]))
            {
                return;
            }

            copiedValues[i] = values[i];
        }

        XClampValues[noteType] = copiedValues;
    }

    public static IReadOnlyList<float> GetXClampValues(NoteType noteType)
    {
        return XClampValues.TryGetValue(noteType, out float[] values)
            ? values
            : TapXClampValues;
    }

    /// <summary>한 마디를 나눌 Y축 가이드 개수를 갱신합니다.</summary>
    public static void SetYGuideCount(int guideCount)
    {
        YGuideCount = Mathf.Max(1, guideCount);
    }

    private Vector3 GetCurrentPreviewPosition()
    {
        return NormalizedToPreviewPosition(
            NormalizedPosition,
            currentNoteType,
            PositionCorrection);
    }

    private static Vector3 NormalizedToPreviewPosition(
        Vector2 normalizedPosition,
        NoteType noteType,
        bool? positionCorrection)
    {
        // 입력 영역을 기존 화면 높이로 변환하고 저장 시 240-pulse 좌표로 환산합니다.
        float normalizedX = Mathf.Lerp(
            NormalizedMinX,
            NormalizedMaxX,
            normalizedPosition.x);
        float clampedNormalizedX = ClampX(
            normalizedX,
            GetXClampValues(noteType));
        float x = Mathf.Lerp(
            PreviewMinX,
            PreviewMaxX,
            Mathf.InverseLerp(
                NormalizedMinX,
                NormalizedMaxX,
                clampedNormalizedX));
        float localY = Mathf.Clamp(
            normalizedPosition.y,
            0f,
            PreviewMaxNormalizedY) *
            (PreviewReferenceY / PreviewMaxNormalizedY) /
            GetVerticalDisplayScale();
        float chartY = GuideGenerate.ReferenceY + localY;
        float calculatedY = UseYClamp
            ? ClampChartYToGuide(chartY)
            : chartY;
        float appliedY = calculatedY;

        Vector3 ret = new Vector3(
            x,
            appliedY,
            0f);

        float appliedX = positionCorrection switch
        {
            true => ret.x + SidePositionCorrection,
            false => ret.x - SidePositionCorrection,
            null => ret.x
        };

        return new Vector3(appliedX, ret.y, ret.z);
    }

    /// <summary>
    /// 입력 영역의 0~1 좌표를 실제 Note Field에서 사용하는 로컬 좌표로 변환합니다.
    /// </summary>
    internal static Vector2 NormalizedToNoteFieldPosition(
        Vector2 normalizedPosition)
    {
        float x = Mathf.Lerp(
            PreviewMinX,
            PreviewMaxX,
            Mathf.Clamp01(normalizedPosition.x));
        float localY = Mathf.Clamp(
            normalizedPosition.y,
            0f,
            PreviewMaxNormalizedY) *
            (PreviewReferenceY / PreviewMaxNormalizedY) /
            GetVerticalDisplayScale();

        return new Vector2(
            x,
            GuideGenerate.ReferenceY + localY);
    }

    private static float GetVerticalDisplayScale()
    {
        GuideGenerate guideGenerate = GuideGenerate.Instance;
        return guideGenerate
            ? guideGenerate.VerticalDisplayScale
            : 1f;
    }

    /// <summary>
    /// 입력 좌표에서 노트의 스냅된 로컬 좌표, 저장 라인과 손 방향을 함께 계산합니다.
    /// </summary>
    private static void GetNotePlacement(
        Vector2 normalizedPosition,
        NoteType noteType,
        bool? positionCorrection,
        out Vector3 notePosition,
        out int line,
        out NoteHandleType handleType)
    {
        Vector3 previewPosition = NormalizedToPreviewPosition(
            normalizedPosition,
            noteType,
            positionCorrection);
        notePosition = ToNoteFieldPosition(
            previewPosition,
            positionCorrection);
        int absolutePosition = Mathf.Max(
            0,
            ChartHolder.WorldYToAbsolutePosition(notePosition.y));
        notePosition.y =
            ChartHolder.AbsolutePositionToWorldY(absolutePosition);
        int mainLine = GetLine(notePosition.x);
        handleType = GetHandleType(positionCorrection, mainLine);
        line = GetStorageLine(noteType, mainLine, handleType);
    }

    private static float ClampChartYToGuide(float chartY)
    {
        int guideIndex = Mathf.Max(
            0,
            Mathf.RoundToInt(
                chartY /
                ChartHolder.WorldUnitsPerMeasure *
                YGuideCount));
        int absolutePosition =
            ChartHolder.GridIndexToAbsolutePosition(
                guideIndex,
                YGuideCount);

        return ChartHolder.AbsolutePositionToWorldY(absolutePosition);
    }

    private static float ClampX(
        float value,
        IReadOnlyList<float> clampValues)
    {
        if (clampValues == null || clampValues.Count == 0)
        {
            return Mathf.Clamp(value, NormalizedMinX, NormalizedMaxX);
        }

        return clampValues[FindNearestClampIndex(value, clampValues)];
    }

    private static int FindNearestClampIndex(
        float value,
        IReadOnlyList<float> clampValues)
    {
        int nearestIndex = 0;
        float nearestDistance = Mathf.Abs(value - clampValues[0]);

        for (int i = 1; i < clampValues.Count; i++)
        {
            float distance = Mathf.Abs(value - clampValues[i]);

            if (distance < nearestDistance)
            {
                nearestIndex = i;
                nearestDistance = distance;
            }
        }

        return nearestIndex;
    }

    private static bool IsValidClampValue(float value)
    {
        return value >= NormalizedMinX &&
            value <= NormalizedMaxX &&
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    private GameObject GetNotePrefab(NoteType noteType)
    {
        return noteType switch
        {
            NoteType.Tap => tapNotePrefab,
            NoteType.LongTap => longTapNotePrefab,
            NoteType.Scratch => scratchNotePrefab,
            NoteType.LongScratch => longScratchNotePrefab,
            NoteType.Air => airNotePrefab,
            NoteType.Speed => chartPreviewSpeedNotePrefab,
            NoteType.Effect => chartPreviewEffectNotePrefab,
            NoteType.Camera => chartPreviewCameraNotePrefab,
            NoteType.Marker => chartPreviewMarkerNotePrefab
                ? chartPreviewMarkerNotePrefab
                : chartPreviewIdleNotePrefab,
            _ => null
        };
    }

    private GameObject GetChartPreviewNotePrefab(NoteType noteType)
    {
        return noteType switch
        {
            NoteType.Tap => chartPreviewTapNotePrefab,
            NoteType.LongTap => chartPreviewLongTapNotePrefab,
            NoteType.Scratch => chartPreviewScratchNotePrefab,
            NoteType.LongScratch => chartPreviewLongScratchNotePrefab,
            NoteType.Air => chartPreviewAirNotePrefab,
            NoteType.Speed => chartPreviewSpeedNotePrefab,
            NoteType.Effect => chartPreviewEffectNotePrefab,
            NoteType.Camera => chartPreviewCameraNotePrefab,
            NoteType.Marker => chartPreviewMarkerNotePrefab
                ? chartPreviewMarkerNotePrefab
                : chartPreviewIdleNotePrefab,
            NoteType.Unknown => chartPreviewIdleNotePrefab,
            _ => null
        };
    }
}

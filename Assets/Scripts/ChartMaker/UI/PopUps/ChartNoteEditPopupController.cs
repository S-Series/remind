using System.Collections.Generic;
using REmind.Data;
using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public sealed class ChartNoteEditPopupController : MonoBehaviour
{
    private const string WindowPositionXKey =
        "REmind.ChartMaker.NoteEditWindow.CenterOffsetX.v4";
    private const string WindowPositionYKey =
        "REmind.ChartMaker.NoteEditWindow.CenterOffsetY.v4";
    private const float MinimumWindowTop = 26f;
    private static readonly Vector2 DefaultWindowCenterOffset =
        new Vector2(623f, 0f);

    private ChartNoteSelectionController selectionController;
    private ChartPlacementController placementController;
    private GameObject selectedNoteObject;
    private NoteType selectedNoteType = NoteType.Unknown;
    private VisualElement rootElement;
    private VisualElement editWindow;
    private VisualElement titleBar;
    private Label titleLabel;
    private IntegerField measureField;
    private IntegerField positionField;
    private VisualElement tapPanel;
    private DropdownField tapLineField;
    private DropdownField tapHandField;
    private Toggle tapPoweredField;
    private Label tapLongPairLabel;
    private VisualElement scratchPanel;
    private DropdownField scratchSideField;
    private Toggle scratchPoweredField;
    private IntegerField scratchStartOffsetField;
    private IntegerField scratchEndOffsetField;
    private DropdownField scratchMotionField;
    private Label scratchLongPairLabel;
    private VisualElement airPanel;
    private DropdownField airLineField;
    private IntegerField airValueField;
    private Label errorLabel;
    private Button closeButton;
    private Button deleteButton;
    private Button cancelButton;
    private Button applyButton;
    private bool isInitialized;
    private bool hasStoredPosition;
    private Vector2 rememberedCenterOffset = DefaultWindowCenterOffset;
    private bool isDraggingWindow;
    private bool isApplyingWindowPosition;
    private int dragPointerId = -1;
    private Vector2 dragStartPointerPosition;
    private Vector2 dragStartWindowPosition;

    /// <summary>상단 메뉴 문서 안의 노트 편집 창을 선택 시스템에 연결합니다.</summary>
    public void Initialize(
        VisualElement root,
        ChartNoteSelectionController selection,
        ChartPlacementController placement)
    {
        if (isInitialized || root == null || !selection || !placement)
        {
            return;
        }

        selectionController = selection;
        placementController = placement;
        rootElement = root;
        editWindow = root.Q<VisualElement>("note-edit-window");
        titleBar = root.Q<VisualElement>("note-edit-title-bar");
        titleLabel = root.Q<Label>("note-edit-title");
        measureField = root.Q<IntegerField>("note-measure-field");
        positionField = root.Q<IntegerField>("note-position-field");
        tapPanel = root.Q<VisualElement>("tap-note-edit-panel");
        tapLineField = root.Q<DropdownField>("tap-line-field");
        tapHandField = root.Q<DropdownField>("tap-hand-field");
        tapPoweredField = root.Q<Toggle>("tap-powered-field");
        tapLongPairLabel = root.Q<Label>("tap-long-pair-label");
        scratchPanel = root.Q<VisualElement>("scratch-note-edit-panel");
        scratchSideField = root.Q<DropdownField>("scratch-side-field");
        scratchPoweredField = root.Q<Toggle>("scratch-powered-field");
        scratchStartOffsetField =
            root.Q<IntegerField>("scratch-start-offset-field");
        scratchEndOffsetField =
            root.Q<IntegerField>("scratch-end-offset-field");
        scratchMotionField = root.Q<DropdownField>("scratch-motion-field");
        scratchLongPairLabel = root.Q<Label>("scratch-long-pair-label");
        airPanel = root.Q<VisualElement>("air-note-edit-panel");
        airLineField = root.Q<DropdownField>("air-line-field");
        airValueField = root.Q<IntegerField>("air-value-field");
        errorLabel = root.Q<Label>("note-edit-error");
        closeButton = root.Q<Button>("note-edit-close-button");
        deleteButton = root.Q<Button>("note-edit-delete-button");
        cancelButton = root.Q<Button>("note-edit-cancel-button");
        applyButton = root.Q<Button>("note-edit-apply-button");

        if (!HasRequiredElements())
        {
            Debug.LogError(
                "Chart note edit UXML is missing one or more elements.",
                this);
            return;
        }

        tapLineField.choices = CreateMainLineChoices();
        airLineField.choices = CreateMainLineChoices();
        tapHandField.choices = new List<string> { "Left", "Right" };
        scratchSideField.choices = new List<string> { "Left", "Right" };
        scratchMotionField.choices =
            new List<string> { "Instant", "Gradual" };
        applyButton.SetEnabled(false);

        closeButton.clicked += HandleCloseRequested;
        cancelButton.clicked += HandleCloseRequested;
        deleteButton.clicked += HandleDeleteRequested;
        applyButton.clicked += HandleApplyRequested;
        titleBar.RegisterCallback<PointerDownEvent>(HandleTitlePointerDown);
        titleBar.RegisterCallback<PointerMoveEvent>(HandleTitlePointerMove);
        titleBar.RegisterCallback<PointerUpEvent>(HandleTitlePointerUp);
        titleBar.RegisterCallback<PointerCaptureOutEvent>(
            HandleTitlePointerCaptureOut);
        rootElement.RegisterCallback<GeometryChangedEvent>(
            HandleRootGeometryChanged);
        editWindow.RegisterCallback<GeometryChangedEvent>(
            HandleWindowGeometryChanged);
        selectionController.SelectionChanged += HandleSelectionChanged;
        isInitialized = true;
        RestoreWindowPosition();
        Hide();
        HandleSelectionChanged(selectionController.SelectedNoteObjects);
    }

    private void OnDestroy()
    {
        if (!isInitialized)
        {
            return;
        }

        closeButton.clicked -= HandleCloseRequested;
        cancelButton.clicked -= HandleCloseRequested;
        deleteButton.clicked -= HandleDeleteRequested;
        applyButton.clicked -= HandleApplyRequested;
        titleBar.UnregisterCallback<PointerDownEvent>(HandleTitlePointerDown);
        titleBar.UnregisterCallback<PointerMoveEvent>(HandleTitlePointerMove);
        titleBar.UnregisterCallback<PointerUpEvent>(HandleTitlePointerUp);
        titleBar.UnregisterCallback<PointerCaptureOutEvent>(
            HandleTitlePointerCaptureOut);
        rootElement.UnregisterCallback<GeometryChangedEvent>(
            HandleRootGeometryChanged);
        editWindow.UnregisterCallback<GeometryChangedEvent>(
            HandleWindowGeometryChanged);

        if (selectionController)
        {
            selectionController.SelectionChanged -= HandleSelectionChanged;
        }
    }

    private void HandleSelectionChanged(
        IReadOnlyList<GameObject> selectedObjects)
    {
        GameObject selectedObject = GetFirstValidObject(selectedObjects);

        if (!selectedObject ||
            !ChartManager.TryGetNoteData(
                selectedObject,
                out ChartHolder holder,
                out int line,
                out NoteType noteType,
                out NoteHandleType handleType,
                out bool isPowered))
        {
            Hide();
            return;
        }

        selectedNoteObject = selectedObject;
        selectedNoteType = noteType;
        SetError(null);

        PopulateCommonFields(holder, noteType);
        ShowTypePanel(noteType);

        if (noteType is NoteType.Tap or NoteType.LongTap)
        {
            tapLineField.SetValueWithoutNotify(line.ToString());
            tapHandField.SetValueWithoutNotify(
                handleType == NoteHandleType.Right ? "Right" : "Left");
            tapPoweredField.SetValueWithoutNotify(isPowered);
            tapPoweredField.SetEnabled(noteType != NoteType.LongTap);
            tapLongPairLabel.style.display = noteType == NoteType.LongTap
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
        else if (noteType is NoteType.Scratch or NoteType.LongScratch)
        {
            scratchSideField.SetValueWithoutNotify(
                line == -2 ? "Right" : "Left");
            scratchPoweredField.SetValueWithoutNotify(isPowered);
            ScratchMotionData scratchMotion = holder.GetScratchMotion(line);
            scratchStartOffsetField.SetValueWithoutNotify(
                scratchMotion.StartOffsetUnits);
            scratchEndOffsetField.SetValueWithoutNotify(
                scratchMotion.EndOffsetUnits);
            scratchMotionField.SetValueWithoutNotify(
                scratchMotion.MotionType.ToString());
            scratchMotionField.SetEnabled(noteType == NoteType.LongScratch);
            scratchLongPairLabel.style.display =
                noteType == NoteType.LongScratch
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }
        else if (noteType == NoteType.Air)
        {
            airLineField.SetValueWithoutNotify(line.ToString());
            airValueField.SetValueWithoutNotify(
                holder.airNoteValues[line - 1]);
        }

        applyButton.SetEnabled(noteType.IsGameplayNote());
        editWindow.style.display = DisplayStyle.Flex;
        editWindow.schedule.Execute(ApplyRememberedWindowPosition);
    }

    private void HandleTitlePointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || IsCloseButtonTarget(evt.target))
        {
            return;
        }

        Vector2 currentPosition = GetWindowPositionInRoot();
        SetWindowPosition(currentPosition);
        dragStartPointerPosition = (Vector2)evt.position;
        dragStartWindowPosition = currentPosition;
        dragPointerId = evt.pointerId;
        isDraggingWindow = true;
        titleBar.CapturePointer(dragPointerId);
        evt.StopPropagation();
    }

    private void HandleTitlePointerMove(PointerMoveEvent evt)
    {
        if (!isDraggingWindow || evt.pointerId != dragPointerId)
        {
            return;
        }

        Vector2 position = dragStartWindowPosition +
            ((Vector2)evt.position - dragStartPointerPosition);
        SetWindowPosition(ClampWindowPosition(position));
        evt.StopPropagation();
    }

    private void HandleTitlePointerUp(PointerUpEvent evt)
    {
        if (!isDraggingWindow || evt.pointerId != dragPointerId)
        {
            return;
        }

        EndWindowDrag(savePosition: true);
        evt.StopPropagation();
    }

    private void HandleTitlePointerCaptureOut(PointerCaptureOutEvent evt)
    {
        if (isDraggingWindow && evt.pointerId == dragPointerId)
        {
            EndWindowDrag(savePosition: true);
        }
    }

    private void HandleRootGeometryChanged(GeometryChangedEvent _)
    {
        if (!isDraggingWindow)
        {
            editWindow?.schedule.Execute(ApplyRememberedWindowPosition);
        }
    }

    private void HandleWindowGeometryChanged(GeometryChangedEvent evt)
    {
        if (isDraggingWindow || isApplyingWindowPosition ||
            evt.newRect.size == evt.oldRect.size)
        {
            return;
        }

        editWindow.schedule.Execute(ApplyRememberedWindowPosition);
    }

    /// <summary>저장된 수정창 위치를 복원하고 현재 패널 안쪽으로 제한합니다.</summary>
    private void RestoreWindowPosition()
    {
        hasStoredPosition =
            PlayerPrefs.HasKey(WindowPositionXKey) &&
            PlayerPrefs.HasKey(WindowPositionYKey);

        rememberedCenterOffset = hasStoredPosition
            ? new Vector2(
                PlayerPrefs.GetFloat(WindowPositionXKey),
                PlayerPrefs.GetFloat(WindowPositionYKey))
            : DefaultWindowCenterOffset;
    }

    private void SaveWindowPosition()
    {
        Vector2 position = ClampWindowPosition(GetWindowPositionInRoot());
        SetWindowPosition(position);
        rememberedCenterOffset = ToCenterOffset(position);
        PlayerPrefs.SetFloat(
            WindowPositionXKey,
            rememberedCenterOffset.x);
        PlayerPrefs.SetFloat(
            WindowPositionYKey,
            rememberedCenterOffset.y);
        PlayerPrefs.Save();
        hasStoredPosition = true;
    }

    private void EndWindowDrag(bool savePosition)
    {
        if (!isDraggingWindow)
        {
            return;
        }

        int pointerId = dragPointerId;
        isDraggingWindow = false;
        dragPointerId = -1;

        if (titleBar.HasPointerCapture(pointerId))
        {
            titleBar.ReleasePointer(pointerId);
        }

        if (savePosition)
        {
            SaveWindowPosition();
        }
    }

    private void ClampWindowToRoot()
    {
        if (editWindow == null || rootElement == null ||
            editWindow.style.display == DisplayStyle.None)
        {
            return;
        }

        Vector2 position = ClampWindowPosition(GetWindowPositionInRoot());
        SetWindowPosition(position);

        if (hasStoredPosition)
        {
            rememberedCenterOffset = ToCenterOffset(position);
            PlayerPrefs.SetFloat(
                WindowPositionXKey,
                rememberedCenterOffset.x);
            PlayerPrefs.SetFloat(
                WindowPositionYKey,
                rememberedCenterOffset.y);
        }
    }

    /// <summary>화면 중앙 기준으로 기억한 오프셋을 UI Toolkit 좌상단 좌표에 적용합니다.</summary>
    private void ApplyRememberedWindowPosition()
    {
        if (editWindow == null || rootElement == null ||
            editWindow.style.display == DisplayStyle.None)
        {
            return;
        }

        if (!TryGetLayoutSize(rootElement, out _) ||
            !TryGetLayoutSize(editWindow, out _))
        {
            editWindow.schedule.Execute(
                ApplyRememberedWindowPosition).ExecuteLater(1);
            return;
        }

        isApplyingWindowPosition = true;

        try
        {
            Vector2 position = FromCenterOffset(rememberedCenterOffset);
            SetWindowPosition(ClampWindowPosition(position));
        }
        finally
        {
            isApplyingWindowPosition = false;
        }
    }

    private Vector2 FromCenterOffset(Vector2 centerOffset)
    {
        TryGetLayoutSize(rootElement, out Vector2 rootSize);
        TryGetLayoutSize(editWindow, out Vector2 windowSize);

        return new Vector2(
            rootSize.x * 0.5f + centerOffset.x - windowSize.x * 0.5f,
            rootSize.y * 0.5f - centerOffset.y - windowSize.y * 0.5f);
    }

    private Vector2 ToCenterOffset(Vector2 windowPosition)
    {
        TryGetLayoutSize(rootElement, out Vector2 rootSize);
        TryGetLayoutSize(editWindow, out Vector2 windowSize);

        return new Vector2(
            windowPosition.x + windowSize.x * 0.5f - rootSize.x * 0.5f,
            rootSize.y * 0.5f -
            (windowPosition.y + windowSize.y * 0.5f));
    }

    private static bool TryGetLayoutSize(
        VisualElement element,
        out Vector2 size)
    {
        size = element != null
            ? new Vector2(element.layout.width, element.layout.height)
            : Vector2.zero;

        return float.IsFinite(size.x) &&
            float.IsFinite(size.y) &&
            size.x > 0f &&
            size.y > 0f;
    }

    private Vector2 ClampWindowPosition(Vector2 position)
    {
        float rootWidth = rootElement.resolvedStyle.width;
        float rootHeight = rootElement.resolvedStyle.height;
        float windowWidth = editWindow.resolvedStyle.width;
        float windowHeight = editWindow.resolvedStyle.height;

        if (!float.IsFinite(rootWidth) || !float.IsFinite(rootHeight) ||
            !float.IsFinite(windowWidth) || !float.IsFinite(windowHeight))
        {
            return position;
        }

        float maxX = Mathf.Max(0f, rootWidth - windowWidth);
        float maxY = Mathf.Max(MinimumWindowTop, rootHeight - windowHeight);
        return new Vector2(
            Mathf.Clamp(position.x, 0f, maxX),
            Mathf.Clamp(position.y, MinimumWindowTop, maxY));
    }

    private Vector2 GetWindowPositionInRoot()
    {
        return rootElement.WorldToLocal(editWindow.worldBound.position);
    }

    private void SetWindowPosition(Vector2 position)
    {
        editWindow.style.left = position.x;
        editWindow.style.top = position.y;
        editWindow.style.right = StyleKeyword.Auto;
    }

    private bool IsCloseButtonTarget(IEventHandler target)
    {
        VisualElement targetElement = target as VisualElement;
        return targetElement != null &&
            (targetElement == closeButton || closeButton.Contains(targetElement));
    }

    private void PopulateCommonFields(
        ChartHolder holder,
        NoteType noteType)
    {
        titleLabel.text = GetWindowTitle(noteType);
        measureField.SetValueWithoutNotify(holder.ChartNumber);
        positionField.SetValueWithoutNotify(holder.ChartPos);
    }

    private void ShowTypePanel(NoteType noteType)
    {
        tapPanel.style.display =
            noteType is NoteType.Tap or NoteType.LongTap
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        scratchPanel.style.display =
            noteType is NoteType.Scratch or NoteType.LongScratch
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        airPanel.style.display = noteType == NoteType.Air
            ? DisplayStyle.Flex
            : DisplayStyle.None;

    }

    private void HandleCloseRequested()
    {
        selectionController?.ClearSelection();
        Hide();
    }

    private void HandleDeleteRequested()
    {
        selectionController?.DeleteSelection();
    }

    /// <summary>현재 노트 종류에 맞는 전용 편집 함수를 호출합니다.</summary>
    private void HandleApplyRequested()
    {
        if (!selectedNoteObject || !placementController)
        {
            SetError("No note is selected.");
            return;
        }

        bool succeeded;
        string error;

        switch (selectedNoteType)
        {
            case NoteType.Tap:
            case NoteType.LongTap:
                succeeded = TryApplyTapEdit(out error);
                break;
            case NoteType.Scratch:
            case NoteType.LongScratch:
                succeeded = TryApplyScratchEdit(out error);
                break;
            case NoteType.Air:
                succeeded = TryApplyAirEdit(out error);
                break;
            default:
                succeeded = false;
                error = $"{selectedNoteType} editing is not supported.";
                break;
        }

        SetError(succeeded ? null : error);
    }

    private bool TryApplyTapEdit(out string error)
    {
        if (!TryParseMainLine(tapLineField.value, out int line))
        {
            error = "Tap line must be between 1 and 4.";
            return false;
        }

        NoteHandleType handleType = tapHandField.value == "Right"
            ? NoteHandleType.Right
            : NoteHandleType.Left;
        bool isPowered = selectedNoteType != NoteType.LongTap &&
            tapPoweredField.value;
        return placementController.TryEditTapNote(
            selectedNoteObject,
            measureField.value,
            positionField.value,
            line,
            handleType,
            isPowered,
            out error);
    }

    private bool TryApplyScratchEdit(out string error)
    {
        NoteHandleType side = scratchSideField.value == "Right"
            ? NoteHandleType.Right
            : NoteHandleType.Left;
        ScratchMotionType motionType =
            scratchMotionField.value == "Gradual"
                ? ScratchMotionType.Gradual
                : ScratchMotionType.Instant;
        return placementController.TryEditScratchNote(
            selectedNoteObject,
            measureField.value,
            positionField.value,
            side,
            scratchPoweredField.value,
            scratchStartOffsetField.value,
            scratchEndOffsetField.value,
            motionType,
            out error);
    }

    private bool TryApplyAirEdit(out string error)
    {
        if (!TryParseMainLine(airLineField.value, out int line))
        {
            error = "Air line must be between 1 and 4.";
            return false;
        }

        return placementController.TryEditAirNote(
            selectedNoteObject,
            measureField.value,
            positionField.value,
            line,
            airValueField.value,
            out error);
    }

    private void SetError(string message)
    {
        if (errorLabel == null)
        {
            return;
        }

        bool hasError = !string.IsNullOrWhiteSpace(message);
        errorLabel.text = hasError ? message : string.Empty;
        errorLabel.style.display = hasError
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        editWindow?.schedule.Execute(ApplyRememberedWindowPosition);
    }

    private void Hide()
    {
        EndWindowDrag(savePosition: true);
        selectedNoteObject = null;
        selectedNoteType = NoteType.Unknown;
        SetError(null);

        if (editWindow != null)
        {
            editWindow.style.display = DisplayStyle.None;
        }
    }

    private bool HasRequiredElements()
    {
        return editWindow != null &&
               titleBar != null &&
               titleLabel != null &&
               measureField != null &&
               positionField != null &&
               tapPanel != null &&
               tapLineField != null &&
               tapHandField != null &&
               tapPoweredField != null &&
               tapLongPairLabel != null &&
               scratchPanel != null &&
               scratchSideField != null &&
               scratchPoweredField != null &&
               scratchStartOffsetField != null &&
               scratchEndOffsetField != null &&
               scratchMotionField != null &&
               scratchLongPairLabel != null &&
               airPanel != null &&
               airLineField != null &&
               airValueField != null &&
               errorLabel != null &&
               closeButton != null &&
               deleteButton != null &&
               cancelButton != null &&
               applyButton != null;
    }

    private static GameObject GetFirstValidObject(
        IReadOnlyList<GameObject> selectedObjects)
    {
        if (selectedObjects == null)
        {
            return null;
        }

        for (int i = 0; i < selectedObjects.Count; i++)
        {
            if (selectedObjects[i])
            {
                return selectedObjects[i];
            }
        }

        return null;
    }

    private static List<string> CreateMainLineChoices()
    {
        return new List<string> { "1", "2", "3", "4" };
    }

    private static bool TryParseMainLine(string value, out int line)
    {
        return int.TryParse(value, out line) &&
            line >= 1 && line <= ChartHolder.MainLineCount;
    }

    private static string GetWindowTitle(NoteType noteType)
    {
        return noteType switch
        {
            NoteType.Tap => "Tap Note",
            NoteType.LongTap => "Long Tap Note",
            NoteType.Scratch => "Scratch Note",
            NoteType.LongScratch => "Long Scratch Note",
            NoteType.Air => "Air Note",
            _ => "Note"
        };
    }
}

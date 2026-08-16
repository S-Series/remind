using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class ChartMakerTopMenuController : MonoBehaviour
{
    private const string RuntimeThemeResourceName = "ChartMakerRuntimeTheme";
    private const string DefaultChartFileName = "chart.txt";

    [Header("UI")]
    [SerializeField] private VisualTreeAsset menuAsset;
    [SerializeField] private int sortingOrder = 100;

    [Header("File Commands")]
    [SerializeField] private ChartMakerInputRouter inputRouter;
    [SerializeField] private ChartToFile chartToFile;
    [SerializeField] private FileToChart fileToChart;
    [SerializeField] private ChartNoteSelectionController selectionController;
    [SerializeField] private ChartPlacementController placementController;
    [SerializeField] private GuideGenerate guideGenerate;
    [SerializeField] private ChartCore chartCore;
    [SerializeField] private ChartNoteEditPopupController noteEditPopup;

    private UIDocument document;
    private PanelSettings runtimePanelSettings;
    private Button fileMenuButton;
    private Button editMenuButton;
    private Button viewMenuButton;
    private Button playbackMenuButton;
    private Button newChartButton;
    private Button openChartButton;
    private Button openMusicButton;
    private Button saveChartButton;
    private Button saveAsChartButton;
    private Button exitButton;
    private Button undoButton;
    private Button redoButton;
    private Button deleteButton;
    private Button cancelButton;
    private Button showGuidesButton;
    private Button snapGuidesButton;
    private Button startTestPlayButton;
    private Button endTestPlayButton;
    private Button savePendingButton;
    private Button discardPendingButton;
    private Button cancelPendingButton;
    private VisualElement menuDismissLayer;
    private VisualElement menuPopupHost;
    private VisualElement fileMenuPopup;
    private VisualElement editMenuPopup;
    private VisualElement viewMenuPopup;
    private VisualElement playbackMenuPopup;
    private VisualElement unsavedChangesOverlay;
    private Label unsavedChangesMessage;
    private PendingFileAction pendingFileAction;
    private TopMenuType openMenu;
    private bool isMenuBound;

    public VisualElement RootVisualElement => document?.rootVisualElement;
    public event Action ChartOpened;

    /// <summary>미저장 변경 확인을 포함한 채보 파일 열기 흐름을 요청합니다.</summary>
    public void RequestOpenChartFile()
    {
        HandleOpenRequested();
    }

    /// <summary>음악 파일 선택 대화상자를 열고 선택한 음악을 로드합니다.</summary>
    public void RequestOpenMusicFile()
    {
        HandleOpenMusicRequested();
    }

    private void Awake()
    {
        if (!menuAsset)
        {
            Debug.LogError(
                "ChartMakerTopMenuController requires a menu UXML asset.",
                this);
            enabled = false;
            return;
        }

        runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        runtimePanelSettings.name = "ChartMaker Top Menu Panel Settings";
        runtimePanelSettings.hideFlags = HideFlags.DontSave;

        ThemeStyleSheet runtimeTheme =
            Resources.Load<ThemeStyleSheet>(RuntimeThemeResourceName);
        if (!runtimeTheme)
        {
            Debug.LogError(
                $"Could not load UI Toolkit theme '{RuntimeThemeResourceName}'.",
                this);
            enabled = false;
            return;
        }

        runtimePanelSettings.themeStyleSheet = runtimeTheme;

        document = GetComponent<UIDocument>();
        if (!document)
        {
            document = gameObject.AddComponent<UIDocument>();
        }

        document.panelSettings = runtimePanelSettings;
        document.visualTreeAsset = menuAsset;
        document.sortingOrder = sortingOrder;

        ResolveDependencies();
        ChartEditHistory.Clear();
        BindMenu();
    }

    private void OnEnable()
    {
        BindMenu();
    }

    private void OnDisable()
    {
        UnbindMenu();
    }

    private void OnDestroy()
    {
        UnbindMenu();

        if (runtimePanelSettings)
        {
            Destroy(runtimePanelSettings);
        }
    }

    private void ResolveDependencies()
    {
        if (!inputRouter)
        {
            inputRouter = FindFirstObjectByType<ChartMakerInputRouter>();
        }

        if (!chartToFile)
        {
            chartToFile = FindFirstObjectByType<ChartToFile>();
        }

        if (!fileToChart)
        {
            fileToChart = FindFirstObjectByType<FileToChart>();
        }

        if (!selectionController)
        {
            selectionController =
                FindFirstObjectByType<ChartNoteSelectionController>();
        }

        if (!placementController)
        {
            placementController =
                FindFirstObjectByType<ChartPlacementController>();
        }

        if (!guideGenerate)
        {
            guideGenerate = FindFirstObjectByType<GuideGenerate>();
        }

        if (!chartCore)
        {
            chartCore = FindFirstObjectByType<ChartCore>();
        }

        if (!noteEditPopup)
        {
            noteEditPopup = GetComponent<ChartNoteEditPopupController>();
        }
    }

    /// <summary>UXML의 상단 메뉴 항목을 편집 시스템의 실제 명령에 연결합니다.</summary>
    private void BindMenu()
    {
        if (isMenuBound || RootVisualElement == null)
        {
            return;
        }

        fileMenuButton = QueryButton("file-menu-button");
        editMenuButton = QueryButton("edit-menu-button");
        viewMenuButton = QueryButton("view-menu-button");
        playbackMenuButton = QueryButton("playback-menu-button");
        newChartButton = QueryButton("new-chart-menu-item");
        openChartButton = QueryButton("open-chart-menu-item");
        openMusicButton = QueryButton("open-music-menu-item");
        saveChartButton = QueryButton("save-chart-menu-item");
        saveAsChartButton = QueryButton("save-as-chart-menu-item");
        exitButton = QueryButton("exit-chart-maker-menu-item");
        undoButton = QueryButton("undo-menu-item");
        redoButton = QueryButton("redo-menu-item");
        deleteButton = QueryButton("delete-menu-item");
        cancelButton = QueryButton("cancel-menu-item");
        showGuidesButton = QueryButton("show-guides-menu-item");
        snapGuidesButton = QueryButton("snap-guides-menu-item");
        startTestPlayButton = QueryButton("start-test-play-menu-item");
        endTestPlayButton = QueryButton("end-test-play-menu-item");
        savePendingButton = QueryButton("save-pending-action-button");
        discardPendingButton = QueryButton("discard-pending-action-button");
        cancelPendingButton = QueryButton("cancel-pending-action-button");
        menuDismissLayer = RootVisualElement.Q<VisualElement>(
            "menu-dismiss-layer");
        menuPopupHost = RootVisualElement.Q<VisualElement>(
            "menu-popup-host");
        fileMenuPopup = RootVisualElement.Q<VisualElement>(
            "file-menu-popup");
        editMenuPopup = RootVisualElement.Q<VisualElement>(
            "edit-menu-popup");
        viewMenuPopup = RootVisualElement.Q<VisualElement>(
            "view-menu-popup");
        playbackMenuPopup = RootVisualElement.Q<VisualElement>(
            "playback-menu-popup");
        unsavedChangesOverlay = RootVisualElement.Q<VisualElement>(
            "unsaved-changes-overlay");
        unsavedChangesMessage = RootVisualElement.Q<Label>(
            "unsaved-changes-message");

        if (!HasRequiredMenuElements())
        {
            Debug.LogError(
                "ChartMakerTopMenu UXML is missing one or more menu elements.",
                this);
            return;
        }

        fileMenuButton.clicked += ToggleFileMenu;
        editMenuButton.clicked += ToggleEditMenu;
        viewMenuButton.clicked += ToggleViewMenu;
        playbackMenuButton.clicked += TogglePlaybackMenu;
        newChartButton.clicked += HandleNewRequested;
        openChartButton.clicked += HandleOpenRequested;
        openMusicButton.clicked += HandleOpenMusicRequested;
        saveChartButton.clicked += HandleSaveRequested;
        saveAsChartButton.clicked += HandleSaveAsRequested;
        exitButton.clicked += HandleExitRequested;
        undoButton.clicked += HandleUndoRequested;
        redoButton.clicked += HandleRedoRequested;
        deleteButton.clicked += HandleDeleteRequested;
        cancelButton.clicked += HandleCancelRequested;
        showGuidesButton.clicked += HandleShowGuidesRequested;
        snapGuidesButton.clicked += HandleSnapGuidesRequested;
        startTestPlayButton.clicked += HandleStartTestPlayRequested;
        endTestPlayButton.clicked += HandleEndTestPlayRequested;
        savePendingButton.clicked += HandleSavePendingAction;
        discardPendingButton.clicked += HandleDiscardPendingAction;
        cancelPendingButton.clicked += CancelPendingAction;
        menuDismissLayer.RegisterCallback<PointerDownEvent>(
            HandleMenuDismissed);

        if (inputRouter)
        {
            inputRouter.OpenChartRequested += HandleOpenRequested;
            inputRouter.OpenMusicRequested += HandleOpenMusicRequested;
            inputRouter.SaveRequested += HandleSaveRequested;
            inputRouter.UndoRequested += HandleUndoRequested;
            inputRouter.RedoRequested += HandleRedoRequested;
        }

        if (chartCore)
        {
            chartCore.TestPlaybackChanged += HandlePlaybackChanged;
        }

        isMenuBound = true;
        noteEditPopup?.Initialize(
            RootVisualElement,
            selectionController,
            placementController);
        HideMenus();
        HideUnsavedChangesDialog();
    }

    private void UnbindMenu()
    {
        if (!isMenuBound)
        {
            return;
        }

        fileMenuButton.clicked -= ToggleFileMenu;
        editMenuButton.clicked -= ToggleEditMenu;
        viewMenuButton.clicked -= ToggleViewMenu;
        playbackMenuButton.clicked -= TogglePlaybackMenu;
        newChartButton.clicked -= HandleNewRequested;
        openChartButton.clicked -= HandleOpenRequested;
        openMusicButton.clicked -= HandleOpenMusicRequested;
        saveChartButton.clicked -= HandleSaveRequested;
        saveAsChartButton.clicked -= HandleSaveAsRequested;
        exitButton.clicked -= HandleExitRequested;
        undoButton.clicked -= HandleUndoRequested;
        redoButton.clicked -= HandleRedoRequested;
        deleteButton.clicked -= HandleDeleteRequested;
        cancelButton.clicked -= HandleCancelRequested;
        showGuidesButton.clicked -= HandleShowGuidesRequested;
        snapGuidesButton.clicked -= HandleSnapGuidesRequested;
        startTestPlayButton.clicked -= HandleStartTestPlayRequested;
        endTestPlayButton.clicked -= HandleEndTestPlayRequested;
        savePendingButton.clicked -= HandleSavePendingAction;
        discardPendingButton.clicked -= HandleDiscardPendingAction;
        cancelPendingButton.clicked -= CancelPendingAction;
        menuDismissLayer.UnregisterCallback<PointerDownEvent>(
            HandleMenuDismissed);

        if (inputRouter)
        {
            inputRouter.OpenChartRequested -= HandleOpenRequested;
            inputRouter.OpenMusicRequested -= HandleOpenMusicRequested;
            inputRouter.SaveRequested -= HandleSaveRequested;
            inputRouter.UndoRequested -= HandleUndoRequested;
            inputRouter.RedoRequested -= HandleRedoRequested;
        }

        if (chartCore)
        {
            chartCore.TestPlaybackChanged -= HandlePlaybackChanged;
        }

        isMenuBound = false;
    }

    private Button QueryButton(string elementName)
    {
        return RootVisualElement.Q<Button>(elementName);
    }

    private bool HasRequiredMenuElements()
    {
        return fileMenuButton != null &&
               editMenuButton != null &&
               viewMenuButton != null &&
               playbackMenuButton != null &&
               newChartButton != null &&
               openChartButton != null &&
               openMusicButton != null &&
               saveChartButton != null &&
               saveAsChartButton != null &&
               exitButton != null &&
               undoButton != null &&
               redoButton != null &&
               deleteButton != null &&
               cancelButton != null &&
               showGuidesButton != null &&
               snapGuidesButton != null &&
               startTestPlayButton != null &&
               endTestPlayButton != null &&
               savePendingButton != null &&
               discardPendingButton != null &&
               cancelPendingButton != null &&
               menuDismissLayer != null &&
               menuPopupHost != null &&
               fileMenuPopup != null &&
               editMenuPopup != null &&
               viewMenuPopup != null &&
               playbackMenuPopup != null &&
               unsavedChangesOverlay != null &&
               unsavedChangesMessage != null;
    }

    private void ToggleFileMenu()
    {
        ToggleMenu(TopMenuType.File, fileMenuButton, fileMenuPopup);
    }

    private void ToggleEditMenu()
    {
        RefreshEditMenuState();
        ToggleMenu(TopMenuType.Edit, editMenuButton, editMenuPopup);
    }

    private void ToggleViewMenu()
    {
        RefreshViewMenuState();
        ToggleMenu(TopMenuType.View, viewMenuButton, viewMenuPopup);
    }

    private void TogglePlaybackMenu()
    {
        RefreshPlaybackMenuState();
        ToggleMenu(
            TopMenuType.Playback,
            playbackMenuButton,
            playbackMenuPopup);
    }

    private void ToggleMenu(
        TopMenuType menuType,
        Button sourceButton,
        VisualElement popup)
    {
        if (openMenu == menuType)
        {
            HideMenus();
            return;
        }

        HideAllPopups();
        openMenu = menuType;
        menuPopupHost.style.left = sourceButton.layout.x;
        menuPopupHost.style.display = DisplayStyle.Flex;
        menuDismissLayer.style.display = DisplayStyle.Flex;
        popup.style.display = DisplayStyle.Flex;
    }

    private void HideMenus()
    {
        openMenu = TopMenuType.None;
        HideAllPopups();
        menuDismissLayer.style.display = DisplayStyle.None;
        menuPopupHost.style.display = DisplayStyle.None;
    }

    private void HideAllPopups()
    {
        fileMenuPopup.style.display = DisplayStyle.None;
        editMenuPopup.style.display = DisplayStyle.None;
        viewMenuPopup.style.display = DisplayStyle.None;
        playbackMenuPopup.style.display = DisplayStyle.None;
    }

    private void HandleMenuDismissed(PointerDownEvent _)
    {
        HideMenus();
    }

    private void HandleUndoRequested()
    {
        HideMenus();

        if (placementController)
        {
            ChartEditHistory.Undo(placementController);
        }
    }

    private void HandleRedoRequested()
    {
        HideMenus();

        if (placementController)
        {
            ChartEditHistory.Redo(placementController);
        }
    }

    private void HandleDeleteRequested()
    {
        HideMenus();
        selectionController?.DeleteSelection();
    }

    private void HandleCancelRequested()
    {
        HideMenus();

        if (inputRouter)
        {
            inputRouter.CancelTool();
        }
        else
        {
            selectionController?.ClearSelection();
        }
    }

    private void HandleShowGuidesRequested()
    {
        guideGenerate?.ToggleGuidesVisible();
        RefreshViewMenuState();
    }

    private void HandleSnapGuidesRequested()
    {
        ChartPlacementController.UseYClamp =
            !ChartPlacementController.UseYClamp;
        RefreshViewMenuState();
    }

    private void HandleStartTestPlayRequested()
    {
        HideMenus();
        chartCore?.StartTestPlay();
    }

    private void HandleEndTestPlayRequested()
    {
        HideMenus();
        chartCore?.EndTestPlay();
    }

    private void HandlePlaybackChanged(bool _)
    {
        RefreshPlaybackMenuState();
    }

    private void RefreshEditMenuState()
    {
        undoButton.SetEnabled(ChartEditHistory.CanUndo);
        redoButton.SetEnabled(ChartEditHistory.CanRedo);
        deleteButton.SetEnabled(
            selectionController != null &&
            selectionController.SelectedNoteObjects.Count > 0);
    }

    private void RefreshViewMenuState()
    {
        bool guidesVisible = !guideGenerate || guideGenerate.GuidesVisible;
        showGuidesButton.text = guidesVisible
            ? "[x] Show Guides"
            : "[ ] Show Guides";
        snapGuidesButton.text = ChartPlacementController.UseYClamp
            ? "[x] Snap to Guides"
            : "[ ] Snap to Guides";
    }

    private void RefreshPlaybackMenuState()
    {
        bool isPlaying = chartCore && chartCore.IsTestPlaying;
        startTestPlayButton.SetEnabled(chartCore && !isPlaying);
        endTestPlayButton.SetEnabled(chartCore && isPlaying);
    }

    private void HandleNewRequested()
    {
        RequestDestructiveAction(PendingFileAction.NewChart);
    }

    private void HandleOpenRequested()
    {
        RequestDestructiveAction(PendingFileAction.OpenChart);
    }

    private void HandleOpenMusicRequested()
    {
        HideMenus();

        if (!chartCore)
        {
            Debug.LogError("ChartCore was not found.", this);
            return;
        }

        string filePath = ChartFileDialog.OpenAudioFile(
            chartCore.CurrentAudioFilePath);

        if (!string.IsNullOrWhiteSpace(filePath) &&
            !chartCore.LoadAudioFile(filePath))
        {
            Debug.LogWarning(
                "The selected music could not start loading.",
                this);
        }
    }

    private void HandleSaveRequested()
    {
        HideMenus();
        TrySaveCurrentChart();
    }

    private void HandleSaveAsRequested()
    {
        HideMenus();
        TrySaveChartAs();
    }

    private void HandleExitRequested()
    {
        RequestDestructiveAction(PendingFileAction.Exit);
    }

    private void RequestDestructiveAction(PendingFileAction action)
    {
        HideMenus();

        if (HasUnsavedChanges())
        {
            ShowUnsavedChangesDialog(action);
            return;
        }

        ExecuteFileAction(action);
    }

    private bool HasUnsavedChanges()
    {
        return chartToFile
            ? chartToFile.HasUnsavedChanges
            : ChartManager.ChartHolders.Count > 0;
    }

    private void ShowUnsavedChangesDialog(PendingFileAction action)
    {
        pendingFileAction = action;
        unsavedChangesMessage.text = action switch
        {
            PendingFileAction.NewChart =>
                "Save the current chart before creating a new one?",
            PendingFileAction.OpenChart =>
                "Save the current chart before opening another file?",
            PendingFileAction.Exit =>
                "Save the current chart before exiting?",
            _ => "Save the current chart?"
        };
        unsavedChangesOverlay.style.display = DisplayStyle.Flex;
    }

    private void HideUnsavedChangesDialog()
    {
        if (unsavedChangesOverlay != null)
        {
            unsavedChangesOverlay.style.display = DisplayStyle.None;
        }
    }

    private void HandleSavePendingAction()
    {
        if (!TrySaveCurrentChart())
        {
            return;
        }

        PendingFileAction action = pendingFileAction;
        CancelPendingAction();
        ExecuteFileAction(action);
    }

    private void HandleDiscardPendingAction()
    {
        PendingFileAction action = pendingFileAction;
        CancelPendingAction();
        ExecuteFileAction(action);
    }

    private void CancelPendingAction()
    {
        pendingFileAction = PendingFileAction.None;
        HideUnsavedChangesDialog();
    }

    private void ExecuteFileAction(PendingFileAction action)
    {
        switch (action)
        {
            case PendingFileAction.NewChart:
                CreateNewChart();
                break;
            case PendingFileAction.OpenChart:
                OpenChart();
                break;
            case PendingFileAction.Exit:
                ExitChartMaker();
                break;
        }
    }

    /// <summary>현재 선택과 채보 데이터를 비우고 저장 경로를 초기화합니다.</summary>
    private void CreateNewChart()
    {
        selectionController?.ClearSelection();
        ChartManager.ClearChart();
        ChartEditHistory.Clear();
        chartToFile?.ResetDocument();
        Debug.Log("Created a new chart.", this);
    }

    /// <summary>파일 대화상자에서 선택한 채보를 검증한 뒤 현재 편집 데이터로 엽니다.</summary>
    private void OpenChart()
    {
        if (!fileToChart)
        {
            Debug.LogError("FileToChart was not found.", this);
            return;
        }

        string filePath = ChartFileDialog.OpenChartFile(
            chartToFile?.CurrentFilePath);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (!fileToChart.TryLoadFromPath(filePath, out _, out string error))
        {
            Debug.LogError($"Failed to open chart: {error}", this);
            return;
        }

        ChartEditHistory.Clear();
        ChartOpened?.Invoke();
        Debug.Log($"Chart opened: {Path.GetFullPath(filePath)}", this);
    }

    private bool TrySaveCurrentChart()
    {
        if (!chartToFile)
        {
            Debug.LogError("ChartToFile was not found.", this);
            return false;
        }

        if (!chartToFile.HasSavePath)
        {
            return TrySaveChartAs();
        }

        if (!chartToFile.TrySaveToPath(
                chartToFile.CurrentFilePath,
                out string error))
        {
            Debug.LogError($"Failed to save chart: {error}", this);
            return false;
        }

        Debug.Log($"Chart saved: {chartToFile.CurrentFilePath}", this);
        return true;
    }

    /// <summary>새 경로를 선택해 채보를 저장하고 이후 Save 대상 경로로 유지합니다.</summary>
    private bool TrySaveChartAs()
    {
        if (!chartToFile)
        {
            Debug.LogError("ChartToFile was not found.", this);
            return false;
        }

        string initialPath = chartToFile.HasSavePath
            ? chartToFile.CurrentFilePath
            : Path.Combine(
                Application.persistentDataPath,
                "Charts",
                DefaultChartFileName);
        string filePath = ChartFileDialog.SaveChartFile(initialPath);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (string.IsNullOrEmpty(Path.GetExtension(filePath)))
        {
            filePath += ".txt";
        }

        if (!chartToFile.TrySaveToPath(filePath, out string error))
        {
            Debug.LogError($"Failed to save chart: {error}", this);
            return false;
        }

        Debug.Log($"Chart saved: {chartToFile.CurrentFilePath}", this);
        return true;
    }

    private static void ExitChartMaker()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private enum PendingFileAction
    {
        None = 0,
        NewChart = 1,
        OpenChart = 2,
        Exit = 3
    }

    private enum TopMenuType
    {
        None = 0,
        File = 1,
        Edit = 2,
        View = 3,
        Playback = 4
    }
}

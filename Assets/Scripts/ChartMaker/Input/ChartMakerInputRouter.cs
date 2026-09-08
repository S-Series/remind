using System;
using System.Collections.Generic;
using REmind.Common.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

public enum ChartToolType
{
    None = 0,
    SingleTap = 1,
    LongTap = 2,
    SingleScratch = 3,
    LongScratch = 4,
    SingleAir = 5,
    Eraser = 6
}

public enum ChartToolShortcut
{
    Tap = 0,
    Scratch = 1,
    Eraser = 2,
    Air = 3,
    Specials = 4
}

[DisallowMultipleComponent]
public sealed class ChartMakerInputRouter : MonoBehaviour
{
    private const string ActionMapName = "ChartMaker";

    private static readonly ShortcutBinding[] ShortcutBindings =
    {
        new ShortcutBinding("SelectTap", ChartToolShortcut.Tap),
        new ShortcutBinding("SelectScratch", ChartToolShortcut.Scratch),
        new ShortcutBinding("SelectEraser", ChartToolShortcut.Eraser),
        new ShortcutBinding("SelectAir", ChartToolShortcut.Air),
        new ShortcutBinding("SelectSpecial", ChartToolShortcut.Specials)
    };

    [SerializeField] private InputActionAsset inputActions;

    private readonly Dictionary<InputAction, ChartToolShortcut>
        shortcutByAction =
            new Dictionary<InputAction, ChartToolShortcut>(
                ShortcutBindings.Length);

    private InputActionMap actionMap;
    private InputAction cancelAction;
    private InputAction deleteAction;
    private InputAction saveAction;
    private InputAction undoAction;
    private InputAction redoAction;
    private InputAction editSelectedTapAction;
    private InputAction moveSelectionLeftAction;
    private InputAction moveSelectionRightAction;
    private InputAction moveSelectionUpAction;
    private InputAction moveSelectionDownAction;
    private InputAction openChartAction;
    private InputAction openMusicAction;
    private bool isBound;
    private bool editingEnabled = true;

    public event Action<ChartToolType> ToolSelected;
    public event Action<ChartToolShortcut> ToolShortcutRequested;
    public event Action CancelRequested;
    public event Action DeleteRequested;
    public event Action SaveRequested;
    public event Action UndoRequested;
    public event Action RedoRequested;
    /// <summary>
    /// 선택된 Tap/Scratch 계열 편집을 요청합니다. true이면 일반/Long 종류를,
    /// false이면 Tap 손 방향 또는 Scratch Powered 상태를 전환합니다.
    /// </summary>
    public event Action<bool> EditSelectedNoteRequested;
    public event Action<Vector2Int, bool> MoveSelectionRequested;
    public event Action OpenChartRequested;
    public event Action OpenMusicRequested;

    public ChartToolType CurrentTool { get; private set; }
    public bool IsReady => isBound;
    public bool EditingEnabled => editingEnabled;

    private void Awake()
    {
        TryBindActions();
    }

    private void OnEnable()
    {
        if (TryBindActions())
        {
            actionMap.Enable();
        }
    }

    private void OnDisable()
    {
        actionMap?.Disable();
    }

    private void OnDestroy()
    {
        UnbindActions();
    }

    /// <summary>ChartMaker 단축키 입력을 활성화하거나 비활성화합니다.</summary>
    public void SetInputEnabled(bool value)
    {
        if (!TryBindActions())
        {
            return;
        }

        if (value)
        {
            actionMap.Enable();
        }
        else
        {
            actionMap.Disable();
        }
    }

    /// <summary>키보드와 UI 버튼이 공유하는 현재 편집 도구를 선택합니다.</summary>
    public void SelectTool(ChartToolType toolType)
    {
        if (!editingEnabled && toolType != ChartToolType.None)
        {
            return;
        }

        CurrentTool = toolType;
        ToolSelected?.Invoke(CurrentTool);
    }

    /// <summary>
    /// Preview owns an immutable chart snapshot. Disable every editing shortcut
    /// and clear the active tool while that snapshot is running.
    /// </summary>
    public void SetEditingEnabled(bool value)
    {
        if (editingEnabled == value)
        {
            return;
        }

        editingEnabled = value;
        if (!editingEnabled)
        {
            CancelTool();
        }
    }

    public void SelectSingleTap()
    {
        SelectTool(ChartToolType.SingleTap);
    }

    public void SelectLongTap()
    {
        SelectTool(ChartToolType.LongTap);
    }

    public void SelectSingleScratch()
    {
        SelectTool(ChartToolType.SingleScratch);
    }

    public void SelectLongScratch()
    {
        SelectTool(ChartToolType.LongScratch);
    }

    public void SelectSingleAir()
    {
        SelectTool(ChartToolType.SingleAir);
    }

    public void SelectEraser()
    {
        SelectTool(ChartToolType.Eraser);
    }

    public void CancelTool()
    {
        ClearTextSelection();
        SelectTool(ChartToolType.None);
        CancelRequested?.Invoke();
    }

    private bool TryBindActions()
    {
        if (isBound)
        {
            return true;
        }

        if (inputActions == null)
        {
            Debug.LogError(
                "ChartMakerInputRouter requires an Input Action Asset.",
                this);
            return false;
        }

        actionMap = inputActions.FindActionMap(ActionMapName, false);

        if (actionMap == null)
        {
            Debug.LogError(
                $"Input Action Map '{ActionMapName}' was not found.",
                this);
            return false;
        }

        for (int i = 0; i < ShortcutBindings.Length; i++)
        {
            ShortcutBinding binding = ShortcutBindings[i];
            InputAction action = actionMap.FindAction(binding.ActionName, false);

            if (action == null)
            {
                return FailBinding(binding.ActionName);
            }

            shortcutByAction.Add(action, binding.Shortcut);
            action.performed += HandleShortcutPerformed;
        }

        cancelAction = FindAction("Cancel");
        deleteAction = FindAction("Delete");
        saveAction = FindAction("Save");
        undoAction = FindAction("Undo");
        redoAction = FindAction("Redo");
        editSelectedTapAction = FindAction("EditSelectedTap");
        moveSelectionLeftAction = FindAction("MoveSelectionLeft");
        moveSelectionRightAction = FindAction("MoveSelectionRight");
        moveSelectionUpAction = FindAction("MoveSelectionUp");
        moveSelectionDownAction = FindAction("MoveSelectionDown");
        openChartAction = FindAction("OpenChart");
        openMusicAction = FindAction("OpenMusic");

        if (cancelAction == null ||
            deleteAction == null ||
            saveAction == null ||
            undoAction == null ||
            redoAction == null ||
            editSelectedTapAction == null ||
            moveSelectionLeftAction == null ||
            moveSelectionRightAction == null ||
            moveSelectionUpAction == null ||
            moveSelectionDownAction == null ||
            openChartAction == null ||
            openMusicAction == null)
        {
            UnbindActions();
            return false;
        }

        cancelAction.performed += HandleCancelPerformed;
        deleteAction.performed += HandleDeletePerformed;
        saveAction.performed += HandleSavePerformed;
        undoAction.performed += HandleUndoPerformed;
        redoAction.performed += HandleRedoPerformed;
        editSelectedTapAction.performed += HandleEditSelectedTapPerformed;
        moveSelectionLeftAction.performed += HandleMoveSelectionLeftPerformed;
        moveSelectionRightAction.performed += HandleMoveSelectionRightPerformed;
        moveSelectionUpAction.performed += HandleMoveSelectionUpPerformed;
        moveSelectionDownAction.performed += HandleMoveSelectionDownPerformed;
        openChartAction.performed += HandleOpenChartPerformed;
        openMusicAction.performed += HandleOpenMusicPerformed;
        isBound = true;
        return true;
    }

    private InputAction FindAction(string actionName)
    {
        InputAction action = actionMap.FindAction(actionName, false);

        if (action == null)
        {
            Debug.LogError(
                $"Input Action '{actionName}' was not found in '{ActionMapName}'.",
                this);
        }

        return action;
    }

    private bool FailBinding(string actionName)
    {
        Debug.LogError(
            $"Input Action '{actionName}' was not found in '{ActionMapName}'.",
            this);
        UnbindActions();
        return false;
    }

    private void UnbindActions()
    {
        foreach (KeyValuePair<InputAction, ChartToolShortcut> pair in
                 shortcutByAction)
        {
            pair.Key.performed -= HandleShortcutPerformed;
        }

        shortcutByAction.Clear();
        Unsubscribe(cancelAction, HandleCancelPerformed);
        Unsubscribe(deleteAction, HandleDeletePerformed);
        Unsubscribe(saveAction, HandleSavePerformed);
        Unsubscribe(undoAction, HandleUndoPerformed);
        Unsubscribe(redoAction, HandleRedoPerformed);
        Unsubscribe(editSelectedTapAction, HandleEditSelectedTapPerformed);
        Unsubscribe(
            moveSelectionLeftAction,
            HandleMoveSelectionLeftPerformed);
        Unsubscribe(
            moveSelectionRightAction,
            HandleMoveSelectionRightPerformed);
        Unsubscribe(moveSelectionUpAction, HandleMoveSelectionUpPerformed);
        Unsubscribe(moveSelectionDownAction, HandleMoveSelectionDownPerformed);
        Unsubscribe(openChartAction, HandleOpenChartPerformed);
        Unsubscribe(openMusicAction, HandleOpenMusicPerformed);

        cancelAction = null;
        deleteAction = null;
        saveAction = null;
        undoAction = null;
        redoAction = null;
        editSelectedTapAction = null;
        moveSelectionLeftAction = null;
        moveSelectionRightAction = null;
        moveSelectionUpAction = null;
        moveSelectionDownAction = null;
        openChartAction = null;
        openMusicAction = null;
        actionMap = null;
        isBound = false;
    }

    private void HandleShortcutPerformed(InputAction.CallbackContext context)
    {
        if (!editingEnabled || IsEditingText() ||
            !shortcutByAction.TryGetValue(
                context.action,
                out ChartToolShortcut shortcut))
        {
            return;
        }

        ToolShortcutRequested?.Invoke(shortcut);
    }

    private void HandleCancelPerformed(InputAction.CallbackContext _)
    {
        if (!PopupContext.HasOpenPopup)
        {
            CancelTool();
        }
    }

    private void HandleDeletePerformed(InputAction.CallbackContext _)
    {
        if (editingEnabled && !IsEditingText())
        {
            DeleteRequested?.Invoke();
        }
    }

    private void HandleSavePerformed(InputAction.CallbackContext _)
    {
        if (editingEnabled && !PopupContext.HasOpenPopup)
        {
            SaveRequested?.Invoke();
        }
    }

    private void HandleUndoPerformed(InputAction.CallbackContext _)
    {
        // Ctrl+Shift+Z also satisfies Ctrl+Z unless shortcut consumption is enabled.
        if (editingEnabled && !IsEditingText() &&
            Keyboard.current?.shiftKey.isPressed != true)
        {
            UndoRequested?.Invoke();
        }
    }

    private void HandleRedoPerformed(InputAction.CallbackContext _)
    {
        if (editingEnabled && !IsEditingText())
        {
            RedoRequested?.Invoke();
        }
    }

    private void HandleEditSelectedTapPerformed(InputAction.CallbackContext _)
    {
        if (editingEnabled && !IsEditingText())
        {
            bool toggleLongType =
                Keyboard.current?.shiftKey.isPressed == true;
            EditSelectedNoteRequested?.Invoke(toggleLongType);
        }
    }

    private void HandleMoveSelectionLeftPerformed(InputAction.CallbackContext _)
    {
        RequestSelectionMove(Vector2Int.left);
    }

    private void HandleMoveSelectionRightPerformed(InputAction.CallbackContext _)
    {
        RequestSelectionMove(Vector2Int.right);
    }

    private void HandleMoveSelectionUpPerformed(InputAction.CallbackContext _)
    {
        RequestSelectionMove(Vector2Int.up);
    }

    private void HandleMoveSelectionDownPerformed(InputAction.CallbackContext _)
    {
        RequestSelectionMove(Vector2Int.down);
    }

    private void RequestSelectionMove(Vector2Int direction)
    {
        if (!editingEnabled || IsEditingText())
        {
            return;
        }

        bool moveByPage = direction.y != 0 &&
            Keyboard.current?.shiftKey.isPressed == true;
        MoveSelectionRequested?.Invoke(direction, moveByPage);
    }

    private void HandleOpenChartPerformed(InputAction.CallbackContext _)
    {
        // Ctrl+Shift+O also satisfies the Ctrl+O composite.
        if (editingEnabled && !PopupContext.HasOpenPopup &&
            Keyboard.current?.shiftKey.isPressed != true)
        {
            OpenChartRequested?.Invoke();
        }
    }

    private void HandleOpenMusicPerformed(InputAction.CallbackContext _)
    {
        if (editingEnabled && !PopupContext.HasOpenPopup)
        {
            OpenMusicRequested?.Invoke();
        }
    }

    private static bool IsEditingText()
    {
        if (PopupContext.HasOpenPopup)
        {
            return true;
        }

        GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;

        if (selectedObject != null &&
            (selectedObject.GetComponent<TMP_InputField>() != null ||
             selectedObject.GetComponent<InputField>() != null))
        {
            return true;
        }

        UIDocument[] documents = FindObjectsByType<UIDocument>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < documents.Length; i++)
        {
            VisualElement focusedElement = documents[i].rootVisualElement?
                .panel?.focusController?.focusedElement as VisualElement;

            if (focusedElement is TextField ||
                focusedElement?.GetFirstAncestorOfType<TextField>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearTextSelection()
    {
        if (IsEditingText())
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private static void Unsubscribe(
        InputAction action,
        Action<InputAction.CallbackContext> callback)
    {
        if (action != null)
        {
            action.performed -= callback;
        }
    }

    private readonly struct ShortcutBinding
    {
        public string ActionName { get; }
        public ChartToolShortcut Shortcut { get; }

        public ShortcutBinding(
            string actionName,
            ChartToolShortcut shortcut)
        {
            ActionName = actionName;
            Shortcut = shortcut;
        }
    }
}

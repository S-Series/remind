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

[DisallowMultipleComponent]
public sealed class ChartMakerInputRouter : MonoBehaviour
{
    private const string ActionMapName = "ChartMaker";

    private static readonly ToolBinding[] ToolBindings =
    {
        new ToolBinding("SelectSingleTap", ChartToolType.SingleTap),
        new ToolBinding("SelectLongTap", ChartToolType.LongTap),
        new ToolBinding("SelectSingleSCT", ChartToolType.SingleScratch),
        new ToolBinding("SelectLongSCT", ChartToolType.LongScratch),
        new ToolBinding("SelectSingleAir", ChartToolType.SingleAir),
        new ToolBinding("SelectEraser", ChartToolType.Eraser)
    };

    [SerializeField] private InputActionAsset inputActions;

    private readonly Dictionary<InputAction, ChartToolType> toolByAction =
        new Dictionary<InputAction, ChartToolType>(ToolBindings.Length);

    private InputActionMap actionMap;
    private InputAction cancelAction;
    private InputAction deleteAction;
    private InputAction saveAction;
    private InputAction undoAction;
    private InputAction redoAction;
    private InputAction togglePoweredAction;
    private InputAction moveSelectionLeftAction;
    private InputAction moveSelectionRightAction;
    private InputAction moveSelectionUpAction;
    private InputAction moveSelectionDownAction;
    private InputAction openChartAction;
    private InputAction openMusicAction;
    private bool isBound;

    public event Action<ChartToolType> ToolSelected;
    public event Action CancelRequested;
    public event Action DeleteRequested;
    public event Action SaveRequested;
    public event Action UndoRequested;
    public event Action RedoRequested;
    public event Action TogglePoweredRequested;
    public event Action<Vector2Int, bool> MoveSelectionRequested;
    public event Action OpenChartRequested;
    public event Action OpenMusicRequested;

    public ChartToolType CurrentTool { get; private set; }
    public bool IsReady => isBound;

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
        CurrentTool = toolType;
        ToolSelected?.Invoke(CurrentTool);
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

        for (int i = 0; i < ToolBindings.Length; i++)
        {
            ToolBinding binding = ToolBindings[i];
            InputAction action = actionMap.FindAction(binding.ActionName, false);

            if (action == null)
            {
                return FailBinding(binding.ActionName);
            }

            toolByAction.Add(action, binding.ToolType);
            action.performed += HandleToolPerformed;
        }

        cancelAction = FindAction("Cancel");
        deleteAction = FindAction("Delete");
        saveAction = FindAction("Save");
        undoAction = FindAction("Undo");
        redoAction = FindAction("Redo");
        togglePoweredAction = FindAction("TogglePowered");
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
            togglePoweredAction == null ||
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
        togglePoweredAction.performed += HandleTogglePoweredPerformed;
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
        foreach (KeyValuePair<InputAction, ChartToolType> pair in toolByAction)
        {
            pair.Key.performed -= HandleToolPerformed;
        }

        toolByAction.Clear();
        Unsubscribe(cancelAction, HandleCancelPerformed);
        Unsubscribe(deleteAction, HandleDeletePerformed);
        Unsubscribe(saveAction, HandleSavePerformed);
        Unsubscribe(undoAction, HandleUndoPerformed);
        Unsubscribe(redoAction, HandleRedoPerformed);
        Unsubscribe(togglePoweredAction, HandleTogglePoweredPerformed);
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
        togglePoweredAction = null;
        moveSelectionLeftAction = null;
        moveSelectionRightAction = null;
        moveSelectionUpAction = null;
        moveSelectionDownAction = null;
        openChartAction = null;
        openMusicAction = null;
        actionMap = null;
        isBound = false;
    }

    private void HandleToolPerformed(InputAction.CallbackContext context)
    {
        if (IsEditingText() ||
            !toolByAction.TryGetValue(context.action, out ChartToolType tool))
        {
            return;
        }

        SelectTool(tool);
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
        if (!IsEditingText())
        {
            DeleteRequested?.Invoke();
        }
    }

    private void HandleSavePerformed(InputAction.CallbackContext _)
    {
        if (!PopupContext.HasOpenPopup)
        {
            SaveRequested?.Invoke();
        }
    }

    private void HandleUndoPerformed(InputAction.CallbackContext _)
    {
        // Ctrl+Shift+Z also satisfies Ctrl+Z unless shortcut consumption is enabled.
        if (!IsEditingText() && Keyboard.current?.shiftKey.isPressed != true)
        {
            UndoRequested?.Invoke();
        }
    }

    private void HandleRedoPerformed(InputAction.CallbackContext _)
    {
        if (!IsEditingText())
        {
            RedoRequested?.Invoke();
        }
    }

    private void HandleTogglePoweredPerformed(InputAction.CallbackContext _)
    {
        if (!PopupContext.HasOpenPopup)
        {
            TogglePoweredRequested?.Invoke();
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
        if (IsEditingText())
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
        if (!PopupContext.HasOpenPopup &&
            Keyboard.current?.shiftKey.isPressed != true)
        {
            OpenChartRequested?.Invoke();
        }
    }

    private void HandleOpenMusicPerformed(InputAction.CallbackContext _)
    {
        if (!PopupContext.HasOpenPopup)
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

    private readonly struct ToolBinding
    {
        public string ActionName { get; }
        public ChartToolType ToolType { get; }

        public ToolBinding(string actionName, ChartToolType toolType)
        {
            ActionName = actionName;
            ToolType = toolType;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace REmind.Common.UI
{
    public enum MenuSelectAxis
    {
        Vertical,
        Horizontal,
        Both
    }

    [Serializable]
    public sealed class MenuSelectionChangedEvent : UnityEvent<Button, int>
    {
    }

    [AddComponentMenu("REmind/Common UI/Menu Select System")]
    [DisallowMultipleComponent]
    public sealed class MenuSelectSystem : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private List<Button> buttons = new List<Button>();
        [SerializeField] private bool collectButtonsFromChildren = true;
        [SerializeField] private bool includeInactiveButtons;

        [Header("Navigation")]
        [SerializeField] private MenuSelectAxis navigationAxis = MenuSelectAxis.Vertical;
        [SerializeField] private bool wrapAround = true;
        [SerializeField] private int startIndex;
        [SerializeField] private bool selectOnEnable = true;

        [Header("Input")]
        [SerializeField] private bool useKeyboardInput = true;
        [SerializeField] private bool selectOnPointerEnter = true;
        [SerializeField] private bool selectOnPointerDown = true;
        [SerializeField] private float repeatDelay = 0.35f;
        [SerializeField] private float repeatInterval = 0.12f;

        [Header("Events")]
        [SerializeField] private MenuSelectionChangedEvent onSelectionChanged = new MenuSelectionChangedEvent();
        [SerializeField] private MenuSelectionChangedEvent onSubmitted = new MenuSelectionChangedEvent();

        public event Action<Button, int> SelectionChanged;
        public event Action<Button, int> Submitted;

        private int _currentIndex = -1;
        private float _nextMoveTime;
        private Vector2Int _lastHeldDirection;
        private bool _isApplyingSelection;

        public IReadOnlyList<Button> Buttons => buttons;
        public int CurrentIndex => _currentIndex;
        public Button CurrentButton => IsValidIndex(_currentIndex) ? buttons[_currentIndex] : null;

        private void Awake()
        {
            RefreshButtons();
        }

        private void OnEnable()
        {
            RefreshButtons();

            if (selectOnEnable)
            {
                Select(Mathf.Clamp(startIndex, 0, Mathf.Max(0, buttons.Count - 1)));
            }
        }

        private void Update()
        {
            if (!useKeyboardInput || buttons.Count == 0)
            {
                return;
            }

            if (TryReadSubmit())
            {
                Submit();
                return;
            }

            var direction = ReadMoveDirection();
            if (direction == Vector2Int.zero)
            {
                _lastHeldDirection = Vector2Int.zero;
                _nextMoveTime = 0f;
                return;
            }

            if (!CanMove(direction))
            {
                return;
            }

            Move(direction);
        }

        public void RefreshButtons()
        {
            if (!collectButtonsFromChildren)
            {
                RemoveInvalidButtons();
                RegisterMenuItems();
                return;
            }

            buttons.Clear();
            GetComponentsInChildren(includeInactiveButtons, buttons);
            RemoveInvalidButtons();
            RegisterMenuItems();
        }

        public void SetButtons(IEnumerable<Button> newButtons, int selectedIndex = 0)
        {
            buttons.Clear();

            if (newButtons != null)
            {
                foreach (var button in newButtons)
                {
                    if (button != null && button.IsInteractable())
                    {
                        buttons.Add(button);
                    }
                }
            }

            RegisterMenuItems();
            Select(selectedIndex);
        }

        public void Select(int index)
        {
            RemoveInvalidButtons();

            if (buttons.Count == 0)
            {
                _currentIndex = -1;
                return;
            }

            _currentIndex = wrapAround ? Mod(index, buttons.Count) : Mathf.Clamp(index, 0, buttons.Count - 1);
            var button = buttons[_currentIndex];
            ApplySelection(button, _currentIndex);
        }

        public void Select(Button button)
        {
            if (button == null)
            {
                return;
            }

            var index = buttons.IndexOf(button);
            if (index >= 0)
            {
                ApplySelection(button, index);
            }
        }

        public void SelectNext()
        {
            SelectRelative(1);
        }

        public void SelectPrevious()
        {
            SelectRelative(-1);
        }

        public void Submit()
        {
            var button = CurrentButton;
            if (button == null || !button.IsInteractable())
            {
                return;
            }

            Submitted?.Invoke(button, _currentIndex);
            onSubmitted?.Invoke(button, _currentIndex);
            button.onClick.Invoke();
        }

        internal void SelectFromPointer(Button button)
        {
            if (selectOnPointerEnter)
            {
                Select(button);
            }
        }

        internal void SelectFromPointerDown(Button button)
        {
            if (selectOnPointerDown)
            {
                Select(button);
            }
        }

        internal void SyncExternalSelection(Button button)
        {
            if (_isApplyingSelection)
            {
                return;
            }

            var index = buttons.IndexOf(button);
            if (index >= 0 && index != _currentIndex)
            {
                _currentIndex = index;
                SelectionChanged?.Invoke(button, _currentIndex);
                onSelectionChanged?.Invoke(button, _currentIndex);
            }
        }

        private void Move(Vector2Int direction)
        {
            var step = ResolveStep(direction);
            if (step == 0)
            {
                return;
            }

            SelectRelative(step);
        }

        private void SelectRelative(int step)
        {
            if (buttons.Count == 0)
            {
                return;
            }

            Select(_currentIndex < 0 ? startIndex : _currentIndex + step);
        }

        private int ResolveStep(Vector2Int direction)
        {
            switch (navigationAxis)
            {
                case MenuSelectAxis.Horizontal:
                    return direction.x;
                case MenuSelectAxis.Both:
                    return direction.x != 0 ? direction.x : -direction.y;
                default:
                    return -direction.y;
            }
        }

        private bool CanMove(Vector2Int direction)
        {
            if (direction != _lastHeldDirection)
            {
                _lastHeldDirection = direction;
                _nextMoveTime = Time.unscaledTime + repeatDelay;
                return true;
            }

            if (Time.unscaledTime < _nextMoveTime)
            {
                return false;
            }

            _nextMoveTime = Time.unscaledTime + repeatInterval;
            return true;
        }

        private Vector2Int ReadMoveDirection()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (AllowsVerticalInput())
                {
                    if (keyboard.upArrowKey.isPressed)
                    {
                        return Vector2Int.up;
                    }

                    if (keyboard.downArrowKey.isPressed)
                    {
                        return Vector2Int.down;
                    }
                }

                if (AllowsHorizontalInput())
                {
                    if (keyboard.leftArrowKey.isPressed)
                    {
                        return Vector2Int.left;
                    }

                    if (keyboard.rightArrowKey.isPressed)
                    {
                        return Vector2Int.right;
                    }
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (AllowsVerticalInput())
            {
                if (Input.GetKey(KeyCode.UpArrow))
                {
                    return Vector2Int.up;
                }

                if (Input.GetKey(KeyCode.DownArrow))
                {
                    return Vector2Int.down;
                }
            }

            if (AllowsHorizontalInput())
            {
                if (Input.GetKey(KeyCode.LeftArrow))
                {
                    return Vector2Int.left;
                }

                if (Input.GetKey(KeyCode.RightArrow))
                {
                    return Vector2Int.right;
                }
            }
#endif

            return Vector2Int.zero;
        }

        private bool TryReadSubmit()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                return true;
            }
#endif

            return false;
        }

        private bool AllowsVerticalInput()
        {
            return navigationAxis == MenuSelectAxis.Vertical || navigationAxis == MenuSelectAxis.Both;
        }

        private bool AllowsHorizontalInput()
        {
            return navigationAxis == MenuSelectAxis.Horizontal || navigationAxis == MenuSelectAxis.Both;
        }

        private void RemoveInvalidButtons()
        {
            buttons.RemoveAll(button => button == null || !button.IsInteractable());
        }

        private void RegisterMenuItems()
        {
            foreach (var button in buttons)
            {
                var item = button.GetComponent<MenuSelectItem>();
                if (item == null)
                {
                    item = button.gameObject.AddComponent<MenuSelectItem>();
                }

                item.Initialize(this, button);
            }
        }

        private void ApplySelection(Button button, int index)
        {
            if (button == null || !button.IsInteractable())
            {
                return;
            }

            if (_currentIndex == index && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject)
            {
                return;
            }

            _currentIndex = index;
            _isApplyingSelection = true;
            button.Select();
            _isApplyingSelection = false;

            SelectionChanged?.Invoke(button, _currentIndex);
            onSelectionChanged?.Invoke(button, _currentIndex);
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < buttons.Count;
        }

        private static int Mod(int value, int count)
        {
            return (value % count + count) % count;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MenuSelectItem : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, ISelectHandler
    {
        private MenuSelectSystem _owner;
        private Button _button;

        public void Initialize(MenuSelectSystem owner, Button button)
        {
            _owner = owner;
            _button = button;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _owner?.SelectFromPointer(_button);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _owner?.SelectFromPointerDown(_button);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _owner?.SyncExternalSelection(_button);
        }
    }
}

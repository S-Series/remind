using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace REmind.Gameplay.Input.Routing
{
    [DisallowMultipleComponent]
    public sealed class RhythmInputRouter : MonoBehaviour
    {
        private const string RhythmActionMapName = "Rhythm";

        private static readonly ActionBinding[] ActionBindings =
        {
            new ActionBinding("Lane01", 0),
            new ActionBinding("Lane02", 1),
            new ActionBinding("Lane03", 2),
            new ActionBinding("Lane04", 3),
            new ActionBinding("Lane05", 4),
            new ActionBinding("Lane06", 5),
            new ActionBinding("Lane07", 6),
            new ActionBinding("Lane08", 7),
            new ActionBinding("Lane09", 8),
            new ActionBinding("Lane10", 9),
        };

        [SerializeField] private InputActionAsset inputActions;

        private readonly Dictionary<InputAction, ActionBinding> bindingByAction =
            new Dictionary<InputAction, ActionBinding>(ActionBindings.Length);

        private InputActionMap rhythmActionMap;
        private bool isBound;

        public event Action<RhythmInputEvent> InputPerformed;

        public InputActionAsset InputActions => inputActions;
        public bool IsReady => isBound;

        private void Awake()
        {
            TryBindActions();
        }

        private void OnEnable()
        {
            if (TryBindActions())
            {
                rhythmActionMap.Enable();
            }
        }

        private void OnDisable()
        {
            rhythmActionMap?.Disable();
        }

        private void OnDestroy()
        {
            UnbindActions();
        }

        public void SetInputEnabled(bool value)
        {
            if (!TryBindActions())
            {
                return;
            }

            if (value)
            {
                rhythmActionMap.Enable();
            }
            else
            {
                rhythmActionMap.Disable();
            }
        }

        private bool TryBindActions()
        {
            if (isBound)
            {
                return true;
            }

            if (inputActions == null)
            {
                Debug.LogError("Input Actions is not assigned to RhythmInputRouter.", this);
                return false;
            }

            rhythmActionMap = inputActions.FindActionMap(RhythmActionMapName, false);
            if (rhythmActionMap == null)
            {
                Debug.LogError(
                    $"Input Action Map '{RhythmActionMapName}' was not found.",
                    this);
                return false;
            }

            for (int i = 0; i < ActionBindings.Length; i++)
            {
                ActionBinding binding = ActionBindings[i];
                InputAction action = rhythmActionMap.FindAction(binding.ActionName, false);

                if (action == null)
                {
                    UnbindActions();
                    Debug.LogError(
                        $"Input Action '{binding.ActionName}' was not found in '{RhythmActionMapName}'.",
                        this);
                    return false;
                }

                bindingByAction.Add(action, binding);
                action.performed += HandleActionPerformed;
            }

            isBound = true;
            return true;
        }

        private void UnbindActions()
        {
            foreach (KeyValuePair<InputAction, ActionBinding> pair in bindingByAction)
            {
                pair.Key.performed -= HandleActionPerformed;
            }

            bindingByAction.Clear();
            rhythmActionMap = null;
            isBound = false;
        }

        private void HandleActionPerformed(InputAction.CallbackContext context)
        {
            if (!bindingByAction.TryGetValue(context.action, out ActionBinding binding))
            {
                return;
            }

            InputPerformed?.Invoke(
                new RhythmInputEvent(binding.Lane, context.time));
        }

        private readonly struct ActionBinding
        {
            public string ActionName { get; }
            public int Lane { get; }

            public ActionBinding(string actionName, int lane)
            {
                ActionName = actionName;
                Lane = lane;
            }
        }
    }
}

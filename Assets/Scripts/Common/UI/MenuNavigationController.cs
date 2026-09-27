using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace REmind.Common.UI
{
    /// <summary>Owns the active menu scope and restores focus after a modal closes.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuNavigationController : MonoBehaviour
    {
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private NavigationScope initialScope;

        private readonly List<NavigationScope> scopes = new List<NavigationScope>();
        private readonly List<NavigationNode> savedSelections = new List<NavigationNode>();

        public NavigationScope ActiveScope => scopes.Count == 0 ? null : scopes[scopes.Count - 1];

        private void Start()
        {
            if (!eventSystem)
                eventSystem = EventSystem.current;
            if (initialScope)
                ShowRoot(initialScope);
        }

        public void ShowRoot(NavigationScope scope)
        {
            if (!scope) return;
            for (int i = scopes.Count - 1; i >= 0; i--)
                scopes[i].SetNavigationActive(false);
            scopes.Clear();
            savedSelections.Clear();
            scopes.Add(scope);
            savedSelections.Add(null);
            scope.SetNavigationActive(true);
            Select(scope.FirstAvailableNode);
        }

        public void Push(NavigationScope scope)
        {
            if (!scope || scopes.Contains(scope)) return;
            var previous = ActiveScope;
            if (previous)
            {
                savedSelections[savedSelections.Count - 1] = previous.FindNode(
                    eventSystem ? eventSystem.currentSelectedGameObject : null);
                previous.SetNavigationActive(false);
            }
            scopes.Add(scope);
            savedSelections.Add(null);
            scope.SetNavigationActive(true);
            Select(scope.FirstAvailableNode);
        }

        public void Pop()
        {
            if (scopes.Count <= 1) return;
            ActiveScope.SetNavigationActive(false);
            scopes.RemoveAt(scopes.Count - 1);
            savedSelections.RemoveAt(savedSelections.Count - 1);
            var restored = ActiveScope;
            restored.SetNavigationActive(true);
            var saved = savedSelections[savedSelections.Count - 1];
            Select(saved && saved.IsAvailable ? saved : restored.FirstAvailableNode);
        }

        public void Clear()
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
                scopes[i].SetNavigationActive(false);
            scopes.Clear();
            savedSelections.Clear();
            if (eventSystem)
                eventSystem.SetSelectedGameObject(null);
        }

        internal void SelectFromPointer(NavigationNode node)
        {
            if (node && node.Scope == ActiveScope && node.IsAvailable)
                Select(node);
        }

        internal void HandleCancel(NavigationNode node)
        {
            if (!node || node.Scope != ActiveScope) return;
            if (ActiveScope.HasCancelAction)
                ActiveScope.InvokeCancel();
            else
                Pop();
        }

        private void Select(NavigationNode node)
        {
            if (eventSystem && node && node.IsAvailable)
                eventSystem.SetSelectedGameObject(node.gameObject);
        }
    }
}

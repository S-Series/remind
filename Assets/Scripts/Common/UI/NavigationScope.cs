using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace REmind.Common.UI
{
    /// <summary>A serialized set of selectable controls on one screen or modal.</summary>
    [DisallowMultipleComponent]
    public sealed class NavigationScope : MonoBehaviour
    {
        [SerializeField] private MenuNavigationController controller;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private NavigationNode initialSelection;
        [SerializeField] private List<NavigationNode> nodes = new List<NavigationNode>();
        [SerializeField] private UnityEvent onCancel = new UnityEvent();

        public MenuNavigationController Controller => controller;
        public void BindController(MenuNavigationController value) =>
            controller = value;
        public bool HasCancelAction => onCancel.GetPersistentEventCount() > 0;
        public NavigationNode FirstAvailableNode
        {
            get
            {
                if (initialSelection && initialSelection.IsAvailable)
                    return initialSelection;
                foreach (var node in nodes)
                    if (node && node.IsAvailable) return node;
                return null;
            }
        }

        private void Awake()
        {
            RebuildNavigation();
        }

        public void RebuildNavigation()
        {
            foreach (var node in nodes)
            {
                if (!node) continue;
                node.Bind(this);
                var selectable = node.Selectable;
                if (!selectable) continue;
                var navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = Resolve(node, -1, 0, node.Up);
                navigation.selectOnDown = Resolve(node, 1, 0, node.Down);
                navigation.selectOnLeft = Resolve(node, 0, -1, node.Left);
                navigation.selectOnRight = Resolve(node, 0, 1, node.Right);
                selectable.navigation = navigation;
            }
        }

        public NavigationNode FindNode(GameObject selected)
        {
            foreach (var node in nodes)
                if (node && node.gameObject == selected) return node;
            return null;
        }

        public void SetNavigationActive(bool active)
        {
            if (!canvasGroup) return;
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
        }

        public void InvokeCancel() => onCancel.Invoke();

        private Selectable Resolve(NavigationNode source, int rowStep,
            int columnStep, NavigationNode explicitNode)
        {
            if (explicitNode)
                return nodes.Contains(explicitNode) ? explicitNode.Selectable : null;
            NavigationNode best = null;
            int bestDistance = int.MaxValue;
            foreach (var candidate in nodes)
            {
                if (!candidate || candidate == source ||
                    !candidate.IsAvailable) continue;
                int deltaRow = candidate.Row - source.Row;
                int deltaColumn = candidate.Column - source.Column;
                int distance = rowStep != 0 ? deltaRow * rowStep :
                    deltaColumn * columnStep;
                if (distance <= 0 ||
                    (rowStep != 0 ? deltaColumn != 0 : deltaRow != 0) ||
                    distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best ? best.Selectable : null;
        }
    }
}

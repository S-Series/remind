using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace REmind.Common.UI
{
    /// <summary>Scene-authored focus position, pointer sync and selection visuals.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class NavigationNode : MonoBehaviour, IPointerEnterHandler,
        IPointerMoveHandler,
        IPointerDownHandler, ISelectHandler, IDeselectHandler, ICancelHandler
    {
        [SerializeField] private Selectable selectable;
        [SerializeField] private int row;
        [SerializeField] private int column;
        [SerializeField] private NavigationNode up;
        [SerializeField] private NavigationNode down;
        [SerializeField] private NavigationNode left;
        [SerializeField] private NavigationNode right;
        [SerializeField] private Graphic[] selectionGraphics;
        [SerializeField] private Color selectedColor = new Color(0.75f, 0.9f, 1f, 1f);

        private Color[] normalColors;
        private NavigationScope scope;

        public Selectable Selectable => selectable ? selectable : GetComponent<Selectable>();
        public NavigationScope Scope => scope;
        public int Row => row;
        public int Column => column;
        public NavigationNode Up => up;
        public NavigationNode Down => down;
        public NavigationNode Left => left;
        public NavigationNode Right => right;
        public bool IsAvailable => isActiveAndEnabled && Selectable &&
            Selectable.IsActive() && Selectable.IsInteractable();

        private void Awake()
        {
            normalColors = new Color[selectionGraphics.Length];
            for (int i = 0; i < selectionGraphics.Length; i++)
                if (selectionGraphics[i]) normalColors[i] = selectionGraphics[i].color;
        }

        internal void Bind(NavigationScope owner) => scope = owner;

        public void OnPointerEnter(PointerEventData eventData)
        {
            // A stationary cursor must not undo keyboard navigation.
            if (eventData.delta.sqrMagnitude > 0f)
                scope?.Controller?.SelectFromPointer(this);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (eventData.delta.sqrMagnitude > 0f)
                scope?.Controller?.SelectFromPointer(this);
        }

        public void OnPointerDown(PointerEventData eventData) =>
            scope?.Controller?.SelectFromPointer(this);

        public void OnSelect(BaseEventData eventData) => SetSelected(true);
        public void OnDeselect(BaseEventData eventData) => SetSelected(false);
        public void OnCancel(BaseEventData eventData) =>
            scope?.Controller?.HandleCancel(this);

        private void SetSelected(bool selected)
        {
            if (normalColors == null) return;
            for (int i = 0; i < selectionGraphics.Length; i++)
                if (selectionGraphics[i])
                    selectionGraphics[i].color = selected ? selectedColor : normalColors[i];
        }
    }
}

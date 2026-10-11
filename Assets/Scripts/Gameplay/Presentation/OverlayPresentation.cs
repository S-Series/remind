using System;
using System.Collections;
using UnityEngine;

namespace REmind.Gameplay.Presentation
{
    /// <summary>Unscaled entrance and exit for a Game modal. Navigation stays with its owner.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class OverlayPresentation : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panel;
        [SerializeField, Min(0f)] private float openSeconds = 0.24f;
        [SerializeField, Min(0f)] private float closeSeconds = 0.18f;
        [SerializeField, Range(0.8f, 1f)] private float hiddenScale = 0.97f;

        private Coroutine activeRoutine;
        private Vector3 panelScale;

        public bool IsOpening { get; private set; }
        public bool IsClosing { get; private set; }
        public bool IsInteractive => gameObject.activeInHierarchy &&
            !IsOpening && !IsClosing && canvasGroup &&
            canvasGroup.interactable;

        public void BlockInteraction()
        {
            if (!canvasGroup) return;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
        }

        private void Awake()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            if (panel) panelScale = panel.localScale;
        }

        private void OnDisable() => ForceHide();

        public void PlayOpen()
        {
            if (!canvasGroup) return;
            StopAnimation();
            IsOpening = true;
            IsClosing = false;
            BlockInteraction();
            canvasGroup.alpha = 0f;
            SetPanelScale(hiddenScale);
            if (openSeconds <= 0f)
            {
                CompleteOpen();
                return;
            }
            activeRoutine = StartCoroutine(Animate(0f, 1f, hiddenScale,
                1f, openSeconds, CompleteOpen));
        }

        public void PlayClose(Action completed)
        {
            if (!canvasGroup || IsClosing) return;
            StopAnimation();
            IsOpening = false;
            IsClosing = true;
            BlockInteraction();
            float fromAlpha = canvasGroup.alpha;
            float fromScale = CurrentScale();
            if (closeSeconds <= 0f)
            {
                CompleteClose(completed);
                return;
            }
            activeRoutine = StartCoroutine(Animate(fromAlpha, 0f,
                fromScale, hiddenScale, closeSeconds,
                () => CompleteClose(completed)));
        }

        public void ForceHide()
        {
            StopAnimation();
            IsOpening = false;
            IsClosing = false;
            if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            SetPanelScale(1f);
        }

        private IEnumerator Animate(float fromAlpha, float toAlpha,
            float fromScale, float toScale, float seconds, Action completed)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                float eased = t * t * (3f - 2f * t);
                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
                SetPanelScale(Mathf.Lerp(fromScale, toScale, eased));
                yield return null;
            }
            activeRoutine = null;
            completed?.Invoke();
        }

        private void CompleteOpen()
        {
            activeRoutine = null;
            IsOpening = false;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            SetPanelScale(1f);
        }

        private void CompleteClose(Action completed)
        {
            activeRoutine = null;
            IsClosing = false;
            canvasGroup.alpha = 0f;
            SetPanelScale(hiddenScale);
            // Keep blocking Home until the owner pops its modal scope.
            completed?.Invoke();
        }

        private void StopAnimation()
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        private float CurrentScale()
        {
            if (!panel || panelScale.x == 0f) return 1f;
            return panel.localScale.x / panelScale.x;
        }

        private void SetPanelScale(float multiplier)
        {
            if (panel) panel.localScale = panelScale * multiplier;
        }
    }
}

using TMPro;
using UnityEngine;

namespace REmind.Gameplay.Demo
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class OverflowMarquee : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup opacity;
        [SerializeField, Min(0f)] private float startDelay = 0.8f;
        [SerializeField, Min(1f)] private float pixelsPerSecond = 36f;
        [SerializeField, Min(0f)] private float endDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.25f;
        [SerializeField, Min(0f)] private float trailingSpace = 6f;

        private enum Phase { StartDelay, Scrolling, EndDelay, FadeOut, FadeIn }

        private RectTransform viewport;
        private string measuredText;
        private float measuredViewportWidth = -1f;
        private float measuredFontSize = -1f;
        private float scrollDistance;
        private float phaseTime;
        private float initialY;
        private Phase phase;

        private void Awake()
        {
            viewport = (RectTransform)transform;
            if (content != null) initialY = content.anchoredPosition.y;
        }

        private void OnEnable()
        {
            measuredViewportWidth = -1f;
            ResetLayout();
        }

        private void OnDisable()
        {
            if (content != null) SetOffset(0f);
            if (opacity != null) opacity.alpha = 1f;
        }

        private void LateUpdate()
        {
            if (content == null || label == null || opacity == null) return;

            float width = viewport.rect.width;
            if (label.text != measuredText ||
                !Mathf.Approximately(width, measuredViewportWidth) ||
                !Mathf.Approximately(label.fontSize, measuredFontSize))
                ResetLayout();

            if (scrollDistance <= 0f) return;

            phaseTime += Time.unscaledDeltaTime;
            switch (phase)
            {
                case Phase.StartDelay:
                    if (phaseTime >= startDelay) Next(Phase.Scrolling);
                    break;
                case Phase.Scrolling:
                    SetOffset(Mathf.Min(scrollDistance, phaseTime * pixelsPerSecond));
                    if (phaseTime * pixelsPerSecond >= scrollDistance) Next(Phase.EndDelay);
                    break;
                case Phase.EndDelay:
                    if (phaseTime >= endDelay) Next(Phase.FadeOut);
                    break;
                case Phase.FadeOut:
                    opacity.alpha = 1f - Mathf.Clamp01(phaseTime / fadeDuration);
                    if (phaseTime >= fadeDuration)
                    {
                        opacity.alpha = 0f;
                        SetOffset(0f);
                        Next(Phase.FadeIn);
                    }
                    break;
                case Phase.FadeIn:
                    opacity.alpha = Mathf.Clamp01(phaseTime / fadeDuration);
                    if (phaseTime >= fadeDuration)
                    {
                        opacity.alpha = 1f;
                        Next(Phase.StartDelay);
                    }
                    break;
            }
        }

        private void ResetLayout()
        {
            if (viewport == null) viewport = (RectTransform)transform;
            if (content == null || label == null || opacity == null) return;

            measuredText = label.text;
            measuredViewportWidth = viewport.rect.width;
            measuredFontSize = label.fontSize;
            float textWidth = label.GetPreferredValues(measuredText, Mathf.Infinity, Mathf.Infinity).x;
            bool overflows = textWidth > measuredViewportWidth + 0.5f;
            float contentWidth = overflows ? textWidth + trailingSpace : measuredViewportWidth;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
            scrollDistance = overflows ? contentWidth - measuredViewportWidth : 0f;
            opacity.alpha = 1f;
            SetOffset(0f);
            Next(Phase.StartDelay);
        }

        private void SetOffset(float x)
        {
            content.anchoredPosition = new Vector2(-x, initialY);
        }

        private void Next(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }
    }
}

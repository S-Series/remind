using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace REmind.Gameplay
{
    /// <summary>Shows Bootstrap loading and waits for a fresh button press before navigation.</summary>
    [DisallowMultipleComponent]
    public sealed class BootstrapLoadingController : MonoBehaviour
    {
        [SerializeField] private AppRoot appRoot;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform progressStar;
        [SerializeField] private TMP_Text percentageLabel;
        [SerializeField] private TMP_Text versionLabel;
        [SerializeField] private TMP_Text loadingMessageLabel;
        [SerializeField, Min(0f)] private float minimumVisibleSeconds = 0.7f;

        private IDisposable buttonPressListener;
        private bool transitionRequested;

        public bool IsReadyForInteraction { get; private set; }

        private IEnumerator Start()
        {
            if (versionLabel)
                versionLabel.text = "VER. " + Application.version;
            SetProgress(0f);

            float elapsed = 0f;
            while (elapsed < minimumVisibleSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetProgress(minimumVisibleSeconds > 0f
                    ? Mathf.Clamp01(elapsed / minimumVisibleSeconds) : 1f);
                yield return null;
            }

            SetProgress(1f);
            if (loadingMessageLabel)
                loadingMessageLabel.text = "아무 키나 누르거나 클릭하세요";
            // A button already held while loading must not dismiss Bootstrap.
            yield return null;
            IsReadyForInteraction = true;
            buttonPressListener = InputSystem.onAnyButtonPress.Call(_ => TryContinue());
        }

        public bool TryContinue()
        {
            if (!IsReadyForInteraction || transitionRequested) return false;
            if (!appRoot)
            {
                Debug.LogError("Bootstrap AppRoot reference is missing.", this);
                return false;
            }

            appRoot.CompleteBootstrapLoading();
            if (!appRoot.IsTransitioning) return false;
            transitionRequested = true;
            buttonPressListener?.Dispose();
            buttonPressListener = null;
            return true;
        }

        private void OnDisable()
        {
            buttonPressListener?.Dispose();
            buttonPressListener = null;
            IsReadyForInteraction = false;
        }

        private void SetProgress(float value)
        {
            if (progressFill) progressFill.fillAmount = value;
            if (percentageLabel)
                percentageLabel.text = Mathf.RoundToInt(value * 100f) + "%";
            if (!progressStar) return;
            RectTransform track = progressStar.parent as RectTransform;
            if (track)
                progressStar.anchoredPosition = new Vector2(
                    track.rect.width * value, -track.rect.height * 0.5f);
        }
    }
}

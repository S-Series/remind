using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace REmind.Gameplay
{
    /// <summary>Plays the persistent Music Select transition over scene UI.</summary>
    public sealed class SceneTransitionController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer frameSource;
        [SerializeField] private AnimationClip musicSelected;
        [SerializeField] private Sprite firstFrame;
        [SerializeField] private Canvas overlayCanvas;
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private Image overlayImage;
        [SerializeField, Min(0f)] private float revealSeconds = 0.25f;

        private bool transitioning;
        private float playbackStartedAt;

        public bool IsTransitioning => transitioning;
        public bool OverlayVisible => overlayCanvas && overlayCanvas.enabled;
        public Sprite CurrentFrame => overlayImage ? overlayImage.sprite : null;

        private void Awake()
        {
            if (frameSource) frameSource.enabled = false;
            if (overlayCanvas) overlayCanvas.enabled = false;
            if (animator)
            {
                // The authored controller changes the clip speed and exits to an empty state.
                // Sample its clip directly so all 48 frames play at the authored rate.
                animator.enabled = false;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        public bool BeginMusicSelected()
        {
            if (transitioning || !animator || !frameSource ||
                !musicSelected || !firstFrame || !overlayCanvas ||
                !overlayGroup || !overlayImage) return false;
            transitioning = true;
            overlayImage.sprite = firstFrame;
            overlayGroup.alpha = 1f;
            overlayGroup.blocksRaycasts = true;
            overlayCanvas.enabled = true;
            playbackStartedAt = Time.unscaledTime;
            musicSelected.SampleAnimation(frameSource.gameObject, 0f);
            StartCoroutine(PlayAndOpenGame());
            return true;
        }

        private void LateUpdate()
        {
            if (!transitioning || !overlayCanvas.enabled) return;
            float sampleTime = Mathf.Min(Time.unscaledTime - playbackStartedAt,
                Mathf.Max(0f, musicSelected.length - 0.001f));
            musicSelected.SampleAnimation(frameSource.gameObject, sampleTime);
            if (frameSource.sprite) overlayImage.sprite = frameSource.sprite;
        }

        private IEnumerator PlayAndOpenGame()
        {
            // Hold the last authored frame while the next scene loads.
            float duration = Mathf.Max(0.1f, musicSelected.length - 0.01f);
            while (Time.unscaledTime - playbackStartedAt < duration)
                yield return null;

            animator.enabled = false;
            AsyncOperation load = SceneManager.LoadSceneAsync("Game");
            if (load != null)
                while (!load.isDone) yield return null;
            yield return null;

            float revealStart = Time.unscaledTime;
            while (Time.unscaledTime - revealStart < revealSeconds)
            {
                overlayGroup.alpha = 1f - Mathf.Clamp01(
                    (Time.unscaledTime - revealStart) /
                    Mathf.Max(0.001f, revealSeconds));
                yield return null;
            }
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = false;
            overlayCanvas.enabled = false;
            overlayImage.sprite = null;
            frameSource.sprite = null;
            transitioning = false;
        }
    }
}

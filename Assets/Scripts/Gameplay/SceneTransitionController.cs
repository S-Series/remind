using System.Collections;
using REmind.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace REmind.Gameplay
{
    /// <summary>Owns scene loading; the authored players own their visuals.</summary>
    public sealed class SceneTransitionController : MonoBehaviour
    {
        private const float DarkFadeSeconds = 0.15f;
        private const float SceneLoadDelaySeconds = 0.25f;

        [SerializeField] private CrystalTransitionPlayer crystalOverlayTransition;
        [SerializeField] private CrystalTransitionPlayer crystalTransition;
        [SerializeField] private Canvas fadeCanvas;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField, Min(0f)] private float revealSeconds = 0.25f;

        private bool transitioning;

        public bool IsTransitioning => transitioning;
        public bool OverlayVisible => (crystalOverlayTransition &&
            crystalOverlayTransition.visibility &&
            crystalOverlayTransition.visibility.alpha > 0f) ||
            (crystalTransition && crystalTransition.visibility &&
             crystalTransition.visibility.alpha > 0f) ||
            (fadeCanvas && fadeCanvas.enabled && fadeGroup &&
             fadeGroup.alpha > 0f);
        public float TransitionElapsed => crystalOverlayTransition
            ? crystalOverlayTransition.Elapsed : 0f;

        private void Awake()
        {
            if (TryGetComponent(out Animator legacyAnimator))
                legacyAnimator.enabled = false;
            if (TryGetComponent(out SpriteRenderer legacyRenderer))
                legacyRenderer.enabled = false;
            if (crystalOverlayTransition) crystalOverlayTransition.Hide();
            if (crystalTransition) crystalTransition.Hide();
            HideFade();
        }

        public bool BeginMusicSelected() => TransitionTo("Game", true);

        public bool TransitionTo(string sceneName, bool useCrystalCover = false)
        {
            if (transitioning || string.IsNullOrEmpty(sceneName) ||
                !Application.CanStreamedLevelBeLoaded(sceneName) ||
                !crystalOverlayTransition || !crystalOverlayTransition.clip ||
                !crystalOverlayTransition.loopClip ||
                !crystalOverlayTransition.outroClip ||
                !crystalOverlayTransition.visibility || !fadeCanvas ||
                !fadeGroup || (useCrystalCover && (!crystalTransition ||
                !crystalTransition.clip || !crystalTransition.visibility ||
                !crystalTransition.whiteCover))) return false;

            transitioning = true;
            float introStartedAt = Time.unscaledTime;
            crystalOverlayTransition.PlayPhase(crystalOverlayTransition.clip,
                crystalOverlayTransition.Duration * 0.5f, false, false);
            if (!crystalOverlayTransition.IsPlaying)
            {
                transitioning = false;
                return false;
            }
            StartCoroutine(PlayAndLoad(sceneName, useCrystalCover, introStartedAt));
            return true;
        }

        private IEnumerator PlayAndLoad(string sceneName, bool useCrystalCover,
            float introStartedAt)
        {
            float halfDuration = crystalOverlayTransition.Duration * 0.5f;
            float darkFadeInSeconds = Mathf.Min(DarkFadeSeconds, halfDuration);
            float darkFadeOutSeconds = Mathf.Min(DarkFadeSeconds, halfDuration);
            if (!useCrystalCover)
            {
                fadeCanvas.enabled = true;
                fadeGroup.blocksRaycasts = true;
            }

            // Let Intro animate before general navigation starts loading. The
            // whiteout route still waits until its own cover is complete.
            AsyncOperation load = null;
            if (!useCrystalCover)
            {
                while (Time.unscaledTime - introStartedAt < SceneLoadDelaySeconds)
                {
                    fadeGroup.alpha = Mathf.Clamp01(
                        crystalOverlayTransition.Elapsed /
                        Mathf.Max(0.001f, darkFadeInSeconds));
                    yield return null;
                }
                load = StartSceneLoad(sceneName);
                if (load == null)
                {
                    FinishTransition();
                    yield break;
                }
            }
            while (crystalOverlayTransition.IsPlaying)
            {
                if (!useCrystalCover)
                    fadeGroup.alpha = Mathf.Clamp01(
                        crystalOverlayTransition.Elapsed /
                        Mathf.Max(0.001f, darkFadeInSeconds));
                yield return null;
            }
            if (!useCrystalCover) fadeGroup.alpha = 1f;

            bool looping = useCrystalCover || load.progress < 0.9f;
            if (looping)
                crystalOverlayTransition.PlayPhase(crystalOverlayTransition.loopClip,
                    crystalOverlayTransition.loopClip.length, true);

            if (useCrystalCover)
            {
                crystalTransition.Play();
                float fadeStart = Time.unscaledTime;
                crystalTransition.visibility.alpha = 0f;
                while (crystalTransition.IsPlaying && !crystalTransition.IsCovered)
                {
                    crystalTransition.visibility.alpha = Mathf.Clamp01(
                        (Time.unscaledTime - fadeStart) / 0.25f);
                    yield return null;
                }
                if (!crystalTransition.IsCovered)
                {
                    FinishTransition();
                    yield break;
                }
                crystalTransition.Pause();
                crystalTransition.visibility.alpha = 1f;
                load = StartSceneLoad(sceneName);
                if (load == null)
                {
                    FinishTransition();
                    yield break;
                }
            }

            while (load.progress < 0.9f) yield return null;
            load.allowSceneActivation = true;
            while (!load.isDone) yield return null;
            yield return null;

            if (looping)
            {
                crystalOverlayTransition.StopAfterCurrentLoop();
                while (crystalOverlayTransition.IsPlaying) yield return null;
            }
            crystalOverlayTransition.PlayPhase(crystalOverlayTransition.outroClip,
                halfDuration);
            if (useCrystalCover)
            {
                float revealStart = Time.unscaledTime;
                while (Time.unscaledTime - revealStart < revealSeconds)
                {
                    crystalTransition.visibility.alpha = 1f - Mathf.Clamp01(
                        (Time.unscaledTime - revealStart) /
                        Mathf.Max(0.001f, revealSeconds));
                    yield return null;
                }
                crystalTransition.Hide();
            }
            while (crystalOverlayTransition.IsPlaying)
            {
                if (!useCrystalCover)
                    fadeGroup.alpha = Mathf.Clamp01(
                        (crystalOverlayTransition.PlaybackDuration -
                         crystalOverlayTransition.Elapsed) /
                        Mathf.Max(0.001f, darkFadeOutSeconds));
                yield return null;
            }
            FinishTransition();
        }

        private void FinishTransition()
        {
            if (crystalTransition) crystalTransition.Hide();
            if (crystalOverlayTransition) crystalOverlayTransition.Hide();
            HideFade();
            transitioning = false;
        }

        private static AsyncOperation StartSceneLoad(string sceneName)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            if (load != null) load.allowSceneActivation = false;
            return load;
        }

        private void HideFade()
        {
            if (fadeGroup)
            {
                fadeGroup.alpha = 0f;
                fadeGroup.blocksRaycasts = false;
            }
            if (fadeCanvas) fadeCanvas.enabled = false;
        }
    }
}

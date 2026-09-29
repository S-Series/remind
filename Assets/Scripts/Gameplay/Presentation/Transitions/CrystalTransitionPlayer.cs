using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace REmind.Gameplay.Presentation
{
    /// <summary>Only displays an authored overlay. Scene loading stays with the application's transition controller.</summary>
    [DisallowMultipleComponent]
    public sealed class CrystalTransitionPlayer : MonoBehaviour
    {
        public AnimationClip clip;
        [Tooltip("Optional loading loop for the transparent scene overlay.")]
        public AnimationClip loopClip;
        [Tooltip("Optional exit clip for the transparent scene overlay.")]
        public AnimationClip outroClip;
        public CanvasGroup visibility;
        public CanvasGroup whiteCover;
        public RectTransform starSizeRoot;
        public CanvasGroup orbitBack, orbitFront, particles;
        [Min(0.05f)] public float duration = 1.5f;
        [Range(0.1f, 3f)] public float starSize = 1f;
        [Range(0f, 1f)] public float orbitOpacity = 1f;
        [Range(0f, 1f)] public float particleOpacity = 1f;
        [Tooltip("Use only in the demo scene; call Play explicitly when integrated into the game.")]
        public bool playOnStart;
        [Tooltip("Disable for a decorative overlay that should let clicks pass through.")]
        public bool blockInputDuringPlayback = true;
        [Tooltip("Transparent overlays return to invisible when the clip ends.")]
        public bool hideOnComplete;
        public UnityEvent covered = new UnityEvent();
        public UnityEvent completed = new UnityEvent();

        private bool coveredSent;
        private AnimationClip activeClip;
        private float activeDuration;
        private bool repeat;
        private bool stopAtLoopEnd;
        private bool hideAtCompletion;
        private int generation;
        public bool IsPlaying { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsCovered => whiteCover && whiteCover.alpha >= 0.999f;
        public float Duration => Mathf.Max(0.05f, duration);
        public float PlaybackDuration => activeClip ? activeDuration : Duration;

        // Clear any saved editing pose before the first frame is rendered.
        private void Awake() { if (!IsPlaying) Hide(); }
        private void Start() { if (IsPlaying) return; if (playOnStart) Play(); else Hide(); }
        private void Update() { if (IsPlaying) Advance(Time.unscaledDeltaTime); }
        private void OnDisable() { Hide(); }

        public void Play() => PlayPhase(clip, Duration);

        /// <summary>Plays one authored phase without changing the prefab's default clip.</summary>
        public void PlayPhase(AnimationClip phaseClip, float seconds, bool loop = false,
            bool hideWhenComplete = true)
        {
            if (!phaseClip) return;
            generation++;
            activeClip = phaseClip;
            activeDuration = Mathf.Max(0.05f, seconds);
            repeat = loop;
            stopAtLoopEnd = false;
            hideAtCompletion = hideWhenComplete;
            coveredSent = false;
            IsPlaying = true;
            Seek(0f);
        }

        public void Pause() { IsPlaying = false; }
        public void Resume() { if ((activeClip || clip) && Elapsed < PlaybackDuration) IsPlaying = true; }
        public void StopAfterCurrentLoop() { if (repeat) stopAtLoopEnd = true; }

        /// <summary>Explicit clock entry point; deterministic even when timeScale is zero.</summary>
        public void Advance(float seconds)
        {
            if (!IsPlaying || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            int cycle = generation;
            float next = Elapsed + Mathf.Max(0f, seconds);
            if (repeat && next >= PlaybackDuration && !stopAtLoopEnd)
                Seek(Mathf.Repeat(next, PlaybackDuration));
            else Seek(next);
            if (!coveredSent && IsCovered)
            {
                coveredSent = true;
                covered.Invoke();
                if (cycle != generation || !IsPlaying) return;
            }
            if (Elapsed >= PlaybackDuration)
            {
                IsPlaying = false;
                if (repeat)
                {
                    repeat = false;
                    stopAtLoopEnd = false;
                }
                else
                {
                    if (hideOnComplete && hideAtCompletion) Hide();
                    completed.Invoke();
                }
            }
        }

        /// <summary>Scrubs without sending lifecycle events. Whiteout holds its last pose; overlays hide at the end.</summary>
        public void Seek(float seconds)
        {
            AnimationClip current = activeClip ? activeClip : clip;
            if (!current || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            Elapsed = Mathf.Clamp(seconds, 0f, PlaybackDuration);
            current.SampleAnimation(gameObject, Elapsed / PlaybackDuration * current.length);
            if (visibility)
            {
                visibility.alpha = hideOnComplete && hideAtCompletion && !repeat &&
                    Elapsed >= PlaybackDuration ? 0f : 1f;
                visibility.blocksRaycasts = blockInputDuringPlayback && visibility.alpha > 0f;
            }
            if (starSizeRoot) starSizeRoot.localScale = Vector3.one * starSize;
            if (orbitBack) orbitBack.alpha = orbitOpacity;
            if (orbitFront) orbitFront.alpha = orbitOpacity;
            if (particles) particles.alpha = particleOpacity;
        }

        public void Hide()
        {
            generation++;
            IsPlaying = false;
            if (visibility) { visibility.alpha = 0f; visibility.blocksRaycasts = false; }
        }
    }
}

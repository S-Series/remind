using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace REmind.Gameplay.Effects
{
    [DisallowMultipleComponent]
    public sealed class JudgeEffect : MonoBehaviour
    {
        private static readonly int PlayTriggerHash = Animator.StringToHash("Play");

        [SerializeField] private TextMeshPro msText;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.05f)] private float displayDurationSeconds = 0.4f;

        private Action<JudgeEffect> releaseAction;
        private float remainingSeconds;
        private bool isPlaying;

        private void Awake()
        {
            CollectReferences();
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            remainingSeconds -= Time.unscaledDeltaTime;
            if (remainingSeconds > 0f)
            {
                return;
            }

            isPlaying = false;
            releaseAction?.Invoke(this);
        }

        private void OnValidate()
        {
            CollectReferences();
        }

        public void Initialize(Action<JudgeEffect> onCompleted)
        {
            releaseAction = onCompleted;
            CollectReferences();
        }

        public void PlayAnimation(double offsetMs, Vector2 localPosition)
        {
            if (msText == null || animator == null)
            {
                Debug.LogError("JudgeEffect references are missing.", this);
                releaseAction?.Invoke(this);
                return;
            }

            RectTransform rectTransform = (RectTransform)transform;
            rectTransform.anchoredPosition = localPosition;

            msText.SetText(
                offsetMs.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture) +
                " ms");

            animator.Rebind();
            animator.Update(0f);
            animator.ResetTrigger(PlayTriggerHash);
            animator.SetTrigger(PlayTriggerHash);

            remainingSeconds = displayDurationSeconds;
            isPlaying = true;
        }

        public void ResetForPool()
        {
            isPlaying = false;
            remainingSeconds = 0f;

            if (animator != null)
            {
                animator.ResetTrigger(PlayTriggerHash);
            }
        }

        private void CollectReferences()
        {
            if (msText == null)
            {
                msText = GetComponentInChildren<TextMeshPro>(true);
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }
    }
}

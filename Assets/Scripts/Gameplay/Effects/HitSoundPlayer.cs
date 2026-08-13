using System;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;

namespace REmind.Gameplay.Effects
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class HitSoundPlayer : MonoBehaviour
    {
        [SerializeField] private NoteJudgementSystem judgementSystem;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Min(0f)] private float trimStartMs;

        private AudioClip preparedClip;

        private void Awake()
        {
            if (!audioSource)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (!judgementSystem)
            {
                judgementSystem = FindFirstObjectByType<NoteJudgementSystem>();
            }

            if (!audioSource || !judgementSystem)
            {
                Debug.LogError(
                    "HitSoundPlayer requires an AudioSource and NoteJudgementSystem.",
                    this);
                enabled = false;
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            PrepareClip();
        }

        private void OnEnable()
        {
            if (judgementSystem)
            {
                judgementSystem.NoteJudged += HandleNoteJudged;
            }
        }

        private void OnDisable()
        {
            if (judgementSystem)
            {
                judgementSystem.NoteJudged -= HandleNoteJudged;
            }
        }

        private void OnDestroy()
        {
            if (preparedClip)
            {
                Destroy(preparedClip);
            }
        }

        public bool Play()
        {
            AudioClip clip = preparedClip ? preparedClip : audioSource.clip;

            if (!clip)
            {
                return false;
            }

            audioSource.PlayOneShot(clip);
            return true;
        }

        private void HandleNoteJudged(NoteJudgementEvent judgementEvent)
        {
            if (judgementEvent.Result == JudgeResult.Perfect)
            {
                Play();
            }
        }

        private void PrepareClip()
        {
            AudioClip sourceClip = audioSource.clip;

            if (!sourceClip || trimStartMs <= 0f)
            {
                return;
            }

            if (sourceClip.loadState == AudioDataLoadState.Unloaded &&
                !sourceClip.LoadAudioData())
            {
                Debug.LogWarning(
                    $"Could not preload hit sound: {sourceClip.name}",
                    this);
                return;
            }

            int trimFrames = Mathf.Clamp(
                Mathf.RoundToInt(trimStartMs / 1000f * sourceClip.frequency),
                0,
                sourceClip.samples - 1);

            if (trimFrames == 0)
            {
                return;
            }

            int channelCount = sourceClip.channels;
            float[] sourceData = new float[sourceClip.samples * channelCount];

            if (!sourceClip.GetData(sourceData, 0))
            {
                Debug.LogWarning(
                    $"Could not read hit sound data: {sourceClip.name}",
                    this);
                return;
            }

            int remainingFrames = sourceClip.samples - trimFrames;
            float[] trimmedData = new float[remainingFrames * channelCount];
            Array.Copy(
                sourceData,
                trimFrames * channelCount,
                trimmedData,
                0,
                trimmedData.Length);

            AudioClip clip = AudioClip.Create(
                $"{sourceClip.name}_RuntimeTrimmed",
                remainingFrames,
                channelCount,
                sourceClip.frequency,
                false);

            if (!clip.SetData(trimmedData, 0))
            {
                Destroy(clip);
                Debug.LogWarning(
                    $"Could not prepare hit sound: {sourceClip.name}",
                    this);
                return;
            }

            preparedClip = clip;
        }
    }
}

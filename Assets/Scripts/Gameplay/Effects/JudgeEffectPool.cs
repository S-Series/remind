using System;
using System.Collections.Generic;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;
using UnityEngine.Pool;

namespace REmind.Gameplay.Effects
{
    [DisallowMultipleComponent]
    public sealed class JudgeEffectPool : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NoteJudgementSystem judgementSystem;
        [SerializeField] private JudgeEffect effectPrefab;

        [Header("Pool")]
        [SerializeField, Min(1)] private int initialPoolSize = 12;
        [SerializeField, Min(1)] private int maxPoolSize = 32;

        [Header("Placement")]
        [SerializeField] private float noteXScale = 0.9f;
        [SerializeField] private float effectY;

        private ObjectPool<JudgeEffect> pool;

        private void Awake()
        {
            CollectReferences();

            if (judgementSystem == null || effectPrefab == null)
            {
                Debug.LogError(
                    "JudgeEffectPool requires a NoteJudgementSystem and JudgeEffect prefab.",
                    this);
                enabled = false;
                return;
            }

            int poolMaximum = Math.Max(initialPoolSize, maxPoolSize);
            pool = new ObjectPool<JudgeEffect>(
                CreateEffect,
                OnTakeFromPool,
                OnReturnedToPool,
                OnDestroyPooledEffect,
                true,
                initialPoolSize,
                poolMaximum);

            RegisterExistingEffects();
            Prewarm();
        }

        private void OnEnable()
        {
            if (judgementSystem != null)
            {
                judgementSystem.NoteJudged += HandleNoteJudged;
            }
        }

        private void OnDisable()
        {
            if (judgementSystem != null)
            {
                judgementSystem.NoteJudged -= HandleNoteJudged;
            }
        }

        private void OnDestroy()
        {
            pool?.Clear();
        }

        private void OnValidate()
        {
            maxPoolSize = Math.Max(initialPoolSize, maxPoolSize);
            CollectReferences();
        }

        private void HandleNoteJudged(NoteJudgementEvent judgementEvent)
        {
            if (judgementEvent.IsAutomaticMiss || pool == null)
            {
                return;
            }

            if (!judgementSystem.TryGetRegisteredNoteView(
                    judgementEvent.Note.Id,
                    out GameObject noteView))
            {
                return;
            }

            float noteX = noteView.transform.localPosition.x;
            JudgeEffect effect = pool.Get();
            effect.PlayAnimation(
                judgementEvent.OffsetMs,
                new Vector2(noteX * noteXScale, effectY));
        }

        private JudgeEffect CreateEffect()
        {
            JudgeEffect effect = Instantiate(effectPrefab, transform, false);
            effect.name = "JudgeEffect (Pooled)";
            effect.Initialize(ReturnToPool);
            return effect;
        }

        private void OnTakeFromPool(JudgeEffect effect)
        {
            effect.gameObject.SetActive(true);
        }

        private static void OnReturnedToPool(JudgeEffect effect)
        {
            effect.ResetForPool();
            effect.gameObject.SetActive(false);
        }

        private static void OnDestroyPooledEffect(JudgeEffect effect)
        {
            if (effect != null)
            {
                Destroy(effect.gameObject);
            }
        }

        private void ReturnToPool(JudgeEffect effect)
        {
            pool?.Release(effect);
        }

        private void RegisterExistingEffects()
        {
            JudgeEffect[] existingEffects = GetComponentsInChildren<JudgeEffect>(true);

            for (int i = 0; i < existingEffects.Length; i++)
            {
                JudgeEffect effect = existingEffects[i];
                effect.Initialize(ReturnToPool);
                pool.Release(effect);
            }
        }

        private void Prewarm()
        {
            var warmedEffects = new List<JudgeEffect>(initialPoolSize);

            for (int i = 0; i < initialPoolSize; i++)
            {
                warmedEffects.Add(pool.Get());
            }

            for (int i = 0; i < warmedEffects.Count; i++)
            {
                pool.Release(warmedEffects[i]);
            }
        }

        private void CollectReferences()
        {
            if (judgementSystem == null)
            {
                judgementSystem = FindFirstObjectByType<NoteJudgementSystem>();
            }

            if (effectPrefab == null)
            {
                effectPrefab = GetComponentInChildren<JudgeEffect>(true);
            }
        }
    }
}

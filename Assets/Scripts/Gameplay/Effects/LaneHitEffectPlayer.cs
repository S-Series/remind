using System;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;

namespace REmind.Gameplay.Effects
{
    [DisallowMultipleComponent]
    public sealed class LaneHitEffectPlayer : MonoBehaviour
    {
        [SerializeField] private NoteJudgementSystem judgementSystem;
        [SerializeField] private Animator[] laneAnimators;
        [SerializeField] private string triggerName = "Play";
        [SerializeField] private string effectStateName =
            "Base Layer.HitEffect";

        private int triggerHash;
        private int effectStateHash;

        private void Reset()
        {
            CollectReferences();
        }

        private void OnValidate()
        {
            CollectReferences();
        }

        private void Awake()
        {
            CollectReferences();

            if (!ValidateAnimators())
            {
                enabled = false;
            }
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

        public void Play(int lane)
        {
            if (laneAnimators == null ||
                lane < 0 ||
                lane >= laneAnimators.Length)
            {
                return;
            }

            Animator laneAnimator = laneAnimators[lane];
            if (laneAnimator == null)
            {
                return;
            }

            laneAnimator.ResetTrigger(triggerHash);
            laneAnimator.Play(effectStateHash, 0, 0f);
            laneAnimator.Update(0f);
        }

        /// <summary>모든 라인 이펙트를 기본 상태로 되돌립니다.</summary>
        public void ResetAll()
        {
            if (laneAnimators == null)
            {
                return;
            }

            for (int lane = 0; lane < laneAnimators.Length; lane++)
            {
                Animator laneAnimator = laneAnimators[lane];

                if (!laneAnimator)
                {
                    continue;
                }

                laneAnimator.ResetTrigger(triggerHash);
                laneAnimator.Rebind();
                laneAnimator.Update(0f);
            }
        }

        private void HandleNoteJudged(NoteJudgementEvent judgementEvent)
        {
            if (!judgementEvent.IsAutomaticMiss)
            {
                Play(judgementEvent.Note.Lane);
            }
        }

        private bool ValidateAnimators()
        {
            if (laneAnimators == null || laneAnimators.Length == 0)
            {
                Debug.LogError("No lane Animators were found under HitEffects.", this);
                return false;
            }

            for (int lane = 0; lane < laneAnimators.Length; lane++)
            {
                Animator laneAnimator = laneAnimators[lane];
                if (laneAnimator == null ||
                    !HasTrigger(laneAnimator, triggerHash) ||
                    !laneAnimator.HasState(0, effectStateHash))
                {
                    Debug.LogError(
                        $"Lane {lane + 1} Animator requires the " +
                        $"'{triggerName}' trigger and '{effectStateName}' state.",
                        this);
                    return false;
                }
            }

            return true;
        }

        private void CollectReferences()
        {
            if (judgementSystem == null)
            {
                judgementSystem = FindFirstObjectByType<NoteJudgementSystem>();
            }

            Animator[] discoveredAnimators =
                GetComponentsInChildren<Animator>(true);
            Array.Sort(discoveredAnimators, CompareAnimatorOrder);

            if (discoveredAnimators.Length > 0 &&
                !HasSameAnimators(laneAnimators, discoveredAnimators))
            {
                laneAnimators = discoveredAnimators;
            }

            triggerHash = Animator.StringToHash(triggerName);
            effectStateHash = Animator.StringToHash(effectStateName);
        }

        private static bool HasSameAnimators(
            Animator[] current,
            Animator[] discovered)
        {
            if (current == null || current.Length != discovered.Length)
            {
                return false;
            }

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] != discovered[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasTrigger(Animator animator, int parameterHash)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == parameterHash &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareAnimatorOrder(Animator left, Animator right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
        }
    }
}

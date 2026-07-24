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

        private int triggerHash;

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

            if (!ValidateReferences())
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
            if (lane < 0 || lane >= laneAnimators.Length)
            {
                return;
            }

            Animator laneAnimator = laneAnimators[lane];
            if (laneAnimator == null)
            {
                return;
            }

            laneAnimator.ResetTrigger(triggerHash);
            laneAnimator.SetTrigger(triggerHash);
        }

        private void HandleNoteJudged(NoteJudgementEvent judgementEvent)
        {
            if (!judgementEvent.IsAutomaticMiss)
            {
                Play(judgementEvent.Note.Lane);
            }
        }

        private bool ValidateReferences()
        {
            if (judgementSystem == null)
            {
                Debug.LogError("NoteJudgementSystem was not found for lane hit effects.", this);
                return false;
            }

            if (laneAnimators == null || laneAnimators.Length == 0)
            {
                Debug.LogError("No lane Animators were found under HitEffects.", this);
                return false;
            }

            for (int lane = 0; lane < laneAnimators.Length; lane++)
            {
                Animator laneAnimator = laneAnimators[lane];
                if (laneAnimator == null || !HasTrigger(laneAnimator, triggerHash))
                {
                    Debug.LogError(
                        $"Lane {lane + 1} Animator does not contain the '{triggerName}' trigger.",
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

            if (laneAnimators == null || laneAnimators.Length == 0)
            {
                laneAnimators = GetComponentsInChildren<Animator>(true);
                Array.Sort(laneAnimators, CompareAnimatorOrder);
            }

            triggerHash = Animator.StringToHash(triggerName);
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

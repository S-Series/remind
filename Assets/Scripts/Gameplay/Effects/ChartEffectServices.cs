using System;
using System.Collections.Generic;
using REmind.Charting;
using UnityEngine;

namespace REmind.Gameplay.Effects
{
    /// <summary>Only this session's contributions are removed. The base camera pose remains owned by its presenter.</summary>
    public sealed class EffectCameraMixer : IEffectCameraService, IDisposable
    {
        private readonly List<Offset> offsets = new List<Offset>();
        private readonly Action<Vector2, float> apply;
        private bool disposed;

        public EffectCameraMixer(Action<Vector2, float> apply)
        {
            this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
        }

        public IEffectCameraOffset CreateOffset(string ownerId)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EffectCameraMixer));
            var offset = new Offset(this);
            offsets.Add(offset);
            return offset;
        }

        public void Apply()
        {
            // A delayed owner callback from an earlier session must not reset the
            // camera contribution installed by a newer session.
            if (disposed) return;

            Vector2 position = Vector2.zero;
            float roll = 0f;
            foreach (Offset offset in offsets)
            {
                position += offset.Position;
                roll += offset.Roll;
            }
            apply(position, roll);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            offsets.Clear();
            apply(Vector2.zero, 0f);
        }

        private sealed class Offset : IEffectCameraOffset
        {
            private EffectCameraMixer owner;
            public Vector2 Position;
            public float Roll;
            public Offset(EffectCameraMixer owner) { this.owner = owner; }
            public void Set(double x, double y, double rollDegrees)
            {
                if (owner == null || owner.disposed) return;
                Position = new Vector2((float)x, (float)y);
                Roll = (float)rollDegrees;
            }
            public void Dispose()
            {
                if (owner == null) return;
                owner.offsets.Remove(this);
                owner = null;
            }
        }
    }

    /// <summary>Used only for explicit editor/test state, never for account progress.</summary>
    public sealed class EffectTestGameState : IEffectGameState
    {
        public double CurrentHealth { get; set; } = 100d;
    }

    /// <summary>Damage modifiers retain their effective interval until session disposal.</summary>
    public sealed class EffectRuleService : IEffectRuleService, IDisposable
    {
        private GameRule rule;
        private readonly List<DamageModifier> owned = new List<DamageModifier>();
        private bool activated;
        private bool disposed;

        // A null rule is an explicit preview simulation: handles still get tracked and released.
        public EffectRuleService(GameRule rule)
        {
            this.rule = rule;
            activated = rule;
        }

        /// <summary>Commits a prepared Gameplay service without exposing its modifiers early.</summary>
        public void Activate(GameRule target)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EffectRuleService));
            if (activated) throw new InvalidOperationException("Effect rules are already active.");
            rule = target ? target : throw new ArgumentNullException(nameof(target));
            int registered = 0;
            try
            {
                for (; registered < owned.Count; registered++)
                    rule.RegisterModifier(owned[registered]);
                activated = true;
            }
            catch
            {
                for (int i = registered - 1; i >= 0; i--)
                    rule.UnregisterModifier(owned[i]);
                rule = null;
                throw;
            }
        }

        public IEffectRuleHandle AddDamageMultiplier(string ownerId, double multiplier, double effectiveTimeMs)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EffectRuleService));
            var modifier = new DamageModifier(multiplier, effectiveTimeMs);
            owned.Add(modifier);
            if (activated) rule.RegisterModifier(modifier);
            return modifier;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (DamageModifier modifier in owned)
            {
                if (activated && rule) rule.UnregisterModifier(modifier);
                modifier.Dispose();
            }
            owned.Clear();
        }

        private sealed class DamageModifier : IRuleModifier, IEffectRuleHandle
        {
            private readonly double multiplier;
            private readonly double startTimeMs;
            private double endTimeMs = double.PositiveInfinity;

            public DamageModifier(double multiplier, double startTimeMs)
            {
                this.multiplier = multiplier;
                this.startTimeMs = startTimeMs;
            }

            public void ReleaseAt(double effectiveTimeMs) { endTimeMs = Math.Min(endTimeMs, effectiveTimeMs); }
            public void Dispose()
            {
                // ReleaseAt closes a historical interval; don't erase it when the handle is disposed.
                // Session disposal unregisters the entire modifier through its owning service.
                if (double.IsPositiveInfinity(endTimeMs)) endTimeMs = startTimeMs;
            }
            public JudgeWindows ModifyJudgeWindows(JudgeWindows current, RuleContext context) => current;
            public JudgeResult ModifyJudgeResult(JudgeResult current, RuleContext context) => current;
            public int ModifyMaxHealth(int current, RuleContext context) => current;
            public int ModifyHealthDelta(int current, JudgeResult result, RuleContext context)
            {
                if (current >= 0 || double.IsNaN(context.EvaluationTimeMs) ||
                    context.EvaluationTimeMs < startTimeMs || context.EvaluationTimeMs >= endTimeMs) return current;
                return (int)Math.Max(int.MinValue, Math.Round(current * multiplier, MidpointRounding.AwayFromZero));
            }
            public double ModifyScoreWeight(double current, JudgeResult result, RuleContext context) => current;
            public ComboBehavior ModifyComboBehavior(ComboBehavior current, JudgeResult result, RuleContext context) => current;
            public bool ModifyShouldFail(bool current, RuleContext context) => current;
            public bool ModifyIsCleared(bool current, RuleContext context) => current;
        }
    }

    /// <summary>Accept a request here; perform any scene/audio replacement after the frame pump returns.</summary>
    public sealed class EffectTransitionMailbox : IEffectTransitionService, IDisposable
    {
        private readonly Func<string, string, bool> canTransition;
        private bool closed;
        public string TargetMusicId { get; private set; }
        public string TargetDifficultyId { get; private set; }
        public bool HasRequest => TargetMusicId != null;

        public EffectTransitionMailbox(Func<string, string, bool> canTransition)
        {
            this.canTransition = canTransition ?? throw new ArgumentNullException(nameof(canTransition));
        }
        public bool CanTransitionTo(string musicId, string difficultyId) =>
            !closed && canTransition(musicId, difficultyId);
        public bool RequestTransition(string musicId, string difficultyId, EffectExecutionMode mode)
        {
            if (closed || HasRequest || !CanTransitionTo(musicId, difficultyId)) return false;
            TargetMusicId = musicId;
            TargetDifficultyId = difficultyId;
            return true;
        }
        public void Dispose() { closed = true; }
    }
}

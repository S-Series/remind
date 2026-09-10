using System;
using System.Collections.Generic;
using REmind.Charting;
using UnityEngine;

namespace REmind.Gameplay.Effects
{
    /// <summary>Damage modifiers retain their effective interval until session disposal.</summary>
    public sealed class EffectRuleService : IEffectRuleService, IDisposable
    {
        private GameRule rule;
        private readonly List<DamageModifier> owned = new List<DamageModifier>();
        private bool activated;
        private bool disposed;

        // A null rule is a staged Gameplay session. Handles are tracked but do
        // not reach the live GameRule until Activate commits the start.
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
}

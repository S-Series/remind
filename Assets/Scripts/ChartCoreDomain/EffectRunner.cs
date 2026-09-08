using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    public sealed class EffectExecutionFailure
    {
        internal EffectExecutionFailure(EffectSessionContext session, PlayableEffectEvent definition, Exception exception)
        {
            Message = $"Effect failed: music={session.MusicId}, difficulty={session.DifficultyId}, " +
                $"effectId={definition?.EffectId}, command={definition?.CommandId}, scheduledMs={definition?.TimeMs}: {exception.Message}";
            Exception = exception;
        }
        public string Message { get; }
        public Exception Exception { get; }
        public override string ToString() => Message;
    }

    /// <summary>Session-owned execution. The owning play loop merges inputs at NextEventTimeMs.</summary>
    public sealed class EffectRunner : IDisposable
    {
        private readonly PreparedEffectPlan plan;
        private readonly EffectSessionContext session;
        private readonly Effect[] effects;
        private readonly List<int> active = new List<int>();
        private MusicGimmick gimmick;
        private int next;
        private bool disposed;
        private bool advancing;
        private bool disposeRequested;
        private bool hasUpdated;
        private readonly double startTimeMs;
        private double triggerWatermarkMs = double.NegativeInfinity;
        private double previousObservedTimeMs;
        private double previousUpdateTimeMs;
        public EffectExecutionFailure Failure { get; private set; }
        public bool TransitionRequested => session.TransitionRequested;
        public double NextEventTimeMs => disposed || Failure != null || TransitionRequested || next >= effects.Length
            ? double.PositiveInfinity : plan.Entries[next].Definition.TimeMs;
        public int TriggeredCount => next;
        public MusicGimmick Gimmick => gimmick;

        public EffectRunner(PreparedEffectPlan plan, EffectSessionContext session, double startTimeMs = 0)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            effects = new Effect[plan.Count];
            try
            {
                if (!EffectParameterValidation.Finite(startTimeMs)) throw new ArgumentOutOfRangeException(nameof(startTimeMs));
                if (startTimeMs > 0 && !plan.SupportsSeek)
                    throw new InvalidOperationException("This chart contains stateful gimmicks. Start from the beginning; middle playback cannot restore their history.");
                this.startTimeMs = startTimeMs;
                previousObservedTimeMs = startTimeMs;
                // Check every capability/transition target before creating or running any effect.
                foreach (PreparedEffectEntry entry in plan.Entries)
                {
                    entry.Registration.ValidateServices?.Invoke(session, entry.Parameters);
                    entry.Command?.ValidateServices?.Invoke(session, entry.Parameters);
                }
                if (plan.Gimmick != null)
                {
                    gimmick = plan.Gimmick.Factory() ?? throw new InvalidOperationException("Gimmick factory returned null.");
                    gimmick.Prepare(session);
                }
                for (int i = 0; i < effects.Length; i++)
                {
                    PreparedEffectEntry entry = plan.Entries[i];
                    effects[i] = entry.Command != null
                        ? new CallMusicGimmickEffect(gimmick, entry.Command, entry.Parameters)
                        : entry.Registration.Factory(entry.Parameters);
                    if (effects[i] == null) throw new InvalidOperationException("Effect factory returned null.");
                }
            }
            catch (Exception exception)
            {
                List<Exception> cleanupFailures = Cleanup();
                if (cleanupFailures.Count > 0)
                {
                    var failures = new List<Exception>(cleanupFailures.Count + 1) { exception };
                    failures.AddRange(cleanupFailures);
                    throw new AggregateException("Effect runner initialization and cleanup failed.", failures);
                }
                throw;
            }
        }

        public bool AdvanceTo(double chartTimeMs)
        {
            if (disposed || Failure != null || TransitionRequested) return false;
            if (advancing) throw new InvalidOperationException("EffectRunner cannot be advanced recursively.");
            ValidateCurrentTime(chartTimeMs, nameof(chartTimeMs));
            if (hasUpdated && chartTimeMs == previousUpdateTimeMs) return true;
            previousObservedTimeMs = chartTimeMs;

            advancing = true;
            PlayableEffectEvent executing = null;
            try
            {
                TriggerThroughCore(chartTimeMs, chartTimeMs, ref executing);
                if (disposeRequested || TransitionRequested) return false;
                for (int i = 0; i < active.Count; i++)
                {
                    int index = active[i];
                    executing = plan.Entries[index].Definition;
                    effects[index].Update(new EffectExecutionContext(session, executing, chartTimeMs));
                    if (disposeRequested) return false;
                    if (effects[index].IsComplete)
                    {
                        effects[index].Dispose();
                        if (disposeRequested) return false;
                        active.RemoveAt(i--);
                    }
                }
                executing = null;
                gimmick?.Update(chartTimeMs);
                if (disposeRequested) return false;
                hasUpdated = true;
                previousUpdateTimeMs = chartTimeMs;
                return !TransitionRequested;
            }
            catch (Exception exception)
            {
                return Fail(executing, exception);
            }
            finally
            {
                advancing = false;
                if (disposeRequested) CompleteDispose();
            }
        }

        /// <summary>
        /// Triggers scheduled entries through <paramref name="cutoffTimeMs"/> while reporting
        /// the actual frame time to their start contexts. Active effects and the shared gimmick
        /// are not updated until AdvanceTo is called for that frame.
        /// </summary>
        public bool TriggerThrough(double cutoffTimeMs, double currentTimeMs)
        {
            if (disposed || Failure != null || TransitionRequested) return false;
            if (advancing) throw new InvalidOperationException("EffectRunner cannot be advanced recursively.");
            if (!EffectParameterValidation.Finite(cutoffTimeMs) || cutoffTimeMs < triggerWatermarkMs)
                throw new ArgumentOutOfRangeException(nameof(cutoffTimeMs), "Effect trigger time must be finite and move forward.");
            ValidateCurrentTime(currentTimeMs, nameof(currentTimeMs));
            if (cutoffTimeMs > currentTimeMs)
                throw new ArgumentOutOfRangeException(nameof(cutoffTimeMs), "Effect trigger time cannot be later than the current frame time.");
            previousObservedTimeMs = currentTimeMs;

            advancing = true;
            PlayableEffectEvent executing = null;
            try
            {
                TriggerThroughCore(cutoffTimeMs, currentTimeMs, ref executing);
                return !disposeRequested && !TransitionRequested;
            }
            catch (Exception exception)
            {
                return Fail(executing, exception);
            }
            finally
            {
                advancing = false;
                if (disposeRequested) CompleteDispose();
            }
        }

        private void TriggerThroughCore(double cutoffTimeMs, double currentTimeMs,
            ref PlayableEffectEvent executing)
        {
            while (next < effects.Length && plan.Entries[next].Definition.TimeMs <= cutoffTimeMs)
            {
                int index = next++;
                executing = plan.Entries[index].Definition;
                // Consume before calling user code: exceptions never implicitly replay side effects.
                active.Add(index);
                effects[index].Start(new EffectExecutionContext(session, executing, currentTimeMs));
                if (disposeRequested || TransitionRequested) break;
            }
            triggerWatermarkMs = cutoffTimeMs;
        }

        private void ValidateCurrentTime(double currentTimeMs, string parameterName)
        {
            if (!EffectParameterValidation.Finite(currentTimeMs) ||
                currentTimeMs < startTimeMs || currentTimeMs < previousObservedTimeMs)
                throw new ArgumentOutOfRangeException(parameterName,
                    "Effect frame time must be finite, no earlier than the session start, and move forward.");
        }

        private bool Fail(PlayableEffectEvent executing, Exception exception)
        {
            List<Exception> cleanupFailures = Cleanup();
            if (cleanupFailures.Count > 0)
            {
                var failures = new List<Exception>(cleanupFailures.Count + 1) { exception };
                failures.AddRange(cleanupFailures);
                exception = new AggregateException("Effect execution and cleanup failed.", failures);
            }
            Failure = new EffectExecutionFailure(session, executing, exception);
            return false;
        }

        public void Dispose()
        {
            if (disposed || disposeRequested) return;
            if (advancing)
            {
                disposeRequested = true;
                return;
            }
            CompleteDispose();
        }

        private void CompleteDispose()
        {
            disposeRequested = false;
            List<Exception> failures = Cleanup();
            if (failures.Count > 0 && Failure == null)
                Failure = new EffectExecutionFailure(session, null,
                    new AggregateException("Cleanup failed.", failures));
        }

        private List<Exception> Cleanup()
        {
            var failures = new List<Exception>();
            if (disposed) return failures;
            disposed = true;
            foreach (Effect effect in effects)
            {
                try { effect?.Dispose(); }
                catch (Exception exception) { failures.Add(exception); }
            }
            try { gimmick?.Dispose(); }
            catch (Exception exception) { failures.Add(exception); }
            try { session.Dispose(); }
            catch (Exception exception) { failures.Add(exception); }
            active.Clear();
            return failures;
        }
    }
}

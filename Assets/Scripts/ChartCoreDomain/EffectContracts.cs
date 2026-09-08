using System;
using System.Collections.Generic;
using System.Threading;

namespace REmind.Charting
{
    public enum EffectExecutionMode { Gameplay, Preview, AutoPlay }

    public interface IEffectCameraOffset : IDisposable
    {
        void Set(double x, double y, double rollDegrees);
    }

    public interface IEffectCameraService
    {
        IEffectCameraOffset CreateOffset(string ownerId);
    }

    public interface IEffectGameState { double CurrentHealth { get; } }

    public interface IEffectRuleHandle : IDisposable
    {
        // A release must preserve its historical interval for queued earlier input.
        void ReleaseAt(double effectiveTimeMs);
    }

    public interface IEffectRuleService
    {
        IEffectRuleHandle AddDamageMultiplier(string ownerId, double multiplier, double effectiveTimeMs);
    }

    public interface IEffectTransitionService
    {
        bool CanTransitionTo(string musicId, string difficultyId);
        // Submit only. The owning play loop performs replacement after the callback returns.
        bool RequestTransition(string musicId, string difficultyId, EffectExecutionMode mode);
    }

    /// <summary>Session-owned capabilities. No permanent-progress capability is exposed.</summary>
    public sealed class EffectSessionContext : IDisposable
    {
        private readonly IEffectCameraService camera;
        private readonly IEffectGameState gameState;
        private readonly IEffectRuleService rules;
        private readonly IEffectTransitionService transitions;
        private readonly List<IDisposable> owned = new List<IDisposable>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;
        private bool disposed;

        public EffectSessionContext(string musicId, string difficultyId, EffectExecutionMode mode,
            IEffectCameraService camera = null, IEffectGameState gameState = null,
            IEffectRuleService rules = null, IEffectTransitionService transitions = null)
        {
            MusicId = musicId ?? string.Empty;
            DifficultyId = difficultyId ?? string.Empty;
            Mode = mode;
            this.camera = camera;
            this.gameState = gameState;
            this.rules = rules;
            this.transitions = transitions;
            cancellationToken = cancellation.Token;
        }

        public string MusicId { get; }
        public string DifficultyId { get; }
        public EffectExecutionMode Mode { get; }
        public CancellationToken CancellationToken => cancellationToken;
        public bool IsActive => !disposed && !TransitionRequested;
        public bool TransitionRequested { get; private set; }
        public bool HasCamera => camera != null;
        public bool HasGameState => gameState != null;
        public bool HasRules => rules != null;
        public double CurrentHealth
        {
            get
            {
                RequireActive();
                if (gameState == null) throw new InvalidOperationException("Game state service is unavailable.");
                double health = gameState.CurrentHealth;
                if (!EffectParameterValidation.Finite(health)) throw new InvalidOperationException("Session health is not finite.");
                return health;
            }
        }

        public T Own<T>(T resource) where T : IDisposable
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (!IsActive)
            {
                resource.Dispose();
                throw new ObjectDisposedException(nameof(EffectSessionContext));
            }
            owned.Add(resource);
            return resource;
        }

        public IEffectCameraOffset CreateCameraOffset(string ownerId)
        {
            RequireActive();
            if (camera == null) throw new InvalidOperationException("Camera effect service is unavailable.");
            return Own(new ScopedCameraOffset(this, camera.CreateOffset(ownerId)));
        }

        public IEffectRuleHandle AddDamageMultiplier(string ownerId, double multiplier, double effectiveTimeMs)
        {
            RequireActive();
            if (rules == null) throw new InvalidOperationException("Rule effect service is unavailable.");
            return Own(new ScopedRuleHandle(this, rules.AddDamageMultiplier(ownerId, multiplier, effectiveTimeMs)));
        }

        public bool CanTransitionTo(string musicId, string difficultyId)
        {
            return IsActive && transitions != null && transitions.CanTransitionTo(musicId, difficultyId);
        }

        public void RequestTransition(string musicId, string difficultyId)
        {
            RequireActive();
            if (!CanTransitionTo(musicId, difficultyId))
                throw new InvalidOperationException("The transition target is unavailable.");
            if (!transitions.RequestTransition(musicId, difficultyId, Mode))
                throw new InvalidOperationException("The transition request was rejected or conflicted.");
            TransitionRequested = true;
            // Prevent late callbacks before the controller reaches its disposal boundary.
            cancellation.Cancel();
        }

        private void RequireActive()
        {
            if (!IsActive) throw new ObjectDisposedException(nameof(EffectSessionContext));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var failures = new List<Exception>();
            try { cancellation.Cancel(); }
            catch (Exception exception) { failures.Add(exception); }
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                try { owned[i].Dispose(); }
                catch (Exception exception) { failures.Add(exception); }
            }
            owned.Clear();
            cancellation.Dispose();
            if (failures.Count > 0) throw new AggregateException("Effect session cleanup failed.", failures);
        }

        private sealed class ScopedCameraOffset : IEffectCameraOffset
        {
            private readonly EffectSessionContext session;
            private IEffectCameraOffset inner;
            public ScopedCameraOffset(EffectSessionContext session, IEffectCameraOffset inner)
            {
                this.session = session;
                this.inner = inner ?? throw new InvalidOperationException("Camera service returned no ownership handle.");
            }
            public void Set(double x, double y, double rollDegrees)
            {
                if (inner == null) throw new ObjectDisposedException(nameof(ScopedCameraOffset));
                session.RequireActive();
                inner.Set(x, y, rollDegrees);
            }
            public void Dispose()
            {
                IEffectCameraOffset resource = inner;
                inner = null;
                resource?.Dispose();
            }
        }

        private sealed class ScopedRuleHandle : IEffectRuleHandle
        {
            private readonly EffectSessionContext session;
            private IEffectRuleHandle inner;
            public ScopedRuleHandle(EffectSessionContext session, IEffectRuleHandle inner)
            {
                this.session = session;
                this.inner = inner ?? throw new InvalidOperationException("Rule service returned no ownership handle.");
            }
            public void ReleaseAt(double effectiveTimeMs)
            {
                if (inner == null) return;
                session.RequireActive();
                inner.ReleaseAt(effectiveTimeMs);
                // The service retains any needed interval history; subsequent cleanup is idempotent.
                Dispose();
            }
            public void Dispose()
            {
                IEffectRuleHandle resource = inner;
                inner = null;
                resource?.Dispose();
            }
        }
    }

    public sealed class EffectExecutionContext
    {
        internal EffectExecutionContext(EffectSessionContext session, PlayableEffectEvent definition, double currentTimeMs)
        {
            Session = session;
            Definition = definition;
            CurrentTimeMs = currentTimeMs;
        }
        public EffectSessionContext Session { get; }
        public PlayableEffectEvent Definition { get; }
        public string EffectId => Definition.EffectId;
        public double ScheduledTimeMs => Definition.TimeMs;
        public double CurrentTimeMs { get; }
        public double ElapsedTimeMs => Math.Max(0d, CurrentTimeMs - ScheduledTimeMs);
    }

    public abstract class Effect : IDisposable
    {
        private bool started;
        private bool disposed;
        public bool IsComplete { get; private set; }
        protected void Complete() { IsComplete = true; }
        protected abstract void OnStart(EffectExecutionContext context);
        protected virtual void OnUpdate(EffectExecutionContext context) { Complete(); }
        protected virtual void OnStop(bool cancelled) { }
        internal void Start(EffectExecutionContext context)
        {
            if (started || disposed) throw new InvalidOperationException("An Effect instance can only execute once.");
            started = true;
            OnStart(context);
        }
        internal void Update(EffectExecutionContext context)
        {
            if (!IsComplete && !disposed) OnUpdate(context);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            bool cancelled = !IsComplete;
            IsComplete = true;
            OnStop(cancelled);
        }
    }

    public abstract class MusicGimmick : IDisposable
    {
        private bool disposed;
        public virtual void Prepare(EffectSessionContext session) { }
        public virtual void Update(double chartTimeMs) { }
        protected virtual void OnDispose() { }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            OnDispose();
        }
    }
}

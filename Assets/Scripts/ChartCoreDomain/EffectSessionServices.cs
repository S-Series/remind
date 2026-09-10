using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    /// <summary>
    /// Session-owned additive camera contributions.  The mixer contains no
    /// Unity presentation code; each application supplies a pose adapter.
    /// </summary>
    public sealed class EffectCameraMixer : IEffectCameraService, IDisposable
    {
        private readonly List<Offset> offsets = new List<Offset>();
        private readonly Action<double, double, double> apply;
        private bool disposed;

        public EffectCameraMixer(Action<double, double, double> apply)
        {
            this.apply = apply ?? throw new ArgumentNullException(
                nameof(apply));
        }

        public IEffectCameraOffset CreateOffset(string ownerId)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(EffectCameraMixer));
            }

            var offset = new Offset(this);
            offsets.Add(offset);
            return offset;
        }

        public void Apply()
        {
            // An earlier session may still own a delayed callback.  Once this
            // mixer is disposed it must never reset a newer presenter's pose.
            if (disposed)
            {
                return;
            }

            double x = 0d;
            double y = 0d;
            double rollDegrees = 0d;
            for (int i = 0; i < offsets.Count; i++)
            {
                x += offsets[i].X;
                y += offsets[i].Y;
                rollDegrees += offsets[i].RollDegrees;
            }

            apply(x, y, rollDegrees);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            offsets.Clear();
            apply(0d, 0d, 0d);
        }

        private sealed class Offset : IEffectCameraOffset
        {
            private EffectCameraMixer owner;

            public Offset(EffectCameraMixer owner)
            {
                this.owner = owner;
            }

            public double X { get; private set; }
            public double Y { get; private set; }
            public double RollDegrees { get; private set; }

            public void Set(double x, double y, double rollDegrees)
            {
                if (owner == null || owner.disposed)
                {
                    return;
                }

                if (!EffectParameterValidation.Finite(x) ||
                    !EffectParameterValidation.Finite(y) ||
                    !EffectParameterValidation.Finite(rollDegrees))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(x),
                        "Camera contributions must be finite.");
                }

                X = x;
                Y = y;
                RollDegrees = rollDegrees;
            }

            public void Dispose()
            {
                EffectCameraMixer current = owner;
                owner = null;
                current?.offsets.Remove(this);
            }
        }
    }

    /// <summary>
    /// Explicit authoring/test state.  It never reads or writes player progress.
    /// </summary>
    public sealed class EffectTestGameState : IEffectGameState
    {
        public double CurrentHealth { get; set; } = 100d;
    }

    /// <summary>
    /// Preview capability for commands that require a scoped rule handle.  It
    /// records lifetime only; actual GameRule mutation remains game-specific.
    /// </summary>
    public sealed class EffectPreviewRuleService :
        IEffectRuleService,
        IDisposable
    {
        private readonly List<Handle> handles = new List<Handle>();
        private bool disposed;

        public IEffectRuleHandle AddDamageMultiplier(
            string ownerId,
            double multiplier,
            double effectiveTimeMs)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(EffectPreviewRuleService));
            }

            if (!EffectParameterValidation.Finite(multiplier) ||
                !EffectParameterValidation.Finite(effectiveTimeMs))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(multiplier),
                    "Preview rule values must be finite.");
            }

            var handle = new Handle(this);
            handles.Add(handle);
            return handle;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (int i = handles.Count - 1; i >= 0; i--)
            {
                handles[i].Detach();
            }
            handles.Clear();
        }

        private sealed class Handle : IEffectRuleHandle
        {
            private EffectPreviewRuleService owner;

            public Handle(EffectPreviewRuleService owner)
            {
                this.owner = owner;
            }

            public void ReleaseAt(double effectiveTimeMs)
            {
                if (owner == null)
                {
                    return;
                }

                if (!EffectParameterValidation.Finite(effectiveTimeMs))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(effectiveTimeMs));
                }

                Dispose();
            }

            public void Dispose()
            {
                EffectPreviewRuleService current = owner;
                owner = null;
                current?.handles.Remove(this);
            }

            public void Detach()
            {
                owner = null;
            }
        }
    }

    /// <summary>
    /// Stores one transition intent.  Scene/audio replacement belongs to the
    /// application composition root after the chart frame has completed.
    /// </summary>
    public sealed class EffectTransitionMailbox :
        IEffectTransitionService,
        IDisposable
    {
        private readonly Func<string, string, bool> canTransition;
        private bool closed;

        public EffectTransitionMailbox(
            Func<string, string, bool> canTransition)
        {
            this.canTransition = canTransition ??
                throw new ArgumentNullException(nameof(canTransition));
        }

        public string TargetMusicId { get; private set; }
        public string TargetDifficultyId { get; private set; }
        public bool HasRequest => TargetMusicId != null;

        public bool CanTransitionTo(
            string musicId,
            string difficultyId)
        {
            return !closed && canTransition(musicId, difficultyId);
        }

        public bool RequestTransition(
            string musicId,
            string difficultyId,
            EffectExecutionMode mode)
        {
            if (closed || HasRequest ||
                !CanTransitionTo(musicId, difficultyId))
            {
                return false;
            }

            TargetMusicId = musicId;
            TargetDifficultyId = difficultyId;
            return true;
        }

        public void Dispose()
        {
            closed = true;
        }
    }
}

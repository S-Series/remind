using System;

namespace REmind.Charting
{
    public sealed class CameraEffectParameters
    {
        public CameraEffectParameters(double durationMs, double offsetX, double offsetY, double rollDegrees)
        { DurationMs = durationMs; OffsetX = offsetX; OffsetY = offsetY; RollDegrees = rollDegrees; }
        public double DurationMs { get; }
        public double OffsetX { get; }
        public double OffsetY { get; }
        public double RollDegrees { get; }
    }

    /// <summary>A temporary constant additive pose, evaluated against absolute chart time.</summary>
    public sealed class CameraEffect : Effect
    {
        public const string TypeId = "camera.offset";
        private readonly CameraEffectParameters parameters;
        private IEffectCameraOffset offset;
        public CameraEffect(CameraEffectParameters parameters) { this.parameters = parameters; }
        protected override void OnStart(EffectExecutionContext context)
        {
            offset = context.Session.CreateCameraOffset(context.EffectId);
        }
        protected override void OnUpdate(EffectExecutionContext context)
        {
            if (context.ElapsedTimeMs >= parameters.DurationMs) { Complete(); return; }
            offset.Set(parameters.OffsetX, parameters.OffsetY, parameters.RollDegrees);
        }
        protected override void OnStop(bool cancelled) { offset?.Dispose(); }
        public static EffectRegistration Registration() => new EffectRegistration(
            TypeId, "Camera offset (temporary)", typeof(CameraEffectParameters), true,
            () => new CameraEffectParameters(400, 0, 0, 0), value =>
            {
                var p = (CameraEffectParameters)value;
                return !EffectParameterValidation.Finite(p.DurationMs) || p.DurationMs < 0 ||
                    !EffectParameterValidation.Finite(p.OffsetX) || Math.Abs(p.OffsetX) > 10000 ||
                    !EffectParameterValidation.Finite(p.OffsetY) || Math.Abs(p.OffsetY) > 10000 ||
                    !EffectParameterValidation.Finite(p.RollDegrees) || Math.Abs(p.RollDegrees) > 36000
                    ? "durationMs must be finite/nonnegative; offsets must be within +/-10000, rollDegrees within +/-36000." : null;
            }, value => new CameraEffect((CameraEffectParameters)value), (session, _) =>
            {
                if (!session.HasCamera) throw new InvalidOperationException("Camera offset presenter is required.");
            }, supportsSeek: true);
    }

    public sealed class CallMusicGimmickEffect : Effect
    {
        public const string TypeId = "music.call";
        private readonly MusicGimmick gimmick;
        private readonly MusicGimmickCommandRegistration command;
        private readonly object parameters;
        internal CallMusicGimmickEffect(MusicGimmick gimmick, MusicGimmickCommandRegistration command, object parameters)
        { this.gimmick = gimmick; this.command = command; this.parameters = parameters; }
        protected override void OnStart(EffectExecutionContext context)
        {
            command.Handler(gimmick, context, parameters);
            Complete();
        }
    }

    public sealed class SampleSectionParameters
    {
        public SampleSectionParameters(double minimumHealth, double damageMultiplier)
        { MinimumHealth = minimumHealth; DamageMultiplier = damageMultiplier; }
        public double MinimumHealth { get; }
        public double DamageMultiplier { get; }
    }
    public sealed class SongTransitionParameters
    {
        public SongTransitionParameters(string targetMusicId, string targetDifficultyId)
        { TargetMusicId = targetMusicId; TargetDifficultyId = targetDifficultyId; }
        public string TargetMusicId { get; }
        public string TargetDifficultyId { get; }
    }

    /// <summary>Development example only: no real song/unlock rules or persistent progress.</summary>
    public sealed class SampleMusicGimmick : MusicGimmick
    {
        private IEffectRuleHandle modifier;
        public bool SectionActive { get; private set; }
        public int SuccessCount { get; private set; }
        public bool SectionSucceeded { get; private set; }
        private void Begin(EffectExecutionContext context, SampleSectionParameters p)
        {
            if (SectionActive) throw new InvalidOperationException("The sample section is already active.");
            SuccessCount = 0;
            SectionSucceeded = false;
            if (context.Session.CurrentHealth < p.MinimumHealth) return; // Normal conditional skip.
            modifier = context.Session.AddDamageMultiplier(context.EffectId, p.DamageMultiplier, context.ScheduledTimeMs);
            SectionActive = true;
        }
        private void Count() { if (SectionActive) SuccessCount++; }
        private void End(EffectExecutionContext context)
        {
            SectionSucceeded = SectionActive && SuccessCount > 0;
            modifier?.ReleaseAt(context.ScheduledTimeMs);
            modifier = null;
            SectionActive = false;
        }
        private void Transition(EffectExecutionContext context, SongTransitionParameters p)
        {
            if (SectionSucceeded) context.Session.RequestTransition(p.TargetMusicId, p.TargetDifficultyId);
        }
        protected override void OnDispose() { modifier?.Dispose(); modifier = null; SectionActive = false; }
        public static MusicGimmickRegistration Registration() => new MusicGimmickRegistration(
            "sample", "Development sample", () => new SampleMusicGimmick(), new[]
            {
                new MusicGimmickCommandRegistration("begin-section", "Begin sample section", typeof(SampleSectionParameters), true,
                    () => new SampleSectionParameters(30, 1.5), value =>
                    {
                        var p = (SampleSectionParameters)value;
                        return !EffectParameterValidation.Finite(p.MinimumHealth) || p.MinimumHealth < 0 || p.MinimumHealth > 100 ||
                            !EffectParameterValidation.Finite(p.DamageMultiplier) || p.DamageMultiplier < 0 || p.DamageMultiplier > 100
                            ? "Sample health must be 0-100 and damageMultiplier 0-100." : null;
                    }, (g, c, p) => ((SampleMusicGimmick)g).Begin(c, (SampleSectionParameters)p), (s, _) =>
                    {
                        if (!s.HasGameState || !s.HasRules) throw new InvalidOperationException("Sample section requires health and rule services.");
                    }),
                new MusicGimmickCommandRegistration("count-success", "Count sample success", null, false, null, null,
                    (g, c, p) => ((SampleMusicGimmick)g).Count()),
                new MusicGimmickCommandRegistration("end-section", "End sample section", null, false, null, null,
                    (g, c, p) => ((SampleMusicGimmick)g).End(c)),
                new MusicGimmickCommandRegistration("request-transition", "Request sample transition", typeof(SongTransitionParameters), true,
                    () => new SongTransitionParameters("sample_song", "default"), value =>
                    {
                        var p = (SongTransitionParameters)value;
                        return string.IsNullOrWhiteSpace(p.TargetMusicId) || string.IsNullOrWhiteSpace(p.TargetDifficultyId)
                            ? "Transition target music/difficulty IDs are required." : null;
                    }, (g, c, p) => ((SampleMusicGimmick)g).Transition(c, (SongTransitionParameters)p), (s, value) =>
                    {
                        var p = (SongTransitionParameters)value;
                        if (!s.CanTransitionTo(p.TargetMusicId, p.TargetDifficultyId))
                            throw new InvalidOperationException("Transition target has not been prepared: " + p.TargetMusicId + "/" + p.TargetDifficultyId);
                    })
            });
    }
}

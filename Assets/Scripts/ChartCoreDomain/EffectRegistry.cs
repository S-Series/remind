using System;
using System.Collections.Generic;

namespace REmind.Charting
{
    public sealed class EffectRegistration
    {
        private readonly Func<object> defaults;
        private readonly Func<object, string> validate;
        internal readonly Func<object, Effect> Factory;
        internal readonly Action<EffectSessionContext, object> ValidateServices;
        internal readonly Func<object, object> CopyParameters;
        public EffectRegistration(string typeId, string displayName, Type parameterType,
            bool parametersRequired, Func<object> defaultParameters, Func<object, string> validateParameters,
            Func<object, Effect> factory, Action<EffectSessionContext, object> validateServices = null,
            bool supportsSeek = false,
            Func<object, object> copyParameters = null)
        {
            if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("Effect type ID is required.");
            TypeId = typeId;
            DisplayName = displayName;
            ParameterType = parameterType;
            ParametersRequired = parametersRequired;
            defaults = defaultParameters;
            validate = validateParameters;
            Factory = factory;
            ValidateServices = validateServices;
            SupportsSeek = supportsSeek;
            CopyParameters = copyParameters;
        }
        public string TypeId { get; }
        public string DisplayName { get; }
        public Type ParameterType { get; }
        public bool ParametersRequired { get; }
        public bool SupportsSeek { get; }
        public object CreateDefaultParameters() => defaults?.Invoke();
        public string ValidateParameters(object value)
        {
            return EffectParameterValidation.Validate(value, ParameterType, ParametersRequired, validate);
        }
    }

    public sealed class MusicGimmickCommandRegistration
    {
        private readonly Func<object> defaults;
        private readonly Func<object, string> validate;
        internal readonly Action<MusicGimmick, EffectExecutionContext, object> Handler;
        internal readonly Action<EffectSessionContext, object> ValidateServices;
        internal readonly Func<object, object> CopyParameters;
        public MusicGimmickCommandRegistration(string commandId, string displayName,
            Type parameterType, bool parametersRequired, Func<object> defaultParameters,
            Func<object, string> validateParameters,
            Action<MusicGimmick, EffectExecutionContext, object> handler,
            Action<EffectSessionContext, object> validateServices = null,
            Func<object, object> copyParameters = null)
        {
            if (string.IsNullOrWhiteSpace(commandId)) throw new ArgumentException("Command ID is required.");
            CommandId = commandId;
            DisplayName = displayName;
            ParameterType = parameterType;
            ParametersRequired = parametersRequired;
            defaults = defaultParameters;
            validate = validateParameters;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            ValidateServices = validateServices;
            CopyParameters = copyParameters;
        }
        public string CommandId { get; }
        public string DisplayName { get; }
        public Type ParameterType { get; }
        public bool ParametersRequired { get; }
        public object CreateDefaultParameters() => defaults?.Invoke();
        public string ValidateParameters(object value)
        {
            return EffectParameterValidation.Validate(value, ParameterType, ParametersRequired, validate);
        }
    }

    internal static class EffectParameterValidation
    {
        internal static string Validate(object value, Type type, bool required, Func<object, string> validator)
        {
            if (value == null)
                return required ? "Required parameters are missing." : null;
            if (type == null) return "This effect or command does not accept parameters.";
            if (!type.IsInstanceOfType(value)) return "Parameters must have type " + type.Name + ".";
            return validator?.Invoke(value);
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class MusicGimmickRegistration
    {
        private readonly Dictionary<string, MusicGimmickCommandRegistration> byId =
            new Dictionary<string, MusicGimmickCommandRegistration>(StringComparer.Ordinal);
        internal readonly Func<MusicGimmick> Factory;
        public MusicGimmickRegistration(string gimmickId, string displayName,
            Func<MusicGimmick> factory, IEnumerable<MusicGimmickCommandRegistration> commands)
        {
            if (string.IsNullOrWhiteSpace(gimmickId)) throw new ArgumentException("Gimmick ID is required.");
            GimmickId = gimmickId;
            DisplayName = displayName;
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            var ordered = new List<MusicGimmickCommandRegistration>();
            foreach (MusicGimmickCommandRegistration command in commands)
            {
                if (command == null) throw new ArgumentException("A command registration is missing.");
                byId.Add(command.CommandId, command);
                ordered.Add(command);
            }
            Commands = ordered.AsReadOnly();
        }
        public string GimmickId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<MusicGimmickCommandRegistration> Commands { get; }
        public bool TryGetCommand(string id, out MusicGimmickCommandRegistration registration)
        {
            return byId.TryGetValue(id ?? string.Empty, out registration);
        }
    }

    public sealed class EffectRegistry
    {
        private readonly List<EffectRegistration> effects = new List<EffectRegistration>();
        private readonly List<MusicGimmickRegistration> gimmicks = new List<MusicGimmickRegistration>();
        private readonly Dictionary<string, EffectRegistration> effectById =
            new Dictionary<string, EffectRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, MusicGimmickRegistration> gimmickById =
            new Dictionary<string, MusicGimmickRegistration>(StringComparer.Ordinal);
        public IReadOnlyList<EffectRegistration> Effects => effects.AsReadOnly();
        public IReadOnlyList<MusicGimmickRegistration> Gimmicks => gimmicks.AsReadOnly();
        public void RegisterEffect(EffectRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            effectById.Add(registration.TypeId, registration);
            effects.Add(registration);
        }
        public void RegisterGimmick(MusicGimmickRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            gimmickById.Add(registration.GimmickId, registration);
            gimmicks.Add(registration);
        }
        public bool TryGetEffect(string id, out EffectRegistration registration)
        {
            return effectById.TryGetValue(id ?? string.Empty, out registration);
        }
        public bool TryGetGimmick(string id, out MusicGimmickRegistration registration)
        {
            return gimmickById.TryGetValue(id ?? string.Empty, out registration);
        }

        /// <summary>
        /// Shared editor/runtime parameter contract. Decoders produce typed values;
        /// this method applies the registration, command and range rules used by play preparation.
        /// </summary>
        public string ValidateParameters(string effectTypeId, string commandId,
            string gimmickId, object parameters)
        {
            if (!TryGetEffect(effectTypeId, out EffectRegistration effect))
                return $"Effect type '{effectTypeId}' is not registered.";

            if (effectTypeId == CallMusicGimmickEffect.TypeId)
            {
                if (!TryGetGimmick(gimmickId, out MusicGimmickRegistration gimmick))
                    return $"Music gimmick '{gimmickId}' is not registered.";
                if (!gimmick.TryGetCommand(commandId,
                        out MusicGimmickCommandRegistration command))
                    return $"Command '{commandId}' is not registered for '{gimmickId}'.";
                return command.ValidateParameters(parameters);
            }

            if (!string.IsNullOrEmpty(commandId))
                return "A common effect must not contain a gimmick command ID.";
            return effect.ValidateParameters(parameters);
        }

        public static EffectRegistry CreateDefault()
        {
            var registry = new EffectRegistry();
            registry.RegisterEffect(CameraEffect.Registration());
            registry.RegisterEffect(new EffectRegistration(CallMusicGimmickEffect.TypeId,
                "Call Music Gimmick", null, false, null, null, null, supportsSeek: false));
            registry.RegisterGimmick(SampleMusicGimmick.Registration());
            return registry;
        }
    }

    public sealed class EffectPreparationResult
    {
        internal EffectPreparationResult(PreparedEffectPlan plan, List<CompileIssue> issues)
        {
            Plan = plan;
            Issues = issues.AsReadOnly();
        }
        public bool Succeeded => Plan != null;
        public PreparedEffectPlan Plan { get; }
        public IReadOnlyList<CompileIssue> Issues { get; }
    }

    internal sealed class PreparedEffectEntry
    {
        internal PreparedEffectEntry(PlayableEffectEvent definition,
            EffectRegistration registration,
            MusicGimmickCommandRegistration command,
            object parameters,
            Func<object, object> copyParameters)
        {
            Definition = definition;
            Registration = registration;
            Command = command;
            Parameters = parameters;
            CopyParameters = copyParameters;
        }

        internal PlayableEffectEvent Definition { get; }
        internal EffectRegistration Registration { get; }
        internal MusicGimmickCommandRegistration Command { get; }
        internal object Parameters { get; }
        private Func<object, object> CopyParameters { get; }
        internal object CreateSessionParameters() =>
            EffectParameterCopies.Copy(Parameters, CopyParameters);
    }

    internal static class EffectParameterCopies
    {
        internal static object Copy(object value, Func<object, object> customCopy)
        {
            if (value == null) return null;
            if (value is CameraEffectParameters camera)
                return new CameraEffectParameters(camera.DurationMs,
                    camera.OffsetX, camera.OffsetY, camera.RollDegrees,
                    camera.AttackMs, camera.ReleaseMs);
            if (value is SampleSectionParameters section)
                return new SampleSectionParameters(section.MinimumHealth,
                    section.DamageMultiplier);
            if (value is SongTransitionParameters transition)
                return new SongTransitionParameters(transition.TargetMusicId,
                    transition.TargetDifficultyId);
            if (customCopy == null)
                throw new InvalidOperationException(
                    $"Effect parameters of type '{value.GetType().Name}' require a copy function.");
            object result = customCopy(value);
            if (result == null || ReferenceEquals(result, value) ||
                result.GetType() != value.GetType())
                throw new InvalidOperationException(
                    "Effect parameter copy must return a separate value of the same type.");
            return result;
        }
    }

    /// <summary>Prepared runtime boundary, intentionally separate from the passive chart Snapshot.</summary>
    public sealed class PreparedEffectPlan
    {
        internal readonly IReadOnlyList<PreparedEffectEntry> Entries;
        internal readonly MusicGimmickRegistration Gimmick;
        internal PreparedEffectPlan(List<PreparedEffectEntry> entries, MusicGimmickRegistration gimmick)
        {
            Entries = entries.AsReadOnly();
            Gimmick = gimmick;
            SupportsSeek = true;
            foreach (PreparedEffectEntry entry in Entries)
                SupportsSeek &= entry.Registration.SupportsSeek;
        }
        public int Count => Entries.Count;
        public bool SupportsSeek { get; }
    }

    public static class EffectPreparation
    {
        public static EffectPreparationResult Prepare(IReadOnlyList<PlayableEffectEvent> events,
            IReadOnlyDictionary<string, object> parameters, EffectRegistry registry, string gimmickId = "")
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var issues = new List<CompileIssue>();
            var entries = new List<PreparedEffectEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            MusicGimmickRegistration gimmick = null;
            bool hasCalls = false;
            foreach (PlayableEffectEvent definition in events)
            {
                if (definition == null || !ids.Add(definition.EffectId))
                {
                    issues.Add(new CompileIssue("EFFECT_ID", "An effect is missing or its ID is duplicated."));
                    continue;
                }
                if (!registry.TryGetEffect(definition.EffectTypeId, out EffectRegistration effect))
                {
                    issues.Add(new CompileIssue("EFFECT_UNREGISTERED", $"Effect '{definition.EffectId}' has unregistered type '{definition.EffectTypeId}'."));
                    continue;
                }
                object value = null;
                parameters?.TryGetValue(definition.EffectId, out value);
                MusicGimmickCommandRegistration command = null;
                string error;
                if (definition.EffectTypeId == CallMusicGimmickEffect.TypeId)
                {
                    hasCalls = true;
                    if (!registry.TryGetGimmick(gimmickId, out gimmick))
                    {
                        issues.Add(new CompileIssue("GIMMICK_UNREGISTERED", $"Effect '{definition.EffectId}' requires a registered song gimmick, got '{gimmickId}'."));
                        continue;
                    }
                    if (!gimmick.TryGetCommand(definition.CommandId, out command))
                    {
                        issues.Add(new CompileIssue("GIMMICK_COMMAND_UNREGISTERED", $"Effect '{definition.EffectId}' command '{definition.CommandId}' is not registered for '{gimmickId}'."));
                        continue;
                    }
                    error = registry.ValidateParameters(definition.EffectTypeId,
                        definition.CommandId, gimmickId, value);
                }
                else
                {
                    error = registry.ValidateParameters(definition.EffectTypeId,
                        definition.CommandId, gimmickId, value);
                }
                if (error != null)
                {
                    issues.Add(new CompileIssue("EFFECT_PARAMETERS", $"Effect '{definition.EffectId}': {error}"));
                    continue;
                }
                Func<object, object> copyParameters = command != null
                    ? command.CopyParameters : effect.CopyParameters;
                object frozenParameters;
                try
                {
                    frozenParameters = EffectParameterCopies.Copy(value,
                        copyParameters);
                }
                catch (Exception exception)
                {
                    issues.Add(new CompileIssue("EFFECT_PARAMETERS_SNAPSHOT",
                        $"Effect '{definition.EffectId}': {exception.Message}"));
                    continue;
                }
                entries.Add(new PreparedEffectEntry(definition, effect,
                    command, frozenParameters, copyParameters));
            }
            if (parameters != null)
                foreach (string id in parameters.Keys)
                    if (!ids.Contains(id))
                        issues.Add(new CompileIssue("EFFECT_PARAMETERS_ORPHAN", $"Configuration '{id}' has no corresponding Effect note."));
            entries.Sort((a, b) => a.Definition.TimeMs != b.Definition.TimeMs
                ? a.Definition.TimeMs.CompareTo(b.Definition.TimeMs)
                : a.Definition.Order.CompareTo(b.Definition.Order));
            return new EffectPreparationResult(issues.Count == 0
                ? new PreparedEffectPlan(entries, hasCalls ? gimmick : null) : null, issues);
        }
    }
}

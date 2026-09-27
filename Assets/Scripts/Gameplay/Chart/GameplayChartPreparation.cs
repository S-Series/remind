using System;
using System.Collections.Generic;
using System.Text;
using REmind.Charting;

namespace REmind.Gameplay.Chart
{
    public readonly struct GameplayChartMetadata
    {
        public GameplayChartMetadata(
            string musicId,
            string difficultyId,
            string gimmickId,
            string revision)
        {
            MusicId = musicId ?? string.Empty;
            DifficultyId = difficultyId ?? string.Empty;
            GimmickId = gimmickId ?? string.Empty;
            Revision = revision ?? string.Empty;
        }

        public string MusicId { get; }
        public string DifficultyId { get; }
        public string GimmickId { get; }
        public string Revision { get; }
    }

    /// <summary>
    /// A completely validated gameplay input.  Parsing and parameter decoding are
    /// finished before this object is published to any live playback system.
    /// </summary>
    public sealed class PreparedGameplayChart
    {
        internal PreparedGameplayChart(
            PlayableChartSnapshot snapshot,
            PreparedEffectPlan effectPlan,
            GameplayChartMetadata metadata,
            double baseBpm,
            double chartOffsetMs)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(
                nameof(snapshot));
            EffectPlan = effectPlan ?? throw new ArgumentNullException(
                nameof(effectPlan));
            Metadata = metadata;
            BaseBpm = baseBpm;
            ChartOffsetMs = chartOffsetMs;
        }

        public PlayableChartSnapshot Snapshot { get; }
        public PreparedEffectPlan EffectPlan { get; }
        public GameplayChartMetadata Metadata { get; }
        public double BaseBpm { get; }

        /// <summary>
        /// Song time = chart time + this offset.  This is the inverse of the
        /// ChartMaker music-start correction.
        /// </summary>
        public double ChartOffsetMs { get; }
    }

    /// <summary>
    /// Game boundary: accepts only the versioned, validated runtime package.
    /// Authoring files are exported by ChartMaker before being bundled here.
    /// </summary>
    public static class GameplayChartPreparation
    {
        public static PreparedGameplayChart Prepare(string packageJson)
        {
            RuntimeChartPackage package = RuntimeChartPackageCodec.Import(
                packageJson);
            var metadata = new GameplayChartMetadata(
                package.MusicId, package.DifficultyId,
                package.GimmickId, package.Revision);
            EffectRegistry registry = EffectRegistry.CreateDefault();
            var parameters = new Dictionary<string, object>(
                StringComparer.Ordinal);
            foreach (PlayableEffectEvent effect in
                package.Snapshot.EffectEvents)
            {
                if (!package.EffectParameterJson.TryGetValue(
                        effect.EffectId, out string json))
                    throw new FormatException("Effect '" + effect.EffectId +
                        "' parameters are missing.");
                if (!ChartEffectParameterCodec.TryDecode(effect.EffectTypeId,
                        effect.CommandId, package.GimmickId, json, registry,
                        out object decoded, out string error))
                    throw new FormatException("Effect '" + effect.EffectId +
                        "' parameters are invalid: " + error);
                parameters.Add(effect.EffectId, decoded);
            }
            EffectPreparationResult preparedEffects = EffectPreparation.Prepare(
                package.Snapshot.EffectEvents, parameters, registry,
                package.GimmickId);
            if (!preparedEffects.Succeeded)
                throw CompileFailure("Effect preparation failed",
                    preparedEffects.Issues);

            return new PreparedGameplayChart(
                package.Snapshot, preparedEffects.Plan,
                metadata,
                package.BaseBpm, package.ChartOffsetMs);
        }

        private static FormatException CompileFailure(
            string stage,
            IReadOnlyList<CompileIssue> issues)
        {
            var message = new StringBuilder(stage);
            message.Append(':');

            for (int index = 0; index < issues.Count; index++)
            {
                CompileIssue issue = issues[index];
                if (issue.Severity != CompileIssueSeverity.Error)
                {
                    continue;
                }

                message.AppendLine();
                message.Append(issue.Code);
                message.Append(": ");
                message.Append(issue.Message);
            }

            return new FormatException(message.ToString());
        }
    }
}

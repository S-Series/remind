using System;
using System.Collections.Generic;
using System.Text;
using REmind.Charting;
using REmind.Data;

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
            NoteData[] notes,
            GameplayChartMetadata metadata,
            double baseBpm,
            double chartOffsetMs)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(
                nameof(snapshot));
            EffectPlan = effectPlan ?? throw new ArgumentNullException(
                nameof(effectPlan));
            Notes = Array.AsReadOnly(notes ??
                throw new ArgumentNullException(nameof(notes)));
            Metadata = metadata;
            BaseBpm = baseBpm;
            ChartOffsetMs = chartOffsetMs;
        }

        public PlayableChartSnapshot Snapshot { get; }
        public PreparedEffectPlan EffectPlan { get; }
        public IReadOnlyList<NoteData> Notes { get; }
        public GameplayChartMetadata Metadata { get; }
        public double BaseBpm { get; }

        /// <summary>
        /// Song time = chart time + this offset.  This is the inverse of the
        /// ChartMaker music-start correction.
        /// </summary>
        public double ChartOffsetMs { get; }
    }

    /// <summary>
    /// Transitional DemoPlay file adapter: current .rd/sidecar authoring data is
    /// converted into a shared Snapshot and immutable Effect plan.  A future
    /// Game composition root should consume the shared runtime package rather
    /// than depend on the ChartMaker storage model.  No file access or parameter
    /// decoding occurs after this boundary.
    /// </summary>
    public static class GameplayChartPreparation
    {
        public static PreparedGameplayChart Prepare(
            string chartJson,
            string effectParameterJson,
            int beatsPerMeasure = 4)
        {
            if (string.IsNullOrWhiteSpace(chartJson))
            {
                throw new FormatException("Gameplay chart data is empty.");
            }

            if (beatsPerMeasure <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(beatsPerMeasure),
                    "Beats per measure must be greater than zero.");
            }

            ChartFile chart = ChartFileCodec.Parse(chartJson);

            if (chart.HasEffectParameterFile)
            {
                if (string.IsNullOrWhiteSpace(effectParameterJson))
                {
                    throw new FormatException(
                        "This chart requires its matching Effect parameter JSON.");
                }

                // This checks music, difficulty and revision ownership before
                // mutating the newly parsed holder collection.
                ChartEffectFileStore.ApplyParameters(
                    chart,
                    effectParameterJson);
            }
            else if (!string.IsNullOrWhiteSpace(effectParameterJson))
            {
                throw new FormatException(
                    "An Effect parameter JSON was supplied for a chart that " +
                    "does not declare one.");
            }

            string musicId = string.IsNullOrWhiteSpace(chart.MusicId)
                ? "untitled"
                : chart.MusicId;
            string difficultyId = string.IsNullOrWhiteSpace(
                chart.DifficultyId)
                    ? "default"
                    : chart.DifficultyId;

            if (chart.HasEffectParameterFile)
            {
                ChartEffectFileStore.ValidateFileId(
                    musicId,
                    nameof(chart.MusicId));
                ChartEffectFileStore.ValidateFileId(
                    difficultyId,
                    nameof(chart.DifficultyId));
            }

            ChartHolder[] holders = chart.chartDatas ??
                Array.Empty<ChartHolder>();
            ChartHolderDocumentBuildResult built =
                ChartHolderDocumentAdapter.Build(
                    holders,
                    chart.BaseBpm,
                    beatsPerMeasure);
            if (!built.Succeeded)
            {
                throw CompileFailure(
                    "Gameplay chart conversion failed",
                    built.Issues);
            }

            ChartCompileResult compiled = ChartCompiler.Compile(
                built.Document,
                1d / ChartHolder.PositionUnitsPerWorldUnit);
            if (!compiled.Succeeded)
            {
                throw CompileFailure(
                    "Gameplay chart compilation failed",
                    compiled.Issues);
            }

            var metadata = new GameplayChartMetadata(
                musicId,
                difficultyId,
                chart.GimmickId ?? string.Empty,
                chart.EffectRevision ?? string.Empty);

            // Decode and validate every Effect once at the file/authoring
            // boundary. Runtime services receive only the immutable plan.
            PreparedEffectPlan effectPlan = ChartEffectJsonCodec.PreparePlan(
                compiled.Snapshot,
                holders,
                metadata.GimmickId);

            NoteData[] notes = BuildJudgementNotes(compiled.Snapshot);
            double chartOffsetMs = -chart.MusicStartCorrectionMs;
            if (double.IsNaN(chartOffsetMs) ||
                double.IsInfinity(chartOffsetMs))
            {
                throw new FormatException(
                    "Gameplay chart offset must be finite.");
            }

            return new PreparedGameplayChart(
                compiled.Snapshot,
                effectPlan,
                notes,
                metadata,
                chart.BaseBpm,
                chartOffsetMs);
        }

        private static NoteData[] BuildJudgementNotes(
            PlayableChartSnapshot snapshot)
        {
            var result = new NoteData[snapshot.Notes.Count];
            var occupied = new HashSet<(double TimeMs, int Lane)>();

            for (int index = 0; index < snapshot.Notes.Count; index++)
            {
                PlayableNoteSnapshot source = snapshot.Notes[index];
                NoteType type = ToGameplayType(source.Kind);
                long startTimeMs = RoundMilliseconds(
                    source.StartTimeMs,
                    source.Id,
                    "start");
                long durationMs = 0L;
                double timelineDurationMs = 0d;

                if (type.IsLong())
                {
                    if (!source.EndTimeMs.HasValue)
                    {
                        throw new FormatException(
                            $"Long note '{source.Id}' has no end time.");
                    }

                    long endTimeMs = RoundMilliseconds(
                        source.EndTimeMs.Value,
                        source.Id,
                        "end");
                    timelineDurationMs =
                        source.EndTimeMs.Value - source.StartTimeMs;
                    durationMs = checked(endTimeMs - startTimeMs);
                    if (timelineDurationMs <= 0d || durationMs <= 0L)
                    {
                        throw new FormatException(
                            $"Long note '{source.Id}' becomes zero-length at " +
                            "the gameplay millisecond resolution.");
                    }
                }

                if (!occupied.Add((source.StartTimeMs, source.Lane)))
                {
                    throw new FormatException(
                        $"Notes collide in lane {source.Lane + 1} at " +
                        $"{source.StartTimeMs:R} ms.");
                }

                result[index] = new NoteData(
                    source.Id,
                    type,
                    source.Lane,
                    startTimeMs,
                    durationMs,
                    source.StartTimeMs,
                    timelineDurationMs);
            }

            return result;
        }

        private static NoteType ToGameplayType(ChartNoteKind kind)
        {
            switch (kind)
            {
                case ChartNoteKind.Tap:
                    return NoteType.Tap;
                case ChartNoteKind.Hold:
                    return NoteType.LongTap;
                case ChartNoteKind.Scratch:
                    return NoteType.Scratch;
                case ChartNoteKind.LongScratch:
                    return NoteType.LongScratch;
                case ChartNoteKind.Air:
                    return NoteType.Air;
                default:
                    throw new FormatException(
                        $"Unsupported gameplay note kind: {kind}.");
            }
        }

        private static long RoundMilliseconds(
            double value,
            string noteId,
            string endpoint)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new FormatException(
                    $"Note '{noteId}' has an invalid {endpoint} time.");
            }

            try
            {
                return checked((long)Math.Round(
                    value,
                    MidpointRounding.AwayFromZero));
            }
            catch (OverflowException exception)
            {
                throw new FormatException(
                    $"Note '{noteId}' {endpoint} time is outside the " +
                    "gameplay range.",
                    exception);
            }
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

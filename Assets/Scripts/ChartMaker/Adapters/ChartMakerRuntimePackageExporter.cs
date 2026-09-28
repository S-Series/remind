using System;
using System.Collections.Generic;
using System.IO;
using REmind.Charting;

/// <summary>Turns the editor chart into a validated Game package.</summary>
public static class ChartMakerRuntimePackageExporter
{
    public static string DefaultOutputPath(string chartPath)
    {
        if (string.IsNullOrWhiteSpace(chartPath))
            throw new ArgumentException("Chart path is required.", nameof(chartPath));
        return Path.Combine(Path.GetDirectoryName(chartPath), "rmp",
            Path.GetFileNameWithoutExtension(chartPath) + ".rmp.json");
    }

    public static string Export(string chartJson, int beatsPerMeasure = 4) =>
        Export(chartJson, null, beatsPerMeasure);

    public static string Export(string chartJson, string effectParameterJson,
        int beatsPerMeasure = 4)
    {
        if (string.IsNullOrWhiteSpace(chartJson))
            throw new FormatException("Chart data is empty.");
        if (beatsPerMeasure <= 0)
            throw new ArgumentOutOfRangeException(nameof(beatsPerMeasure));

        ChartFile chart = ChartFileCodec.Parse(chartJson);
        if (chart.HasEffectParameterFile)
        {
            if (string.IsNullOrWhiteSpace(effectParameterJson))
                throw new FormatException("Matching Effect parameters are missing.");
            ChartEffectFileStore.ApplyParameters(chart,
                effectParameterJson);
        }
        else if (!string.IsNullOrWhiteSpace(effectParameterJson))
            throw new FormatException("Unexpected Effect parameter file.");

        string musicId = string.IsNullOrWhiteSpace(chart.MusicId)
            ? "untitled" : chart.MusicId;
        string difficultyId = string.IsNullOrWhiteSpace(chart.DifficultyId)
            ? "default" : chart.DifficultyId;
        if (chart.HasEffectParameterFile)
        {
            ChartEffectFileStore.ValidateFileId(musicId,
                nameof(chart.MusicId));
            ChartEffectFileStore.ValidateFileId(difficultyId,
                nameof(chart.DifficultyId));
        }

        ChartHolder[] holders = chart.chartDatas ??
            Array.Empty<ChartHolder>();
        ChartHolderDocumentBuildResult built =
            ChartHolderDocumentAdapter.Build(holders, chart.BaseBpm,
                beatsPerMeasure);
        if (!built.Succeeded)
            throw new FormatException(FirstError(built.Issues));
        ChartCompileResult compiled = ChartCompiler.Compile(built.Document,
            1d / ChartHolder.PositionUnitsPerWorldUnit);
        if (!compiled.Succeeded)
            throw new FormatException(FirstError(compiled.Issues));

        // Parameter decoding and effect registration are checked before export.
        ChartEffectJsonCodec.PreparePlan(compiled.Snapshot, holders,
            chart.GimmickId ?? string.Empty);
        var parameters = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (ChartHolder holder in holders)
            if (holder != null && holder.isEffect)
            {
                if (!parameters.TryAdd(holder.effectId,
                    holder.effectParametersJson ?? string.Empty))
                    throw new FormatException("Duplicate Effect ID: " +
                        holder.effectId);
            }

        return RuntimeChartPackageCodec.Export(built.Document, musicId,
            difficultyId, chart.GimmickId ?? string.Empty,
            chart.EffectRevision ?? string.Empty,
            -chart.MusicStartCorrectionMs,
            1d / ChartHolder.PositionUnitsPerWorldUnit, parameters);
    }

    private static string FirstError(IReadOnlyList<CompileIssue> issues)
    {
        foreach (CompileIssue issue in issues)
            if (issue.Severity == CompileIssueSeverity.Error)
                return issue.Code + ": " + issue.Message;
        return "Chart conversion failed.";
    }
}

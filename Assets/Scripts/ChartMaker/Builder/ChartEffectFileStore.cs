using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

/// <summary>Current single-file chart persistence and older paired-file recovery.</summary>
public static class ChartEffectFileStore
{
    public static string GetParameterPath(string chartPath, string musicId, string difficultyId)
    {
        ValidateFileId(musicId, nameof(musicId));
        ValidateFileId(difficultyId, nameof(difficultyId));
        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(chartPath)),
            "effect." + musicId + "." + difficultyId + ".json");
    }

    public static void ValidateFileId(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80)
            throw new FormatException(name + " must contain 1-80 letters, numbers, '_' or '-'.");
        foreach (char character in value)
            if (!(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') &&
                !(character >= '0' && character <= '9') && character != '_' && character != '-')
                throw new FormatException(name + " must use letters, numbers, '_' or '-'.");
    }

    public static string SerializeParameters(IReadOnlyList<ChartHolder> holders,
        ChartEffectDocumentState.Metadata metadata)
    {
        ChartEffectJsonCodec.ValidateKnownParameters(holders,
            metadata.GimmickId);
        XElement entries = new XElement("parameters", new XAttribute("type", "array"));
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChartHolder holder in holders)
        {
            if (holder == null || !holder.isEffect) continue;
            if (string.IsNullOrWhiteSpace(holder.effectId) || !ids.Add(holder.effectId))
                throw new FormatException("Effect IDs must be nonempty and unique: " + holder.effectId);
            if (string.IsNullOrWhiteSpace(holder.effectParametersJson)) continue;
            XElement data = ChartEffectJsonCodec.ReadObject(holder.effectParametersJson);
            data.Name = "data";
            entries.Add(new XElement("item", new XAttribute("type", "object"),
                ChartEffectJsonCodec.JsonString("effectId", holder.effectId), data));
        }
        return ChartEffectJsonCodec.WriteObject(new XElement("root", new XAttribute("type", "object"),
            ChartEffectJsonCodec.JsonNumber("version", 1),
            ChartEffectJsonCodec.JsonString("musicId", metadata.MusicId),
            ChartEffectJsonCodec.JsonString("difficultyId", metadata.DifficultyId),
            ChartEffectJsonCodec.JsonString("revision", metadata.Revision), entries));
    }

    public static void ApplyParameters(ChartFile chart, string parameterJson)
    {
        XElement root = ChartEffectJsonCodec.ReadObject(parameterJson);
        ChartEffectJsonCodec.Only(root, "version", "musicId", "difficultyId", "revision", "parameters");
        if (ChartEffectJsonCodec.Number(root, "version") != 1)
            throw new FormatException("Unsupported effect parameter version.");
        if (ChartEffectJsonCodec.String(root, "musicId") != chart.MusicId ||
            ChartEffectJsonCodec.String(root, "difficultyId") != chart.DifficultyId ||
            ChartEffectJsonCodec.String(root, "revision") != chart.EffectRevision)
            throw new FormatException("Chart and effect JSON revisions/identities do not match. Restore a matching pair.");
        XElement entries = ChartEffectJsonCodec.Member(root, "parameters");
        ChartEffectJsonCodec.RequireType(entries, "array");
        Dictionary<string, ChartHolder> effects = new Dictionary<string, ChartHolder>(StringComparer.Ordinal);
        foreach (ChartHolder holder in chart.chartDatas)
            if (holder.isEffect) effects.Add(holder.effectId, holder);
        Dictionary<string, string> loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement entry in entries.Elements())
        {
            ChartEffectJsonCodec.RequireType(entry, "object");
            ChartEffectJsonCodec.Only(entry, "effectId", "data");
            string id = ChartEffectJsonCodec.String(entry, "effectId");
            if (!effects.ContainsKey(id) || loaded.ContainsKey(id))
                throw new FormatException("Orphaned or duplicate effect parameter ID: " + id);
            XElement data = ChartEffectJsonCodec.Member(entry, "data");
            ChartEffectJsonCodec.RequireType(data, "object");
            loaded.Add(id, ChartEffectJsonCodec.WriteObject(data));
        }
        // Apply only after the complete sidecar passes structural validation.
        foreach (KeyValuePair<string, ChartHolder> effect in effects)
            effect.Value.effectParametersJson = loaded.TryGetValue(effect.Key, out string json) ? json : null;
    }

    public static ChartFile Load(string chartPath, out bool recovered, out string chartText)
    {
        return LoadInternal(Path.GetFullPath(chartPath), out recovered, out chartText, out _);
    }

    private static ChartFile LoadInternal(string path, out bool recovered,
        out string chartText, out string parameterText)
    {
        List<string> failures = new List<string>();
        foreach (string chartCandidate in new[] { path, path + ".bak" })
        {
            if (!File.Exists(chartCandidate)) continue;
            try
            {
                string text = File.ReadAllText(chartCandidate, Encoding.UTF8);
                ChartFile chart = ChartFileCodec.Parse(text);
                if (!chart.HasEffectParameterFile)
                {
                    recovered = chartCandidate != path;
                    chartText = text;
                    parameterText = null;
                    return chart;
                }
                string parameterPath = GetParameterPath(path, chart.MusicId, chart.DifficultyId);
                foreach (string parameterCandidate in new[] { parameterPath, parameterPath + ".bak" })
                {
                    if (!File.Exists(parameterCandidate)) continue;
                    try
                    {
                        string parameters = File.ReadAllText(parameterCandidate, Encoding.UTF8);
                        ApplyParameters(chart, parameters);
                        // Loading must enforce the same registered parameter
                        // contract as saving and preview preparation.  Reject a
                        // semantically invalid current pair before it can replace
                        // the live editor document, then continue looking for a
                        // matching valid backup pair.
                        ChartEffectJsonCodec.ValidateKnownParameters(
                            chart.chartDatas,
                            chart.GimmickId);
                        recovered = chartCandidate != path || parameterCandidate != parameterPath;
                        chartText = text;
                        parameterText = parameters;
                        return chart;
                    }
                    catch (Exception exception) { failures.Add(parameterCandidate + ": " + exception.Message); }
                }
                failures.Add(chartCandidate + ": No matching effect parameter revision was found.");
            }
            catch (Exception exception) { failures.Add(chartCandidate + ": " + exception.Message); }
        }
        throw new IOException("Could not load a valid chart or backup. " + string.Join("\n", failures));
    }

    public static void Save(string chartPath, string chartText, string parameterText)
    {
        string path = Path.GetFullPath(chartPath);
        ChartFile next = ChartFileCodec.Parse(chartText);
        string parameterPath = next.HasEffectParameterFile
            ? GetParameterPath(path, next.MusicId, next.DifficultyId) : null;
        if (parameterPath == null && !string.IsNullOrWhiteSpace(parameterText))
            throw new FormatException("Version 1 charts contain Effect parameters in the .rd file.");
        if (parameterPath != null)
        {
            ApplyParameters(next, parameterText);
            ChartEffectJsonCodec.ValidateKnownParameters(next.chartDatas,
                next.GimmickId);
        }
        // The sidecar name is chart-independent, so an existing destination chart is
        // not evidence that it owns this music/difficulty file.  Only a matching
        // current/backup revision proves ownership without adding another manifest.
        if (parameterPath != null &&
            (File.Exists(parameterPath) || File.Exists(parameterPath + ".bak")) &&
            !TargetOwnsParameterFile(path, parameterPath))
            throw new IOException(
                "Another chart owns the Effect JSON for this music/difficulty in " +
                "the destination folder. Use a distinct music/difficulty ID or " +
                "another folder.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string suffix = "." + Guid.NewGuid().ToString("N") + ".tmp";
        string chartTemp = path + suffix;
        string parameterTemp = parameterPath == null ? null : parameterPath + suffix;
        try
        {
            WriteDurably(chartTemp, chartText);
            if (parameterTemp != null) WriteDurably(parameterTemp, parameterText);

            // Preserve the last matching pair, not an already torn current pair.
            if (IsCurrentJsonFile(path) || IsCurrentJsonFile(path + ".bak"))
            {
                ChartFile previous = LoadInternal(path, out _, out string previousChart, out string previousParameters);
                ReplaceText(path + ".bak", previousChart);
                if (previous.HasEffectParameterFile)
                    ReplaceText(GetParameterPath(path, previous.MusicId, previous.DifficultyId) + ".bak", previousParameters);
            }
            else if (File.Exists(path))
                ReplaceText(path + ".bak", File.ReadAllText(path, Encoding.UTF8));

            bool createdParameter = parameterTemp != null &&
                !File.Exists(parameterPath);
            if (parameterTemp != null) Replace(parameterTemp, parameterPath);
            try
            {
                Replace(chartTemp, path);
            }
            catch (Exception chartException)
            {
                // A first save has no previous pair to recover. If its sidecar was
                // newly installed but the chart was not, remove only that new file
                // so an explicit retry is possible and no orphan claims ownership.
                if (createdParameter && File.Exists(parameterPath))
                {
                    try
                    {
                        if (!string.Equals(File.ReadAllText(parameterPath,
                                Encoding.UTF8), parameterText,
                                StringComparison.Ordinal))
                            throw new IOException(
                                "The Effect JSON changed while rolling back.");
                        File.Delete(parameterPath);
                    }
                    catch (Exception rollbackException)
                    {
                        throw new IOException(
                            "Chart replacement failed and the newly-created Effect " +
                            "JSON could not be rolled back. Manual recovery is required.",
                            new AggregateException(chartException,
                                rollbackException));
                    }
                }

                throw;
            }
        }
        finally
        {
            if (File.Exists(chartTemp)) File.Delete(chartTemp);
            if (parameterTemp != null && File.Exists(parameterTemp)) File.Delete(parameterTemp);
        }
    }

    private static void ReplaceText(string path, string text)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { WriteDurably(temporary, text); Replace(temporary, path); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void WriteDurably(string path, string text)
    {
        byte[] bytes = new UTF8Encoding(false).GetBytes(text);
        using FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    private static void Replace(string temporary, string path)
    {
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }

    private static bool TargetOwnsParameterFile(string chartPath, string parameterPath)
    {
        foreach (string chartCandidate in new[] { chartPath, chartPath + ".bak" })
        {
            if (!File.Exists(chartCandidate)) continue;
            string chartText;
            ChartFile chart;
            try
            {
                chartText = File.ReadAllText(chartCandidate, Encoding.UTF8);
                chart = ChartFileCodec.Parse(chartText);
                if (!chart.HasEffectParameterFile ||
                    !SamePath(GetParameterPath(chartPath, chart.MusicId,
                        chart.DifficultyId), parameterPath))
                    continue;
            }
            catch
            {
                continue;
            }

            foreach (string parameterCandidate in new[]
                     { parameterPath, parameterPath + ".bak" })
            {
                if (!File.Exists(parameterCandidate)) continue;
                try
                {
                    // ApplyParameters mutates holders, so parse a fresh candidate for
                    // every attempted pair.  A matching revision is the ownership token.
                    ChartFile pair = ChartFileCodec.Parse(chartText);
                    ApplyParameters(pair,
                        File.ReadAllText(parameterCandidate, Encoding.UTF8));
                    return true;
                }
                catch
                {
                    // Keep looking for a recoverable current/backup pair.
                }
            }
        }

        return false;
    }

    private static bool SamePath(string first, string second)
    {
        StringComparison comparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
            comparison);
    }

    private static bool IsCurrentJsonFile(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            return ChartFileCodec.IsCurrentJsonDocument(
                File.ReadAllText(path, Encoding.UTF8));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

using System;
using System.IO;
using System.Text;
using REmind.Charting;

/// <summary>ChartMaker's file-system adapter for the shared song contract.</summary>
public static class SongContentFileStore
{
    public static SongContent Load(string dataPath)
    {
        string fullPath = Path.GetFullPath(dataPath);
        SongContent song = SongContentCodec.Parse(
            File.ReadAllText(fullPath, Encoding.UTF8));
        foreach (string reference in new[] { song.AudioFile, song.JacketFile })
        {
            string path = SongContentCodec.ResolveReference(fullPath, reference);
            if (!File.Exists(path))
                throw new FileNotFoundException("Song resource is missing: " + reference, path);
        }
        foreach (SongChartEntry entry in song.Charts)
            SongContentCodec.ResolveReference(fullPath, entry.ChartFile);
        return song;
    }

    public static SongContent ValidateChart(string chartPath, ChartFile chart)
    {
        string fullChartPath = Path.GetFullPath(chartPath);
        string dataPath = FindDataPath(fullChartPath);
        if (dataPath == null) return null; // Standalone and legacy charts remain supported.
        SongContent song = Load(dataPath);
        if (!string.Equals(song.MusicId, chart.MusicId, StringComparison.Ordinal))
            throw new FormatException("Chart musicId does not match song data: " +
                chart.MusicId + " / " + song.MusicId);
        SongChartEntry entry = song.FindChart(chart.DifficultyId);
        if (entry == null || !SamePath(
                SongContentCodec.ResolveReference(dataPath, entry.ChartFile),
                fullChartPath))
            throw new FormatException("The song data does not list this chart and difficulty.");
        return song;
    }

    public static string FindDataPath(string chartPath)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(chartPath));
        for (int depth = 0; depth < 2 && folder != null; depth++)
        {
            string candidate = Path.Combine(folder, "data.json");
            if (File.Exists(candidate)) return candidate;
            folder = Path.GetDirectoryName(folder);
        }
        return null;
    }

    private static bool SamePath(string first, string second) =>
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
            Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
}

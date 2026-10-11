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
        string fileName = Path.GetFileName(fullChartPath);
        string difficultyId = SongChartFiles.GetDifficultyIdFromFileName(fileName);
        SongChartFiles.Validate(fileName, chart.Level, chart.Author, chart.ExtraName);
        if (!string.IsNullOrWhiteSpace(chart.DifficultyId) &&
            !string.Equals(chart.DifficultyId, difficultyId, StringComparison.Ordinal))
            throw new FormatException("Legacy chart difficultyId does not match its filename.");
        chart.DifficultyId = difficultyId;
        if (!SamePath(SongContentCodec.ResolveReference(dataPath, fileName),
                fullChartPath))
            throw new FormatException("Song charts must be in the song folder.");
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

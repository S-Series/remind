using System;
using System.Collections.Generic;
using System.IO;
using REmind.Charting;
using REmind.Gameplay.Demo;
using UnityEditor;
using UnityEngine;

/// <summary>Turns song folders into build-safe Unity references for Music Select.</summary>
public static class MusicCatalogBuilder
{
    private const string MusicFolder = "Assets/Data/Music";
    private const string CatalogPath = MusicFolder + "/MusicCatalog.asset";

    [MenuItem("REmind/Music/Rebuild Catalog")]
    public static void Rebuild()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string musicDirectory = Path.Combine(projectRoot, MusicFolder);
        if (!Directory.Exists(musicDirectory))
            throw new DirectoryNotFoundException(musicDirectory);

        var songs = new List<(string id, TextAsset data, Sprite jacket, AudioClip audio,
            IReadOnlyList<(string id, int level, Sprite jacket, TextAsset package)> difficulties)>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string folder in Directory.GetDirectories(musicDirectory))
        {
            string dataPath = Path.Combine(folder, "data.json");
            if (!File.Exists(dataPath)) continue;
            SongContent song = SongContentCodec.Parse(File.ReadAllText(dataPath));
            if (!ids.Add(song.MusicId))
                throw new FormatException("Duplicate musicId: " + song.MusicId);

            string jacketPath = SongContentCodec.ResolveReference(dataPath, song.JacketFile);
            string audioPath = SongContentCodec.ResolveReference(dataPath, song.AudioFile);
            if (!File.Exists(jacketPath) || !File.Exists(audioPath))
                throw new FileNotFoundException("Song jacket or audio is missing: " + dataPath);
            TextAsset dataAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ToAssetPath(projectRoot, dataPath));
            Sprite jacketAsset = AssetDatabase.LoadAssetAtPath<Sprite>(ToAssetPath(projectRoot, jacketPath));
            AudioClip audioAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(ToAssetPath(projectRoot, audioPath));
            if (!dataAsset)
                throw new InvalidOperationException("Song data is not imported as a TextAsset: " + dataPath);
            if (!jacketAsset)
                throw new InvalidOperationException("Song jacket is not imported as a Sprite: " + jacketPath);
            if (!audioAsset)
                throw new InvalidOperationException("Song audio is not imported as an AudioClip: " + audioPath);

            var difficulties = new List<(string id, int level, Sprite jacket, TextAsset package)>();
            foreach (SongChartEntry entry in song.Charts)
            {
                string chartPath = SongContentCodec.ResolveReference(dataPath, entry.ChartFile);
                if (!File.Exists(chartPath))
                    throw new FileNotFoundException("Song chart is missing: " + entry.ChartFile);
                string chartText = File.ReadAllText(chartPath);
                ChartFile chart = ChartFileCodec.Parse(chartText);
                if (!string.Equals(chart.MusicId, song.MusicId, StringComparison.Ordinal) ||
                    !string.Equals(chart.DifficultyId, entry.DifficultyId, StringComparison.Ordinal))
                    throw new FormatException("Song chart identity does not match data.json: " + chartPath);

                string chartJacketPath = SongContentCodec.ResolveReference(
                    dataPath, chart.EffectiveJacketFile);
                Sprite chartJacket = jacketAsset;
                if (File.Exists(chartJacketPath))
                {
                    chartJacket = AssetDatabase.LoadAssetAtPath<Sprite>(
                        ToAssetPath(projectRoot, chartJacketPath));
                    if (!chartJacket)
                        throw new InvalidOperationException(
                            "Chart jacket is not imported as a Sprite: " + chartJacketPath);
                }
                else if (!string.IsNullOrEmpty(chart.JacketFile))
                    throw new FileNotFoundException(
                        "Explicit chart jacket is missing: " + chart.JacketFile,
                        chartJacketPath);

                string packagePath = ChartMakerRuntimePackageExporter
                    .DefaultOutputPath(chartPath);
                string packageText = ChartMakerRuntimePackageExporter.Export(chartText);
                string packageFolder = ToAssetPath(projectRoot,
                    Path.GetDirectoryName(packagePath));
                if (!AssetDatabase.IsValidFolder(packageFolder))
                    AssetDatabase.CreateFolder(
                        ToAssetPath(projectRoot, Path.GetDirectoryName(chartPath)),
                        "rmp");
                if (!File.Exists(packagePath) ||
                    !string.Equals(File.ReadAllText(packagePath), packageText,
                        StringComparison.Ordinal))
                {
                    File.WriteAllText(packagePath, packageText,
                        new System.Text.UTF8Encoding(false));
                    AssetDatabase.ImportAsset(ToAssetPath(projectRoot, packagePath),
                        ImportAssetOptions.ForceUpdate);
                }
                TextAsset packageAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    ToAssetPath(projectRoot, packagePath));
                if (!packageAsset)
                    throw new InvalidOperationException(
                        "Runtime package is not imported as a TextAsset: " + packagePath);
                difficulties.Add((entry.DifficultyId, entry.Level, chartJacket,
                    packageAsset));
            }
            songs.Add((song.MusicId, dataAsset, jacketAsset, audioAsset, difficulties));
        }
        songs.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        MusicCatalog catalog = AssetDatabase.LoadAssetAtPath<MusicCatalog>(CatalogPath);
        if (!catalog)
        {
            catalog = ScriptableObject.CreateInstance<MusicCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        var serialized = new SerializedObject(catalog);
        SerializedProperty entries = serialized.FindProperty("songs");
        entries.arraySize = songs.Count;
        for (int index = 0; index < songs.Count; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("musicId").stringValue = songs[index].id;
            entry.FindPropertyRelative("songData").objectReferenceValue = songs[index].data;
            entry.FindPropertyRelative("jacket").objectReferenceValue = songs[index].jacket;
            entry.FindPropertyRelative("audioClip").objectReferenceValue = songs[index].audio;
            SerializedProperty difficulties = entry.FindPropertyRelative("difficulties");
            difficulties.arraySize = songs[index].difficulties.Count;
            for (int chartIndex = 0; chartIndex < difficulties.arraySize; chartIndex++)
            {
                var chart = songs[index].difficulties[chartIndex];
                SerializedProperty difficulty = difficulties.GetArrayElementAtIndex(chartIndex);
                difficulty.FindPropertyRelative("difficultyId").stringValue = chart.id;
                difficulty.FindPropertyRelative("level").intValue = chart.level;
                difficulty.FindPropertyRelative("jacket").objectReferenceValue = chart.jacket;
                difficulty.FindPropertyRelative("runtimePackage").objectReferenceValue = chart.package;
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Music catalog rebuilt: " + songs.Count + " songs.");
    }

    private static string ToAssetPath(string projectRoot, string path)
    {
        return path.Substring(projectRoot.Length + 1).Replace('\\', '/');
    }
}

public sealed class MusicCatalogAssetPostprocessor : AssetPostprocessor
{
    private static bool queued;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        if (!ContainsSongChange(imported) && !ContainsSongChange(deleted) &&
            !ContainsSongChange(moved) && !ContainsSongChange(movedFrom)) return;
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            try { MusicCatalogBuilder.Rebuild(); }
            catch (Exception exception) { Debug.LogException(exception); }
        };
    }

    private static bool ContainsSongChange(string[] paths)
    {
        foreach (string path in paths)
            if (path.StartsWith("Assets/Data/Music/", StringComparison.Ordinal) &&
                !string.Equals(path, "Assets/Data/Music/MusicCatalog.asset", StringComparison.Ordinal))
                return true;
        return false;
    }
}

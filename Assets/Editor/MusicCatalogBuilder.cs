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
            IReadOnlyList<SongChartEntry> difficulties)>();
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
            foreach (SongChartEntry chart in song.Charts)
                if (!File.Exists(SongContentCodec.ResolveReference(dataPath, chart.ChartFile)))
                    throw new FileNotFoundException("Song chart is missing: " + chart.ChartFile);

            TextAsset dataAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ToAssetPath(projectRoot, dataPath));
            Sprite jacketAsset = AssetDatabase.LoadAssetAtPath<Sprite>(ToAssetPath(projectRoot, jacketPath));
            AudioClip audioAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(ToAssetPath(projectRoot, audioPath));
            if (!dataAsset)
                throw new InvalidOperationException("Song data is not imported as a TextAsset: " + dataPath);
            if (!jacketAsset)
                throw new InvalidOperationException("Song jacket is not imported as a Sprite: " + jacketPath);
            if (!audioAsset)
                throw new InvalidOperationException("Song audio is not imported as an AudioClip: " + audioPath);
            songs.Add((song.MusicId, dataAsset, jacketAsset, audioAsset, song.Charts));
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
                SongChartEntry chart = songs[index].difficulties[chartIndex];
                SerializedProperty difficulty = difficulties.GetArrayElementAtIndex(chartIndex);
                difficulty.FindPropertyRelative("difficultyId").stringValue = chart.DifficultyId;
                difficulty.FindPropertyRelative("level").intValue = chart.Level;
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

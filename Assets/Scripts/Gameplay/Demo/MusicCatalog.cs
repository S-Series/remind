using System;
using System.Collections.Generic;
using UnityEngine;

namespace REmind.Gameplay.Demo
{
    [Serializable]
    public sealed class MusicDifficultyEntry
    {
        [SerializeField] private string difficultyId;
        [SerializeField] private int level;
        [SerializeField] private string author;
        [SerializeField] private string extraName;
        [SerializeField] private Sprite jacket;
        [SerializeField] private TextAsset runtimePackage;

        public string DifficultyId => difficultyId;
        public int Level => level;
        public string Author => author;
        public string DisplayName => difficultyId == "extra" ? extraName : difficultyId?.ToUpperInvariant();
        public Sprite Jacket => jacket;
        public TextAsset RuntimePackage => runtimePackage;
    }

    [Serializable]
    public sealed class MusicCatalogEntry
    {
        [SerializeField] private string musicId;
        [SerializeField] private TextAsset songData;
        [SerializeField] private Sprite jacket;
        [SerializeField] private AudioClip audioClip;
        [SerializeField] private MusicDifficultyEntry[] difficulties = Array.Empty<MusicDifficultyEntry>();

        public string MusicId => musicId;
        public TextAsset SongData => songData;
        public Sprite Jacket => jacket;
        public AudioClip AudioClip => audioClip;
        public IReadOnlyList<MusicDifficultyEntry> Difficulties =>
            difficulties ?? Array.Empty<MusicDifficultyEntry>();

        public Sprite GetJacket(string difficultyId)
        {
            foreach (MusicDifficultyEntry difficulty in Difficulties)
                if (string.Equals(difficulty.DifficultyId, difficultyId,
                        StringComparison.Ordinal))
                    return difficulty.Jacket ? difficulty.Jacket : jacket;
            return jacket;
        }

        public bool TryGetLevel(string difficultyId, out int level)
        {
            foreach (MusicDifficultyEntry difficulty in Difficulties)
                if (string.Equals(difficulty.DifficultyId, difficultyId, StringComparison.Ordinal))
                {
                    level = difficulty.Level;
                    return true;
                }
            level = 0;
            return false;
        }

        public MusicDifficultyEntry FindDifficulty(string difficultyId)
        {
            foreach (MusicDifficultyEntry difficulty in Difficulties)
                if (string.Equals(difficulty.DifficultyId, difficultyId,
                        StringComparison.Ordinal))
                    return difficulty;
            return null;
        }
    }

    /// <summary>Build-safe references generated from Assets/Data/Music/*/data.json.</summary>
    public sealed class MusicCatalog : ScriptableObject
    {
        [SerializeField] private MusicCatalogEntry[] songs = Array.Empty<MusicCatalogEntry>();

        public IReadOnlyList<MusicCatalogEntry> Songs => songs;

        public MusicCatalogEntry FindSong(string musicId)
        {
            foreach (MusicCatalogEntry song in Songs)
                if (string.Equals(song.MusicId, musicId,
                        StringComparison.Ordinal))
                    return song;
            return null;
        }
    }

    // Five fixed difficulty slots; availability is determined by the catalog.
    internal static class MusicDifficultySlots
    {
        public const int Count = 5;

        public static string GetId(IReadOnlyList<MusicDifficultyEntry> difficulties, int index)
        {
            return REmind.Charting.SongChartFiles.GetDifficultyId(index);
        }
    }
}

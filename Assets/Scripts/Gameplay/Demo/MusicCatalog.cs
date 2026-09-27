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

        public string DifficultyId => difficultyId;
        public int Level => level;
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
    }

    /// <summary>Build-safe references generated from Assets/Data/Music/*/data.json.</summary>
    public sealed class MusicCatalog : ScriptableObject
    {
        [SerializeField] private MusicCatalogEntry[] songs = Array.Empty<MusicCatalogEntry>();

        public IReadOnlyList<MusicCatalogEntry> Songs => songs;
    }
}

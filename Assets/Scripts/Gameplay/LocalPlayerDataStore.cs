using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace REmind.Gameplay
{
    public readonly struct ChartProgressSnapshot
    {
        public string MusicId { get; }
        public string DifficultyId { get; }
        public double BestScore { get; }
        public RankGrade BestRank { get; }
        public bool Cleared { get; }
        public int PlayCount { get; }
        public int MaxCombo { get; }

        internal ChartProgressSnapshot(string musicId, string difficultyId,
            double bestScore, RankGrade bestRank, bool cleared,
            int playCount, int maxCombo)
        {
            MusicId = musicId;
            DifficultyId = difficultyId;
            BestScore = bestScore;
            BestRank = bestRank;
            Cleared = cleared;
            PlayCount = playCount;
            MaxCombo = maxCombo;
        }
    }

    /// <summary>Local, replaceable storage for player-owned song progress.</summary>
    public sealed class LocalPlayerDataStore
    {
        [Serializable]
        private sealed class SaveDocument
        {
            public int version = 1;
            public List<ChartRecord> charts = new List<ChartRecord>();
            public List<string> favoriteMusicIds = new List<string>();
        }

        [Serializable]
        private sealed class ChartRecord
        {
            public string musicId;
            public string difficultyId;
            public double bestScore;
            public int rank;
            public bool cleared;
            public int playCount;
            public int maxCombo;
        }

        private readonly string path;
        private readonly SaveDocument document;

        private LocalPlayerDataStore(string path, SaveDocument document)
        {
            this.path = path;
            this.document = document;
        }

        public static LocalPlayerDataStore Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A player data path is required.",
                    nameof(path));
            string fullPath = Path.GetFullPath(path);
            SaveDocument document = Read(fullPath) ?? Read(fullPath + ".bak") ??
                new SaveDocument();
            return new LocalPlayerDataStore(fullPath, document);
        }

        public bool TryGetBestRecord(string musicId, string difficultyId,
            out double score, out RankGrade rank)
        {
            ChartRecord record = FindRecord(musicId, difficultyId);
            score = record?.bestScore ?? 0d;
            rank = record != null ? (RankGrade)record.rank : RankGrade.D;
            return record != null;
        }

        public bool TryGetProgress(string musicId, string difficultyId,
            out ChartProgressSnapshot progress)
        {
            ChartRecord record = FindRecord(musicId, difficultyId);
            progress = record == null ? default : new ChartProgressSnapshot(
                record.musicId, record.difficultyId, record.bestScore,
                (RankGrade)record.rank, record.cleared, record.playCount,
                record.maxCombo);
            return record != null;
        }

        // One fragment is permanently earned per cleared song/difficulty.
        // Deriving the balance from chart records avoids a second mutable ledger.
        public int MemoryFragmentCount
        {
            get
            {
                int count = 0;
                foreach (ChartRecord record in document.charts)
                    if (record.cleared) count++;
                return count;
            }
        }

        public bool IsFavorite(string musicId) =>
            !string.IsNullOrWhiteSpace(musicId) &&
            document.favoriteMusicIds.Contains(musicId);

        public void SetFavorite(string musicId, bool favorite)
        {
            if (string.IsNullOrWhiteSpace(musicId))
                throw new ArgumentException("A music ID is required.",
                    nameof(musicId));
            if (favorite)
            {
                if (!IsFavorite(musicId))
                    document.favoriteMusicIds.Add(musicId);
            }
            else
                document.favoriteMusicIds.Remove(musicId);
        }

        public bool RecordResult(GameResultSnapshot result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            ChartRecord record = FindRecord(result.MusicId,
                result.DifficultyId);
            bool newBest = record == null || result.Score > record.bestScore;
            if (record == null)
            {
                record = new ChartRecord
                {
                    musicId = result.MusicId,
                    difficultyId = result.DifficultyId
                };
                document.charts.Add(record);
            }
            if (newBest)
            {
                record.bestScore = result.Score;
                record.rank = (int)result.Rank;
            }
            record.cleared |= result.IsCleared;
            record.maxCombo = Math.Max(record.maxCombo, result.MaxCombo);
            if (record.playCount < int.MaxValue) record.playCount++;
            return newBest;
        }

        public void Save()
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(document, true));
            if (File.Exists(path))
                File.Replace(temporary, path, path + ".bak");
            else
                File.Move(temporary, path);
        }

        private ChartRecord FindRecord(string musicId, string difficultyId)
        {
            if (string.IsNullOrWhiteSpace(musicId) ||
                string.IsNullOrWhiteSpace(difficultyId)) return null;
            return document.charts.Find(record => record != null &&
                string.Equals(record.musicId, musicId, StringComparison.Ordinal) &&
                string.Equals(record.difficultyId, difficultyId,
                    StringComparison.Ordinal));
        }

        private static SaveDocument Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                SaveDocument document = JsonUtility.FromJson<SaveDocument>(
                    File.ReadAllText(path));
                if (document == null || document.version != 1 ||
                    document.charts == null ||
                    document.favoriteMusicIds == null)
                    throw new FormatException("Unsupported player data format.");
                foreach (ChartRecord record in document.charts)
                    if (record == null ||
                        string.IsNullOrWhiteSpace(record.musicId) ||
                        string.IsNullOrWhiteSpace(record.difficultyId) ||
                        double.IsNaN(record.bestScore) ||
                        double.IsInfinity(record.bestScore) ||
                        record.bestScore < 0d ||
                        record.playCount < 0 || record.maxCombo < 0 ||
                        record.rank < (int)RankGrade.D ||
                        record.rank > (int)RankGrade.S)
                        throw new FormatException("Invalid chart record.");
                return document;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read local player data at " +
                    path + ": " + exception.Message);
                return null;
            }
        }
    }
}

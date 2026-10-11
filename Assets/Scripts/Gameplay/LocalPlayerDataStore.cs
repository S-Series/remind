using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using REmind.Charting;
using REmind.Gameplay.Items;
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

    /// <summary>Local storage for Game-owned player progress.</summary>
    public sealed class LocalPlayerDataStore
    {
        public const string MemoryFragmentItemId = "currency.memory_fragment";

        [Serializable]
        private sealed class SaveDocument
        {
            public int version = 5;
            public List<ChartRecord> charts = new List<ChartRecord>();
            public List<string> favoriteMusicIds = new List<string>();
            public List<string> ownedCharacterIds = new List<string>();
            public string equippedCharacterId;
            public List<SongAccessRecord> songAccess = new List<SongAccessRecord>();
            public List<ItemRecord> inventory = new List<ItemRecord>();
        }

        // JsonUtility normalizes missing/null lists to empty lists, hiding damaged saves.
        [DataContract]
        private sealed class VersionProbe
        {
            [DataMember] public int version;
            [DataMember] public object[] charts;
            [DataMember] public object[] favoriteMusicIds;
            [DataMember] public object[] ownedCharacterIds;
            [DataMember] public SongAccessProbe[] songAccess;
            [DataMember] public ItemProbe[] inventory;
        }

        [DataContract]
        private sealed class SongAccessProbe
        {
            [DataMember] public object[] unlockedDifficultyIds;
        }

        [DataContract]
        private sealed class ItemProbe
        {
            [DataMember] public string itemId;
            [DataMember] public long? quantity;
        }

        [Serializable]
        private sealed class ItemRecord
        {
            public string itemId;
            public long quantity;
        }

        [Serializable]
        private sealed class SongAccessRecord
        {
            public string musicId;
            public bool isOwned;
            public bool isUnlocked;
            public List<string> unlockedDifficultyIds = new List<string>();
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
        private SaveDocument document;
        private bool recoveredFromBackup;

        private LocalPlayerDataStore(string path, SaveDocument document,
            bool recoveredFromBackup)
        {
            this.path = path;
            this.document = document;
            this.recoveredFromBackup = recoveredFromBackup;
        }

        public static LocalPlayerDataStore Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A player data path is required.",
                    nameof(path));
            string fullPath = Path.GetFullPath(path);
            SaveDocument document = ReadWithBackup(fullPath,
                out bool recoveredFromBackup);
            return new LocalPlayerDataStore(fullPath, document,
                recoveredFromBackup);
        }

        public IReadOnlyList<string> OwnedCharacterIds =>
            document.ownedCharacterIds.AsReadOnly();
        public string EquippedCharacterId => document.equippedCharacterId;

        public long GetItemQuantity(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("An item ID is required.",
                    nameof(itemId));
            return FindItem(document, itemId)?.quantity ?? 0L;
        }

        public void GrantItem(ItemCatalog catalog, string itemId,
            long quantity)
        {
            ItemDefinitionAsset definition = RequireItem(catalog, itemId);
            SaveDocument candidate = CloneDocument();
            AddItem(candidate, definition, quantity);
            Persist(candidate);
            document = candidate;
        }

        private static void AddItem(SaveDocument candidate,
            ItemDefinitionAsset definition, long quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            ItemRecord record = FindItem(candidate, definition.ItemId);
            long current = record?.quantity ?? 0L;
            if (quantity > long.MaxValue - current)
                throw new OverflowException("Item quantity exceeds Int64 range.");
            long next = current + quantity;
            if (next > definition.MaxQuantity)
                throw new InvalidOperationException(
                    $"Item '{definition.ItemId}' exceeds its quantity limit.");
            if (record == null)
                candidate.inventory.Add(new ItemRecord
                {
                    itemId = definition.ItemId,
                    quantity = next
                });
            else record.quantity = next;
        }

        public bool TryConsumeItem(ItemCatalog catalog, string itemId,
            long quantity)
        {
            RequireItem(catalog, itemId);
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (GetItemQuantity(itemId) < quantity) return false;

            SaveDocument candidate = CloneDocument();
            ItemRecord record = FindItem(candidate, itemId);
            record.quantity -= quantity;
            if (record.quantity == 0) candidate.inventory.Remove(record);
            Persist(candidate);
            document = candidate;
            return true;
        }

        private static ItemDefinitionAsset RequireItem(ItemCatalog catalog,
            string itemId)
        {
            if (!catalog) throw new ArgumentNullException(nameof(catalog));
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("An item ID is required.",
                    nameof(itemId));
            if (!catalog.TryGetEntry(itemId, out ItemDefinitionAsset definition))
                throw new InvalidOperationException(
                    $"Item '{itemId}' is not in the catalog.");
            return definition;
        }

        private static ItemRecord FindItem(SaveDocument source,
            string itemId) => source.inventory.Find(record =>
            string.Equals(record.itemId, itemId, StringComparison.Ordinal));

        public bool OwnsSong(string musicId) => FindSongAccess(musicId)?.isOwned ?? false;
        public bool IsSongUnlocked(string musicId)
        {
            SongAccessRecord access = FindSongAccess(musicId);
            if (access == null) return false;
            if (access.isUnlocked) return true;
            for (int index = 0; index < SongChartFiles.Count; index++)
                if (!access.unlockedDifficultyIds.Contains(
                    SongChartFiles.GetDifficultyId(index))) return false;
            return true;
        }
        public bool HasAnyUnlockedDifficulty(string musicId)
        {
            SongAccessRecord access = FindSongAccess(musicId);
            return access != null &&
                (access.isUnlocked || access.unlockedDifficultyIds.Count > 0);
        }

        public bool IsSongDifficultyUnlocked(string musicId, string difficultyId)
        {
            if (SongChartFiles.GetFileName(difficultyId) == null) return false;
            SongAccessRecord access = FindSongAccess(musicId);
            return access != null && (access.isUnlocked ||
                access.unlockedDifficultyIds.Contains(difficultyId));
        }

        public void SetSongOwned(string musicId, bool owned) =>
            SetSongAccess(musicId, owned, IsSongUnlocked(musicId));

        public void SetSongUnlocked(string musicId, bool unlocked) =>
            SetSongAccess(musicId, OwnsSong(musicId), unlocked,
                clearPartialUnlocks: !unlocked);

        private SongAccessRecord FindSongAccess(string musicId) =>
            document.songAccess.Find(record =>
                string.Equals(record.musicId, musicId, StringComparison.Ordinal));

        private void SetSongAccess(string musicId, bool owned, bool unlocked,
            bool clearPartialUnlocks = false)
        {
            if (string.IsNullOrWhiteSpace(musicId))
                throw new ArgumentException("A music ID is required.", nameof(musicId));
            SongAccessRecord current = FindSongAccess(musicId);
            if (OwnsSong(musicId) == owned && IsSongUnlocked(musicId) == unlocked &&
                (!clearPartialUnlocks || current == null ||
                 current.unlockedDifficultyIds.Count == 0)) return;
            SaveDocument candidate = CloneDocument();
            SongAccessRecord record = candidate.songAccess.Find(item =>
                string.Equals(item.musicId, musicId, StringComparison.Ordinal));
            if (record == null)
            {
                record = new SongAccessRecord { musicId = musicId };
                candidate.songAccess.Add(record);
            }
            record.isOwned = owned;
            record.isUnlocked = unlocked;
            if (clearPartialUnlocks) record.unlockedDifficultyIds.Clear();
            Persist(candidate);
            document = candidate;
        }

        public bool OwnsCharacter(string characterId) =>
            !string.IsNullOrWhiteSpace(characterId) &&
            document.ownedCharacterIds.Contains(characterId);

        /// <summary>
        /// Grants the starter once and repairs an unavailable equipped ID. Unknown
        /// owned IDs remain in the save so future content can recognize them.
        /// The caller supplies the validated game catalog, including the starter.
        /// </summary>
        public bool EnsureStarterCharacter(string starterId,
            Func<string, bool> isAvailableCharacter)
        {
            if (string.IsNullOrWhiteSpace(starterId))
                throw new ArgumentException("A starter character ID is required.",
                    nameof(starterId));
            if (isAvailableCharacter == null)
                throw new ArgumentNullException(nameof(isAvailableCharacter));
            if (!isAvailableCharacter(starterId))
                throw new InvalidOperationException(
                    "The starter character is not available in the catalog.");

            bool ownsStarter = OwnsCharacter(starterId);
            bool equippedAvailable = OwnsCharacter(document.equippedCharacterId) &&
                isAvailableCharacter(document.equippedCharacterId);
            if (ownsStarter && equippedAvailable &&
                !recoveredFromBackup) return false;

            SaveDocument candidate = CloneDocument();
            if (!ownsStarter) candidate.ownedCharacterIds.Add(starterId);
            if (!equippedAvailable) candidate.equippedCharacterId = starterId;
            Persist(candidate);
            document = candidate;
            return !ownsStarter || !equippedAvailable;
        }

        /// <summary>Commits equipment only after the replacement file succeeds.</summary>
        public void EquipCharacter(string characterId)
        {
            if (!OwnsCharacter(characterId))
                throw new InvalidOperationException(
                    "The selected character is not owned.");
            if (string.Equals(document.equippedCharacterId, characterId,
                StringComparison.Ordinal)) return;
            SaveDocument candidate = CloneDocument();
            candidate.equippedCharacterId = characterId;
            Persist(candidate);
            document = candidate;
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

        public long MemoryFragmentCount =>
            GetItemQuantity(MemoryFragmentItemId);

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
            record.cleared |= result.IsCleared && !result.IsFailed;
            record.maxCombo = Math.Max(record.maxCombo, result.MaxCombo);
            if (record.playCount < int.MaxValue) record.playCount++;
            return newBest;
        }

        /// <summary>Commits the result, unlock, and first-clear fragment together.</summary>
        public bool RecordAndSaveResult(GameResultSnapshot result,
            string unlockTargetMusicId, ItemCatalog itemCatalog)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.IsAutoPlay)
                throw new ArgumentException("Auto Play results cannot be recorded.",
                    nameof(result));

            bool firstClear = result.IsCleared && !result.IsFailed &&
                !(FindRecord(result.MusicId, result.DifficultyId)?.cleared ?? false);
            SaveDocument previous = document;
            document = CloneDocument();
            try
            {
                bool newBest = RecordResult(result);
                if (firstClear)
                    AddItem(document,
                        RequireItem(itemCatalog, MemoryFragmentItemId), 1L);
                if (result.IsCleared && !result.IsFailed &&
                    !string.IsNullOrEmpty(unlockTargetMusicId))
                {
                    int clearIndex = SongUnlockDifficultyOrder.IndexOf(
                        result.DifficultyId);
                    if (clearIndex < 0)
                        throw new InvalidOperationException(
                            "The challenge difficulty has no defined unlock order.");
                    SongAccessRecord access = FindSongAccess(unlockTargetMusicId);
                    if (access == null)
                    {
                        access = new SongAccessRecord
                        {
                            musicId = unlockTargetMusicId
                        };
                        document.songAccess.Add(access);
                    }
                    if (!access.isUnlocked)
                        for (int index = 0; index <= clearIndex; index++)
                        {
                            string difficultyId = SongChartFiles.GetDifficultyId(index);
                            if (!access.unlockedDifficultyIds.Contains(difficultyId))
                                access.unlockedDifficultyIds.Add(difficultyId);
                        }
                }
                Persist(document);
                return newBest;
            }
            catch
            {
                document = previous;
                throw;
            }
        }

        public void Save()
        {
            Persist(document);
        }

        private void Persist(SaveDocument candidate)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(candidate, true));
            if (File.Exists(path))
                File.Replace(temporary, path,
                    recoveredFromBackup ? null : path + ".bak");
            else
                File.Move(temporary, path);
            recoveredFromBackup = false;
        }

        private SaveDocument CloneDocument() => new SaveDocument
        {
            charts = document.charts.ConvertAll(record => new ChartRecord
            {
                musicId = record.musicId,
                difficultyId = record.difficultyId,
                bestScore = record.bestScore,
                rank = record.rank,
                cleared = record.cleared,
                playCount = record.playCount,
                maxCombo = record.maxCombo
            }),
            favoriteMusicIds = new List<string>(document.favoriteMusicIds),
            ownedCharacterIds = new List<string>(document.ownedCharacterIds),
            equippedCharacterId = document.equippedCharacterId,
            songAccess = document.songAccess.ConvertAll(record => new SongAccessRecord
            {
                musicId = record.musicId,
                isOwned = record.isOwned,
                isUnlocked = record.isUnlocked,
                unlockedDifficultyIds = new List<string>(
                    record.unlockedDifficultyIds)
            }),
            inventory = document.inventory.ConvertAll(record => new ItemRecord
            {
                itemId = record.itemId,
                quantity = record.quantity
            })
        };

        private ChartRecord FindRecord(string musicId, string difficultyId)
        {
            if (string.IsNullOrWhiteSpace(musicId) ||
                string.IsNullOrWhiteSpace(difficultyId)) return null;
            return document.charts.Find(record => record != null &&
                string.Equals(record.musicId, musicId, StringComparison.Ordinal) &&
                string.Equals(record.difficultyId, difficultyId,
                    StringComparison.Ordinal));
        }

        private static SaveDocument ReadWithBackup(string path,
            out bool recoveredFromBackup)
        {
            recoveredFromBackup = false;
            if (!File.Exists(path) && !File.Exists(path + ".bak"))
                return new SaveDocument();
            try { return Read(path); }
            catch (UnsupportedSaveVersionException) { throw; }
            catch (Exception primaryFailure)
            {
                if (!File.Exists(path + ".bak"))
                    throw new InvalidDataException(
                        "Player data is unreadable and has no backup.",
                        primaryFailure);
                Debug.LogWarning("Could not read local player data at " +
                    path + ": " + primaryFailure.Message +
                    ". Trying the backup.");
                try
                {
                    SaveDocument backup = Read(path + ".bak");
                    recoveredFromBackup = true;
                    return backup;
                }
                catch (Exception backupFailure)
                {
                    throw new InvalidDataException(
                        "Player data and its backup are unreadable.",
                        new AggregateException(primaryFailure, backupFailure));
                }
            }
        }

        private static SaveDocument Read(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Player data file is missing.", path);
            string json = File.ReadAllText(path);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            VersionProbe header = (VersionProbe)new DataContractJsonSerializer(
                typeof(VersionProbe)).ReadObject(stream);
            if (header == null || header.version < 1)
                throw new FormatException("Player data version is missing or invalid.");
            if (header.version > 5)
                throw new UnsupportedSaveVersionException(header.version);
            if (header.charts == null || header.favoriteMusicIds == null ||
                (header.version >= 2 && header.ownedCharacterIds == null) ||
                (header.version >= 3 && header.songAccess == null) ||
                (header.version >= 5 && header.inventory == null))
                throw new FormatException("Player data fields are missing.");
            if (header.version >= 4)
                foreach (SongAccessProbe access in header.songAccess)
                    if (access == null || access.unlockedDifficultyIds == null)
                        throw new FormatException(
                            "Song difficulty unlock list is missing.");
            if (header.version >= 5)
                foreach (ItemProbe item in header.inventory)
                    if (item == null || string.IsNullOrWhiteSpace(item.itemId) ||
                        !item.quantity.HasValue || item.quantity.Value < 0)
                        throw new FormatException("Invalid item quantity.");
            SaveDocument document = JsonUtility.FromJson<SaveDocument>(json);
            if (document == null)
                throw new FormatException("Player data is empty or malformed.");
            if (document.version < 1 || document.version > 5 ||
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

            if (document.version == 1)
            {
                document.ownedCharacterIds = new List<string>();
                document.equippedCharacterId = null;
            }
            else if (document.ownedCharacterIds == null)
                throw new FormatException("Invalid character ownership list.");
            foreach (string ownedId in document.ownedCharacterIds)
                if (string.IsNullOrWhiteSpace(ownedId))
                    throw new FormatException("Invalid owned character ID.");
            if (document.version < 3) document.songAccess = new List<SongAccessRecord>();
            var songIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SongAccessRecord record in document.songAccess)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.musicId) ||
                    !songIds.Add(record.musicId))
                    throw new FormatException("Invalid or duplicate song access record.");
                if (document.version < 4)
                    record.unlockedDifficultyIds = new List<string>();
                if (record.unlockedDifficultyIds == null)
                    throw new FormatException("Invalid song difficulty unlock list.");
                var difficultyIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string difficultyId in record.unlockedDifficultyIds)
                    if (SongChartFiles.GetFileName(difficultyId) == null ||
                        !difficultyIds.Add(difficultyId))
                        throw new FormatException(
                            "Invalid or duplicate song difficulty unlock.");
            }
            if (document.version < 5)
            {
                document.inventory = new List<ItemRecord>();
                long fragments = 0L;
                foreach (ChartRecord record in document.charts)
                    if (record.cleared) fragments++;
                if (fragments > 0L)
                    document.inventory.Add(new ItemRecord
                    {
                        itemId = MemoryFragmentItemId,
                        quantity = fragments
                    });
            }
            if (document.inventory == null)
                throw new FormatException("Invalid inventory list.");
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemRecord item in document.inventory)
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) ||
                    item.quantity < 0 || !itemIds.Add(item.itemId))
                    throw new FormatException("Invalid or duplicate item record.");
            document.version = 5;
            return document;
        }

        private sealed class UnsupportedSaveVersionException :
            NotSupportedException
        {
            public UnsupportedSaveVersionException(int version)
                : base("Player data version " + version +
                    " is newer than this build supports.") { }
        }
    }
}

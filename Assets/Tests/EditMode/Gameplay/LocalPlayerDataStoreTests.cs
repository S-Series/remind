using System;
using System.IO;
using NUnit.Framework;

namespace REmind.Gameplay.Tests
{
    public sealed class LocalPlayerDataStoreTests
    {
        private string path;

        [SetUp]
        public void SetUp() => path = Path.Combine(Path.GetTempPath(),
            "remind-player-test-" + Guid.NewGuid().ToString("N") + ".json");

        [TearDown]
        public void TearDown()
        {
            foreach (string candidate in new[] { path, path + ".bak", path + ".tmp" })
            {
                if (File.Exists(candidate)) File.Delete(candidate);
                if (Directory.Exists(candidate)) Directory.Delete(candidate, true);
            }
        }

        [Test]
        public void VersionOneMigrationPreservesRecordsFavoritesAndStarterAcrossReload()
        {
            File.WriteAllText(path,
                "{\"version\":1,\"charts\":[{\"musicId\":\"song\",\"difficultyId\":\"hard\",\"bestScore\":750000,\"rank\":1,\"cleared\":true,\"playCount\":2,\"maxCombo\":11}],\"favoriteMusicIds\":[\"song\"]}");
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(store.EnsureStarterCharacter("non", id => id == "non"));
            Assert.IsFalse(store.EnsureStarterCharacter("non", id => id == "non"));

            LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(path);
            Assert.AreEqual("non", reloaded.EquippedCharacterId);
            CollectionAssert.AreEquivalent(new[] { "non" }, reloaded.OwnedCharacterIds);
            Assert.IsTrue(reloaded.IsFavorite("song"));
            Assert.IsTrue(reloaded.TryGetProgress("song", "hard",
                out ChartProgressSnapshot progress));
            Assert.AreEqual(750000d, progress.BestScore);
            Assert.IsTrue(progress.Cleared);
            Assert.AreEqual(2, progress.PlayCount);
            StringAssert.Contains("\"version\": 4", File.ReadAllText(path));
            Assert.IsFalse(reloaded.OwnsSong("song"));
            Assert.IsFalse(reloaded.IsSongUnlocked("song"));
        }

        [Test]
        public void VersionTwoMigratesWithoutGrantingSongOwnershipOrUnlocks()
        {
            File.WriteAllText(path,
                "{\"version\":2,\"charts\":[],\"favoriteMusicIds\":[\"song\"]," +
                "\"ownedCharacterIds\":[\"non\"],\"equippedCharacterId\":\"non\"}");
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            Assert.IsFalse(store.OwnsSong("song"));
            Assert.IsFalse(store.IsSongUnlocked("song"));
            store.Save();
            LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(reloaded.IsFavorite("song"));
            Assert.IsTrue(reloaded.OwnsCharacter("non"));
            Assert.AreEqual("non", reloaded.EquippedCharacterId);
            Assert.IsFalse(reloaded.OwnsSong("song"));
            Assert.IsFalse(reloaded.IsSongUnlocked("song"));
            StringAssert.Contains("\"version\": 4", File.ReadAllText(path));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void VersionThreeFullUnlockMigratesWithoutChangingOwnership(bool unlocked)
        {
            File.WriteAllText(path,
                "{\"version\":3,\"charts\":[],\"favoriteMusicIds\":[]," +
                "\"ownedCharacterIds\":[],\"songAccess\":[{" +
                "\"musicId\":\"designant\",\"isOwned\":true," +
                "\"isUnlocked\":" + (unlocked ? "true" : "false") + "}]}");
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(store.OwnsSong("designant"));
            Assert.AreEqual(unlocked, store.IsSongUnlocked("designant"));
            foreach (string difficulty in new[]
            {
                "easy", "normal", "hard", "chaos", "extra"
            })
                Assert.AreEqual(unlocked,
                    store.IsSongDifficultyUnlocked("designant", difficulty));
            store.Save();

            store = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(store.OwnsSong("designant"));
            Assert.AreEqual(unlocked,
                store.IsSongDifficultyUnlocked("designant", "extra"));
            StringAssert.Contains("\"version\": 4", File.ReadAllText(path));
        }

        [Test]
        public void OwnershipChangesPreservePartialDifficultyUnlocks()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.RecordAndSaveResult(new GameResultSnapshot(
                "lament_rain", "normal", 1000d, 1_000_000, RankGrade.D,
                1, 0, 0, 0, 1, 1, true, false), "designant");
            store.SetSongOwned("designant", true);
            store.SetSongOwned("designant", false);

            store = LocalPlayerDataStore.Load(path);
            Assert.IsFalse(store.OwnsSong("designant"));
            Assert.IsFalse(store.IsSongUnlocked("designant"));
            Assert.IsTrue(store.IsSongDifficultyUnlocked("designant", "normal"));
            Assert.IsFalse(store.IsSongDifficultyUnlocked("designant", "hard"));
        }

        [Test]
        public void SongOwnershipAndUnlockPersistIndependently()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.SetSongOwned("song", true);
            store = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(store.OwnsSong("song"));
            Assert.IsFalse(store.IsSongUnlocked("song"));
            store.SetSongUnlocked("song", true);
            store.SetSongOwned("song", false);
            store = LocalPlayerDataStore.Load(path);
            Assert.IsFalse(store.OwnsSong("song"));
            Assert.IsTrue(store.IsSongUnlocked("song"));
            store.SetSongOwned("song", true);
            store.SetSongUnlocked("song", false);
            store = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(store.OwnsSong("song"));
            Assert.IsFalse(store.IsSongUnlocked("song"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FailedSongAccessWritePreservesMemoryAndDisk(bool ownership)
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.SetSongOwned("song", true);
            string original = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");
            Assert.Throws<UnauthorizedAccessException>(() =>
            {
                if (ownership) store.SetSongOwned("song", false);
                else store.SetSongUnlocked("song", true);
            });
            Assert.IsTrue(store.OwnsSong("song"));
            Assert.IsFalse(store.IsSongUnlocked("song"));
            Assert.AreEqual(original, File.ReadAllText(path));
        }

        [TestCase("")]
        [TestCase(",\"songAccess\":null")]
        [TestCase(",\"songAccess\":[{\"musicId\":\"song\"},{\"musicId\":\"song\"}]")]
        public void VersionThreeRejectsMissingOrInvalidSongAccess(string accessJson)
        {
            File.WriteAllText(path,
                "{\"version\":3,\"charts\":[],\"favoriteMusicIds\":[]," +
                "\"ownedCharacterIds\":[]" + accessJson + "}");
            Assert.Throws<InvalidDataException>(() => LocalPlayerDataStore.Load(path));
        }

        [TestCase("")]
        [TestCase(",\"unlockedDifficultyIds\":null")]
        public void VersionFourRejectsMissingOrNullDifficultyUnlockList(
            string difficultyField)
        {
            File.WriteAllText(path,
                "{\"version\":4,\"charts\":[],\"favoriteMusicIds\":[]," +
                "\"ownedCharacterIds\":[],\"songAccess\":[{" +
                "\"musicId\":\"designant\",\"isOwned\":false," +
                "\"isUnlocked\":false" + difficultyField + "}]}");
            Assert.Throws<InvalidDataException>(() => LocalPlayerDataStore.Load(path));
        }

        [Test]
        public void UnavailableEquippedIdRepairsToStarterWithoutRemovingUnknownOwnedId()
        {
            File.WriteAllText(path,
                "{\"version\":2,\"charts\":[],\"favoriteMusicIds\":[],\"ownedCharacterIds\":[\"retired\"],\"equippedCharacterId\":\"retired\"}");
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(store.EnsureStarterCharacter("non", id => id == "non"));

            LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(path);
            Assert.AreEqual("non", reloaded.EquippedCharacterId);
            CollectionAssert.AreEquivalent(new[] { "retired", "non" },
                reloaded.OwnedCharacterIds);
        }

        [Test]
        public void InvalidStarterDefinitionDoesNotGrantOrSaveCharacter()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            Assert.Throws<InvalidOperationException>(() =>
                store.EnsureStarterCharacter("non", _ => false));
            Assert.IsFalse(File.Exists(path));
            Assert.IsNull(store.EquippedCharacterId);
        }

        [Test]
        public void FailedEquipmentWriteKeepsMemoryAndDiskAtPreviousCharacter()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.EnsureStarterCharacter("non", id => id == "non");
            // A second owned character is represented by a valid existing save.
            File.WriteAllText(path,
                "{\"version\":2,\"charts\":[],\"favoriteMusicIds\":[],\"ownedCharacterIds\":[\"non\",\"other\"],\"equippedCharacterId\":\"non\"}");
            store = LocalPlayerDataStore.Load(path);
            Directory.CreateDirectory(path + ".tmp");

            Assert.Throws<UnauthorizedAccessException>(() =>
                store.EquipCharacter("other"));
            Assert.AreEqual("non", store.EquippedCharacterId);
            Assert.AreEqual("non", LocalPlayerDataStore.Load(path)
                .EquippedCharacterId);
        }

        [Test]
        public void NewerOrUnrecoverableSaveCannotBeReplacedByFreshProgress()
        {
            File.WriteAllText(path,
                "{\"version\":99,\"charts\":[],\"favoriteMusicIds\":[]}");
            string future = File.ReadAllText(path);
            Assert.That(() => LocalPlayerDataStore.Load(path),
                Throws.InstanceOf<NotSupportedException>());
            Assert.AreEqual(future, File.ReadAllText(path));

            File.WriteAllText(path, "{broken json");
            string corrupt = File.ReadAllText(path);
            Assert.Throws<InvalidDataException>(() =>
                LocalPlayerDataStore.Load(path));
            Assert.AreEqual(corrupt, File.ReadAllText(path));
        }

        [Test]
        public void SaveReload_PreservesBestPerDifficultyAndFavorites()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(store.RecordResult(Result("hard", 810000d,
                RankGrade.A)));
            Assert.IsFalse(store.RecordResult(Result("hard", 600000d,
                RankGrade.B)));
            Assert.IsTrue(store.RecordResult(Result("easy", 910000d,
                RankGrade.S)));
            store.SetFavorite("song", true);
            store.Save();

            LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(reloaded.TryGetBestRecord("song", "hard",
                out double hardScore, out RankGrade hardRank));
            Assert.AreEqual(810000d, hardScore);
            Assert.AreEqual(RankGrade.A, hardRank);
            Assert.IsTrue(reloaded.TryGetBestRecord("song", "easy",
                out double easyScore, out RankGrade easyRank));
            Assert.AreEqual(910000d, easyScore);
            Assert.AreEqual(RankGrade.S, easyRank);
            Assert.IsTrue(reloaded.IsFavorite("song"));
            reloaded.SetFavorite("song", false);
            reloaded.Save();
            Assert.IsFalse(LocalPlayerDataStore.Load(path).IsFavorite("song"));
        }

        [Test]
        public void CorruptCurrentFile_RecoversLastSavedBackup()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.RecordResult(Result("hard", 750000d, RankGrade.B));
            store.Save();
            store.RecordResult(Result("hard", 800000d, RankGrade.A));
            store.Save();
            File.WriteAllText(path, "{not json");

            LocalPlayerDataStore recovered = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(recovered.TryGetBestRecord("song", "hard",
                out double score, out RankGrade rank));
            Assert.AreEqual(750000d, score);
            Assert.AreEqual(RankGrade.B, rank);
        }

        [Test]
        public void SavingRecoveredDataKeepsTheLastValidBackup()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.RecordResult(Result("hard", 750000d, RankGrade.B));
            store.Save();
            store.RecordResult(Result("hard", 800000d, RankGrade.A));
            store.Save();
            File.WriteAllText(path, "{broken primary");

            LocalPlayerDataStore recovered = LocalPlayerDataStore.Load(path);
            recovered.EnsureStarterCharacter("non", id => id == "non");
            File.WriteAllText(path, "{broken again");

            LocalPlayerDataStore secondRecovery = LocalPlayerDataStore.Load(path);
            Assert.IsTrue(secondRecovery.TryGetBestRecord("song", "hard",
                out double score, out _));
            Assert.AreEqual(750000d, score);
        }

        [Test]
        public void ProgressTracksAttemptsClearAndBestComboAcrossReload()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.RecordResult(Result("hard", 0d, RankGrade.D,
                cleared: false, combo: 0));
            Assert.IsTrue(store.TryGetProgress("song", "hard",
                out ChartProgressSnapshot failed));
            Assert.AreEqual(1, failed.PlayCount);
            Assert.IsFalse(failed.Cleared);
            store.RecordResult(Result("hard", 700000d, RankGrade.B,
                cleared: true, combo: 12));
            store.RecordResult(Result("hard", 600000d, RankGrade.C,
                cleared: false, combo: 8));
            store.Save();

            Assert.IsTrue(LocalPlayerDataStore.Load(path).TryGetProgress(
                "song", "hard", out ChartProgressSnapshot progress));
            Assert.AreEqual(3, progress.PlayCount);
            Assert.IsTrue(progress.Cleared);
            Assert.AreEqual(12, progress.MaxCombo);
            Assert.AreEqual(700000d, progress.BestScore);
            Assert.AreEqual(RankGrade.B, progress.BestRank);
        }

        [Test]
        public void MemoryFragmentsAreEarnedOncePerChartFirstClear()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.RecordResult(Result("hard", 0d, RankGrade.D,
                cleared: false, combo: 0));
            Assert.AreEqual(0, store.MemoryFragmentCount);
            store.RecordResult(Result("hard", 700000d, RankGrade.B));
            store.RecordResult(Result("hard", 800000d, RankGrade.A));
            store.RecordResult(Result("easy", 500000d, RankGrade.C));
            Assert.AreEqual(2, store.MemoryFragmentCount);
            store.Save();
            Assert.AreEqual(2, LocalPlayerDataStore.Load(path)
                .MemoryFragmentCount);
        }

        private static GameResultSnapshot Result(string difficulty,
            double score, RankGrade rank, bool cleared = true,
            int combo = 1) => new GameResultSnapshot(
                "song", difficulty, score, 1000000, rank,
                cleared ? 1 : 0, 0, 0, cleared ? 0 : 1,
                combo, Math.Max(1, combo), cleared, !cleared);
    }
}

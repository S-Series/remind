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
                if (File.Exists(candidate)) File.Delete(candidate);
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

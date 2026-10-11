using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using REmind.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class ItemInventoryTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private string path;
        private ItemDefinitionAsset item;
        private ItemCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Path.GetTempPath(),
                "remind-items-" + Guid.NewGuid().ToString("N") + ".json");
            item = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            SetField(item, "itemId", "currency.memory_fragment");
            SetField(item, "displayName", "기억 조각");
            SetField(item, "kindId", "currency");
            SetField(item, "maxQuantity", 10L);
            SetField(catalog, "entries", new[] { item });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(item);
            foreach (string candidate in new[]
                { path, path + ".bak", path + ".tmp" })
            {
                if (File.Exists(candidate)) File.Delete(candidate);
                if (Directory.Exists(candidate)) Directory.Delete(candidate, true);
            }
        }

        [Test]
        public void AuthoredCatalogRegistersMemoryFragmentWithoutStartingBalance()
        {
            ItemCatalog authored = AssetDatabase.LoadAssetAtPath<ItemCatalog>(
                "Assets/Data/Items/ItemCatalog.asset");
            Assert.IsNotNull(authored);
            authored.ValidateForUse();
            Assert.IsTrue(authored.TryGetEntry("currency.memory_fragment",
                out ItemDefinitionAsset definition));
            Assert.AreEqual(LocalPlayerDataStore.MemoryFragmentItemId,
                definition.ItemId);
            Assert.AreEqual("currency", definition.KindId);
            Assert.AreEqual(0L, LocalPlayerDataStore.Load(path)
                .GetItemQuantity("currency.memory_fragment"));
        }

        [Test]
        public void CatalogRejectsDuplicateIdsAndInvalidQuantityLimit()
        {
            ItemDefinitionAsset duplicate =
                ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            try
            {
                SetField(duplicate, "itemId", "currency.memory_fragment");
                SetField(duplicate, "displayName", "다른 재화");
                SetField(duplicate, "kindId", "currency");
                SetField(catalog, "entries", new[] { item, duplicate });
                Assert.Throws<InvalidOperationException>(catalog.ValidateForUse);

                SetField(catalog, "entries", new[] { item });
                SetField(item, "maxQuantity", 0L);
                Assert.Throws<InvalidOperationException>(catalog.ValidateForUse);
            }
            finally { UnityEngine.Object.DestroyImmediate(duplicate); }
        }

        [Test]
        public void GrantConsumeAndReloadUseIntegerQuantities()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.GrantItem(catalog, "currency.memory_fragment", 7L);
            Assert.IsFalse(store.TryConsumeItem(catalog, "currency.memory_fragment", 8L));
            Assert.AreEqual(7L, store.GetItemQuantity("currency.memory_fragment"));
            Assert.IsTrue(store.TryConsumeItem(catalog, "currency.memory_fragment", 3L));
            Assert.AreEqual(4L, LocalPlayerDataStore.Load(path)
                .GetItemQuantity("currency.memory_fragment"));
            Assert.IsTrue(store.TryConsumeItem(catalog, "currency.memory_fragment", 4L));
            Assert.AreEqual(0L, LocalPlayerDataStore.Load(path)
                .GetItemQuantity("currency.memory_fragment"));
        }

        [Test]
        public void FirstClearCommitPreservesExistingBalanceAndAwardsOne()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.GrantItem(catalog, "currency.memory_fragment", 3L);
            store.RecordAndSaveResult(new GameResultSnapshot(
                "song", "hard", 750000d, 1_000_000, RankGrade.B,
                1, 0, 0, 0, 1, 1, true, false), null, catalog);

            Assert.AreEqual(4L, LocalPlayerDataStore.Load(path)
                .GetItemQuantity("currency.memory_fragment"));
        }

        [Test]
        public void AppRootSpendsThroughItsLoadedPlayerStore()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.GrantItem(catalog, "currency.memory_fragment", 2L);
            var rootObject = new GameObject("Item owner");
            rootObject.SetActive(false);
            try
            {
                AppRoot app = rootObject.AddComponent<AppRoot>();
                SetField(app, "playerData", store);
                SetField(app, "itemCatalog", catalog);
                Assert.AreEqual(2L, app.MemoryFragmentCount);
                Assert.IsTrue(app.TrySpendMemoryFragments(1L));
                Assert.AreEqual(1L, app.MemoryFragmentCount);
                Assert.IsFalse(app.TrySpendMemoryFragments(2L));
                Assert.AreEqual(1L, LocalPlayerDataStore.Load(path)
                    .MemoryFragmentCount);
            }
            finally { UnityEngine.Object.DestroyImmediate(rootObject); }
        }

        [Test]
        public void VersionFourMigrationSeedsBalanceOnceAndPreservesProgress()
        {
            File.WriteAllText(path,
                "{\"version\":4,\"charts\":[{\"musicId\":\"song\"," +
                "\"difficultyId\":\"hard\",\"bestScore\":750000," +
                "\"rank\":1,\"cleared\":true,\"playCount\":2," +
                "\"maxCombo\":11}],\"favoriteMusicIds\":[\"song\"]," +
                "\"ownedCharacterIds\":[\"non\"]," +
                "\"equippedCharacterId\":\"non\"," +
                "\"songAccess\":[{\"musicId\":\"song\"," +
                "\"isOwned\":true,\"isUnlocked\":false," +
                "\"unlockedDifficultyIds\":[\"hard\"]}]}");
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            Assert.AreEqual(1L, store.GetItemQuantity("currency.memory_fragment"));
            Assert.IsTrue(store.TryConsumeItem(catalog,
                "currency.memory_fragment", 1L));

            LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(path);
            Assert.AreEqual(0L, reloaded.GetItemQuantity("currency.memory_fragment"));
            Assert.IsTrue(reloaded.TryGetProgress("song", "hard",
                out ChartProgressSnapshot progress));
            Assert.AreEqual(2, progress.PlayCount);
            Assert.IsTrue(progress.Cleared);
            Assert.IsTrue(reloaded.IsFavorite("song"));
            Assert.AreEqual("non", reloaded.EquippedCharacterId);
            Assert.IsTrue(reloaded.OwnsCharacter("non"));
            Assert.IsTrue(reloaded.OwnsSong("song"));
            Assert.IsTrue(reloaded.IsSongDifficultyUnlocked("song", "hard"));
            StringAssert.Contains("\"version\": 5", File.ReadAllText(path));
        }

        [Test]
        public void FailedThenFirstClearAndReclearAfterSpendingAwardsOnlyOnce()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            var failure = new GameResultSnapshot("song", "hard", 0d,
                1_000_000, RankGrade.D, 0, 0, 0, 1, 0, 1,
                false, true);
            var clear = new GameResultSnapshot("song", "hard", 750000d,
                1_000_000, RankGrade.B, 1, 0, 0, 0, 1, 1,
                true, false);
            store.RecordAndSaveResult(failure, null, catalog);
            Assert.AreEqual(0L, store.MemoryFragmentCount);
            store.RecordAndSaveResult(clear, null, catalog);
            Assert.AreEqual(1L, store.MemoryFragmentCount);
            Assert.IsTrue(store.TryConsumeItem(catalog,
                "currency.memory_fragment", 1L));
            store.RecordAndSaveResult(clear, null, catalog);

            LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(path);
            Assert.AreEqual(0L, reloaded.MemoryFragmentCount);
            Assert.IsTrue(reloaded.TryGetProgress("song", "hard",
                out ChartProgressSnapshot progress));
            Assert.AreEqual(3, progress.PlayCount);
            Assert.IsTrue(progress.Cleared);
        }

        [Test]
        public void FailedFirstClearWriteCanRetryWithoutPartialAward()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            var clear = new GameResultSnapshot("song", "hard", 750000d,
                1_000_000, RankGrade.B, 1, 0, 0, 0, 1, 1,
                true, false);
            Directory.CreateDirectory(path + ".tmp");
            try
            {
                Assert.Throws<UnauthorizedAccessException>(() =>
                    store.RecordAndSaveResult(clear, null, catalog));
                Assert.AreEqual(0L, store.MemoryFragmentCount);
                Assert.IsFalse(store.TryGetProgress("song", "hard", out _));
                Assert.IsFalse(File.Exists(path));
            }
            finally { Directory.Delete(path + ".tmp"); }

            store.RecordAndSaveResult(clear, null, catalog);
            Assert.AreEqual(1L, LocalPlayerDataStore.Load(path)
                .MemoryFragmentCount);
        }

        [Test]
        public void FullInventoryRejectsFirstClearWithoutRecordingIt()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.GrantItem(catalog, "currency.memory_fragment", 10L);
            string before = File.ReadAllText(path);
            var clear = new GameResultSnapshot("song", "hard", 750000d,
                1_000_000, RankGrade.B, 1, 0, 0, 0, 1, 1,
                true, false);

            Assert.Throws<InvalidOperationException>(() =>
                store.RecordAndSaveResult(clear, null, catalog));
            Assert.IsFalse(store.TryGetProgress("song", "hard", out _));
            Assert.AreEqual(10L, store.MemoryFragmentCount);
            Assert.AreEqual(before, File.ReadAllText(path));
        }

        [TestCase("")]
        [TestCase(",\"inventory\":null")]
        [TestCase(",\"inventory\":[{\"itemId\":\"currency.memory_fragment\"}]")]
        [TestCase(",\"inventory\":[{\"itemId\":\"currency.memory_fragment\",\"quantity\":-1}]")]
        [TestCase(",\"inventory\":[{\"itemId\":\"currency.memory_fragment\",\"quantity\":1.5}]")]
        [TestCase(",\"inventory\":[{\"itemId\":\"currency.memory_fragment\",\"quantity\":9223372036854775808}]")]
        [TestCase(",\"inventory\":[{\"itemId\":\"currency.memory_fragment\",\"quantity\":1},{\"itemId\":\"currency.memory_fragment\",\"quantity\":2}]")]
        public void VersionFiveRejectsMissingOrDamagedInventory(
            string inventoryField)
        {
            File.WriteAllText(path,
                "{\"version\":5,\"charts\":[],\"favoriteMusicIds\":[]," +
                "\"ownedCharacterIds\":[],\"songAccess\":[]" +
                inventoryField + "}");
            Assert.Throws<InvalidDataException>(() =>
                LocalPlayerDataStore.Load(path));
        }

        [Test]
        public void UnknownSavedIdSurvivesButCannotBeChanged()
        {
            File.WriteAllText(path,
                "{\"version\":5,\"charts\":[],\"favoriteMusicIds\":[]," +
                "\"ownedCharacterIds\":[],\"songAccess\":[]," +
                "\"inventory\":[{\"itemId\":\"retired.item\",\"quantity\":7}]}");
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            Assert.AreEqual(7L, store.GetItemQuantity("retired.item"));
            Assert.Throws<InvalidOperationException>(() =>
                store.GrantItem(catalog, "retired.item", 1L));
            Assert.Throws<InvalidOperationException>(() =>
                store.TryConsumeItem(catalog, "retired.item", 1L));
            store.GrantItem(catalog, "currency.memory_fragment", 2L);

            LocalPlayerDataStore reloaded = LocalPlayerDataStore.Load(path);
            Assert.AreEqual(7L, reloaded.GetItemQuantity("retired.item"));
            Assert.AreEqual(2L, reloaded.GetItemQuantity("currency.memory_fragment"));
        }

        [Test]
        public void LimitOverflowAndInsufficientBalanceLeaveSaveUnchanged()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.GrantItem(catalog, "currency.memory_fragment", 10L);
            string atLimit = File.ReadAllText(path);
            Assert.Throws<InvalidOperationException>(() =>
                store.GrantItem(catalog, "currency.memory_fragment", 1L));
            Assert.IsFalse(store.TryConsumeItem(catalog, "currency.memory_fragment", 11L));
            Assert.AreEqual(atLimit, File.ReadAllText(path));
            Assert.AreEqual(10L, store.GetItemQuantity("currency.memory_fragment"));

            SetField(item, "maxQuantity", long.MaxValue);
            store.GrantItem(catalog, "currency.memory_fragment", long.MaxValue - 10L);
            string atIntegerLimit = File.ReadAllText(path);
            Assert.Throws<OverflowException>(() =>
                store.GrantItem(catalog, "currency.memory_fragment", 1L));
            Assert.AreEqual(atIntegerLimit, File.ReadAllText(path));
            Assert.AreEqual(long.MaxValue, store.GetItemQuantity("currency.memory_fragment"));
            Assert.AreEqual(long.MaxValue, LocalPlayerDataStore.Load(path)
                .GetItemQuantity("currency.memory_fragment"));
        }

        [Test]
        public void ZeroOrNegativeChangesAreRejected()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                store.GrantItem(catalog, "currency.memory_fragment", 0L));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                store.TryConsumeItem(catalog, "currency.memory_fragment", -1L));
            Assert.IsFalse(File.Exists(path));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FailedWritePreservesMemoryAndDisk(bool grant)
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.GrantItem(catalog, "currency.memory_fragment", 5L);
            string before = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");
            Assert.Throws<UnauthorizedAccessException>(() =>
            {
                if (grant) store.GrantItem(catalog, "currency.memory_fragment", 1L);
                else store.TryConsumeItem(catalog, "currency.memory_fragment", 1L);
            });
            Assert.AreEqual(5L, store.GetItemQuantity("currency.memory_fragment"));
            Assert.AreEqual(before, File.ReadAllText(path));
        }

        [Test]
        public void CorruptPrimaryRecoversSavedInventoryFromBackup()
        {
            LocalPlayerDataStore store = LocalPlayerDataStore.Load(path);
            store.GrantItem(catalog, "currency.memory_fragment", 1L);
            store.GrantItem(catalog, "currency.memory_fragment", 1L);
            File.WriteAllText(path, "{broken json");

            LocalPlayerDataStore recovered = LocalPlayerDataStore.Load(path);
            Assert.AreEqual(1L, recovered.MemoryFragmentCount);
            recovered.GrantItem(catalog, "currency.memory_fragment", 1L);
            Assert.AreEqual(2L, LocalPlayerDataStore.Load(path)
                .MemoryFragmentCount);
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, PrivateInstance)
                .SetValue(target, value);
    }
}

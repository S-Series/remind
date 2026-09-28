using System;
using System.IO;
using NUnit.Framework;

namespace REmind.Gameplay.Tests
{
    public sealed class LocalGameSettingsStoreTests
    {
        private string path;

        [SetUp]
        public void SetUp() => path = Path.Combine(Path.GetTempPath(),
            "remind-settings-test-" + Guid.NewGuid().ToString("N") + ".json");

        [TearDown]
        public void TearDown()
        {
            foreach (string candidate in new[] { path, path + ".bak",
                         path + ".tmp" })
                if (File.Exists(candidate)) File.Delete(candidate);
        }

        [Test]
        public void SaveReload_PreservesVolumeOffsetAndBindings()
        {
            LocalGameSettingsStore settings = LocalGameSettingsStore.Load(path);
            Assert.AreEqual(1f, settings.MusicVolume);
            Assert.AreEqual(0d, settings.JudgementOffsetMs);
            Assert.AreEqual("<Keyboard>/z", settings.GetLaneBinding(0));
            settings.SetMusicVolume(0.35f);
            settings.SetJudgementOffsetMs(-45d);
            settings.SetLaneBinding(0, "<Keyboard>/a");
            settings.Save();

            LocalGameSettingsStore reloaded = LocalGameSettingsStore.Load(path);
            Assert.AreEqual(0.35f, reloaded.MusicVolume);
            Assert.AreEqual(-45d, reloaded.JudgementOffsetMs);
            Assert.AreEqual("<Keyboard>/a", reloaded.GetLaneBinding(0));
            Assert.AreEqual("<Keyboard>/rightShift",
                reloaded.GetLaneBinding(9));
        }

        [Test]
        public void InvalidValuesAndDuplicateKeysAreRejected()
        {
            LocalGameSettingsStore settings = LocalGameSettingsStore.Load(path);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                settings.SetMusicVolume(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                settings.SetMusicVolume(1.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                settings.SetJudgementOffsetMs(201d));
            Assert.Throws<ArgumentException>(() =>
                settings.SetLaneBinding(0, "<Keyboard>/escape"));
            Assert.Throws<ArgumentException>(() =>
                settings.SetLaneBinding(0, "<Keyboard>/x"));
            Assert.AreEqual("<Keyboard>/z", settings.GetLaneBinding(0));
        }

        [Test]
        public void CorruptCurrentFile_RecoversLastSavedBackup()
        {
            LocalGameSettingsStore settings = LocalGameSettingsStore.Load(path);
            settings.SetMusicVolume(0.4f);
            settings.Save();
            settings.SetMusicVolume(0.8f);
            settings.Save();
            File.WriteAllText(path, "{not json");

            LocalGameSettingsStore recovered = LocalGameSettingsStore.Load(path);
            Assert.AreEqual(0.4f, recovered.MusicVolume);
        }
    }
}

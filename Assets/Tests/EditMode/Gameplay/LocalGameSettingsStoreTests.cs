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
            settings.SetMasterVolume(0.8f);
            settings.SetSfxVolume(0.6f);
            settings.SetVoiceVolume(0.7f);
            settings.SetHitSoundStyle(
                LocalGameSettingsStore.HitSoundStyle.Sharp);
            settings.SetSpatialAudio(true);
            settings.SetMuteWhenUnfocused(true);
            settings.SetReduceBgmDuringVoice(false);
            settings.SetKeepAudioInBackground(false);
            settings.SetJudgementOffsetMs(-45d);
            settings.SetLaneBinding(0, "<Keyboard>/a");
            settings.Save();

            LocalGameSettingsStore reloaded = LocalGameSettingsStore.Load(path);
            Assert.AreEqual(0.35f, reloaded.MusicVolume);
            Assert.AreEqual(0.8f, reloaded.MasterVolume);
            Assert.AreEqual(0.6f, reloaded.SfxVolume);
            Assert.AreEqual(0.7f, reloaded.VoiceVolume);
            Assert.AreEqual(LocalGameSettingsStore.HitSoundStyle.Sharp,
                reloaded.HitSound);
            Assert.IsTrue(reloaded.SpatialAudio);
            Assert.IsTrue(reloaded.MuteWhenUnfocused);
            Assert.IsFalse(reloaded.ReduceBgmDuringVoice);
            Assert.IsFalse(reloaded.KeepAudioInBackground);
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
                settings.SetMasterVolume(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                settings.SetSfxVolume(-0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                settings.SetVoiceVolume(1.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                settings.SetHitSoundStyle(
                    (LocalGameSettingsStore.HitSoundStyle)99));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                settings.SetJudgementOffsetMs(201d));
            Assert.Throws<ArgumentException>(() =>
                settings.SetLaneBinding(0, "<Keyboard>/escape"));
            Assert.Throws<ArgumentException>(() =>
                settings.SetLaneBinding(0, "<Keyboard>/x"));
            Assert.AreEqual("<Keyboard>/z", settings.GetLaneBinding(0));
        }

        [Test]
        public void LegacyVersionOne_MigratesAudioDefaultsWithoutLosingBindings()
        {
            File.WriteAllText(path,
                "{\"version\":1,\"musicVolume\":0.45," +
                "\"judgementOffsetMs\":-10," +
                "\"laneBindings\":[\"<Keyboard>/a\",\"<Keyboard>/x\"," +
                "\"<Keyboard>/c\",\"<Keyboard>/v\",\"<Keyboard>/m\"," +
                "\"<Keyboard>/comma\",\"<Keyboard>/period\"," +
                "\"<Keyboard>/slash\",\"<Keyboard>/leftShift\"," +
                "\"<Keyboard>/rightShift\"]}");
            LocalGameSettingsStore migrated = LocalGameSettingsStore.Load(path);
            Assert.AreEqual(0.45f, migrated.MusicVolume);
            Assert.AreEqual(1f, migrated.MasterVolume);
            Assert.AreEqual(1f, migrated.SfxVolume);
            Assert.AreEqual(1f, migrated.VoiceVolume);
            Assert.AreEqual("<Keyboard>/a", migrated.GetLaneBinding(0));
            migrated.Save();
            StringAssert.Contains("\"version\": 2", File.ReadAllText(path));
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

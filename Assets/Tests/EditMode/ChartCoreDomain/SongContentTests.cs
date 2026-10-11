using System;
using System.Globalization;
using NUnit.Framework;

namespace REmind.Charting.Tests
{
    public sealed class SongContentTests
    {
        private const string Valid =
            "{\"format\":\"REmindSong\",\"formatVersion\":1," +
            "\"musicId\":\"i\",\"title\":\"I\",\"artist\":\"Unknown\"," +
            "\"previewStartMs\":0,\"previewDurationMs\":15000}";

        [Test]
        public void Parse_KeepsCommonSongMetadata()
        {
            SongContent song = SongContentCodec.Parse(Valid);
            Assert.That(song.MusicId, Is.EqualTo("i"));
            Assert.That(song.Title, Is.EqualTo("I"));
            Assert.That(song.MusicVolumeMultiplier, Is.EqualTo(1d));
            Assert.That(song.PlaybackVolume, Is.EqualTo(0.5f));
            Assert.That(song.AudioFile, Is.EqualTo("audio.mp3"));
            Assert.That(song.JacketFile, Is.EqualTo("art.jpg"));
            Assert.That(song.HiddenType, Is.EqualTo(SongHiddenType.NotHidden));
            Assert.That(song.UnlockHint, Is.Empty);
        }

        [Test]
        public void Parse_KeepsOptionalUnlockHint()
        {
            SongContent song = SongContentCodec.Parse(Valid.Replace("}",
                ",\"unlockHint\":\"Look for the clue.\"}"));
            Assert.That(song.UnlockHint, Is.EqualTo("Look for the clue."));
        }

        [TestCase("NotHidden", SongHiddenType.NotHidden)]
        [TestCase("Masked", SongHiddenType.Masked)]
        [TestCase("Puzzle", SongHiddenType.Puzzle)]
        [TestCase("Invisible", SongHiddenType.Invisible)]
        public void Parse_MapsHiddenTypeNames(string name, SongHiddenType expected)
        {
            string json = Valid.Replace("}", ",\"hiddenType\":\"" + name + "\"}");
            Assert.That(SongContentCodec.Parse(json).HiddenType, Is.EqualTo(expected));
        }

        [Test]
        public void Parse_DefaultsNullHiddenTypeToNotHidden()
        {
            string json = Valid.Replace("}", ",\"hiddenType\":null}");
            Assert.That(SongContentCodec.Parse(json).HiddenType,
                Is.EqualTo(SongHiddenType.NotHidden));
        }

        [TestCase("\"\"")]
        [TestCase("\"masked\"")]
        [TestCase("\"Unknown\"")]
        [TestCase("\"1\"")]
        [TestCase("1")]
        public void Parse_RejectsUnsupportedHiddenType(string jsonValue)
        {
            string json = Valid.Replace("}", ",\"hiddenType\":" + jsonValue + "}");
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(json));
        }

        [TestCase(0d, 0f)]
        [TestCase(1d, 0.5f)]
        [TestCase(1.5d, 0.75f)]
        [TestCase(2d, 1f)]
        public void Parse_MapsMusicMultiplierToAudioSourceVolume(
            double multiplier, float expectedVolume)
        {
            string json = Valid.Replace("\"artist\":\"Unknown\"",
                "\"artist\":\"Unknown\",\"musicVolumeMultiplier\":" +
                multiplier.ToString(CultureInfo.InvariantCulture));
            Assert.That(SongContentCodec.Parse(json).PlaybackVolume,
                Is.EqualTo(expectedVolume));
        }

        [TestCase(-0.01d)]
        [TestCase(2.01d)]
        public void Parse_RejectsOutOfRangeMusicMultiplier(double multiplier)
        {
            string json = Valid.Replace("\"artist\":\"Unknown\"",
                "\"artist\":\"Unknown\",\"musicVolumeMultiplier\":" +
                multiplier.ToString(CultureInfo.InvariantCulture));
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(json));
        }

        [Test]
        public void Parse_RejectsLegacyChartListAndEscapingPaths()
        {
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(
                Valid.Replace("}", ",\"charts\":[]}")));
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(
                Valid.Replace("\"artist\":\"Unknown\"",
                    "\"artist\":\"Unknown\",\"audioFile\":\"audio/audio.mp3\"")));
        }

        [Test]
        public void Parse_AllowsFlatCommonFilenameOverrides()
        {
            string custom = Valid.Replace("\"artist\":\"Unknown\"",
                "\"artist\":\"Unknown\",\"audioFile\":\"song.ogg\"," +
                "\"jacketFile\":\"cover.png\"");
            SongContent song = SongContentCodec.Parse(custom);
            Assert.That(song.AudioFile, Is.EqualTo("song.ogg"));
            Assert.That(song.JacketFile, Is.EqualTo("cover.png"));
        }

        [Test]
        public void FixedChartFileNames_DetermineIdentity()
        {
            string[] names = { "Easy.rd", "Normal.rd", "Hard.rd", "Chaos.rd", "Extra.rd" };
            for (int index = 0; index < names.Length; index++)
            {
                string id = SongChartFiles.GetDifficultyId(index);
                Assert.That(SongChartFiles.GetFileName(id), Is.EqualTo(names[index]));
                Assert.That(SongChartFiles.GetDifficultyIdFromFileName(names[index]), Is.EqualTo(id));
                SongChartFiles.Validate(names[index], 0, "Unknown",
                    id == "extra" ? "DREAM" : null);
            }
            Assert.Throws<FormatException>(() => SongChartFiles.Validate(
                "Expert.rd", 10, "A", null));
            Assert.Throws<FormatException>(() => SongChartFiles.Validate(
                "Extra.rd", 10, "A", null));
            Assert.Throws<FormatException>(() => SongChartFiles.Validate(
                "Hard.rd", -1, "A", null));
            Assert.Throws<FormatException>(() => SongChartFiles.Validate(
                "Hard.rd", 10, "", null));
            Assert.Throws<FormatException>(() => SongChartFiles.Validate(
                "Hard.rd", 10, "A", "DREAM"));
        }

        [Test]
        public void Parse_RejectsOldSongIdWithoutCurrentMusicId()
        {
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(
                Valid.Replace("\"musicId\"", "\"songId\"")));
        }
    }
}

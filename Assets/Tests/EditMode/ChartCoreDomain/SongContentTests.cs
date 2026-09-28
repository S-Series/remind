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
            "\"previewStartMs\":0,\"previewDurationMs\":15000," +
            "\"charts\":[{\"difficultyId\":\"hard\",\"level\":0," +
            "\"chartAuthor\":\"Unknown\"}]}";

        [Test]
        public void Parse_KeepsSongAndDifficultyOwnership()
        {
            SongContent song = SongContentCodec.Parse(Valid);
            Assert.That(song.MusicId, Is.EqualTo("i"));
            Assert.That(song.Title, Is.EqualTo("I"));
            Assert.That(song.MusicVolumeMultiplier, Is.EqualTo(1d));
            Assert.That(song.PlaybackVolume, Is.EqualTo(0.5f));
            Assert.That(song.AudioFile, Is.EqualTo("audio.mp3"));
            Assert.That(song.JacketFile, Is.EqualTo("art.jpg"));
            Assert.That(song.FindChart("hard").ChartFile,
                Is.EqualTo("hard.rd"));
            Assert.That(song.FindChart("normal"), Is.Null);
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
        public void Parse_RejectsDuplicateDifficultyAndEscapingPaths()
        {
            string duplicate = Valid.Replace("]}", "," +
                "{\"difficultyId\":\"hard\",\"chartAuthor\":\"Other\"," +
                "\"chartFile\":\"other.rd\"}]}");
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(duplicate));
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(
                Valid.Replace("\"chartAuthor\":\"Unknown\"",
                    "\"chartAuthor\":\"Unknown\",\"chartFile\":\"../outside.rd\"")));
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(
                Valid.Replace("\"artist\":\"Unknown\"",
                    "\"artist\":\"Unknown\",\"audioFile\":\"audio/audio.mp3\"")));
        }

        [Test]
        public void Parse_AllowsFlatFilenameOverrides()
        {
            string custom = Valid.Replace("\"artist\":\"Unknown\"",
                "\"artist\":\"Unknown\",\"audioFile\":\"song.ogg\"," +
                "\"jacketFile\":\"cover.png\"").Replace(
                "\"chartAuthor\":\"Unknown\"",
                "\"chartAuthor\":\"Unknown\",\"chartFile\":\"expert.rd\"");
            SongContent song = SongContentCodec.Parse(custom);
            Assert.That(song.AudioFile, Is.EqualTo("song.ogg"));
            Assert.That(song.JacketFile, Is.EqualTo("cover.png"));
            Assert.That(song.FindChart("hard").ChartFile, Is.EqualTo("expert.rd"));
        }

        [Test]
        public void Parse_RejectsOldSongIdWithoutCurrentMusicId()
        {
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(
                Valid.Replace("\"musicId\"", "\"songId\"")));
        }
    }
}

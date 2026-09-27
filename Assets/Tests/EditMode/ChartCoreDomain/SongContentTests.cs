using System;
using NUnit.Framework;

namespace REmind.Charting.Tests
{
    public sealed class SongContentTests
    {
        private const string Valid =
            "{\"format\":\"REmindSong\",\"formatVersion\":2," +
            "\"musicId\":\"i\",\"title\":\"I\",\"artist\":\"Unknown\"," +
            "\"audioFile\":\"audio/music.mp3\",\"jacketFile\":\"jackets/hard.jpg\"," +
            "\"previewStartMs\":0,\"previewDurationMs\":15000," +
            "\"charts\":[{\"difficultyId\":\"hard\",\"level\":0," +
            "\"chartAuthor\":\"Unknown\",\"chartFile\":\"charts/hard.rd\"}]}";

        [Test]
        public void Parse_KeepsSongAndDifficultyOwnership()
        {
            SongContent song = SongContentCodec.Parse(Valid);
            Assert.That(song.MusicId, Is.EqualTo("i"));
            Assert.That(song.Title, Is.EqualTo("I"));
            Assert.That(song.FindChart("hard").ChartFile,
                Is.EqualTo("charts/hard.rd"));
            Assert.That(song.FindChart("normal"), Is.Null);
        }

        [Test]
        public void Parse_RejectsDuplicateDifficultyAndEscapingPaths()
        {
            string duplicate = Valid.Replace("]}", "," +
                "{\"difficultyId\":\"hard\",\"chartAuthor\":\"Other\"," +
                "\"chartFile\":\"charts/other.rd\"}]}");
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(duplicate));
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(
                Valid.Replace("charts/hard.rd", "../outside.rd")));
        }

        [Test]
        public void Parse_RejectsOldSongIdWithoutCurrentMusicId()
        {
            Assert.Throws<FormatException>(() => SongContentCodec.Parse(
                Valid.Replace("\"musicId\"", "\"songId\"")));
        }
    }
}

using NUnit.Framework;
using REmind.Charting;

namespace REmind.Gameplay.Tests
{
    public sealed class SongAccessPolicyTests
    {
        [TestCase("NotHidden", true, false, true, false)]
        [TestCase("Masked", true, true, false, false)]
        [TestCase("Puzzle", true, true, false, true)]
        [TestCase("Invisible", false, true, false, false)]
        public void MissingProgressUsesHiddenPresentation(string hiddenType,
            bool visible, bool masked, bool playable, bool puzzle)
        {
            var access = new SongAccessPolicy(Song(hiddenType), false, false);
            Assert.AreEqual(visible, access.IsVisible);
            Assert.AreEqual(masked, access.IsMasked);
            Assert.AreEqual(playable, access.CanPlay);
            Assert.AreEqual(puzzle, access.ShowPuzzleHint);
        }

        [TestCase("Masked")]
        [TestCase("Puzzle")]
        [TestCase("Invisible")]
        public void OwnershipAloneDoesNotRevealOrUnlockHiddenSongs(string hiddenType)
        {
            var access = new SongAccessPolicy(Song(hiddenType, true), true, false);
            Assert.IsTrue(access.NeedsUnlock);
            Assert.IsTrue(access.IsMasked);
            Assert.IsFalse(access.CanPlay);
        }

        [TestCase("NotHidden")]
        [TestCase("Masked")]
        [TestCase("Puzzle")]
        [TestCase("Invisible")]
        public void UnlockRevealsSongButPurchaseRequirementStillApplies(string hiddenType)
        {
            SongContent song = Song(hiddenType, true);
            var unowned = new SongAccessPolicy(song, false, true);
            Assert.IsTrue(unowned.IsVisible);
            Assert.IsFalse(unowned.IsMasked);
            Assert.IsFalse(unowned.ShowPuzzleHint);
            Assert.IsFalse(unowned.CanPlay);
            Assert.IsTrue(unowned.NeedsOwnership);
            Assert.IsTrue(new SongAccessPolicy(song, true, true).CanPlay);
        }

        [TestCase("Masked")]
        [TestCase("Puzzle")]
        [TestCase("Invisible")]
        public void FreeHiddenSongOnlyNeedsUnlock(string hiddenType)
        {
            var access = new SongAccessPolicy(Song(hiddenType), false, true);
            Assert.IsTrue(access.CanPlay);
            Assert.IsTrue(access.IsVisible);
            Assert.IsFalse(access.IsMasked);
        }

        [Test]
        public void PartialUnlockRevealsSongButBlocksHigherDifficulty()
        {
            SongContent song = Song("Masked");
            var lockedDifficulty = new SongAccessPolicy(song, false, true, false);
            Assert.IsTrue(lockedDifficulty.IsVisible);
            Assert.IsFalse(lockedDifficulty.IsMasked);
            Assert.IsTrue(lockedDifficulty.NeedsUnlock);
            Assert.IsFalse(lockedDifficulty.CanPlay);
            Assert.IsTrue(new SongAccessPolicy(song, false, true, true).CanPlay);
        }

        private static SongContent Song(string hiddenType, bool requiresPurchase = false) =>
            SongContentCodec.Parse("{\"format\":\"REmindSong\",\"formatVersion\":1," +
                "\"musicId\":\"song\",\"title\":\"Song\",\"artist\":\"Artist\"," +
                "\"previewStartMs\":0,\"previewDurationMs\":1000," +
                "\"hiddenType\":\"" + hiddenType + "\",\"requiresPurchase\":" +
                (requiresPurchase ? "true" : "false") + "}");
    }
}

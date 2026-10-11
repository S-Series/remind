using System;
using REmind.Charting;

namespace REmind.Gameplay
{
    /// <summary>Game access rules; ownership and hidden-song disclosure are independent.</summary>
    public readonly struct SongAccessPolicy
    {
        public bool IsVisible { get; }
        public bool IsMasked { get; }
        public bool NeedsUnlock { get; }
        public bool NeedsOwnership { get; }
        public bool ShowPuzzleHint { get; }
        public bool CanPlay => !NeedsUnlock && !NeedsOwnership;

        public SongAccessPolicy(SongContent song, bool isOwned, bool isUnlocked)
            : this(song, isOwned, isUnlocked, isUnlocked)
        {
        }

        public SongAccessPolicy(SongContent song, bool isOwned,
            bool hasAnyUnlockedDifficulty, bool isSelectedDifficultyUnlocked)
        {
            if (song == null) throw new ArgumentNullException(nameof(song));
            NeedsUnlock = song.HiddenType != SongHiddenType.NotHidden &&
                !isSelectedDifficultyUnlocked;
            NeedsOwnership = song.RequiresPurchase && !isOwned;
            IsMasked = song.HiddenType != SongHiddenType.NotHidden &&
                !hasAnyUnlockedDifficulty;
            IsVisible = !IsMasked || song.HiddenType != SongHiddenType.Invisible;
            ShowPuzzleHint = NeedsUnlock && song.HiddenType == SongHiddenType.Puzzle;
        }
    }
}

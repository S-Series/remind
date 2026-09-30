using System;
using REmind.Gameplay.Characters;

namespace REmind.Gameplay
{
    /// <summary>Values captured for one start, independent of later selections or assets.</summary>
    public sealed class GameAttemptStartSnapshot
    {
        public Guid AttemptId { get; }
        public string MusicId { get; }
        public string DifficultyId { get; }
        /// <summary>Auto Play mode selected when this attempt started.</summary>
        public bool IsAutoPlay { get; }
        public CharacterGaugeRuleSnapshot GaugeRule { get; }
        public CharacterAbilityStartSnapshot CharacterAbility { get; }

        public GameAttemptStartSnapshot(Guid attemptId, string musicId,
            string difficultyId, bool isAutoPlay,
            CharacterGaugeRuleSnapshot gaugeRule,
            CharacterAbilityStartSnapshot characterAbility)
        {
            if (attemptId == Guid.Empty)
                throw new ArgumentException("An attempt needs an ID.", nameof(attemptId));
            if (string.IsNullOrWhiteSpace(musicId))
                throw new ArgumentException("An attempt needs a music ID.", nameof(musicId));
            if (string.IsNullOrWhiteSpace(difficultyId))
                throw new ArgumentException("An attempt needs a difficulty ID.", nameof(difficultyId));
            if (characterAbility != null &&
                !characterAbility.GaugeRule.Equals(gaugeRule))
                throw new ArgumentException(
                    "Character and attempt gauge rules do not match.",
                    nameof(characterAbility));

            AttemptId = attemptId;
            MusicId = musicId;
            DifficultyId = difficultyId;
            IsAutoPlay = isAutoPlay;
            GaugeRule = gaugeRule;
            CharacterAbility = characterAbility;
        }
    }

    /// <summary>Immutable end-of-play data passed from Game to Result.</summary>
    public sealed class GameResultSnapshot
    {
        public string MusicId { get; }
        public string DifficultyId { get; }
        public double Score { get; }
        public int MaxScore { get; }
        public RankGrade Rank { get; }
        public int Perfect { get; }
        public int Great { get; }
        public int Good { get; }
        public int Miss { get; }
        public int MaxCombo { get; }
        public int TotalNoteCount { get; }
        public bool IsCleared { get; }
        public bool IsFailed { get; }
        /// <summary>True if Auto Play was enabled or used at any time in this attempt.</summary>
        public bool IsAutoPlay { get; }
        public GameAttemptStartSnapshot AttemptStart { get; }

        public double ScoreRatio => MaxScore > 0
            ? Math.Max(0d, Math.Min(1d, Score / MaxScore)) : 0d;
        public bool IsAllPerfect => TotalNoteCount > 0 &&
            Perfect == TotalNoteCount && Great == 0 && Good == 0 && Miss == 0;

        public GameResultSnapshot(string musicId, string difficultyId,
            double score, int maxScore, RankGrade rank,
            int perfect, int great, int good, int miss,
            int maxCombo, int totalNoteCount, bool isCleared, bool isFailed,
            bool isAutoPlay = false,
            GameAttemptStartSnapshot attemptStart = null)
        {
            if (string.IsNullOrWhiteSpace(musicId))
                throw new ArgumentException("Result needs a music ID.", nameof(musicId));
            if (string.IsNullOrWhiteSpace(difficultyId))
                throw new ArgumentException("Result needs a difficulty ID.", nameof(difficultyId));
            if (double.IsNaN(score) || double.IsInfinity(score) || score < 0d ||
                maxScore < 0 || perfect < 0 || great < 0 || good < 0 || miss < 0 ||
                maxCombo < 0 || totalNoteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(score), "Result values cannot be negative or non-finite.");
            if (attemptStart != null &&
                (!string.Equals(musicId, attemptStart.MusicId, StringComparison.Ordinal) ||
                 !string.Equals(difficultyId, attemptStart.DifficultyId, StringComparison.Ordinal) ||
                 (attemptStart.IsAutoPlay && !isAutoPlay)))
                throw new ArgumentException(
                    "Result IDs must match the captured attempt and Auto Play cannot be unmarked.",
                    nameof(attemptStart));

            MusicId = musicId;
            DifficultyId = difficultyId;
            Score = score;
            MaxScore = maxScore;
            Rank = rank;
            Perfect = perfect;
            Great = great;
            Good = good;
            Miss = miss;
            MaxCombo = maxCombo;
            TotalNoteCount = totalNoteCount;
            IsCleared = isCleared;
            IsFailed = isFailed;
            IsAutoPlay = isAutoPlay;
            AttemptStart = attemptStart;
        }
    }
}

using System;
using REmind.Charting;
using UnityEngine;

namespace REmind.Gameplay
{
    /// <summary>A Game-owned clear challenge that temporarily replaces the equipped gauge.</summary>
    [CreateAssetMenu(fileName = "SongUnlockChallenge",
        menuName = "REmind/Gameplay/Song Unlock Challenge")]
    public sealed class SongUnlockChallengeDefinition : ScriptableObject
    {
        [SerializeField] private string sourceMusicId;
        [SerializeField] private string targetMusicId;
        [SerializeField] private string difficultyId;
        [SerializeField] private GameRuleConfig specialHardRule;

        public string TargetMusicId => targetMusicId;
        public GameRuleConfig SpecialHardRule => specialHardRule;

        public void ValidateForUse()
        {
            if (string.IsNullOrWhiteSpace(sourceMusicId) ||
                string.IsNullOrWhiteSpace(targetMusicId) ||
                string.Equals(sourceMusicId, targetMusicId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "A song unlock challenge needs distinct source and target music IDs.");
            if (!specialHardRule || specialHardRule.GaugeType != HealthGaugeType.Hard ||
                specialHardRule.SpecialHardGauge == null ||
                specialHardRule.MaxHealth != 100 ||
                specialHardRule.InitialHealth != 100)
                throw new InvalidOperationException(
                    "A song unlock challenge needs the Special Hard gauge with 100 starting health.");
            specialHardRule.ValidateForSession();
        }

        public bool AppliesTo(string musicId, string selectedDifficultyId,
            bool targetUnlocked)
        {
            return !targetUnlocked &&
                string.Equals(sourceMusicId, musicId, StringComparison.Ordinal) &&
                (string.IsNullOrEmpty(difficultyId) ||
                 string.Equals(difficultyId, selectedDifficultyId,
                     StringComparison.Ordinal));
        }
    }

    internal static class SongUnlockDifficultyOrder
    {
        public const int Count = SongChartFiles.Count;

        public static int IndexOf(string difficultyId)
        {
            for (int index = 0; index < Count; index++)
                if (string.Equals(SongChartFiles.GetDifficultyId(index),
                    difficultyId, StringComparison.Ordinal))
                    return index;
            return -1;
        }
    }
}

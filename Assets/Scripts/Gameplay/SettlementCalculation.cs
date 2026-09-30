using System;
using REmind.Gameplay.Characters;

namespace REmind.Gameplay
{
    /// <summary>
    /// Unrounded quantities calculated for one play. Eligibility, awarding,
    /// world limits, and persistence are separate decisions.
    /// </summary>
    public readonly struct SettlementCalculation
    {
        public decimal WorldProgress { get; }
        public decimal Reward { get; }

        internal SettlementCalculation(decimal worldProgress, decimal reward)
        {
            WorldProgress = worldProgress;
            Reward = reward;
        }
    }

    public static class SettlementCalculator
    {
        public static SettlementCalculation Calculate(
            decimal baseWorldProgress, decimal baseReward,
            CharacterAbilityStartSnapshot startSnapshot)
        {
            if (startSnapshot == null)
                throw new ArgumentNullException(nameof(startSnapshot));
            if (baseWorldProgress < 0m)
                throw new ArgumentOutOfRangeException(nameof(baseWorldProgress));
            if (baseReward < 0m)
                throw new ArgumentOutOfRangeException(nameof(baseReward));

            CharacterAbilityStats stats = startSnapshot.Stats;
            return new SettlementCalculation(
                baseWorldProgress * stats.WorldProgressMultiplier,
                baseReward * stats.RewardMultiplier);
        }
    }
}

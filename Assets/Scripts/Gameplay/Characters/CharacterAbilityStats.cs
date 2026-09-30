using System;

namespace REmind.Gameplay.Characters
{
    /// <summary>
    /// Values for the two connected character stats. Reserved types intentionally
    /// have no value or conversion until their product meaning is defined.
    /// </summary>
    public readonly struct CharacterAbilityStats
    {
        public int WorldProgressBonusPercent { get; }
        public int RewardBonusPercent { get; }

        public decimal WorldProgressMultiplier =>
            1m + WorldProgressBonusPercent / 100m;
        public decimal RewardMultiplier =>
            1m + RewardBonusPercent / 100m;

        public CharacterAbilityStats(int worldProgressBonusPercent,
            int rewardBonusPercent)
        {
            if (worldProgressBonusPercent < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(worldProgressBonusPercent));
            if (rewardBonusPercent < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(rewardBonusPercent));

            WorldProgressBonusPercent = worldProgressBonusPercent;
            RewardBonusPercent = rewardBonusPercent;
        }

        public bool TryGetBonusPercent(CharacterStatType type,
            out int bonusPercent)
        {
            switch (type)
            {
                case CharacterStatType.WorldProgress:
                    bonusPercent = WorldProgressBonusPercent;
                    return true;
                case CharacterStatType.Reward:
                    bonusPercent = RewardBonusPercent;
                    return true;
                default:
                    bonusPercent = 0;
                    return false;
            }
        }
    }
}

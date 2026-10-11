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
        public decimal WorldUnlock { get; }
        public decimal RegularCurrency { get; }

        internal SettlementCalculation(decimal worldUnlock,
            decimal regularCurrency)
        {
            WorldUnlock = worldUnlock;
            RegularCurrency = regularCurrency;
        }
    }

    public static class SettlementCalculator
    {
        public static SettlementCalculation Calculate(
            decimal baseWorldUnlock, decimal baseRegularCurrency,
            CharacterAbilityStartSnapshot startSnapshot)
        {
            if (startSnapshot == null)
                throw new ArgumentNullException(nameof(startSnapshot));
            if (baseWorldUnlock < 0m)
                throw new ArgumentOutOfRangeException(nameof(baseWorldUnlock));
            if (baseRegularCurrency < 0m)
                throw new ArgumentOutOfRangeException(
                    nameof(baseRegularCurrency));

            return new SettlementCalculation(
                baseWorldUnlock * startSnapshot.WorldUnlockMultiplier,
                baseRegularCurrency *
                    startSnapshot.RegularCurrencyMultiplier);
        }
    }
}

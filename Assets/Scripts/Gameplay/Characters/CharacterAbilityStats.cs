using System;

namespace REmind.Gameplay.Characters
{
    /// <summary>
    /// Raw character stats and their shared 100% basis. The fourth stat has no
    /// authored value or runtime effect until its product meaning is decided.
    /// </summary>
    public readonly struct CharacterAbilityStats
    {
        public int WorldUnlock { get; }
        public int RegularCurrency { get; }
        public int HiddenUnlock { get; }
        public int BasisForOneHundredPercent { get; }

        public decimal WorldUnlockMultiplier => Multiplier(WorldUnlock);
        public decimal RegularCurrencyMultiplier => Multiplier(RegularCurrency);
        public decimal HiddenUnlockMultiplier => Multiplier(HiddenUnlock);

        public CharacterAbilityStats(int worldUnlock, int regularCurrency,
            int hiddenUnlock, int basisForOneHundredPercent)
        {
            if (worldUnlock < 0)
                throw new ArgumentOutOfRangeException(nameof(worldUnlock));
            if (regularCurrency < 0)
                throw new ArgumentOutOfRangeException(nameof(regularCurrency));
            if (hiddenUnlock < 0)
                throw new ArgumentOutOfRangeException(nameof(hiddenUnlock));
            if (basisForOneHundredPercent <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(basisForOneHundredPercent));

            WorldUnlock = worldUnlock;
            RegularCurrency = regularCurrency;
            HiddenUnlock = hiddenUnlock;
            BasisForOneHundredPercent = basisForOneHundredPercent;
        }

        public bool TryGetRawValue(CharacterStatType type, out int value)
        {
            switch (type)
            {
                case CharacterStatType.WorldUnlock:
                    value = WorldUnlock;
                    return true;
                case CharacterStatType.RegularCurrency:
                    value = RegularCurrency;
                    return true;
                case CharacterStatType.HiddenUnlock:
                    value = HiddenUnlock;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }

        public bool TryGetMultiplier(CharacterStatType type,
            out decimal multiplier)
        {
            if (TryGetRawValue(type, out int value))
            {
                multiplier = Multiplier(value);
                return true;
            }
            multiplier = 0m;
            return false;
        }

        private decimal Multiplier(int value) =>
            BasisForOneHundredPercent > 0
                ? value / (decimal)BasisForOneHundredPercent : 0m;
    }
}

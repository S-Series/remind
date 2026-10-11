using System;

namespace REmind.Gameplay.Characters
{
    /// <summary>
    /// Game-owned character values. The caller passes its active GameRule.Config.
    /// The complete gauge values are copied into the play-start snapshot.
    /// </summary>
    public sealed class CharacterAbilityDefinition
    {
        public string CharacterId { get; }
        public int DefinitionVersion { get; }
        public string DisplayName { get; }
        public HealthGaugeType RequiredGaugeType { get; }
        public CharacterAbilityStats Stats { get; }

        public CharacterAbilityDefinition(string characterId,
            HealthGaugeType requiredGaugeType, CharacterAbilityStats stats)
            : this(characterId, 1, characterId, requiredGaugeType, stats)
        {
        }

        public CharacterAbilityDefinition(string characterId,
            int definitionVersion, string displayName,
            HealthGaugeType requiredGaugeType, CharacterAbilityStats stats)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                throw new ArgumentException(
                    "A character needs a stable ID.", nameof(characterId));
            if (definitionVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionVersion));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException(
                    "A character needs a display name.", nameof(displayName));
            if (!Enum.IsDefined(typeof(HealthGaugeType), requiredGaugeType))
                throw new ArgumentOutOfRangeException(nameof(requiredGaugeType));
            if (stats.BasisForOneHundredPercent <= 0)
                throw new ArgumentException(
                    "Character stats need a positive multiplier basis.",
                    nameof(stats));

            CharacterId = characterId.Trim();
            DefinitionVersion = definitionVersion;
            DisplayName = displayName.Trim();
            RequiredGaugeType = requiredGaugeType;
            Stats = stats;
        }

        public CharacterAbilityStartSnapshot FreezeForStart(
            GameRuleConfig selectedGaugeRule)
        {
            if (selectedGaugeRule == null)
                throw new ArgumentNullException(nameof(selectedGaugeRule));
            if (selectedGaugeRule.GaugeType != RequiredGaugeType)
                throw new InvalidOperationException(
                    $"Character '{CharacterId}' requires {RequiredGaugeType} " +
                    $"gauge, but the selected rule is {selectedGaugeRule.GaugeType}.");

            return new CharacterAbilityStartSnapshot(CharacterId,
                DefinitionVersion, DisplayName, Stats,
                CharacterGaugeRuleSnapshot.Capture(selectedGaugeRule));
        }
    }

    /// <summary>Immutable character values handed to one play attempt.</summary>
    public sealed class CharacterAbilityStartSnapshot
    {
        public string CharacterId { get; }
        public int DefinitionVersion { get; }
        public string DisplayName { get; }
        public HealthGaugeType GaugeType => GaugeRule.GaugeType;
        public CharacterAbilityStats Stats { get; }
        public int StatBasis => Stats.BasisForOneHundredPercent;
        public decimal WorldUnlockMultiplier => Stats.WorldUnlockMultiplier;
        public decimal RegularCurrencyMultiplier =>
            Stats.RegularCurrencyMultiplier;
        public decimal HiddenUnlockMultiplier => Stats.HiddenUnlockMultiplier;
        public CharacterGaugeRuleSnapshot GaugeRule { get; }

        internal CharacterAbilityStartSnapshot(string characterId,
            int definitionVersion, string displayName,
            CharacterAbilityStats stats, CharacterGaugeRuleSnapshot gaugeRule)
        {
            CharacterId = characterId;
            DefinitionVersion = definitionVersion;
            DisplayName = displayName;
            Stats = stats;
            GaugeRule = gaugeRule;
        }
    }
}

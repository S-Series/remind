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
        public HealthGaugeType RequiredGaugeType { get; }
        public CharacterAbilityStats Stats { get; }

        public CharacterAbilityDefinition(string characterId,
            HealthGaugeType requiredGaugeType, CharacterAbilityStats stats)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                throw new ArgumentException(
                    "A character needs a stable ID.", nameof(characterId));
            if (!Enum.IsDefined(typeof(HealthGaugeType), requiredGaugeType))
                throw new ArgumentOutOfRangeException(nameof(requiredGaugeType));

            CharacterId = characterId.Trim();
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

            return new CharacterAbilityStartSnapshot(CharacterId, Stats,
                CharacterGaugeRuleSnapshot.Capture(selectedGaugeRule));
        }
    }

    /// <summary>Immutable character values handed to one play attempt.</summary>
    public sealed class CharacterAbilityStartSnapshot
    {
        public string CharacterId { get; }
        public HealthGaugeType GaugeType => GaugeRule.GaugeType;
        public CharacterAbilityStats Stats { get; }
        public CharacterGaugeRuleSnapshot GaugeRule { get; }

        internal CharacterAbilityStartSnapshot(string characterId,
            CharacterAbilityStats stats, CharacterGaugeRuleSnapshot gaugeRule)
        {
            CharacterId = characterId;
            Stats = stats;
            GaugeRule = gaugeRule;
        }
    }
}

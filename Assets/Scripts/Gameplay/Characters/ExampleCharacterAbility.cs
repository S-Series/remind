namespace REmind.Gameplay.Characters
{
    /// <summary>
    /// Sample values only. This does not create or configure Hard gauge rules.
    /// </summary>
    public static class ExampleCharacterAbility
    {
        public static CharacterAbilityDefinition CreateHardWorldReward()
        {
            return new CharacterAbilityDefinition(
                "example-hard-world-reward",
                HealthGaugeType.Hard,
                new CharacterAbilityStats(
                    worldProgressBonusPercent: 20,
                    rewardBonusPercent: 50));
        }
    }
}

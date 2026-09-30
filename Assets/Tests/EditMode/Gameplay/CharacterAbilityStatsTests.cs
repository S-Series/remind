using System;
using System.Reflection;
using NUnit.Framework;
using REmind.Gameplay.Characters;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class CharacterAbilityStatsTests
    {
        [Test]
        public void ExampleExposesTwoSettlementBonusesAndTwoUnconnectedSlots()
        {
            CharacterAbilityDefinition example =
                ExampleCharacterAbility.CreateHardWorldReward();

            Assert.AreEqual(4, Enum.GetValues(typeof(CharacterStatType)).Length);
            Assert.AreEqual(HealthGaugeType.Hard, example.RequiredGaugeType);
            Assert.IsTrue(example.Stats.TryGetBonusPercent(
                CharacterStatType.WorldProgress, out int worldBonus));
            Assert.IsTrue(example.Stats.TryGetBonusPercent(
                CharacterStatType.Reward, out int rewardBonus));
            Assert.AreEqual(20, worldBonus);
            Assert.AreEqual(50, rewardBonus);
            Assert.AreEqual(1.2m, example.Stats.WorldProgressMultiplier);
            Assert.AreEqual(1.5m, example.Stats.RewardMultiplier);
            Assert.IsFalse(example.Stats.TryGetBonusPercent(
                CharacterStatType.ReservedExtension1, out _));
            Assert.IsFalse(example.Stats.TryGetBonusPercent(
                CharacterStatType.ReservedExtension2, out _));
        }

        [Test]
        public void StartRejectsNormalRuleForHardExample()
        {
            var normalRule = ScriptableObject.CreateInstance<GameRuleConfig>();
            try
            {
                Assert.AreEqual(HealthGaugeType.Normal, normalRule.GaugeType);
                CharacterAbilityDefinition example =
                    ExampleCharacterAbility.CreateHardWorldReward();
                Assert.Throws<InvalidOperationException>(() =>
                    example.FreezeForStart(normalRule));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(normalRule);
            }
        }

        [Test]
        public void TestOnlyHardTypeFreezesCharacterValuesWithoutRetainingRuleAsset()
        {
            var hardRule = ScriptableObject.CreateInstance<GameRuleConfig>();
            try
            {
                typeof(GameRuleConfig).GetField("gaugeType",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(hardRule, HealthGaugeType.Hard);
                CharacterAbilityStartSnapshot start =
                    ExampleCharacterAbility.CreateHardWorldReward()
                        .FreezeForStart(hardRule);

                Assert.AreEqual("example-hard-world-reward", start.CharacterId);
                Assert.AreEqual(HealthGaugeType.Hard, start.GaugeType);
                Assert.AreEqual(1.2m, start.Stats.WorldProgressMultiplier);
                Assert.AreEqual(1.5m, start.Stats.RewardMultiplier);
                Assert.AreEqual(100, start.GaugeRule.MaxHealth);
                Assert.AreEqual(100, start.GaugeRule.InitialHealth);
                Assert.AreEqual(70, start.GaugeRule.ClearHealth);
                Assert.AreEqual(-10,
                    start.GaugeRule.GetHealthDelta(JudgeResult.Miss));

                typeof(GameRuleConfig).GetField("gaugeType",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(hardRule, HealthGaugeType.Normal);
                typeof(GameRuleConfig).GetField("maxHealth",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(hardRule, 200);
                Assert.AreEqual(HealthGaugeType.Hard, start.GaugeType);
                Assert.AreEqual(100, start.GaugeRule.MaxHealth);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hardRule);
            }
        }

        [Test]
        public void ConnectedBonusesRejectNegativePercentages()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CharacterAbilityStats(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CharacterAbilityStats(0, -1));
        }
    }
}

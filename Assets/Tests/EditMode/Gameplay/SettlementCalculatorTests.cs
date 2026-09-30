using System;
using NUnit.Framework;
using REmind.Gameplay.Characters;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class SettlementCalculatorTests
    {
        private GameRuleConfig normalRule;

        [SetUp]
        public void SetUp() =>
            normalRule = ScriptableObject.CreateInstance<GameRuleConfig>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(normalRule);

        [Test]
        public void StartSnapshotAppliesOnlyItsTwoSettlementBonuses()
        {
            CharacterAbilityStartSnapshot start = StartWithBonuses(20, 50);

            SettlementCalculation calculated = SettlementCalculator.Calculate(
                100m, 100m, start);

            Assert.AreEqual(120m, calculated.WorldProgress);
            Assert.AreEqual(150m, calculated.Reward);
            Assert.IsFalse(start.Stats.TryGetBonusPercent(
                CharacterStatType.ReservedExtension1, out _));
            Assert.IsFalse(start.Stats.TryGetBonusPercent(
                CharacterStatType.ReservedExtension2, out _));
        }

        [Test]
        public void EachBonusChangesOnlyItsOwnQuantity()
        {
            SettlementCalculation worldOnly = SettlementCalculator.Calculate(
                10m, 10m, StartWithBonuses(20, 0));
            SettlementCalculation rewardOnly = SettlementCalculator.Calculate(
                10m, 10m, StartWithBonuses(0, 50));

            Assert.AreEqual(12m, worldOnly.WorldProgress);
            Assert.AreEqual(10m, worldOnly.Reward);
            Assert.AreEqual(10m, rewardOnly.WorldProgress);
            Assert.AreEqual(15m, rewardOnly.Reward);
        }

        [Test]
        public void FractionalQuantitiesRemainUnrounded()
        {
            SettlementCalculation calculated = SettlementCalculator.Calculate(
                0.5m, 0.5m, StartWithBonuses(20, 50));

            Assert.AreEqual(0.6m, calculated.WorldProgress);
            Assert.AreEqual(0.75m, calculated.Reward);
        }

        [Test]
        public void InvalidInputsAreRejectedBeforeCalculation()
        {
            CharacterAbilityStartSnapshot start = StartWithBonuses(20, 50);

            Assert.Throws<ArgumentNullException>(() =>
                SettlementCalculator.Calculate(1m, 1m, null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SettlementCalculator.Calculate(-1m, 1m, start));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SettlementCalculator.Calculate(1m, -1m, start));
        }

        private CharacterAbilityStartSnapshot StartWithBonuses(
            int worldBonusPercent, int rewardBonusPercent)
        {
            var character = new CharacterAbilityDefinition(
                "settlement-test", HealthGaugeType.Normal,
                new CharacterAbilityStats(worldBonusPercent,
                    rewardBonusPercent));
            return character.FreezeForStart(normalRule);
        }
    }
}

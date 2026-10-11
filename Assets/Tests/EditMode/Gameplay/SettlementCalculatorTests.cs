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
        public void StartSnapshotAppliesOnlyExistingTwoSettlementQuantities()
        {
            CharacterAbilityStartSnapshot start = StartWithStats(60, 75);

            SettlementCalculation calculated = SettlementCalculator.Calculate(
                100m, 100m, start);

            Assert.AreEqual(120m, calculated.WorldUnlock);
            Assert.AreEqual(150m, calculated.RegularCurrency);
            Assert.IsFalse(start.Stats.TryGetMultiplier(
                CharacterStatType.Reserved, out _));
        }

        [Test]
        public void EachRawStatChangesOnlyItsOwnQuantity()
        {
            SettlementCalculation worldOnly = SettlementCalculator.Calculate(
                10m, 10m, StartWithStats(60, 50));
            SettlementCalculation rewardOnly = SettlementCalculator.Calculate(
                10m, 10m, StartWithStats(50, 75));

            Assert.AreEqual(12m, worldOnly.WorldUnlock);
            Assert.AreEqual(10m, worldOnly.RegularCurrency);
            Assert.AreEqual(10m, rewardOnly.WorldUnlock);
            Assert.AreEqual(15m, rewardOnly.RegularCurrency);
        }

        [Test]
        public void ZeroRawStatsProduceZeroExistingQuantities()
        {
            SettlementCalculation calculated = SettlementCalculator.Calculate(
                10m, 10m, StartWithStats(0, 0));

            Assert.AreEqual(0m, calculated.WorldUnlock);
            Assert.AreEqual(0m, calculated.RegularCurrency);
        }

        [Test]
        public void FractionalQuantitiesRemainUnrounded()
        {
            SettlementCalculation calculated = SettlementCalculator.Calculate(
                0.5m, 0.5m, StartWithStats(60, 75));

            Assert.AreEqual(0.6m, calculated.WorldUnlock);
            Assert.AreEqual(0.75m, calculated.RegularCurrency);
        }

        [Test]
        public void LaterBasisChangeCannotRepriceAnExistingStart()
        {
            var oldDefinition = new CharacterAbilityDefinition(
                "sample", HealthGaugeType.Normal,
                new CharacterAbilityStats(50, 50, 0, 50));
            CharacterAbilityStartSnapshot oldStart =
                oldDefinition.FreezeForStart(normalRule);
            var updatedDefinition = new CharacterAbilityDefinition(
                "sample", HealthGaugeType.Normal,
                new CharacterAbilityStats(50, 50, 0, 25));
            CharacterAbilityStartSnapshot laterStart =
                updatedDefinition.FreezeForStart(normalRule);

            Assert.AreEqual(100m,
                SettlementCalculator.Calculate(100m, 100m, oldStart)
                    .WorldUnlock);
            Assert.AreEqual(200m,
                SettlementCalculator.Calculate(100m, 100m, laterStart)
                    .WorldUnlock);
            Assert.AreEqual(50, oldStart.StatBasis);
        }

        [Test]
        public void InvalidInputsAreRejectedBeforeCalculation()
        {
            CharacterAbilityStartSnapshot start = StartWithStats(60, 75);

            Assert.Throws<ArgumentNullException>(() =>
                SettlementCalculator.Calculate(1m, 1m, null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SettlementCalculator.Calculate(-1m, 1m, start));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SettlementCalculator.Calculate(1m, -1m, start));
        }

        private CharacterAbilityStartSnapshot StartWithStats(
            int worldUnlock, int regularCurrency)
        {
            var character = new CharacterAbilityDefinition(
                "settlement-test", HealthGaugeType.Normal,
                new CharacterAbilityStats(worldUnlock,
                    regularCurrency, hiddenUnlock: 0,
                    basisForOneHundredPercent: 50));
            return character.FreezeForStart(normalRule);
        }
    }
}

using System;
using System.Reflection;
using NUnit.Framework;
using REmind.Gameplay.Characters;
using UnityEditor;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class CharacterAbilityStatsTests
    {
        [Test]
        public void ExamplePreservesDevelopmentMultipliersWithRawStats()
        {
            CharacterAbilityDefinition example =
                ExampleCharacterAbility.CreateHardWorldReward();

            Assert.AreEqual(4, Enum.GetValues(typeof(CharacterStatType)).Length);
            Assert.AreEqual(HealthGaugeType.Hard, example.RequiredGaugeType);
            Assert.IsTrue(example.Stats.TryGetRawValue(
                CharacterStatType.WorldUnlock, out int worldRaw));
            Assert.IsTrue(example.Stats.TryGetRawValue(
                CharacterStatType.RegularCurrency, out int currencyRaw));
            Assert.IsTrue(example.Stats.TryGetRawValue(
                CharacterStatType.HiddenUnlock, out int hiddenRaw));
            Assert.AreEqual(60, worldRaw);
            Assert.AreEqual(75, currencyRaw);
            Assert.AreEqual(0, hiddenRaw);
            Assert.AreEqual(50, example.Stats.BasisForOneHundredPercent);
            Assert.AreEqual(1.2m, example.Stats.WorldUnlockMultiplier);
            Assert.AreEqual(1.5m, example.Stats.RegularCurrencyMultiplier);
            Assert.IsFalse(example.Stats.TryGetRawValue(
                CharacterStatType.Reserved, out _));
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
                Assert.AreEqual(1, start.DefinitionVersion);
                Assert.AreEqual(50, start.StatBasis);
                Assert.AreEqual(HealthGaugeType.Hard, start.GaugeType);
                Assert.AreEqual(1.2m, start.WorldUnlockMultiplier);
                Assert.AreEqual(1.5m,
                    start.RegularCurrencyMultiplier);
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
        public void RawStatsUseOneConfigurableBasisAndRejectInvalidValues()
        {
            var stats = new CharacterAbilityStats(0, 50, 100, 50);
            Assert.AreEqual(0m, stats.WorldUnlockMultiplier);
            Assert.AreEqual(1m, stats.RegularCurrencyMultiplier);
            Assert.AreEqual(2m, stats.HiddenUnlockMultiplier);
            Assert.AreEqual(2m,
                new CharacterAbilityStats(50, 0, 0, 25)
                    .WorldUnlockMultiplier);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CharacterAbilityStats(-1, 0, 0, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CharacterAbilityStats(0, -1, 0, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CharacterAbilityStats(0, 0, -1, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CharacterAbilityStats(0, 0, 0, 0));
        }

        [Test]
        public void CatalogResolvesAuthoredDataAndStartFreezesBasisAndValues()
        {
            GameRuleConfig rule = ScriptableObject.CreateInstance<GameRuleConfig>();
            CharacterDefinitionAsset authored =
                ScriptableObject.CreateInstance<CharacterDefinitionAsset>();
            CharacterCatalog catalog =
                ScriptableObject.CreateInstance<CharacterCatalog>();
            try
            {
                // These are test values, not Non's product stats.
                SetField(authored, "characterId", "non");
                SetField(authored, "definitionVersion", 3);
                SetField(authored, "displayName", "논");
                SetField(authored, "englishName", "NON");
                SetField(authored, "gaugeRule", rule);
                SetField(authored, "worldUnlock", 0);
                SetField(authored, "regularCurrency", 50);
                SetField(authored, "hiddenUnlock", 100);
                SetField(catalog, "defaultCharacterId", "non");
                SetField(catalog, "statBasis", 50);
                SetField(catalog, "entries", new[] { authored });

                catalog.ValidateForUse();
                Assert.AreEqual("non", catalog.DefaultCharacterId);
                Assert.AreEqual(1, catalog.Entries.Count);
                Assert.IsTrue(catalog.TryResolve("non",
                    out CharacterAbilityDefinition definition,
                    out GameRuleConfig selectedRule));
                Assert.AreSame(rule, selectedRule);
                CharacterAbilityStartSnapshot start =
                    definition.FreezeForStart(selectedRule);
                Assert.AreEqual("non", start.CharacterId);
                Assert.AreEqual(3, start.DefinitionVersion);
                Assert.AreEqual("논", start.DisplayName);
                Assert.AreEqual(50, start.StatBasis);
                Assert.AreEqual(0m, start.WorldUnlockMultiplier);
                Assert.AreEqual(1m, start.RegularCurrencyMultiplier);
                Assert.AreEqual(2m, start.HiddenUnlockMultiplier);
                Assert.IsFalse(start.Stats.TryGetRawValue(
                    CharacterStatType.Reserved, out _));

                SetField(authored, "regularCurrency", 100);
                SetField(catalog, "statBasis", 25);
                Assert.AreEqual(50, start.Stats.RegularCurrency);
                Assert.AreEqual(50, start.StatBasis);
                Assert.AreEqual(1m, start.RegularCurrencyMultiplier);
                Assert.IsTrue(catalog.TryResolve("non", out definition,
                    out selectedRule));
                Assert.AreEqual(4m, definition.Stats.RegularCurrencyMultiplier);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(authored);
                UnityEngine.Object.DestroyImmediate(rule);
            }
        }

        [Test]
        public void CatalogRejectsMissingDefaultAndDuplicateIds()
        {
            GameRuleConfig rule = ScriptableObject.CreateInstance<GameRuleConfig>();
            CharacterDefinitionAsset first =
                ScriptableObject.CreateInstance<CharacterDefinitionAsset>();
            CharacterDefinitionAsset second =
                ScriptableObject.CreateInstance<CharacterDefinitionAsset>();
            CharacterCatalog catalog =
                ScriptableObject.CreateInstance<CharacterCatalog>();
            try
            {
                foreach (CharacterDefinitionAsset authored in
                         new[] { first, second })
                {
                    SetField(authored, "characterId", "non");
                    SetField(authored, "displayName", "논");
                    SetField(authored, "englishName", "NON");
                    SetField(authored, "gaugeRule", rule);
                }
                SetField(catalog, "defaultCharacterId", "missing");
                SetField(catalog, "entries", new[] { first });
                Assert.Throws<InvalidOperationException>(
                    catalog.ValidateForUse);

                SetField(catalog, "defaultCharacterId", "non");
                SetField(catalog, "entries", new[] { first, second });
                Assert.Throws<InvalidOperationException>(
                    catalog.ValidateForUse);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(rule);
            }
        }

        [Test]
        public void AuthoredNonUsesNeutralDevelopmentStatsAndBaseGauge()
        {
            CharacterCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CharacterCatalog>(
                    "Assets/Data/Characters/CharacterCatalog.asset");
            Assert.IsNotNull(catalog);
            catalog.ValidateForUse();
            Assert.AreEqual("non", catalog.DefaultCharacterId);
            Assert.AreEqual(50, catalog.StatBasis);
            Assert.IsTrue(catalog.TryGetEntry("non",
                out CharacterDefinitionAsset entry));
            Assert.AreEqual("논", entry.DisplayName);
            Assert.AreEqual("NON", entry.EnglishName);
            Assert.IsNotNull(entry.PortraitTexture);
            Assert.AreEqual(50, entry.WorldUnlock);
            Assert.AreEqual(50, entry.RegularCurrency);
            Assert.AreEqual(50, entry.HiddenUnlock);
            Assert.AreEqual(0, entry.GaugeRule.InitialHealth);
            Assert.AreEqual(100, entry.GaugeRule.MaxHealth);

            Assert.IsTrue(catalog.TryResolve("non",
                out CharacterAbilityDefinition definition,
                out GameRuleConfig rule));
            CharacterAbilityStartSnapshot start = definition.FreezeForStart(rule);
            Assert.AreEqual(1m, start.WorldUnlockMultiplier);
            Assert.AreEqual(1m, start.RegularCurrencyMultiplier);
            Assert.AreEqual(1m, start.HiddenUnlockMultiplier);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}

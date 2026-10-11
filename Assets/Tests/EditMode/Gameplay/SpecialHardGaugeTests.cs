using System;
using System.Reflection;
using NUnit.Framework;
using REmind.Gameplay.Effects;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class SpecialHardGaugeTests
    {
        private const BindingFlags Private = BindingFlags.Instance |
            BindingFlags.NonPublic;

        [Test]
        public void InlineSettingsDoNotEnableSpecialHardWithoutFlag()
        {
            var config = ScriptableObject.CreateInstance<GameRuleConfig>();
            try
            {
                Assert.That(config.SpecialHardGauge, Is.Null);
                Assert.DoesNotThrow(config.ValidateForSession);
                SetField(config, "specialHardGauge",
                    new SpecialHardGaugeSettings());
                Assert.That(config.SpecialHardGauge, Is.Null);

                SetField(config, "gaugeType", HealthGaugeType.Hard);
                SetField(config, "useSpecialHardGauge", true);
                Assert.That(config.SpecialHardGauge, Is.Not.Null);
                Assert.DoesNotThrow(config.ValidateForSession);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void RfClampsAtBothEndsAndRejectsEmptyChart()
        {
            var settings = new SpecialHardGaugeSettings();
            Assert.That(settings.GetDelta(JudgeResult.Perfect, 50d, 100, 100),
                Is.EqualTo(0.3375d).Within(0.000001d));
            Assert.That(settings.GetDelta(JudgeResult.Perfect, 50d, 100, 10000),
                Is.EqualTo(0.1875d).Within(0.000001d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                settings.GetDelta(JudgeResult.Perfect, 50d, 100, 0));
            Assert.That(settings.GetDelta(JudgeResult.PerfectPlus, 50d, 100, 1000),
                Is.EqualTo(settings.GetDelta(JudgeResult.Perfect, 50d, 100, 1000)));
        }

        [Test]
        public void DamageReductionUsesHealthBeforeJudgement()
        {
            var settings = new SpecialHardGaugeSettings();
            Assert.That(settings.GetDelta(JudgeResult.Great, 30.001d, 100, 1000),
                Is.EqualTo(-0.5d).Within(0.000001d));
            Assert.That(settings.GetDelta(JudgeResult.Great, 30.000001d,
                100, 1000), Is.EqualTo(-0.5d).Within(0.000001d));
            Assert.That(settings.GetDelta(JudgeResult.Great, 30d, 100, 1000),
                Is.EqualTo(-0.25d).Within(0.000001d));
            Assert.That(settings.GetDelta(JudgeResult.Miss, 30d, 100, 1000),
                Is.EqualTo(-3.75d).Within(0.000001d));
        }

        [Test]
        public void EffectDamageMultiplierRetainsFractionalSpecialHardLoss()
        {
            var config = ScriptableObject.CreateInstance<GameRuleConfig>();
            var game = new GameObject("Special Hard modifier test");
            game.SetActive(false);
            try
            {
                SetField(config, "gaugeType", HealthGaugeType.Hard);
                SetField(config, "useSpecialHardGauge", true);
                SetField(config, "specialHardGauge",
                    new SpecialHardGaugeSettings());
                var rule = game.AddComponent<DefaultGameRule>();
                typeof(GameRule).GetField("config", Private).SetValue(rule,
                    config);
                using (var effects = new EffectRuleService(rule))
                {
                    effects.AddDamageMultiplier("test", 0.5d, 0d);
                    RuleContext context = default(RuleContext).AtTime(1d);
                    Assert.That(rule.ApplyHealthDelta(50d,
                        JudgeResult.Miss, context, 1000),
                        Is.EqualTo(46.25d).Within(0.000001d));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(game);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void SetField(object target, string name,
            object value) => target.GetType().GetField(name, Private)
                .SetValue(target, value);
    }
}

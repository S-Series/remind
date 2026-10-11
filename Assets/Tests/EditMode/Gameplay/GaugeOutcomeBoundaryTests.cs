using System;
using System.Reflection;
using NUnit.Framework;
using REmind.Data;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class GaugeOutcomeBoundaryTests
    {
        private const BindingFlags Private = BindingFlags.Instance |
            BindingFlags.NonPublic;

        [TestCase(HealthGaugeType.Normal)]
        [TestCase(HealthGaugeType.Easy)]
        public void NormalAndEasyNeedSeventyHealthAtCompletion(
            HealthGaugeType gaugeType)
        {
            using (var fixture = new Fixture(gaugeType))
            {
                Assert.That(fixture.Rule.GetInitialHealth(default(RuleContext)),
                    Is.Zero);
                Assert.That(fixture.Rule.ShouldFail(Context(0d, false)),
                    Is.False);
                Assert.That(fixture.Rule.IsCleared(Context(0d, false)),
                    Is.False);
                Assert.That(fixture.Rule.ShouldFail(Context(0d, true)),
                    Is.True);
                Assert.That(fixture.Rule.ShouldFail(Context(69.99d, true)),
                    Is.True);
                Assert.That(fixture.Rule.IsCleared(Context(69.99d, true)),
                    Is.False);
                Assert.That(fixture.Rule.ShouldFail(Context(70d, true)),
                    Is.False);
                Assert.That(fixture.Rule.IsCleared(Context(70d, true)),
                    Is.True);
                Assert.That(fixture.Rule.IsCleared(Context(70d, false)),
                    Is.False);
            }
        }

        [Test]
        public void HardNeedsCompletionWithAnyPositiveHealth()
        {
            using (var fixture = new Fixture(HealthGaugeType.Hard))
            {
                Assert.That(fixture.Rule.FailImmediately, Is.True);
                Assert.That(fixture.Rule.ContinueAfterFail, Is.False);
                Assert.That(fixture.Rule.ShouldFail(Context(0d, false)),
                    Is.True);
                Assert.That(fixture.Rule.ShouldFail(Context(0d, true)),
                    Is.True);
                Assert.That(fixture.Rule.IsCleared(Context(0d, true)),
                    Is.False);
                Assert.That(fixture.Rule.ShouldFail(Context(0.1d, true)),
                    Is.False);
                Assert.That(fixture.Rule.IsCleared(Context(0.1d, true)),
                    Is.True);
                Assert.That(fixture.Rule.IsCleared(Context(0.1d, false)),
                    Is.False);

                Assert.That(fixture.Rule.ApplyHealthDelta(0.1d,
                    JudgeResult.None, Context(0.1d, false), 1),
                    Is.EqualTo(0.1d).Within(0.000001d));
            }
        }

        private static RuleContext Context(double health, bool completed) =>
            new RuleContext(health, 0, NoteType.Tap, false, false,
                completed);

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject game;
            private readonly GameRuleConfig config;

            public DefaultGameRule Rule { get; }

            public Fixture(HealthGaugeType gaugeType)
            {
                config = ScriptableObject.CreateInstance<GameRuleConfig>();
                typeof(GameRuleConfig).GetField("gaugeType", Private)
                    .SetValue(config, gaugeType);
                if (gaugeType != HealthGaugeType.Hard)
                    typeof(GameRuleConfig).GetField("initialHealth", Private)
                        .SetValue(config, 0);
                game = new GameObject("Gauge outcome boundary");
                game.SetActive(false);
                Rule = game.AddComponent<DefaultGameRule>();
                typeof(GameRule).GetField("config", Private)
                    .SetValue(Rule, config);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(game);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}

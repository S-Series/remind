using System;
using System.Reflection;
using NUnit.Framework;
using REmind.Charting;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class GameplaySessionFailureTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance |
            BindingFlags.NonPublic;

        [Test]
        public void TerminalMiss_IgnoresLaterHealingJudgementUntilFreshStart()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Judge(JudgeResult.Miss);
                Assert.That(fixture.State.CurrentHealth, Is.Zero);
                Assert.That(fixture.State.IsFailed, Is.True);
                Assert.That(fixture.State.IsCleared, Is.False);

                fixture.Judge(JudgeResult.Perfect);
                Assert.That(fixture.State.CurrentHealth, Is.Zero);
                Assert.That(fixture.State.IsFailed, Is.True);
                Assert.That(fixture.State.JudgedNoteCount, Is.EqualTo(1));
                Assert.That(fixture.State.CurrentScore, Is.Zero);

                fixture.RestartState();
                Assert.That(fixture.State.IsFailed, Is.False);
                fixture.Judge(JudgeResult.Perfect);
                Assert.That(fixture.State.CurrentHealth, Is.EqualTo(21));
                Assert.That(fixture.State.JudgedNoteCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void PerfectBeforeMiss_UsesJudgementOrderWithoutPrematureFailure()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Judge(JudgeResult.Perfect);
                fixture.Judge(JudgeResult.Miss);

                Assert.That(fixture.State.CurrentHealth, Is.EqualTo(1));
                Assert.That(fixture.State.IsFailed, Is.False);
                Assert.That(fixture.State.JudgedNoteCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void HardCannotRecoverEvenWhenContinuationWasConfigured()
        {
            using (var fixture = new Fixture(true))
            {
                fixture.Judge(JudgeResult.Miss);
                Assert.That(fixture.State.IsFailed, Is.True);
                Assert.That(fixture.State.CurrentHealth, Is.Zero);
                fixture.Judge(JudgeResult.Perfect);

                Assert.That(fixture.State.CurrentHealth, Is.Zero);
                Assert.That(fixture.State.IsFailed, Is.True);
                Assert.That(fixture.State.IsCleared, Is.False);
                Assert.That(fixture.State.JudgedNoteCount, Is.EqualTo(1));
            }
        }

        [TestCase(HealthGaugeType.Normal)]
        [TestCase(HealthGaugeType.Easy)]
        public void ZeroHealthCanContinueUntilCompletionOnNonHardGauge(
            HealthGaugeType gaugeType)
        {
            using (var fixture = new Fixture(true, gaugeType, 0))
            {
                Assert.That(fixture.State.CurrentHealth, Is.Zero);
                Assert.That(fixture.State.IsFailed, Is.False);
                fixture.Judge(JudgeResult.Miss);
                Assert.That(fixture.State.CurrentHealth, Is.Zero);
                Assert.That(fixture.State.IsFailed, Is.False);
                Assert.That(fixture.State.JudgedNoteCount, Is.EqualTo(1));

                fixture.Judge(JudgeResult.Perfect);
                Assert.That(fixture.State.CurrentHealth, Is.EqualTo(1));
                Assert.That(fixture.State.IsFailed, Is.True);
                Assert.That(fixture.State.IsCleared, Is.False);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly GameRuleConfig config;
            private readonly PlayableNoteSnapshot note;

            public GameplaySessionState State { get; }

            public Fixture(bool continueAfterFail,
                HealthGaugeType gaugeType = HealthGaugeType.Hard,
                int initialHealth = 20)
            {
                root = new GameObject("Failure state test (inactive)");
                root.SetActive(false);
                var manager = root.AddComponent<GameManager>();
                var play = root.AddComponent<GamePlay>();
                var rule = root.AddComponent<DefaultGameRule>();
                State = root.AddComponent<GameplaySessionState>();
                config = ScriptableObject.CreateInstance<GameRuleConfig>();

                SetField(typeof(GameRuleConfig), config, "gaugeType",
                    gaugeType);
                SetField(typeof(GameRuleConfig), config, "initialHealth",
                    initialHealth);
                SetField(typeof(GameRuleConfig), config, "clearHealth", 1);
                SetField(typeof(GameRuleConfig), config, "failImmediately", true);
                SetField(typeof(GameRuleConfig), config, "continueAfterFail",
                    continueAfterFail);
                SetField(typeof(JudgeHealthDeltaSettings),
                    config.HealthDelta, "perfect", 1);
                SetField(typeof(JudgeHealthDeltaSettings),
                    config.HealthDelta, "miss", -20);
                SetField(typeof(GameRule), rule, "config", config);
                SetField(typeof(GameManager), manager, "gamePlay", play);
                SetField(typeof(GameplaySessionState), State, "gameManager",
                    manager);
                SetField(typeof(GameplaySessionState), State, "gameRule", rule);
                SetField(typeof(GameplaySessionState), State, "totalNoteCount", 2);
                SetField(typeof(GameplaySessionState), State,
                    "<HasPreparedChart>k__BackingField", true);
                RestartState();

                var document = new ChartDocument(4800, 4, 120);
                document.Notes.Add(new ChartDocumentNote("tap", ChartNoteKind.Tap,
                    (int)ChartLane.GroundLeft, 0));
                ChartCompileResult compiled = ChartCompiler.Compile(document);
                Assert.That(compiled.Succeeded, Is.True);
                note = compiled.Snapshot.Notes[0];
            }

            public void Judge(JudgeResult result)
            {
                var judgement = new NoteJudgementEvent(note, -1, result,
                    TimingSide.Exact, 0d, 0d, 0d, result == JudgeResult.Miss);
                typeof(GameplaySessionState).GetMethod("HandleNoteJudged",
                    PrivateInstance).Invoke(State, new object[] { judgement });
            }

            public void RestartState()
            {
                typeof(GameplaySessionState).GetMethod("ResetForFreshSession",
                    PrivateInstance).Invoke(State, null);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void SetField(Type owner, object target,
            string name, object value)
        {
            owner.GetField(name, PrivateInstance).SetValue(target, value);
        }
    }
}

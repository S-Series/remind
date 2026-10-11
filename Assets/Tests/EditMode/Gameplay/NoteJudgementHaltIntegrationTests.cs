using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using REmind.Charting;
using REmind.Gameplay.Input.Judgement;
using UnityEngine;

namespace REmind.Gameplay.Tests
{
    public sealed class NoteJudgementHaltIntegrationTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void TerminalFailureStopsSameTimeAutomaticBatchAndFreshResetReopensIt()
        {
            using (var fixture = new Fixture(false))
            {
                int judgements = 0;
                int completedFrames = 0;
                fixture.Judgement.NoteJudged += _ => judgements++;
                fixture.Judgement.EffectFrameCompleted += _ => completedFrames++;
                fixture.Playing();

                fixture.Judgement.ProcessFrame(1000d);

                Assert.That(judgements, Is.EqualTo(1));
                Assert.That(fixture.State.IsFailed, Is.True);
                Assert.That(fixture.Manager.PlaybackState, Is.EqualTo(PlaybackState.Empty));
                Assert.That(completedFrames, Is.Zero,
                    "The stopped generation must not publish an Effect frame.");
                fixture.Judgement.ProcessFrame(1000d);
                Assert.That(judgements, Is.EqualTo(1));

                fixture.Judgement.ResetJudgements(false);
                Invoke(fixture.State, "ResetForFreshSession");
                fixture.Playing();
                fixture.Judgement.ProcessFrame(1000d);
                Assert.That(judgements, Is.EqualTo(2),
                    "Reset opens a new attempt, which can fail independently.");
            }
        }

        [Test]
        public void PauseAndResumeDoNotBypassHardTerminalFailure()
        {
            using (var fixture = new Fixture(true))
            {
                int judgements = 0;
                fixture.Judgement.NoteJudged += _ => judgements++;
                fixture.Playing();
                Invoke(fixture.Playback, "SetState", PlaybackState.Paused);
                Invoke(fixture.Playback, "SetState", PlaybackState.Playing);

                fixture.Judgement.ProcessFrame(1000d);

                Assert.That(judgements, Is.EqualTo(1));
                Assert.That(fixture.Manager.PlaybackState,
                    Is.EqualTo(PlaybackState.Empty));
            }
        }

        [Test]
        public void ResetDuringAutomaticCallbackCannotContinueOldBatchOrEffectFrame()
        {
            using (var fixture = new Fixture(true, HealthGaugeType.Normal))
            {
                int judgements = 0;
                int completedFrames = 0;
                var results = new List<JudgeResult>();
                fixture.Judgement.EffectFrameCompleted += _ => completedFrames++;
                fixture.Judgement.NoteJudged += result =>
                {
                    judgements++;
                    results.Add(result.Result);
                    if (judgements != 1) return;
                    // Playback commit does both operations for a fresh Restart.
                    fixture.Judgement.ResetJudgements(false);
                    fixture.Judgement.AttachEffectRunner(null);
                    Invoke(fixture.State, "ResetForFreshSession");
                };
                fixture.Playing();

                fixture.Judgement.ProcessFrame(1000d);
                Assert.That(judgements, Is.EqualTo(1));
                Assert.That(completedFrames, Is.Zero);

                fixture.QueueInput((int)ChartLane.GroundLeft, 0d);
                fixture.Judgement.ProcessFrame(1000d);
                Assert.That(judgements, Is.EqualTo(3));
                Assert.That(completedFrames, Is.EqualTo(1));
                Assert.That(results[1], Is.EqualTo(JudgeResult.PerfectPlus),
                    "The new attempt must accept its time-zero input.");
                Assert.That(fixture.Judgement.DiscardedLateInputCount,
                    Is.Zero);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly GameRuleConfig config;

            public GameManager Manager { get; }
            public GamePlay Playback { get; }
            public NoteJudgementSystem Judgement { get; }
            public GameplaySessionState State { get; }

            public Fixture(bool continueAfterFail,
                HealthGaugeType gaugeType = HealthGaugeType.Hard)
            {
                root = new GameObject("Judgement halt integration");
                root.SetActive(false);
                var source = root.AddComponent<AudioSource>();
                Playback = root.AddComponent<GamePlay>();
                SetField(Playback, "audioSource", source);
                SetField(Playback, "cameraTransform", root.transform);

                config = ScriptableObject.CreateInstance<GameRuleConfig>();
                SetField(config, "gaugeType", gaugeType);
                SetField(config, "initialHealth", 20);
                SetField(config, "clearHealth", 1);
                SetField(config, "failImmediately", true);
                SetField(config, "continueAfterFail", continueAfterFail);
                SetField(config.HealthDelta, "miss", -20);
                var rule = root.AddComponent<DefaultGameRule>();
                SetField(rule, "config", config);
                Manager = root.AddComponent<GameManager>();
                SetField(Manager, "gamePlay", Playback);
                SetField(Manager, "gameRule", rule);

                Judgement = root.AddComponent<NoteJudgementSystem>();
                SetField(Judgement, "gameManager", Manager);
                SetField(Judgement, "gameRule", rule);
                State = root.AddComponent<GameplaySessionState>();
                SetField(State, "gameManager", Manager);
                SetField(State, "judgementSystem", Judgement);
                SetField(State, "gameRule", rule);
                // The fixture stays inactive to avoid an input device. Bind the
                // same playback event that NoteJudgementSystem.OnEnable binds.
                Playback.PlaybackStateChanged +=
                    (Action<PlaybackState>)Delegate.CreateDelegate(
                        typeof(Action<PlaybackState>), Judgement,
                        typeof(NoteJudgementSystem).GetMethod(
                            "HandlePlaybackStateChanged", PrivateInstance));

                var chart = new ChartDocument(4800, 4, 120);
                chart.Notes.Add(new ChartDocumentNote("a", ChartNoteKind.Tap,
                    (int)ChartLane.GroundLeft, 0));
                chart.Notes.Add(new ChartDocumentNote("b", ChartNoteKind.Tap,
                    (int)ChartLane.GroundRight, 0));
                ChartCompileResult compiled = ChartCompiler.Compile(chart);
                Assert.That(compiled.Succeeded, Is.True);
                Assert.That(State.TryConfigureChart(2, out string error),
                    Is.True, error);
                Assert.That(Judgement.Initialize(compiled.Snapshot), Is.True);
            }

            public void Playing() =>
                SetField(Playback, "<State>k__BackingField",
                    PlaybackState.Playing);

            public void QueueInput(int lane, double chartTimeMs)
            {
                Type queuedInput = typeof(NoteJudgementSystem).GetNestedType(
                    "QueuedInput", BindingFlags.NonPublic);
                object input = Activator.CreateInstance(queuedInput,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic, null,
                    new object[] { lane, chartTimeMs, 0L, true }, null);
                ((IList)typeof(NoteJudgementSystem).GetField("pendingInputs",
                    PrivateInstance).GetValue(Judgement)).Add(input);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void SetField(object target, string name, object value) =>
            FindField(target.GetType(), name).SetValue(target, value);

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type owner = type; owner != null; owner = owner.BaseType)
            {
                FieldInfo field = owner.GetField(name,
                    PrivateInstance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        private static void Invoke(object target, string name,
            params object[] arguments) =>
            target.GetType().GetMethod(name, PrivateInstance)
                .Invoke(target, arguments);
    }
}

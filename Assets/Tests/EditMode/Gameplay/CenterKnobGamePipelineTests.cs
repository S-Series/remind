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
    public sealed class CenterKnobGamePipelineTests
    {
        [Test]
        public void EveryKnobTargetScoresOnceAndFinalTickCompletesWithoutUp()
        {
            using (var fixture = new Fixture(false))
            {
                var results = new List<NoteJudgementEvent>();
                fixture.Judgement.NoteJudged += results.Add;
                fixture.Queue(9, 0d);
                fixture.Judgement.ProcessFrame(1000d);

                Assert.That(results.Count, Is.EqualTo(fixture.Chart.JudgementUnitCount));
                Assert.That(fixture.State.JudgedNoteCount, Is.EqualTo(fixture.State.TotalNoteCount));
                Assert.That(fixture.State.CurrentScore, Is.EqualTo(1_000_000d).Within(0.001d));
                Assert.That(fixture.State.IsCleared, Is.True);
                Assert.That(fixture.Judgement.PendingNoteCount, Is.Zero);
                Assert.That(results.FindAll(r => r.IsNoteComplete).Count, Is.EqualTo(1));
                Assert.That(results[results.Count - 1].EffectiveHitTimeMs, Is.EqualTo(500d));
                Assert.That(results[results.Count - 1].TargetKind, Is.EqualTo(JudgementTargetKind.KnobTick));
            }
        }

        [Test]
        public void SpecialHardFailureStopsKnobBatchAndLaterRecoveryCannotReviveIt()
        {
            using (var fixture = new Fixture(true))
            {
                int count = 0;
                fixture.Judgement.NoteJudged += _ => count++;
                fixture.Judgement.ProcessFrame(1000d);
                Assert.That(count, Is.EqualTo(2));
                Assert.That(fixture.State.IsFailed, Is.True);
                Assert.That(fixture.State.CurrentHealth, Is.Zero);
                Assert.That(fixture.Manager.PlaybackState, Is.EqualTo(PlaybackState.Empty));
                fixture.Queue(9, 1100d);
                fixture.Judgement.ProcessFrame(2000d);
                Assert.That(count, Is.EqualTo(2));
                Assert.That(fixture.State.IsFailed, Is.True);
            }
        }

        [Test]
        public void SpaceOnlyResultUsesSamePipelineButCannotSatisfyKnob()
        {
            using (var fixture = new Fixture(false, true))
            {
                var results = new List<NoteJudgementEvent>();
                fixture.Judgement.NoteJudged += results.Add;
                fixture.Queue(10, 0d);
                fixture.Judgement.ProcessFrame(1000d);
                Assert.That(results.Find(r => r.ChartNote.Kind == ChartNoteKind.CenterTap).Result,
                    Is.EqualTo(JudgeResult.PerfectPlus));
                Assert.That(results.FindAll(r => r.ChartNote.Kind == ChartNoteKind.Knob &&
                    r.TargetKind != JudgementTargetKind.KnobStart)
                    .TrueForAll(r => r.Result == JudgeResult.Miss), Is.True);
                Assert.That(results.Count, Is.EqualTo(fixture.Chart.JudgementUnitCount));
            }
        }

        [Test]
        public void RuntimeV2CentralTapUsesAllFourWindowsIncludingEarlyMiss()
        {
            double[] boundaries = { 0d, 40d, 40.001d, 80d, 80.001d, 130d, 130.001d, 175d, 175.001d };
            JudgeResult[] grades = { JudgeResult.PerfectPlus, JudgeResult.PerfectPlus,
                JudgeResult.Perfect, JudgeResult.Perfect, JudgeResult.Great, JudgeResult.Great,
                JudgeResult.Miss, JudgeResult.Miss, JudgeResult.None };
            for (int sign = -1; sign <= 1; sign += 2)
            for (int index = 0; index < boundaries.Length; index++)
            {
                var document = new ChartDocument(4800, 4, 120d);
                document.Notes.Add(new ChartDocumentNote("window", ChartNoteKind.CenterTap, 10, 2400));
                using (var fixture = new Fixture(false, document: document))
                {
                    var results = new List<NoteJudgementEvent>();
                    fixture.Judgement.NoteJudged += results.Add;
                    double offset = sign * boundaries[index];
                    fixture.Queue(10, 1000d + offset);
                    fixture.Judgement.ProcessFrame(1000d + offset);
                    var manual = results.FindAll(r => !r.IsAutomaticMiss);
                    string context = "Center Tap at " + offset + "ms";
                    Assert.That(manual.Count, Is.EqualTo(grades[index] == JudgeResult.None ? 0 : 1), context);
                    if (manual.Count == 0) continue;
                    Assert.That(manual[0].Result, Is.EqualTo(grades[index]), context);
                    Assert.That(manual[0].OffsetMs, Is.EqualTo(offset).Within(0.000001d), context);
                }
            }
        }

        [Test]
        public void PositionAlignedFlatScoresWithoutInputAndPersistsAfterEnd()
        {
            var document = new ChartDocument(4800, 4, 120d);
            var note = new ChartDocumentNote("flat", ChartNoteKind.Knob, 10, 0, 1200);
            note.Points[0].KnobX = note.Points[1].KnobX = 0.6d;
            document.Notes.Add(note);
            using (var fixture = new Fixture(false, document: document))
            {
                var results = new List<NoteJudgementEvent>();
                fixture.Judgement.NoteJudged += results.Add;
                fixture.Judgement.ProcessFrame(1000d);
                Assert.That(results.TrueForAll(r => r.Result == JudgeResult.PerfectPlus), Is.True);
                Assert.That(fixture.State.CurrentScore, Is.EqualTo(1_000_000d).Within(0.001d));
                Assert.That(fixture.Judgement.PlayerKnobX, Is.EqualTo(1.5d));
                Assert.That(fixture.Judgement.PendingNoteCount, Is.Zero);
            }
        }

        [Test]
        public void EarlyStepUpdatesActualCursorAndDefersSingleScoreToOriginalTime()
        {
            var document = new ChartDocument(4800, 4, 120d);
            var note = new ChartDocumentNote("step", ChartNoteKind.Knob, 10, 0, 2400);
            note.Points[1].KnobX = 1d;
            note.Points.Insert(1, new ChartNotePoint(1200, ChartNotePointKind.Mid)
                { KnobX = 1d, KnobInterpolation = ChartKnobInterpolation.Step });
            document.Notes.Add(note);
            using (var fixture = new Fixture(false, document: document))
            {
                var results = new List<NoteJudgementEvent>();
                fixture.Judgement.NoteJudged += results.Add;
                fixture.Queue(9, 420d);
                fixture.Judgement.ProcessFrame(420d);
                Assert.That(fixture.Judgement.PlayerKnobX, Is.EqualTo(2.5d));
                Assert.That(results.Exists(r => r.TargetKind == JudgementTargetKind.KnobTurn), Is.False);
                fixture.Judgement.ProcessFrame(499d);
                Assert.That(fixture.Judgement.PlayerKnobX, Is.EqualTo(2.5d));
                fixture.Judgement.ProcessFrame(1000d);
                var steps = results.FindAll(r => r.TargetKind == JudgementTargetKind.KnobTurn);
                Assert.That(steps.Count, Is.EqualTo(1));
                Assert.That(steps[0].Result, Is.EqualTo(JudgeResult.PerfectPlus));
                Assert.That(steps[0].EvaluationTimeMs, Is.EqualTo(500d));
                Assert.That(results.Count, Is.EqualTo(fixture.Chart.JudgementUnitCount));
            }
        }

        [Test]
        public void NextFrameDeviceTimestampRecomputesCursorBeforeRenderWatermark()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Judgement.ProcessFrame(90d);
                Assert.That(fixture.Judgement.PlayerKnobX, Is.EqualTo(-2.5d));
                fixture.Queue(9, 20d);
                fixture.Judgement.ProcessFrame(95d);
                Assert.That(fixture.Judgement.DiscardedLateInputCount, Is.Zero);
                Assert.That(fixture.Judgement.PlayerKnobX, Is.EqualTo(-1.55d).Within(0.000001d));
            }
        }

        [Test]
        public void PreStartStepSelectsActualCursorAndKeepsSnapAcrossStart()
        {
            var document = new ChartDocument(4800, 4, 120d);
            var note = new ChartDocumentNote("preview", ChartNoteKind.Knob, 10, 240, 1200);
            note.Points[1].KnobX = 1d;
            note.Points.Insert(1, new ChartNotePoint(360, ChartNotePointKind.Mid)
                { KnobX = 1d, KnobInterpolation = ChartKnobInterpolation.Step });
            document.Notes.Add(note);
            using (var fixture = new Fixture(false, document: document))
            {
                var results = new List<NoteJudgementEvent>();
                fixture.Judgement.NoteJudged += results.Add;
                fixture.Judgement.ProcessFrame(90d);
                Assert.That(fixture.Judgement.PlayerKnobNoteId, Is.Null);
                fixture.Queue(9, 70d);
                fixture.Judgement.ProcessFrame(95d);
                Assert.That(fixture.Judgement.DiscardedLateInputCount, Is.Zero);
                Assert.That(fixture.Judgement.PlayerKnobNoteId, Is.EqualTo("preview"));
                Assert.That(fixture.Judgement.PlayerKnobX, Is.EqualTo(2.5d));
                Assert.That(results, Is.Empty);
                fixture.Judgement.ProcessFrame(100d);
                Assert.That(fixture.Judgement.PlayerKnobX, Is.EqualTo(2.5d));
                Assert.That(results.Count, Is.EqualTo(1));
                Assert.That(results[0].Result, Is.EqualTo(JudgeResult.PerfectPlus));
                Assert.That(results[0].EvaluationTimeMs, Is.EqualTo(100d));
                fixture.Judgement.ProcessFrame(500d);
                Assert.That(results.TrueForAll(r => r.Result == JudgeResult.PerfectPlus), Is.True);
                Assert.That(results.Count, Is.EqualTo(fixture.Chart.JudgementUnitCount));
                fixture.Judgement.ResetJudgements();
                Assert.That(fixture.Judgement.PlayerKnobNoteId, Is.Null);
                Assert.That(fixture.Judgement.PlayerKnobX, Is.Zero);
            }
        }

        [Test]
        public void PreviousHardMissStopsReservedFutureStartAtTheSameTimestamp()
        {
            var document = new ChartDocument(4800, 4, 120d);
            document.Notes.Add(new ChartDocumentNote("previous", ChartNoteKind.Knob, 10, 0, 240));
            var note = new ChartDocumentNote("preview", ChartNoteKind.Knob, 10, 240, 1200);
            note.Points[1].KnobX = 1d;
            note.Points.Insert(1, new ChartNotePoint(360, ChartNotePointKind.Mid)
                { KnobX = 1d, KnobInterpolation = ChartKnobInterpolation.Step });
            document.Notes.Add(note);
            using (var fixture = new Fixture(true, document: document))
            {
                var results = new List<NoteJudgementEvent>();
                fixture.Judgement.NoteJudged += results.Add;
                fixture.Queue(9, 70d);
                fixture.Judgement.ProcessFrame(200d);
                Assert.That(fixture.State.IsFailed, Is.True);
                Assert.That(results.Count, Is.EqualTo(2));
                Assert.That(results.Exists(r => r.NoteId == "preview"), Is.False);
                fixture.Judgement.ProcessFrame(500d);
                Assert.That(results.Count, Is.EqualTo(2));
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameManager Manager;
            public readonly NoteJudgementSystem Judgement;
            public readonly GameplaySessionState State;
            public readonly PlayableChartSnapshot Chart;
            private readonly GameObject root = new GameObject("Center Knob rule integration");
            private readonly GameRuleConfig config = ScriptableObject.CreateInstance<GameRuleConfig>();

            public Fixture(bool specialHard, bool addTap = false, ChartDocument document = null)
            {
                root.SetActive(false);
                var playback = root.AddComponent<GamePlay>();
                SetField(playback, "audioSource", root.AddComponent<AudioSource>());
                SetField(playback, "cameraTransform", root.transform);
                SetField(config, "gaugeType", specialHard ? HealthGaugeType.Hard : HealthGaugeType.Normal);
                SetField(config, "initialHealth", specialHard ? 1 : 100);
                if (specialHard)
                {
                    SetField(config, "useSpecialHardGauge", true);
                    SetField(config, "specialHardGauge", new SpecialHardGaugeSettings());
                }
                var rule = root.AddComponent<DefaultGameRule>();
                SetField(rule, "config", config);
                Manager = root.AddComponent<GameManager>();
                SetField(Manager, "gamePlay", playback);
                SetField(Manager, "gameRule", rule);
                Judgement = root.AddComponent<NoteJudgementSystem>();
                SetField(Judgement, "gameManager", Manager);
                SetField(Judgement, "gameRule", rule);
                State = root.AddComponent<GameplaySessionState>();
                SetField(State, "gameManager", Manager);
                SetField(State, "judgementSystem", Judgement);
                SetField(State, "gameRule", rule);
                playback.PlaybackStateChanged += (Action<PlaybackState>)Delegate.CreateDelegate(
                    typeof(Action<PlaybackState>), Judgement,
                    typeof(NoteJudgementSystem).GetMethod("HandlePlaybackStateChanged", Private));
                if (document == null)
                {
                    document = new ChartDocument(4800, 4, 120d);
                    var note = new ChartDocumentNote("knob", ChartNoteKind.Knob, 10, 0, 1200);
                    note.Points[0].KnobX = -1d;
                    note.Points[0].KnobDirection = ChartKnobDirection.Right;
                    note.Points[1].KnobX = 1d;
                    document.Notes.Add(note);
                    if (addTap) document.Notes.Add(new ChartDocumentNote("tap", ChartNoteKind.CenterTap, 10, 0));
                }
                Chart = RuntimeChartPackageCodec.Import(RuntimeChartPackageCodec.Export(
                    document, "test", "normal", "", "", 0d, 1d, null)).Snapshot;
                Assert.That(Chart, Is.Not.Null);
                Assert.That(State.TryConfigureChart(Chart.JudgementUnitCount, out string error), Is.True, error);
                Assert.That(Judgement.Initialize(Chart), Is.True);
                SetField(playback, "<State>k__BackingField", PlaybackState.Playing);
            }

            public void Queue(int lane, double time)
            {
                Type queued = typeof(NoteJudgementSystem).GetNestedType("QueuedInput", BindingFlags.NonPublic);
                object input = Activator.CreateInstance(queued, BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic, null, new object[] { lane, time, 0L, true }, null);
                ((IList)typeof(NoteJudgementSystem).GetField("pendingInputs", Private)
                    .GetValue(Judgement)).Add(input);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void SetField(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Private | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().FullName, name);
        }
    }
}

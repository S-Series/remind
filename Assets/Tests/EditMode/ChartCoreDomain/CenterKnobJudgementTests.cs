using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace REmind.Charting.Tests
{
    public sealed class CenterKnobJudgementTests
    {
        [TestCase(0)]
        [TestCase(1200)]
        [TestCase(2400)]
        public void SecondPoint_JumpsAtItsTimestampAndSurvivesPackageRoundTrip(int position)
        {
            var document = Document();
            document.Notes.Add(Knob("horizontal", (0, -0.5d, false),
                (position, -0.5d, false), (position, 0.5d, true), (2400, 0.5d, false)));
            // At a path boundary the pair occupies Start/Mid or Mid/End, never three points.
            if (position == 0) document.Notes[0].Points.RemoveAt(1);
            if (position == 2400)
            {
                document.Notes[0].Points.RemoveAt(3);
                document.Notes[0].Points[2].Kind = ChartNotePointKind.End;
            }
            var snapshot = Compile(document);
            double time = snapshot.TimingMap.TimeAtPosition(position);
            Assert.That(KnobPathEvaluator.TargetX(snapshot.Notes[0], snapshot.TimingMap, time - 0.001), Is.EqualTo(-1.25));
            Assert.That(KnobPathEvaluator.TargetX(snapshot.Notes[0], snapshot.TimingMap, time), Is.EqualTo(1.25));
            var imported = RuntimeChartPackageCodec.Import(Export(document)).Snapshot;
            Assert.That(imported.Notes[0].Points.Select(p => p.Position),
                Is.EqualTo(snapshot.Notes[0].Points.Select(p => p.Position)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Input(session, 9, time, true);
            Input(session, 9, time + 1, false);
            Advance(session, 1100);
            Assert.That(results.All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
            Assert.That(session.PendingNoteCount, Is.Zero);
        }

        [Test]
        public void SecondPoint_StandaloneHorizontalPathSupportsManualAndAutoPlay()
        {
            var document = Document();
            document.Notes.Add(Knob("instant", (0, -0.5d, false), (0, 0.5d, true)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Input(session, 9, 0d, true);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results.All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
            Assert.That(session.PlayerKnobX, Is.EqualTo(1.25));
            results.Clear();
            session = Session(document, results);
            session.ProcessAutomatic(0d, true);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(session.PlayerKnobX, Is.EqualTo(1.25));
            Assert.That(session.PendingNoteCount, Is.Zero);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void SecondPoint_RejectsLinearCoincidentOrThirdPoint(bool coincident, bool third)
        {
            var document = Document();
            var note = Knob("invalid", (0, -0.5d, false), (1200, -0.5d, false),
                (1200, coincident ? -0.5d : 0.5d, coincident || third), (2400, 0.5d, false));
            if (third) note.Points.Insert(3, new ChartNotePoint(1200, ChartNotePointKind.Mid)
                { KnobX = -0.25, KnobInterpolation = ChartKnobInterpolation.Step });
            document.Notes.Add(note);
            Assert.That(ChartCompiler.Compile(document).Succeeded, Is.False);
        }

        [TestCase(ChartKnobInterpolation.Sin)]
        [TestCase(ChartKnobInterpolation.Cos)]
        public void QuarterCurve_SharedTargetsPackageAndFramePartitionsAgree(ChartKnobInterpolation curve)
        {
            var document = Document();
            var note = Knob("curve", (0, -0.5d, false), (2400, 0.5d, false));
            note.Points[1].KnobInterpolation = curve;
            document.Notes.Add(note);
            var snapshot = Compile(document);
            double middle = curve == ChartKnobInterpolation.Sin ? Math.Sin(Math.PI / 4) : 1 - Math.Cos(Math.PI / 4);
            Assert.That(KnobPathEvaluator.TargetX(snapshot.Notes[0], snapshot.TimingMap, 500),
                Is.EqualTo((-0.5 + middle) * 2.5).Within(0.000001));
            Assert.That(KnobPathEvaluator.TargetX(snapshot.Notes[0], snapshot.TimingMap, 0), Is.EqualTo(-1.25));
            Assert.That(KnobPathEvaluator.TargetX(snapshot.Notes[0], snapshot.TimingMap, 1000), Is.EqualTo(1.25));
            var imported = RuntimeChartPackageCodec.Import(Export(document)).Snapshot;
            Assert.That(imported.Notes[0].Points[1].KnobInterpolation, Is.EqualTo(curve));
            var coarseResults = new List<ChartJudgementResolution>();
            var fineResults = new List<ChartJudgementResolution>();
            var coarse = Session(document, coarseResults);
            var fine = Session(document, fineResults);
            foreach (var session in new[] { coarse, fine }) Input(session, 9, 0, true);
            for (int time = 10; time <= 1000; time += 10)
                Input(fine, 9, time, time < 400 || time >= 700);
            Input(coarse, 9, 400, false);
            Input(coarse, 9, 700, true);
            Advance(coarse, 1000);
            Assert.That(fineResults.Select(r => r.Grade), Is.EqualTo(coarseResults.Select(r => r.Grade)));
            Assert.That(fine.PlayerKnobX, Is.EqualTo(coarse.PlayerKnobX).Within(0.000001));
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void CenterTapAcceptsEachIndependentFreshInput(int lane)
        {
            var document = Document();
            document.Notes.Add(new ChartDocumentNote("tap", ChartNoteKind.CenterTap, 10, 0));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            session.Input(lane, 0d, true);
            session.Input(lane, 0d, true);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
        }

        [Test]
        public void OppositeFreshTapWorksWhileFirstSideRemainsHeld()
        {
            var document = Document();
            document.Notes.Add(new ChartDocumentNote("first", ChartNoteKind.CenterTap, 10, 0));
            document.Notes.Add(new ChartDocumentNote("second", ChartNoteKind.CenterTap, 10, 480));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Input(session, 8, 0d, true);
            Input(session, 8, 200d, true);
            Assert.That(results.Count, Is.EqualTo(1));
            Input(session, 9, 200d, true);
            Assert.That(results.Count, Is.EqualTo(2));
        }

        [Test]
        public void FirstFlatAndNoInputZeroPathScorePerfectPlusWithoutEntryPress()
        {
            var document = Document();
            document.Notes.Add(Knob("zero", (0, 0d, false), (2400, 0d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Advance(session, 1000d);
            Assert.That(results.Count, Is.EqualTo(11));
            Assert.That(results.All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
            Assert.That(session.PendingNoteCount, Is.Zero);
            Assert.That(session.PlayerKnobX, Is.Zero);
        }

        [Test]
        public void MissedDiagonalDoesNotPreventLaterZeroFlatFromScoring()
        {
            var document = Document();
            document.Notes.Add(Knob("return", (0, 0d, false), (1200, 1d, false),
                (2400, 0d, false), (3600, 0d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Advance(session, 1500d);
            Assert.That(results.Single(r => r.TargetTimeMs == 500d).Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(results.Where(r => r.TargetTimeMs >= 1000d)
                .All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
            Assert.That(session.PlayerKnobX, Is.Zero);
        }

        [Test]
        public void StartsInitializeXAndGapsPreserveActualValueUntilNextStartOrReset()
        {
            var document = Document();
            document.Notes.Add(Knob("first", (0, -0.4d, false), (1200, -0.4d, false)));
            document.Notes.Add(Knob("second", (2400, 0.6d, false), (3600, 0.6d, false)));
            var session = Session(document, new List<ChartJudgementResolution>());
            Advance(session, 0d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(-1d));
            Input(session, 9, 100d, true);
            Advance(session, 800d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(-1d));
            Advance(session, 1000d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(1.5d));
            session.Reset();
            Assert.That(session.PlayerKnobX, Is.Zero);
        }

        [Test]
        public void CorrectDiagonalTracksAndReleaseKeepsActualPosition()
        {
            var document = Document();
            document.Notes.Add(Knob("diagonal", (0, -1d, false), (2400, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Input(session, 9, 0d, true);
            Advance(session, 400d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(-0.5d).Within(0.000001d));
            Input(session, 9, 400d, false);
            Advance(session, 1000d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(-0.5d).Within(0.000001d));
            Assert.That(results.Single(r => r.TargetTimeMs == 500d).Grade, Is.EqualTo(ChartJudgementGrade.Miss));
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void AnyKeyCannotDislodgeAnAlignedFlat(int lane)
        {
            var document = Document();
            document.Notes.Add(Knob("flat", (0, 0d, false), (2400, 0d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Input(session, lane, 0d, true);
            Advance(session, 1000d);
            Assert.That(session.PlayerKnobX, Is.Zero);
            Assert.That(results.All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
        }

        [Test]
        public void BothKeysAreNeutralAndSpaceNeverMovesDiagonal()
        {
            var document = Document();
            document.Notes.Add(Knob("diagonal", (0, 0d, false), (2400, 1d, false)));
            var session = Session(document, new List<ChartJudgementResolution>());
            session.Input(8, 0d, true);
            session.Input(9, 0d, true);
            Input(session, 10, 0d, true);
            Advance(session, 500d);
            Assert.That(session.PlayerKnobX, Is.Zero);
            Input(session, 8, 500d, false);
            Advance(session, 600d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(0.5d).Within(0.000001d));
        }

        [Test]
        public void OutsideRangeMovesAtFixedSpeedThenReacquiresPath()
        {
            var document = Document();
            document.Notes.Add(Knob("diagonal", (0, 0d, false), (2400, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Advance(session, 500d);
            Input(session, 9, 500d, true);
            Advance(session, 600d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(0.5d).Within(0.000001d));
            Advance(session, 1000d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(2.5d).Within(0.000001d));
            Assert.That(results.Last().Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
        }

        [Test]
        public void ManualMovementClampsAtPhysicalBounds()
        {
            var document = Document();
            document.Notes.Add(Knob("diagonal", (0, 0d, false), (4800, 1d, false)));
            var session = Session(document, new List<ChartJudgementResolution>());
            Input(session, 8, 0d, true);
            Advance(session, 2000d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(-2.5d));
        }

        [TestCase(100d, ChartJudgementGrade.PerfectPlus)]
        [TestCase(100.001d, ChartJudgementGrade.Miss)]
        public void SpatialToleranceIncludesExactlyQuarterUnit(double duration, ChartJudgementGrade expected)
        {
            var document = Document();
            document.Notes.Add(Knob("range", (0, 0d, false), (2400, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            // A release after following the path leaves exactly the requested spatial error at tick 200.
            Input(session, 9, 0d, true);
            Input(session, 9, 200d - duration, false);
            Advance(session, 200d);
            Assert.That(results.Last().Grade, Is.EqualTo(expected));
        }

        [TestCase(920d, true)]
        [TestCase(919.999d, false)]
        [TestCase(1080d, true)]
        [TestCase(1080.001d, false)]
        public void StepWindowIncludesBothEightyMillisecondEdges(double inputTime, bool accepts)
        {
            var document = Document();
            document.Notes.Add(Knob("step", (0, 0d, false), (2400, 1d, true), (3600, 1d, false)));
            var session = Session(document, new List<ChartJudgementResolution>());
            Input(session, 9, inputTime, true);
            Assert.That(session.PlayerKnobX, Is.EqualTo(accepts ? 2.5d : 0d).Within(0.000001d));
        }

        [Test]
        public void StepNeedsFreshCorrectDirectionAndSpaceCannotSnap()
        {
            var document = Document();
            document.Notes.Add(Knob("step", (0, 0d, false), (2400, 1d, true), (3600, 1d, false)));
            var session = Session(document, new List<ChartJudgementResolution>());
            Input(session, 9, 800d, true);
            Input(session, 9, 950d, true);
            Input(session, 10, 950d, true);
            Assert.That(session.PlayerKnobX, Is.Zero);
            Input(session, 9, 950d, false);
            Input(session, 8, 950d, true);
            Assert.That(session.PlayerKnobX, Is.Zero);
            Input(session, 9, 950d, true);
            Assert.That(session.PlayerKnobX, Is.EqualTo(2.5d));
        }

        [Test]
        public void PreStartStepSnapsImmediatelyKeepsOwnerAndResolvesOnOriginalTimes()
        {
            var document = Document();
            document.Notes.Add(Knob("preview", (240, 0d, false), (360, 1d, true), (1200, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Advance(session, 69d);
            Assert.That(session.PlayerKnobNoteId, Is.Null);
            Input(session, 9, 70d, true);
            Assert.That(session.PlayerKnobX, Is.EqualTo(2.5d));
            Assert.That(session.PlayerKnobNoteId, Is.EqualTo("preview"));
            Assert.That(results, Is.Empty);
            Advance(session, 100d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(2.5d));
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].TargetTimeMs, Is.EqualTo(100d));
            Assert.That(results[0].EvaluationTimeMs, Is.EqualTo(100d));
            Advance(session, 500d);
            Assert.That(results.Single(r => r.TargetKind == JudgementTargetKind.KnobTurn).EvaluationTimeMs,
                Is.EqualTo(150d));
            Assert.That(results.All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
            Assert.That(results.Count, Is.EqualTo(Compile(document).JudgementUnitCount));
            session.Reset();
            Assert.That(session.PlayerKnobNoteId, Is.Null);
            Assert.That(session.PlayerKnobX, Is.Zero);
        }

        [TestCase("too-early")]
        [TestCase("wrong-direction")]
        [TestCase("space")]
        [TestCase("not-fresh")]
        public void InvalidPreStartInputCannotSelectFutureCursorOrReplaceStart(string invalid)
        {
            var document = Document();
            document.Notes.Add(Knob("preview", (240, -0.4d, false), (360, 1d, true), (1200, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            if (invalid == "not-fresh") Input(session, 9, 0d, true);
            Input(session, invalid == "wrong-direction" ? 8 : invalid == "space" ? 10 : 9,
                invalid == "too-early" ? 69.999d : 70d, true);
            Assert.That(session.PlayerKnobNoteId, Is.Null);
            Assert.That(session.PlayerKnobX, Is.Zero);
            Advance(session, 100d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(-1d));
            Assert.That(session.PlayerKnobNoteId, Is.EqualTo("preview"));
            Assert.That(results[0].Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
        }

        [Test]
        public void PreStartStepDoesNotCompensateAnotherNoteAndDoesNotReplayItsScore()
        {
            var document = Document();
            document.Notes.Add(Knob("previous", (0, 0d, false), (240, 0d, false)));
            document.Notes.Add(Knob("preview", (240, 0d, false), (360, 1d, true), (1200, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Input(session, 9, 70d, true);
            Input(session, 9, 75d, false);
            Input(session, 9, 75d, true);
            Advance(session, 150d);
            Assert.That(results.Single(r => r.Note.Id == "previous" && r.TargetTimeMs == 100d).Grade,
                Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(results.Where(r => r.Note.Id == "preview")
                .All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
            Assert.That(results.Count(r => r.TargetKind == JudgementTargetKind.KnobTurn), Is.EqualTo(1));
            Assert.That(session.PlayerKnobX, Is.EqualTo(2.5d));
            Assert.That(session.PlayerKnobNoteId, Is.EqualTo("preview"));
        }

        [Test]
        public void PreStartSnapAlsoFulfillsStartWhenStepFollowsAnIntermediatePoint()
        {
            var document = Document();
            document.Notes.Add(Knob("preview", (240, 0d, false), (288, 0.5d, false),
                (360, 1d, true), (1200, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Input(session, 9, 70d, true);
            Advance(session, 150d);
            Assert.That(results.All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
            Assert.That(results.Count, Is.EqualTo(2));
        }

        [Test]
        public void OverlappingWindowsConsumeOneDownForEarliestEligibleStepOnly()
        {
            var document = Document();
            document.Notes.Add(Knob("previous", (0, 0d, false), (240, 1d, true)));
            document.Notes.Add(Knob("preview", (240, 0d, false), (360, 1d, true), (1200, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Input(session, 9, 70d, true);
            Assert.That(session.PlayerKnobNoteId, Is.EqualTo("previous"));
            Advance(session, 150d);
            Assert.That(results.Single(r => r.Note.Id == "previous" && r.TargetTimeMs == 100d).Grade,
                Is.EqualTo(ChartJudgementGrade.PerfectPlus));
            Assert.That(results.Single(r => r.Note.Id == "preview" && r.TargetTimeMs == 150d).Grade,
                Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(session.PlayerKnobNoteId, Is.EqualTo("preview"));
        }

        [Test]
        public void EarlyStepReservesOnlyPendingPreviousSpanAtOriginalTimes()
        {
            var document = Document();
            document.Notes.Add(Knob("step", (0, 0d, false), (1920, -1d, false),
                (2520, 1d, true), (3600, 0d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Advance(session, 900d);
            Input(session, 9, 970d, true);
            Assert.That(results.Last().TargetTimeMs, Is.EqualTo(900d));
            Assert.That(session.PlayerKnobX, Is.EqualTo(2.5d));
            session.UpdateKnobDisplayTime(1049d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(2.5d), "Early snap must not attach to the previous span.");
            Advance(session, 1500d);
            Assert.That(results.Single(r => r.TargetTimeMs == 900d).Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            var reserved = results.Single(r => r.TargetTimeMs == 1000d);
            Assert.That(reserved.Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
            Assert.That(reserved.EvaluationTimeMs, Is.EqualTo(1000d));
            Assert.That(results.Single(r => r.TargetTimeMs == 1050d).Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
            Assert.That(results.Last().Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(results.Count, Is.EqualTo(Compile(document).JudgementUnitCount));
        }

        [Test]
        public void LateStepDoesNotRestorePastMissAndRepeatedPressCannotScoreAgain()
        {
            var document = Document();
            document.Notes.Add(Knob("step", (0, 0d, false), (2400, 1d, true), (3600, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Advance(session, 1000d);
            Input(session, 9, 1080d, true);
            Input(session, 9, 1090d, false);
            Input(session, 9, 1090d, true);
            Advance(session, 1500d);
            Assert.That(results.Single(r => r.TargetTimeMs == 1000d).Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(results.Single(r => r.TargetTimeMs == 1100d).Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
            Assert.That(results.Count(r => r.TargetKind == JudgementTargetKind.KnobTurn), Is.EqualTo(1));
        }

        [Test]
        public void StepDownHasPriorityOverCentralTapAndSecondIndependentDownCanHitTap()
        {
            var document = Document();
            document.Notes.Add(Knob("step", (0, 0d, false), (2400, 1d, true), (3600, 1d, false)));
            document.Notes.Add(new ChartDocumentNote("tap", ChartNoteKind.CenterTap, 10, 2400));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Before(session, 1000d);
            session.Input(9, 1000d, true);
            Assert.That(results.Any(r => r.Note.Kind == ChartNoteKind.CenterTap), Is.False);
            session.Input(10, 1000d, true);
            Advance(session, 1000d);
            Assert.That(results.Count(r => r.Note.Kind == ChartNoteKind.CenterTap), Is.EqualTo(1));
            Assert.That(results.Count(r => r.TargetKind == JudgementTargetKind.KnobTurn), Is.EqualTo(1));
        }

        [Test]
        public void AdjacentNotesScorePriorEndBeforeResettingTheVisiblePosition()
        {
            var document = Document();
            document.Notes.Add(Knob("a", (0, -1d, false), (1200, -1d, false)));
            document.Notes.Add(Knob("b", (1200, 1d, false), (2400, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Advance(session, 1000d);
            Assert.That(results.All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
            Assert.That(session.PlayerKnobX, Is.EqualTo(2.5d));
        }

        [Test]
        public void StepAtTouchingEndDoesNotOverwriteNextStartPosition()
        {
            var document = Document();
            document.Notes.Add(Knob("a", (0, 0d, false), (1200, 1d, true)));
            document.Notes.Add(Knob("b", (1200, -1d, false), (2400, -1d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Input(session, 9, 500d, true);
            Assert.That(results.All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
            Assert.That(session.PlayerKnobX, Is.EqualTo(-2.5d));
        }

        [Test]
        public void LateEndStepCanSnapWithinWindowWithoutRestoringFinalMiss()
        {
            var document = Document();
            document.Notes.Add(Knob("end", (0, 0d, false), (1200, 1d, true)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Advance(session, 500d);
            Input(session, 9, 580d, true);
            Advance(session, 1000d);
            Assert.That(results.Last().Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(results.Count, Is.EqualTo(6));
            Assert.That(session.PlayerKnobX, Is.EqualTo(2.5d));
        }

        [Test]
        public void ProjectionDoesNotCommitRenderWatermarkOrChangeInputReplay()
        {
            var document = Document();
            document.Notes.Add(Knob("curve", (0, 0d, false), (2400, 1d, false)));
            var session = Session(document, new List<ChartJudgementResolution>());
            Advance(session, 0d);
            session.UpdateKnobDisplayTime(90d);
            Assert.That(session.PlayerKnobX, Is.Zero);
            session.Input(9, 50d, true);
            Assert.That(session.PlayerKnobX, Is.EqualTo(0.225d).Within(0.000001d));
            Advance(session, 100d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(0.25d).Within(0.000001d));
        }

        [Test]
        public void PauseResumeKeepsSongTimeAndSynchronizesHeldWithoutFreshSnap()
        {
            var document = Document();
            document.Notes.Add(Knob("curve", (0, -1d, false), (2400, 1d, false)));
            var session = Session(document, new List<ChartJudgementResolution>());
            Input(session, 9, 0d, true);
            Advance(session, 400d);
            session.BreakReleasedHolds(_ => false, 400d);
            session.UpdateKnobDisplayTime(400d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(-0.5d).Within(0.000001d));
            Advance(session, 600d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(-0.5d).Within(0.000001d));
        }

        [Test]
        public void PositionLinearUsesSharedTimingAcrossBpmAndScrollChanges()
        {
            var document = Document();
            document.TimingEvents.Add(new ChartTimingEvent(1200, 240d));
            document.LineSpeedEvents.Add(new ChartLineSpeedEvent(600, 2d));
            document.Notes.Add(Knob("curve", (0, -1d, false), (2400, 1d, false)));
            var chart = Compile(document);
            Assert.That(KnobPathEvaluator.TargetX(chart.Notes[0], chart.TimingMap, 250d),
                Is.EqualTo(-1.25d).Within(0.000001d));
            Assert.That(KnobPathEvaluator.TargetX(chart.Notes[0], chart.TimingMap, 625d),
                Is.EqualTo(1.25d).Within(0.000001d));
            var session = Session(document, new List<ChartJudgementResolution>());
            Input(session, 9, 0d, true);
            Advance(session, 625d);
            Assert.That(session.PlayerKnobX, Is.EqualTo(1.25d).Within(0.000001d));
        }

        [Test]
        public void FineAndCoarseRenderFramesHaveIdenticalScoresAndActualPosition()
        {
            Assert.That(Replay(7d), Is.EqualTo(Replay(10000d)));
        }

        [Test]
        public void HaltInMissCallbackStopsRemainingBatchAndResetStartsFresh()
        {
            var document = Document();
            document.Notes.Add(Knob("curve", (0, 0d, false), (2400, 1d, false)));
            var results = new List<ChartJudgementResolution>();
            PlayableJudgementSession session = null;
            session = new PlayableJudgementSession(Compile(document), Judge, (_, __) => 100d,
                r => { results.Add(r); if (r.Grade == ChartJudgementGrade.Miss) session.Halt(); });
            session.ProcessAutomatic(1000d, false);
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(session.NextAutomaticTime(false), Is.EqualTo(double.PositiveInfinity));
            double stopped = session.PlayerKnobX;
            session.Input(9, 2000d, true);
            session.UpdateKnobDisplayTime(2000d, true);
            Assert.That(session.PlayerKnobX, Is.EqualTo(stopped));
            session.Reset();
            Assert.That(session.PlayerKnobX, Is.Zero);
            session.ProcessAutomatic(1000d, true);
            Assert.That(results.Last().Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
            Assert.That(session.PendingNoteCount, Is.Zero);
        }

        [Test]
        public void RuntimeV2RoundTripsStepAndAbsentInterpolationDefaultsToLinear()
        {
            var document = Document();
            document.Notes.Add(Knob("step", (0, 0d, false), (2400, 1d, true)));
            string json = Export(document);
            StringAssert.Contains("\"Version\":2", json);
            var chart = RuntimeChartPackageCodec.Import(json).Snapshot;
            Assert.That(chart.Notes[0].Points[1].KnobInterpolation, Is.EqualTo(ChartKnobInterpolation.Step));
            chart = RuntimeChartPackageCodec.Import(json.Replace(",\"KnobInterpolation\":1", "")
                .Replace("\"KnobInterpolation\":1,", "")).Snapshot;
            Assert.That(chart.Notes[0].Points[1].KnobInterpolation, Is.EqualTo(ChartKnobInterpolation.Linear));
            Assert.Throws<FormatException>(() => RuntimeChartPackageCodec.Import(
                json.Replace("\"Version\":2", "\"Version\":1")));
        }

        [TestCase("x")]
        [TestCase("interpolation")]
        [TestCase("start-step")]
        [TestCase("overlap")]
        [TestCase("direction")]
        public void InvalidPathsStillFailValidation(string invalid)
        {
            var document = Document();
            var note = Knob("curve", (0, -1d, false), (2400, 1d, false));
            if (invalid == "x") note.Points[1].KnobX = 1.001d;
            if (invalid == "interpolation") note.Points[1].KnobInterpolation = (ChartKnobInterpolation)5;
            if (invalid == "start-step") note.Points[0].KnobInterpolation = ChartKnobInterpolation.Step;
            if (invalid == "direction") note.Points[0].KnobDirection = ChartKnobDirection.Left;
            if (invalid == "overlap") document.Notes.Add(Knob("other", (0, 0d, false), (2400, 1d, false)));
            document.Notes.Add(note);
            Assert.That(ChartCompiler.Compile(document).Succeeded, Is.False);
        }

        [Test]
        public void LegacyV1ScratchLanesRetainIndependentInputAndNoKnobFields()
        {
            var document = Document();
            document.Notes.Add(new ChartDocumentNote("left", ChartNoteKind.Scratch, 8, 0));
            document.Notes.Add(new ChartDocumentNote("right", ChartNoteKind.Scratch, 9, 0));
            string json = Export(document);
            StringAssert.Contains("\"Version\":1", json);
            StringAssert.DoesNotContain("KnobX", json);
            StringAssert.DoesNotContain("KnobInterpolation", json);
            Assert.That(RuntimeChartPackageCodec.Import(json).Snapshot.JudgementUnitCount, Is.EqualTo(2));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            session.Input(8, 0d, true);
            session.Input(9, 0d, true);
            Assert.That(results.Count, Is.EqualTo(2));
        }

        private static string Replay(double frame)
        {
            var document = Document();
            document.TimingEvents.Add(new ChartTimingEvent(1200, 240d));
            document.Notes.Add(Knob("curve", (0, 0d, false), (1920, -1d, false),
                (3120, 1d, true), (4800, 0d, false)));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            double[] times = { 0d, 230d, 550d, 840d, 1000d, 1200d };
            int[] lanes = { 8, 8, 9, 9, 8, 8 };
            bool[] down = { true, false, true, false, true, false };
            double last = 0d;
            for (int index = 0; index < times.Length; index++)
            {
                for (double time = last + frame; time < times[index]; time += frame)
                    Advance(session, time);
                Input(session, lanes[index], times[index], down[index]);
                last = times[index];
            }
            Advance(session, 2000d);
            return string.Join("|", results.Select(r => r.TargetTimeMs.ToString("R") + ":" + r.Grade)) +
                "/" + Math.Round(session.PlayerKnobX, 8);
        }

        private static ChartDocument Document() => new ChartDocument(4800, 4, 120d);
        private static ChartDocumentNote Knob(string id, params (int position, double x, bool step)[] points)
        {
            var note = new ChartDocumentNote(id, ChartNoteKind.Knob, 10, points[0].position,
                points[points.Length - 1].position);
            note.Points.Clear();
            for (int index = 0; index < points.Length; index++)
                note.Points.Add(new ChartNotePoint(points[index].position,
                    index == 0 ? ChartNotePointKind.Start : index == points.Length - 1
                        ? ChartNotePointKind.End : ChartNotePointKind.Mid)
                    { KnobX = points[index].x, KnobInterpolation = points[index].step
                        ? ChartKnobInterpolation.Step : ChartKnobInterpolation.Linear });
            return note;
        }
        private static PlayableChartSnapshot Compile(ChartDocument document)
        {
            var result = ChartCompiler.Compile(document);
            Assert.That(result.Succeeded, Is.True, string.Join(", ", result.Issues.Select(i => i.Code)));
            return result.Snapshot;
        }
        private static ChartJudgementGrade Judge(PlayableNoteSnapshot note, int point, double offset) =>
            Math.Abs(offset) <= 100d ? ChartJudgementGrade.PerfectPlus : ChartJudgementGrade.None;
        private static PlayableJudgementSession Session(ChartDocument document,
            List<ChartJudgementResolution> results) =>
            new PlayableJudgementSession(Compile(document), Judge, (_, __) => 100d, results.Add);
        private static string Export(ChartDocument document) =>
            RuntimeChartPackageCodec.Export(document, "song", "hard", "", "", 0d, 1d, null);
        private static void Before(PlayableJudgementSession session, double time)
        {
            while (session.NextAutomaticTime(false) < time)
                session.ProcessAutomatic(session.NextAutomaticTime(false), false);
        }
        private static void Input(PlayableJudgementSession session, int lane, double time, bool pressed)
        {
            Before(session, time);
            session.Input(lane, time, pressed);
            Advance(session, time);
        }
        private static void Advance(PlayableJudgementSession session, double time)
        {
            while (session.NextAutomaticTime(false) <= time)
                session.ProcessAutomatic(session.NextAutomaticTime(false), false);
            session.UpdateKnobDisplayTime(time);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace REmind.Charting.Tests
{
    public sealed class CenterHoldJudgementTests
    {
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void EachCentralInputCanStartAndFinishWithoutUpOrTicks(int lane)
        {
            var results = new List<ChartJudgementResolution>();
            var session = Session(Document(), results);
            session.Input(lane, 0d, true);
            Assert.That(results, Is.Empty);
            Assert.That(session.NextAutomaticTime(false), Is.EqualTo(1000d));
            Advance(session, 1000d);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
            Assert.That(results[0].TargetKind, Is.EqualTo(JudgementTargetKind.HoldEnd));
            Assert.That(results[0].IsNoteComplete, Is.True);
            Assert.That(results[0].EvaluationTimeMs, Is.EqualTo(1000d));
            Assert.That(session.PendingNoteCount, Is.Zero);
        }

        [TestCase(-175d, ChartJudgementGrade.Miss)]
        [TestCase(-130.001d, ChartJudgementGrade.Miss)]
        [TestCase(-130d, ChartJudgementGrade.Great)]
        [TestCase(-80d, ChartJudgementGrade.Perfect)]
        [TestCase(-40d, ChartJudgementGrade.PerfectPlus)]
        [TestCase(40d, ChartJudgementGrade.PerfectPlus)]
        [TestCase(80d, ChartJudgementGrade.Perfect)]
        [TestCase(130d, ChartJudgementGrade.Great)]
        [TestCase(175d, ChartJudgementGrade.Miss)]
        public void EndPreservesStartGrade(double offset, ChartJudgementGrade grade)
        {
            var document = Document(2400, 4800);
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            session.Input(8, 1000d + offset, true);
            Advance(session, 2000d);
            Assert.That(results.Single().Grade, Is.EqualTo(grade));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SameTimestampSwapDoesNotDependOnUpDownOrder(bool upFirst)
        {
            var results = new List<ChartJudgementResolution>();
            var session = Session(Document(), results);
            session.Input(8, 0d, true);
            session.Input(upFirst ? 8 : 9, 500d, !upFirst);
            session.Input(upFirst ? 9 : 8, 500d, upFirst);
            Advance(session, 1000d);
            Assert.That(results.Single().Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
        }

        [Test]
        public void ReleasingOneOfTwoHeldKeysKeepsUnionHeld()
        {
            var results = new List<ChartJudgementResolution>();
            var session = Session(Document(), results);
            session.Input(8, 0d, true);
            session.Input(9, 0d, true);
            session.Input(8, 500d, false);
            Advance(session, 1000d);
            Assert.That(results.Single().Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
        }

        [TestCase(0.001d)]
        [TestCase(1d)]
        public void PositiveDurationReleaseCannotRejoin(double gap)
        {
            var results = new List<ChartJudgementResolution>();
            var session = Session(Document(), results);
            session.Input(8, 0d, true);
            session.Input(8, 500d, false);
            session.Input(9, 500d + gap, true);
            Advance(session, 1000d);
            Assert.That(results.Single().Grade, Is.EqualTo(ChartJudgementGrade.Miss));
        }

        [TestCase(1000d, ChartJudgementGrade.PerfectPlus)]
        [TestCase(999.999d, ChartJudgementGrade.Miss)]
        public void ReleaseAtEndIsAllowedButAnyEarlierGapFails(double release, ChartJudgementGrade grade)
        {
            var results = new List<ChartJudgementResolution>();
            var session = Session(Document(), results);
            session.Input(8, 0d, true);
            session.Input(8, release, false);
            Advance(session, 1000d);
            Assert.That(results.Single().Grade, Is.EqualTo(grade));
        }

        [Test]
        public void HoldingBeforeStartWindowDoesNotReplaceFreshStartDown()
        {
            var results = new List<ChartJudgementResolution>();
            var session = Session(Document(2400, 4800), results);
            session.Input(8, 0d, true);
            session.Input(8, 1000d, true);
            Advance(session, 2000d);
            Assert.That(results.Single().Grade, Is.EqualTo(ChartJudgementGrade.Miss));
        }

        [Test]
        public void KnobStepConsumesSwapDownButDoesNotBreakAlreadyJoinedHold()
        {
            var document = Document();
            document.Notes.Add(StepKnob());
            document.Notes.Add(new ChartDocumentNote("tap", ChartNoteKind.CenterTap, 10, 1200));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            session.Input(8, 0d, true);
            Advance(session, 0d);
            Before(session, 500d);
            session.Input(8, 500d, false);
            session.Input(9, 500d, true);
            Assert.That(results.Any(r => r.Note.Kind == ChartNoteKind.CenterTap), Is.False);
            Advance(session, 1000d);
            Assert.That(results.Single(r => r.Note.Kind == ChartNoteKind.CenterHold).Grade,
                Is.EqualTo(ChartJudgementGrade.PerfectPlus));
            Assert.That(results.Single(r => r.TargetKind == JudgementTargetKind.KnobTurn).Grade,
                Is.EqualTo(ChartJudgementGrade.PerfectPlus));
        }

        [TestCase(false, ChartJudgementGrade.Miss)]
        [TestCase(true, ChartJudgementGrade.PerfectPlus)]
        public void StepDownCannotAlsoStartHoldButSecondIndependentDownCan(bool secondDown, ChartJudgementGrade grade)
        {
            var document = Document(1200, 2400);
            document.Notes.Add(StepKnob());
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            Before(session, 500d);
            session.Input(9, 500d, true);
            if (secondDown) session.Input(10, 500d, true);
            Advance(session, 1000d);
            Assert.That(results.Single(r => r.Note.Kind == ChartNoteKind.CenterHold).Grade, Is.EqualTo(grade));
        }

        [TestCase(false, ChartJudgementGrade.Miss)]
        [TestCase(true, ChartJudgementGrade.PerfectPlus)]
        public void ResumeReadsUnionRatherThanOriginalStartingKey(bool held, ChartJudgementGrade grade)
        {
            var results = new List<ChartJudgementResolution>();
            var session = Session(Document(), results);
            session.Input(8, 0d, true);
            session.BreakReleasedHolds(lane => held && lane == 9, 500d);
            Advance(session, 1000d);
            Assert.That(results.Single().Grade, Is.EqualTo(grade));
        }

        [Test]
        public void ResumeClearsSameTimePendingReleaseWhenAnotherKeyIsActuallyHeld()
        {
            var results = new List<ChartJudgementResolution>();
            var session = Session(Document(), results);
            session.Input(8, 0d, true);
            session.Input(8, 500d, false);
            session.BreakReleasedHolds(lane => lane == 9, 500d);
            Advance(session, 1000d);
            Assert.That(results.Single().Grade, Is.EqualTo(ChartJudgementGrade.PerfectPlus));
        }

        [TestCase(false, ChartJudgementGrade.Miss)]
        [TestCase(true, ChartJudgementGrade.PerfectPlus)]
        public void ResetClearsHeldJoinedAndPendingReleaseState(bool newDown, ChartJudgementGrade grade)
        {
            var results = new List<ChartJudgementResolution>();
            var session = Session(Document(), results);
            session.Input(8, 0d, true);
            session.Input(8, 500d, false);
            session.Reset();
            if (newDown) session.Input(9, 0d, true);
            Advance(session, 1000d);
            Assert.That(results.Single().Grade, Is.EqualTo(grade));
        }

        [Test]
        public void HaltOnFirstEndMissStopsSameTimeSuccessfulHoldAndRetryStartsFresh()
        {
            var document = Document();
            document.Notes.Add(new ChartDocumentNote("second", ChartNoteKind.CenterHold, 10, 480, 2400));
            var results = new List<ChartJudgementResolution>();
            PlayableJudgementSession session = null;
            session = new PlayableJudgementSession(Compile(document), Judge, (_, __) => 175d,
                r => { results.Add(r); if (r.Grade == ChartJudgementGrade.Miss) session.Halt(); });
            session.Input(8, 0d, true);
            session.Input(8, 100d, false);
            session.Input(9, 200d, true);
            session.ProcessAutomatic(1000d, false);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Note.Id, Is.EqualTo("hold"));
            Assert.That(session.NextAutomaticTime(false), Is.EqualTo(double.PositiveInfinity));
            session.Reset();
            session.ProcessAutomatic(1000d, true);
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results.Skip(1).All(r => r.Grade == ChartJudgementGrade.PerfectPlus), Is.True);
        }

        [Test]
        public void V2RoundTripsCenterHoldWithoutAddingPointFieldsAndV1RejectsIt()
        {
            string json = Export(Document());
            StringAssert.Contains("\"Version\":2", json);
            StringAssert.DoesNotContain("KnobX", json);
            var chart = RuntimeChartPackageCodec.Import(json).Snapshot;
            Assert.That(chart.Notes[0].Kind, Is.EqualTo(ChartNoteKind.CenterHold));
            Assert.That(chart.Notes[0].Lane, Is.EqualTo(10));
            Assert.That(chart.Notes[0].Points.Count, Is.EqualTo(2));
            Assert.That(chart.JudgementTargets.Count, Is.EqualTo(2));
            Assert.That(chart.JudgementUnitCount, Is.EqualTo(1));
            Assert.Throws<FormatException>(() => RuntimeChartPackageCodec.Import(
                json.Replace("\"Version\":2", "\"Version\":1")));
        }

        [TestCase("side-lane")]
        [TestCase("legacy-hold-center")]
        [TestCase("middle")]
        [TestCase("no-end")]
        [TestCase("zero-length")]
        [TestCase("motion")]
        public void InvalidCentralLongContractsAreRejected(string invalid)
        {
            var document = Document();
            var note = document.Notes[0];
            if (invalid == "side-lane") note.Lane = 8;
            if (invalid == "legacy-hold-center") note.Kind = ChartNoteKind.Hold;
            if (invalid == "middle") note.Points.Insert(1, new ChartNotePoint(1200, ChartNotePointKind.Mid));
            if (invalid == "no-end") note.EndPosition = null;
            if (invalid == "zero-length") note.EndPosition = note.Points[1].Position = 0;
            if (invalid == "motion") note.Points[0].Motion = ChartScratchMotionKind.Instant;
            Assert.That(ChartCompiler.Compile(document).Succeeded, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CentralTapAtHoldStartIsRejectedRegardlessOfDocumentOrder(bool tapFirst)
        {
            var document = Document();
            var tap = new ChartDocumentNote("tap", ChartNoteKind.CenterTap, 10, 0);
            if (tapFirst) document.Notes.Insert(0, tap);
            else document.Notes.Add(tap);
            var result = ChartCompiler.Compile(document);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues.Any(i => i.Code == "NOTE_LANE_TIME_COLLISION"), Is.True);
        }

        [Test]
        public void TapDuringCentralHoldAndKnobAtStartRemainValid()
        {
            var document = Document();
            document.Notes.Add(new ChartDocumentNote("tap", ChartNoteKind.CenterTap, 10, 1200));
            document.Notes.Add(StepKnob());
            var chart = Compile(document);
            Assert.That(chart.Notes.Count, Is.EqualTo(3));
        }

        [Test]
        public void LegacyV1ScratchSidesAndMainHoldStillRequireTheirOwnRelease()
        {
            var document = new ChartDocument(4800, 4, 120d);
            document.Notes.Add(new ChartDocumentNote("left", ChartNoteKind.Scratch, 8, 0));
            document.Notes.Add(new ChartDocumentNote("right", ChartNoteKind.Scratch, 9, 0));
            document.Notes.Add(new ChartDocumentNote("main", ChartNoteKind.Hold, 0, 0, 2400));
            string json = Export(document);
            StringAssert.Contains("\"Version\":1", json);
            Assert.That(RuntimeChartPackageCodec.Import(json).Snapshot.JudgementUnitCount, Is.EqualTo(3));
            var results = new List<ChartJudgementResolution>();
            var session = Session(document, results);
            session.Input(8, 0d, true);
            session.Input(9, 0d, true);
            session.Input(0, 0d, true);
            session.ProcessAutomatic(1000d, false);
            Assert.That(results.Count, Is.EqualTo(2));
            session.Input(0, 1000d, false);
            Assert.That(results.Count, Is.EqualTo(3));
        }

        private static ChartDocument Document(int start = 0, int end = 2400)
        {
            var document = new ChartDocument(4800, 4, 120d);
            document.Notes.Add(new ChartDocumentNote("hold", ChartNoteKind.CenterHold, 10, start, end));
            return document;
        }
        private static ChartDocumentNote StepKnob()
        {
            var note = new ChartDocumentNote("knob", ChartNoteKind.Knob, 10, 0, 2400);
            note.Points[1].KnobX = 1d;
            note.Points.Insert(1, new ChartNotePoint(1200, ChartNotePointKind.Mid)
                { KnobX = 1d, KnobInterpolation = ChartKnobInterpolation.Step });
            return note;
        }
        private static PlayableChartSnapshot Compile(ChartDocument document)
        {
            var result = ChartCompiler.Compile(document);
            Assert.That(result.Succeeded, Is.True, string.Join(", ", result.Issues.Select(i => i.Code)));
            return result.Snapshot;
        }
        private static ChartJudgementGrade Judge(PlayableNoteSnapshot note, int point, double offset)
        {
            double magnitude = Math.Abs(offset);
            return magnitude <= 40d ? ChartJudgementGrade.PerfectPlus : magnitude <= 80d
                ? ChartJudgementGrade.Perfect : magnitude <= 130d ? ChartJudgementGrade.Great
                : magnitude <= 175d ? ChartJudgementGrade.Miss : ChartJudgementGrade.None;
        }
        private static PlayableJudgementSession Session(ChartDocument document,
            List<ChartJudgementResolution> results) =>
            new PlayableJudgementSession(Compile(document), Judge, (_, __) => 175d, results.Add);
        private static string Export(ChartDocument document) =>
            RuntimeChartPackageCodec.Export(document, "song", "normal", "", "", 0d, 1d, null);
        private static void Before(PlayableJudgementSession session, double time)
        {
            while (session.NextAutomaticTime(false) < time)
                session.ProcessAutomatic(session.NextAutomaticTime(false), false);
        }
        private static void Advance(PlayableJudgementSession session, double time)
        {
            while (session.NextAutomaticTime(false) <= time)
                session.ProcessAutomatic(session.NextAutomaticTime(false), false);
        }
    }
}

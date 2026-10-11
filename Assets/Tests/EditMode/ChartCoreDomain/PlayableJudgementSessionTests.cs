using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace REmind.Charting.Tests
{
    public sealed class PlayableJudgementSessionTests
    {
        [Test]
        public void LongScratch_MissedStartCanRejoinAtEveryMid()
        {
            PlayableChartSnapshot chart = LongScratchChart();
            var results = new List<ChartJudgementResolution>();
            var session = Create(chart, results);

            // A Mid press at the boundary belongs to the next interval.
            session.Input((int)ChartLane.GroundLeft, 1000d, true);
            session.ProcessAutomatic(1000d, false);
            session.Input((int)ChartLane.GroundLeft, 1500d, false);
            session.Input((int)ChartLane.GroundLeft, 2000d, true);
            session.ProcessAutomatic(2000d, false);
            session.Input((int)ChartLane.GroundLeft, 3000d, false);

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].SegmentIndex, Is.EqualTo(0));
            Assert.That(results[0].Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(results[1].SegmentIndex, Is.EqualTo(1));
            Assert.That(results[1].Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(results[2].SegmentIndex, Is.EqualTo(2));
            Assert.That(results[2].Grade, Is.EqualTo(ChartJudgementGrade.Perfect));
            Assert.That(session.PendingNoteCount, Is.Zero);
        }

        [Test]
        public void LongScratch_HeldMidsAndEndReleaseScoreEveryInterval()
        {
            var results = new List<ChartJudgementResolution>();
            var session = new PlayableJudgementSession(LongScratchChart(),
                (_, point, offset) => Math.Abs(offset) <=
                    (point == 3 ? 75d : 100d)
                    ? point == 0 ? ChartJudgementGrade.PerfectPlus :
                        ChartJudgementGrade.Perfect
                    : ChartJudgementGrade.None,
                (_, point) => point == 3 ? 75d : 100d, results.Add);
            session.Input((int)ChartLane.GroundLeft, 0d, true);
            session.ProcessAutomatic(1000d, false);
            session.ProcessAutomatic(2000d, false);
            session.Input((int)ChartLane.GroundLeft, 3000d, false);

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].Grade,
                Is.EqualTo(ChartJudgementGrade.PerfectPlus));
            Assert.That(results[1].Grade,
                Is.EqualTo(ChartJudgementGrade.PerfectPlus));
            Assert.That(results[2].Grade,
                Is.EqualTo(ChartJudgementGrade.Perfect));
        }

        [Test]
        public void LongScratch_EndWithoutReleaseMissesAfterDedicatedWindow()
        {
            var results = new List<ChartJudgementResolution>();
            var session = Create(LongScratchChart(), results);
            session.Input((int)ChartLane.GroundLeft, 0d, true);
            session.ProcessAutomatic(1000d, false);
            session.ProcessAutomatic(2000d, false);
            session.ProcessAutomatic(3150d, false);
            Assert.That(results.Count, Is.EqualTo(2));
            session.ProcessAutomatic(session.NextAutomaticTime(false), false);
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[2].Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(results[2].IsAutomaticMiss, Is.True);
        }

        [Test]
        public void LongScratch_MidRejoinKeepsItsInputGradeUntilEnd()
        {
            var results = new List<ChartJudgementResolution>();
            var session = new PlayableJudgementSession(LongScratchChart(),
                (_, _, offset) => Math.Abs(offset) <= 30d
                    ? ChartJudgementGrade.Perfect
                    : Math.Abs(offset) <= 100d
                        ? ChartJudgementGrade.Great
                        : ChartJudgementGrade.None,
                (_, _) => 150d, results.Add);
            session.ProcessAutomatic(1000d, false);
            session.ProcessAutomatic(2000d, false);
            session.Input((int)ChartLane.GroundLeft, 2070d, true);
            session.Input((int)ChartLane.GroundLeft, 3000d, false);

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[2].Grade,
                Is.EqualTo(ChartJudgementGrade.Great));
        }

        [Test]
        public void LongScratch_ReleaseDuringPauseBreaksHeldInterval()
        {
            var results = new List<ChartJudgementResolution>();
            var session = Create(LongScratchChart(), results);
            session.Input((int)ChartLane.GroundLeft, 0d, true);
            session.BreakReleasedHolds(_ => false);
            session.ProcessAutomatic(1000d, false);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Grade,
                Is.EqualTo(ChartJudgementGrade.Miss));
        }

        [Test]
        public void LongScratch_UsesPointSpecificTimingAndEndDeadline()
        {
            var results = new List<ChartJudgementResolution>();
            var session = new PlayableJudgementSession(LongScratchChart(),
                (_, point, offset) =>
                {
                    double distance = Math.Abs(offset);
                    if (point == 0)
                        return distance <= 50d ? ChartJudgementGrade.Perfect :
                            distance <= 100d ? ChartJudgementGrade.Great :
                            ChartJudgementGrade.None;
                    return distance <= (point == 3 ? 75d : 100d)
                        ? ChartJudgementGrade.Perfect :
                        ChartJudgementGrade.None;
                },
                (_, point) => point == 3 ? 75d : 100d,
                results.Add);

            session.Input((int)ChartLane.GroundLeft, 100d, true);
            session.ProcessAutomatic(1000d, false);
            session.Input((int)ChartLane.GroundLeft, 1500d, false);
            session.ProcessAutomatic(2000d, false);
            session.Input((int)ChartLane.GroundLeft, 2100d, true);
            session.Input((int)ChartLane.GroundLeft, 3075d, false);

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].Grade, Is.EqualTo(ChartJudgementGrade.Great));
            Assert.That(results[1].Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            Assert.That(results[2].Grade, Is.EqualTo(ChartJudgementGrade.Perfect));
            Assert.That(session.PendingNoteCount, Is.Zero);

            results.Clear();
            session.Reset();
            session.Input((int)ChartLane.GroundLeft, 0d, true);
            session.ProcessAutomatic(1000d, false);
            session.ProcessAutomatic(2000d, false);
            session.ProcessAutomatic(3075d, false);
            Assert.That(results.Count, Is.EqualTo(2));
            session.ProcessAutomatic(session.NextAutomaticTime(false), false);
            Assert.That(results[2].Grade, Is.EqualTo(ChartJudgementGrade.Miss));
        }

        [Test]
        public void AutomaticBatch_ResetInResolutionDoesNotContinueOldAttempt()
        {
            var document = new ChartDocument(4800, 4, 120);
            document.Notes.Add(new ChartDocumentNote("a-miss", ChartNoteKind.Tap,
                (int)ChartLane.GroundLeft, 0));
            document.Notes.Add(new ChartDocumentNote("z-later", ChartNoteKind.Tap,
                (int)ChartLane.GroundRight, 0));
            ChartCompileResult compiled = ChartCompiler.Compile(document);
            Assert.That(compiled.Succeeded, Is.True);

            var results = new List<ChartJudgementResolution>();
            PlayableJudgementSession session = null;
            session = new PlayableJudgementSession(compiled.Snapshot,
                (_, _, _) => ChartJudgementGrade.Perfect,
                (_, _) => 100d,
                result =>
                {
                    results.Add(result);
                    if (results.Count == 1) session.Reset();
                });

            session.ProcessAutomatic(100.000001d, false);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(session.PendingNoteCount, Is.EqualTo(2));
            session.ProcessAutomatic(100.000001d, false);
            Assert.That(results.Count, Is.EqualTo(3));
        }

        [Test]
        public void AutomaticBatch_HaltInResolutionStopsLaterSameTimeJudgements()
        {
            var document = new ChartDocument(4800, 4, 120);
            document.Notes.Add(new ChartDocumentNote("a-miss", ChartNoteKind.Tap,
                (int)ChartLane.GroundLeft, 0));
            document.Notes.Add(new ChartDocumentNote("z-heal", ChartNoteKind.Tap,
                (int)ChartLane.GroundRight, 0));
            ChartCompileResult compiled = ChartCompiler.Compile(document);
            Assert.That(compiled.Succeeded, Is.True);

            var results = new List<ChartJudgementResolution>();
            PlayableJudgementSession session = null;
            session = new PlayableJudgementSession(compiled.Snapshot,
                (_, _, _) => ChartJudgementGrade.Perfect,
                (_, _) => 100d,
                result =>
                {
                    results.Add(result);
                    session.Halt();
                });

            session.ProcessAutomatic(100.000001d, false);
            session.ProcessAutomatic(100.000001d, false);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(session.NextAutomaticTime(false),
                Is.EqualTo(double.PositiveInfinity));
            session.Input((int)ChartLane.GroundRight, 0d, true);
            Assert.That(results.Count, Is.EqualTo(1));
            session.Reset();
            session.ProcessAutomatic(100.000001d, false);
            Assert.That(results.Count, Is.EqualTo(2));
        }

        [Test]
        public void LongScratchMiss_HaltSkipsHeldIntervalAtSameTimestamp()
        {
            var document = new ChartDocument(4800, 4, 120);
            document.Notes.Add(new ChartDocumentNote("a-miss", ChartNoteKind.LongScratch,
                (int)ChartLane.GroundLeft, 0, 4800));
            document.Notes.Add(new ChartDocumentNote("z-held", ChartNoteKind.LongScratch,
                (int)ChartLane.GroundRight, 0, 4800));
            foreach (ChartDocumentNote note in document.Notes)
                note.Points.Insert(1, new ChartNotePoint(2400,
                    ChartNotePointKind.Mid));
            ChartCompileResult compiled = ChartCompiler.Compile(document);
            Assert.That(compiled.Succeeded, Is.True);

            var results = new List<ChartJudgementResolution>();
            PlayableJudgementSession session = null;
            session = new PlayableJudgementSession(compiled.Snapshot,
                (_, _, _) => ChartJudgementGrade.Perfect,
                (_, _) => 100d,
                result =>
                {
                    results.Add(result);
                    if (result.Grade == ChartJudgementGrade.Miss)
                        session.Halt();
                });
            session.Input((int)ChartLane.GroundRight, 0d, true);

            session.ProcessAutomatic(1000d, false);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Note.Id, Is.EqualTo("a-miss"));
            Assert.That(results[0].Grade, Is.EqualTo(ChartJudgementGrade.Miss));
            session.ProcessAutomatic(1000d, false);
            Assert.That(results.Count, Is.EqualTo(1));
        }

        private static PlayableJudgementSession Create(
            PlayableChartSnapshot chart,
            List<ChartJudgementResolution> results)
        {
            return new PlayableJudgementSession(chart,
                (_, _, offset) => Math.Abs(offset) <= 100d
                    ? ChartJudgementGrade.Perfect
                    : Math.Abs(offset) <= 150d
                        ? ChartJudgementGrade.Miss
                        : ChartJudgementGrade.None,
                (_, _) => 150d, results.Add);
        }

        private static PlayableChartSnapshot LongScratchChart()
        {
            var document = new ChartDocument(4800, 4, 120);
            var note = new ChartDocumentNote("scratch", ChartNoteKind.LongScratch,
                (int)ChartLane.GroundLeft, 0, 7200);
            note.Points.Insert(1, new ChartNotePoint(2400,
                ChartNotePointKind.Mid));
            note.Points.Insert(2, new ChartNotePoint(4800,
                ChartNotePointKind.Mid));
            document.Notes.Add(note);
            ChartCompileResult result = ChartCompiler.Compile(document);
            Assert.That(result.Succeeded, Is.True);
            return result.Snapshot;
        }
    }
}

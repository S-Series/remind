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
            var session = Create(LongScratchChart(), results);
            session.Input((int)ChartLane.GroundLeft, 0d, true);
            session.ProcessAutomatic(1000d, false);
            session.ProcessAutomatic(2000d, false);
            session.Input((int)ChartLane.GroundLeft, 3000d, false);

            Assert.That(results.Count, Is.EqualTo(3));
            foreach (ChartJudgementResolution result in results)
                Assert.That(result.Grade,
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
                (_, offset) => Math.Abs(offset) <= 30d
                    ? ChartJudgementGrade.Perfect
                    : Math.Abs(offset) <= 100d
                        ? ChartJudgementGrade.Good
                        : ChartJudgementGrade.None,
                _ => 150d, results.Add);
            session.ProcessAutomatic(1000d, false);
            session.ProcessAutomatic(2000d, false);
            session.Input((int)ChartLane.GroundLeft, 2070d, true);
            session.Input((int)ChartLane.GroundLeft, 3000d, false);

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[2].Grade,
                Is.EqualTo(ChartJudgementGrade.Good));
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

        private static PlayableJudgementSession Create(
            PlayableChartSnapshot chart,
            List<ChartJudgementResolution> results)
        {
            return new PlayableJudgementSession(chart,
                (_, offset) => Math.Abs(offset) <= 100d
                    ? ChartJudgementGrade.Perfect
                    : Math.Abs(offset) <= 150d
                        ? ChartJudgementGrade.Miss
                        : ChartJudgementGrade.None,
                _ => 150d, results.Add);
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

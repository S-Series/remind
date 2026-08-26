using System;
using NUnit.Framework;

namespace REmind.Charting.Tests
{
    public sealed class TimingMapTests
    {
        [Test]
        public void Compile_CreatesContinuousTimingPointAtBpmChange()
        {
            ChartCompileResult result = CompileExample();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.TimingMap.Points.Count, Is.EqualTo(2));

            TimingPoint change = result.Snapshot.TimingMap.Points[1];
            Assert.That(change.StartPosition, Is.EqualTo(4800d));
            Assert.That(change.StartTimeMs, Is.EqualTo(2000d).Within(0.000001d));
            Assert.That(change.Bpm, Is.EqualTo(240d));
        }

        [Test]
        public void PositionAtTime_UsesAbsoluteAnchorAfterBpmChange()
        {
            TimingMap timingMap = CompileExample().Snapshot.TimingMap;

            double position = timingMap.PositionAtTime(2500d);

            Assert.That(position, Is.EqualTo(7200d).Within(0.000001d));
        }

        [Test]
        public void PositionAndTimeConversions_RoundTripAcrossSegments()
        {
            TimingMap timingMap = CompileExample().Snapshot.TimingMap;
            double[] positions = { -1200d, 0d, 2400d, 4800d, 7200d };

            for (int i = 0; i < positions.Length; i++)
            {
                double timeMs = timingMap.TimeAtPosition(positions[i]);
                double restored = timingMap.PositionAtTime(timeMs);
                Assert.That(
                    restored,
                    Is.EqualTo(positions[i]).Within(0.000001d));
            }
        }

        [Test]
        public void Compile_UsesTimingMapForJudgementTargetTime()
        {
            ChartDocument document = CreateDocument();
            document.Notes.Add(new ChartDocumentNote(
                "note-after-change",
                ChartNoteKind.Tap,
                (int)ChartLane.Main1,
                7200));

            ChartCompileResult result = ChartCompiler.Compile(document);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.JudgementTargets.Count, Is.EqualTo(1));
            Assert.That(
                result.Snapshot.JudgementTargets[0].TargetTimeMs,
                Is.EqualTo(2500d).Within(0.000001d));
        }

        [Test]
        public void Compile_RejectsDuplicateTimingPositions()
        {
            ChartDocument document = CreateDocument();
            document.TimingEvents.Add(new ChartTimingEvent(4800, 180d));

            ChartCompileResult result = ChartCompiler.Compile(document);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(
                result.Issues,
                Has.Some.Matches<CompileIssue>(
                    issue => issue.Code == "TIMING_EVENT_DUPLICATE"));
        }

        [Test]
        public void DspSongClock_DerivesSongTimeWithoutBpmState()
        {
            DspSongClock clock = new DspSongClock(10d, 2000d);

            Assert.That(clock.SongTimeMsAt(10.25d), Is.EqualTo(2250d));
            Assert.That(clock.DspTimeAt(2250d), Is.EqualTo(10.25d));
        }

        [Test]
        public void LineSpeed_ChangesFloorPositionWithoutChangingJudgementTime()
        {
            ChartDocument normal = CreateDocument();
            normal.Notes.Add(new ChartDocumentNote(
                "normal-note",
                ChartNoteKind.Tap,
                (int)ChartLane.Main1,
                7200));
            ChartDocument directed = CreateDocument();
            directed.LineSpeedEvents.Add(
                new ChartLineSpeedEvent(4800, 0.5d));
            directed.Notes.Add(new ChartDocumentNote(
                "directed-note",
                ChartNoteKind.Tap,
                (int)ChartLane.Main1,
                7200));

            PlayableChartSnapshot normalSnapshot =
                ChartCompiler.Compile(normal).Snapshot;
            PlayableChartSnapshot directedSnapshot =
                ChartCompiler.Compile(directed).Snapshot;

            Assert.That(
                directedSnapshot.JudgementTargets[0].TargetTimeMs,
                Is.EqualTo(
                    normalSnapshot.JudgementTargets[0].TargetTimeMs)
                    .Within(0.000001d));
            Assert.That(
                directedSnapshot.JudgementTargets[0].FloorPosition,
                Is.LessThan(
                    normalSnapshot.JudgementTargets[0].FloorPosition));
        }

        [Test]
        public void Bpm240AtHalfLineSpeed_MatchesBpm120VisualSpeed()
        {
            ChartDocument fastTiming = new ChartDocument(4800, 4, 240d);
            fastTiming.LineSpeedEvents.Add(
                new ChartLineSpeedEvent(0, 0.5d));
            ChartDocument normalTiming = new ChartDocument(4800, 4, 120d);

            ScrollMap fastScroll = ChartCompiler
                .Compile(fastTiming)
                .Snapshot
                .ScrollMap;
            ScrollMap normalScroll = ChartCompiler
                .Compile(normalTiming)
                .Snapshot
                .ScrollMap;

            Assert.That(
                fastScroll.FloorPositionAtTime(1000d),
                Is.EqualTo(normalScroll.FloorPositionAtTime(1000d))
                    .Within(0.000001d));
            Assert.That(
                fastScroll.TimingMap.TimeAtPosition(4800d),
                Is.EqualTo(1000d).Within(0.000001d));
            Assert.That(
                normalScroll.TimingMap.TimeAtPosition(4800d),
                Is.EqualTo(2000d).Within(0.000001d));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        public void Compile_RejectsUnconfirmedStopAndReverseLineSpeed(
            double lineSpeed)
        {
            ChartDocument document = CreateDocument();
            document.LineSpeedEvents.Add(
                new ChartLineSpeedEvent(2400, lineSpeed));

            ChartCompileResult result = ChartCompiler.Compile(document);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(
                result.Issues,
                Has.Some.Matches<CompileIssue>(
                    issue => issue.Code == "LINE_SPEED_EVENT_VALUE"));
        }

        [Test]
        public void CameraSpinDurationAt120Bpm_Is175Milliseconds()
        {
            ChartDocument document = new ChartDocument(4800, 4, 120d);
            document.CameraEvents.Add(new ChartCameraEvent(
                0,
                -5d,
                ChartCameraSpinDirection.Left));

            CameraMotionMap cameraMap = ChartCompiler
                .Compile(document)
                .Snapshot
                .CameraMotionMap;
            CameraMotionPoint point = cameraMap.Points[0];
            CameraMotionState halfway = cameraMap.EvaluateAtTime(87.5d);

            Assert.That(point.SpinDurationMs, Is.EqualTo(175d));
            Assert.That(halfway.HasReference, Is.True);
            Assert.That(halfway.ReferenceOffsetX, Is.EqualTo(-5d));
            Assert.That(halfway.SpinDegrees, Is.EqualTo(180d));
            Assert.That(
                cameraMap.EvaluateAtTime(175d).SpinDegrees,
                Is.EqualTo(0d));
        }

        [Test]
        public void CameraSpinDurationAt240Bpm_IsHalfOf175Milliseconds()
        {
            ChartDocument document = CreateDocument();
            document.CameraEvents.Add(new ChartCameraEvent(
                4800,
                0d,
                ChartCameraSpinDirection.Right));

            CameraMotionPoint point = ChartCompiler
                .Compile(document)
                .Snapshot
                .CameraMotionMap
                .Points[0];

            Assert.That(
                point.SpinDurationMs,
                Is.EqualTo(87.5d).Within(0.000001d));

            CameraMotionState halfway = ChartCompiler
                .Compile(document)
                .Snapshot
                .CameraMotionMap
                .EvaluateAtTime(point.TimeMs + point.SpinDurationMs * 0.5d);
            Assert.That(
                halfway.SpinDegrees,
                Is.EqualTo(-180d).Within(0.000001d));
        }

        private static ChartCompileResult CompileExample()
        {
            return ChartCompiler.Compile(CreateDocument());
        }

        private static ChartDocument CreateDocument()
        {
            ChartDocument document = new ChartDocument(
                positionUnitsPerMeasure: 4800,
                beatsPerMeasure: 4,
                baseBpm: 120d);
            document.TimingEvents.Add(new ChartTimingEvent(4800, 240d));
            return document;
        }
    }
}

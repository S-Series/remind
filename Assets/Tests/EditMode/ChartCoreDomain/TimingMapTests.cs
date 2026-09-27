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
        public void Compile_LongScratchPreservesIntermediateMotionAndTiming()
        {
            ChartDocument document = CreateDocument();
            var note = new ChartDocumentNote("scratch-chain",
                ChartNoteKind.LongScratch, (int)ChartLane.GroundLeft,
                2400, 7200);
            note.Points.Clear();
            note.Points.Add(new ChartNotePoint(2400,
                ChartNotePointKind.Start, ChartScratchMotionKind.Gradual, 12));
            note.Points.Add(new ChartNotePoint(4800,
                ChartNotePointKind.Mid, ChartScratchMotionKind.Release, 7));
            note.Points.Add(new ChartNotePoint(7200,
                ChartNotePointKind.End));
            document.Notes.Add(note);

            ChartCompileResult result = ChartCompiler.Compile(document);

            Assert.That(result.Succeeded, Is.True);
            PlayableNoteSnapshot runtimeNote = result.Snapshot.Notes[0];
            Assert.That(runtimeNote.Points.Count, Is.EqualTo(3));
            Assert.That(runtimeNote.Points[1].Kind,
                Is.EqualTo(ChartNotePointKind.Mid));
            Assert.That(runtimeNote.Points[1].Motion,
                Is.EqualTo(ChartScratchMotionKind.Release));
            Assert.That(runtimeNote.Points[1].MoveAmount, Is.EqualTo(7));
            Assert.That(runtimeNote.Points[1].TimeMs,
                Is.EqualTo(2000d).Within(0.000001d));
            Assert.That(runtimeNote.Points[2].TimeMs,
                Is.EqualTo(2500d).Within(0.000001d));
            Assert.That(result.Snapshot.JudgementTargets.Count, Is.EqualTo(3));
            Assert.That(result.Snapshot.JudgementTargets[1].Kind,
                Is.EqualTo(JudgementTargetKind.HoldMid));
            Assert.That(result.Snapshot.JudgementSegments.Count, Is.EqualTo(2));
            Assert.That(result.Snapshot.JudgementSegments[0].StartTimeMs,
                Is.EqualTo(1000d).Within(0.000001d));
            Assert.That(result.Snapshot.JudgementSegments[0].EndTimeMs,
                Is.EqualTo(2000d).Within(0.000001d));
        }

        [Test]
        public void RuntimePackage_RoundTripsValidatedLongScratchAndEffect()
        {
            ChartDocument document = CreateDocument();
            var note = new ChartDocumentNote("scratch-chain",
                ChartNoteKind.LongScratch, (int)ChartLane.GroundLeft,
                2400, 7200);
            note.Points.Insert(1, new ChartNotePoint(4800,
                ChartNotePointKind.Mid, ChartScratchMotionKind.Gradual, 3));
            document.Notes.Add(note);
            document.EffectEvents.Add(new ChartEffectEvent(4800, "fx1",
                "camera.offset", "", 0));
            var parameters = new System.Collections.Generic.Dictionary<string,
                string> { ["fx1"] = "{\"durationMs\":400}" };

            string json = RuntimeChartPackageCodec.Export(document, "song",
                "hard", "", "rev1", 35d, 0.01d, parameters);
            RuntimeChartPackage package = RuntimeChartPackageCodec.Import(json);

            Assert.That(package.Snapshot.Notes.Count, Is.EqualTo(1));
            Assert.That(package.Snapshot.Notes[0].Points[1].Kind,
                Is.EqualTo(ChartNotePointKind.Mid));
            Assert.That(package.Snapshot.Notes[0].Points[1].Motion,
                Is.EqualTo(ChartScratchMotionKind.Gradual));
            Assert.That(package.Snapshot.JudgementSegments.Count, Is.EqualTo(2));
            Assert.That(package.EffectParameterJson["fx1"],
                Is.EqualTo(parameters["fx1"]));
            Assert.That(package.ChartOffsetMs, Is.EqualTo(35d));
            Assert.That(package.Snapshot.Notes[0].StartFloorPosition,
                Is.EqualTo(24d).Within(0.000001d));

            string malformed = json.Replace("\"Move\":3",
                "\"Move\":100");
            Assert.That(malformed, Is.Not.EqualTo(json));
            Assert.Throws<System.FormatException>(() =>
                RuntimeChartPackageCodec.Import(malformed));
        }

        [Test]
        public void Compile_RejectsIntermediatePointOutsideLongNote()
        {
            ChartDocument document = CreateDocument();
            var note = new ChartDocumentNote("bad-chain",
                ChartNoteKind.LongScratch, (int)ChartLane.GroundLeft,
                0, 4800);
            note.Points.Insert(1, new ChartNotePoint(7200,
                ChartNotePointKind.Mid));
            document.Notes.Add(note);

            ChartCompileResult result = ChartCompiler.Compile(document);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues,
                Has.Some.Matches<CompileIssue>(
                    issue => issue.Code == "NOTE_POINTS"));
        }

        [Test]
        public void Compile_RejectsSameLaneAndTimeBeforePreviewOrGame()
        {
            ChartDocument document = CreateDocument();
            document.Notes.Add(new ChartDocumentNote("first",
                ChartNoteKind.Tap, (int)ChartLane.Main1, 2400));
            document.Notes.Add(new ChartDocumentNote("second",
                ChartNoteKind.Tap, (int)ChartLane.Main1, 2400));

            ChartCompileResult result = ChartCompiler.Compile(document);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<CompileIssue>(
                issue => issue.Code == "NOTE_LANE_TIME_COLLISION"));
        }

        [Test]
        public void Compile_SameTimeTargetsUseLaneThenStableIdOrder()
        {
            ChartDocument document = CreateDocument();
            document.Notes.Add(new ChartDocumentNote("later-lane",
                ChartNoteKind.Tap, (int)ChartLane.Main2, 2400));
            document.Notes.Add(new ChartDocumentNote("z-first-lane",
                ChartNoteKind.Tap, (int)ChartLane.Main1, 2400));
            document.Notes.Add(new ChartDocumentNote("a-third-lane",
                ChartNoteKind.Tap, (int)ChartLane.Main3, 2400));

            ChartCompileResult result = ChartCompiler.Compile(document);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.JudgementTargets[0].NoteId,
                Is.EqualTo("z-first-lane"));
            Assert.That(result.Snapshot.JudgementTargets[1].NoteId,
                Is.EqualTo("later-lane"));
            Assert.That(result.Snapshot.JudgementTargets[2].NoteId,
                Is.EqualTo("a-third-lane"));
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

        [Test]
        public void InstantScratchCameraTiltAt120Bpm_HitsFastAndReturnsIn200Ms()
        {
            ChartDocument document = new ChartDocument(4800, 4, 120d);
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    0,
                    ChartLane.GroundRight));

            ScratchCameraTiltMap tiltMap = ChartCompiler
                .Compile(document)
                .Snapshot
                .ScratchCameraTiltMap;
            ScratchCameraTiltPoint point = tiltMap.Points[0];

            Assert.That(point.DurationMs, Is.EqualTo(200d));
            Assert.That(tiltMap.EvaluateAtTime(0d), Is.EqualTo(0d));
            Assert.That(tiltMap.EvaluateAtTime(10d), Is.EqualTo(-8.75d));
            Assert.That(tiltMap.EvaluateAtTime(20d), Is.EqualTo(-10d));
            Assert.That(
                tiltMap.EvaluateAtTime(100d),
                Is.EqualTo(-250d / 81d).Within(0.000001d));
            Assert.That(tiltMap.EvaluateAtTime(200d), Is.EqualTo(0d));
        }

        [Test]
        public void InstantScratchCameraTiltAt240Bpm_Uses100Milliseconds()
        {
            ChartDocument document = CreateDocument();
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    4800,
                    ChartLane.GroundLeft));

            ScratchCameraTiltMap tiltMap = ChartCompiler
                .Compile(document)
                .Snapshot
                .ScratchCameraTiltMap;
            ScratchCameraTiltPoint point = tiltMap.Points[0];

            Assert.That(
                point.DurationMs,
                Is.EqualTo(100d).Within(0.000001d));
            Assert.That(
                tiltMap.EvaluateAtTime(point.TimeMs + 10d),
                Is.EqualTo(10d).Within(0.000001d));
        }

        [Test]
        public void ReverseInstantScratchCameraTilt_UsesOppositeDirection()
        {
            ChartDocument document = new ChartDocument(4800, 4, 120d);
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    0,
                    ChartLane.GroundRight,
                    ChartScratchCameraTiltEventType.ReverseInstant));

            ScratchCameraTiltMap tiltMap = ChartCompiler
                .Compile(document)
                .Snapshot
                .ScratchCameraTiltMap;

            Assert.That(tiltMap.Points.Count, Is.EqualTo(1));
            Assert.That(tiltMap.Points[0].DurationMs, Is.EqualTo(200d));
            Assert.That(tiltMap.EvaluateAtTime(20d), Is.EqualTo(10d));
            Assert.That(tiltMap.EvaluateAtTime(200d), Is.EqualTo(0d));
        }

        [Test]
        public void GradualScratchCameraTilt_AttacksHoldsAndReleasesAtEnd()
        {
            ChartDocument document = new ChartDocument(4800, 4, 120d);
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    0,
                    ChartLane.GroundLeft,
                    ChartScratchCameraTiltEventType.Gradual));
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    4800,
                    ChartLane.GroundLeft,
                    ChartScratchCameraTiltEventType.Release));

            ChartCompileResult result = ChartCompiler.Compile(document);

            Assert.That(result.Succeeded, Is.True);
            ScratchCameraTiltMap tiltMap = result.Snapshot
                .ScratchCameraTiltMap;
            Assert.That(tiltMap.HoldPoints.Count, Is.EqualTo(1));
            Assert.That(tiltMap.EvaluateAtTime(0d), Is.EqualTo(0d));
            Assert.That(tiltMap.EvaluateAtTime(10d), Is.EqualTo(0.5d));
            Assert.That(tiltMap.EvaluateAtTime(50d), Is.EqualTo(2.5d));
            Assert.That(tiltMap.EvaluateAtTime(100d), Is.EqualTo(5d));
            Assert.That(tiltMap.EvaluateAtTime(1000d), Is.EqualTo(5d));
            Assert.That(tiltMap.EvaluateAtTime(2000d), Is.EqualTo(5d));
            Assert.That(tiltMap.EvaluateAtTime(2010d), Is.EqualTo(2.5d));
            Assert.That(tiltMap.EvaluateAtTime(2020d), Is.EqualTo(0d));
        }

        [Test]
        public void GradualScratchCameraTilt_GradualMidDoesNotRestartAttack()
        {
            ChartDocument document = new ChartDocument(4800, 4, 120d);
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    0,
                    ChartLane.GroundRight,
                    ChartScratchCameraTiltEventType.Gradual));
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    2400,
                    ChartLane.GroundRight,
                    ChartScratchCameraTiltEventType.Gradual));
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    4800,
                    ChartLane.GroundRight,
                    ChartScratchCameraTiltEventType.Release));

            ScratchCameraTiltMap tiltMap = ChartCompiler
                .Compile(document)
                .Snapshot
                .ScratchCameraTiltMap;

            Assert.That(tiltMap.HoldPoints.Count, Is.EqualTo(1));
            Assert.That(tiltMap.HoldPoints[0].StartPosition, Is.EqualTo(0));
            Assert.That(tiltMap.HoldPoints[0].EndPosition, Is.EqualTo(4800));
            Assert.That(tiltMap.EvaluateAtTime(1000d), Is.EqualTo(-5d));
        }

        [Test]
        public void GradualScratchCameraTilt_InstantMidUsesTheNewMotion()
        {
            ChartDocument document = new ChartDocument(4800, 4, 120d);
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    0,
                    ChartLane.GroundLeft,
                    ChartScratchCameraTiltEventType.Gradual));
            document.ScratchCameraTiltEvents.Add(
                new ChartScratchCameraTiltEvent(
                    2400,
                    ChartLane.GroundLeft,
                    ChartScratchCameraTiltEventType.Instant));

            ScratchCameraTiltMap tiltMap = ChartCompiler
                .Compile(document)
                .Snapshot
                .ScratchCameraTiltMap;

            Assert.That(tiltMap.HoldPoints.Count, Is.EqualTo(1));
            Assert.That(tiltMap.Points.Count, Is.EqualTo(1));
            Assert.That(tiltMap.HoldPoints[0].EndPosition, Is.EqualTo(2400));
            Assert.That(tiltMap.EvaluateAtTime(1000d), Is.EqualTo(5d));
            Assert.That(tiltMap.EvaluateAtTime(1010d), Is.EqualTo(10d));
            Assert.That(tiltMap.EvaluateAtTime(1200d), Is.EqualTo(0d));
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

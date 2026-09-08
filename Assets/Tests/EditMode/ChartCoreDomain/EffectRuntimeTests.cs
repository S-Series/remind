using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace REmind.Charting.Tests
{
    public sealed class EffectRuntimeTests
    {
        private static PlayableChartSnapshot Snapshot(params ChartEffectEvent[] effects)
        {
            var document = new ChartDocument(4800, 4, 120);
            foreach (var effect in effects) document.EffectEvents.Add(effect);
            var result = ChartCompiler.Compile(document);
            Assert.That(result.Succeeded, Is.True);
            return result.Snapshot;
        }
        private static ChartEffectEvent Call(int position, string id, string command, int order = 0) =>
            new ChartEffectEvent(position, id, "music.call", command, order);

        [Test]
        public void Compiler_UsesTimingMapAndExplicitOrder_NotJudgementTargets()
        {
            var snapshot = Snapshot(Call(4800, "b", "count-success", 2), Call(4800, "a", "count-success", 1));
            Assert.That(snapshot.EffectEvents[0].EffectId, Is.EqualTo("a"));
            Assert.That(snapshot.EffectEvents[0].TimeMs, Is.EqualTo(2000));
            Assert.That(snapshot.JudgementTargets, Is.Empty);
        }
        [Test]
        public void Compiler_RejectsDuplicateIdentity()
        {
            var document = new ChartDocument(4800, 4, 120);
            document.EffectEvents.Add(Call(0, "x", "count-success"));
            document.EffectEvents.Add(Call(0, "x", "count-success"));
            Assert.That(ChartCompiler.Compile(document).Succeeded, Is.False);
        }
        [Test]
        public void Compiler_RejectsAmbiguousSameTimeOrderWithDistinctIds()
        {
            var document = new ChartDocument(4800, 4, 120);
            document.EffectEvents.Add(Call(0, "x", "count-success"));
            document.EffectEvents.Add(Call(0, "y", "count-success"));
            Assert.That(ChartCompiler.Compile(document).Succeeded, Is.False);
        }
        [Test]
        public void Prepare_RejectsUnknownCommandMissingAndWrongTypedParameters()
        {
            var registry = EffectRegistry.CreateDefault();
            Assert.That(EffectPreparation.Prepare(Snapshot(Call(0, "x", "unknown")).EffectEvents,
                null, registry, "sample").Succeeded, Is.False);
            var events = Snapshot(Call(0, "x", "begin-section")).EffectEvents;
            Assert.That(EffectPreparation.Prepare(events, null, registry, "sample").Succeeded, Is.False);
            Assert.That(EffectPreparation.Prepare(events, new Dictionary<string, object> { ["x"] = "wrong" }, registry, "sample").Succeeded, Is.False);
        }
        [Test]
        public void DelayedFrame_PreservesScheduleOrderAndDoesNotRetrigger()
        {
            var seen = new List<string>();
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration("record", "Record", null, false, null, null,
                _ => new RecordingEffect(c => seen.Add(c.EffectId + ":" + c.ScheduledTimeMs + ":" + c.CurrentTimeMs))));
            var snapshot = Snapshot(new ChartEffectEvent(240, "a", "record", "", 0), new ChartEffectEvent(480, "b", "record", "", 0));
            var plan = EffectPreparation.Prepare(snapshot.EffectEvents, null, registry).Plan;
            using var runner = new EffectRunner(plan, new EffectSessionContext("test", "normal", EffectExecutionMode.Preview));
            Assert.That(runner.TriggerThrough(200, 1000), Is.True);
            Assert.That(runner.AdvanceTo(1000), Is.True);
            runner.AdvanceTo(1000);
            CollectionAssert.AreEqual(new[] { "a:100:1000", "b:200:1000" }, seen);
            Assert.That(runner.TriggeredCount, Is.EqualTo(2));
        }
        [Test]
        public void SameTime_UsesExplicitOrder()
        {
            var seen = new List<string>();
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration("record", "Record", null, false, null, null,
                _ => new RecordingEffect(c => seen.Add(c.EffectId))));
            var snapshot = Snapshot(new ChartEffectEvent(0, "b", "record", "", 2),
                new ChartEffectEvent(0, "a", "record", "", 1));
            var plan = EffectPreparation.Prepare(snapshot.EffectEvents, null, registry).Plan;
            using var runner = new EffectRunner(plan,
                new EffectSessionContext("test", "normal", EffectExecutionMode.Preview));
            Assert.That(runner.AdvanceTo(0), Is.True);
            CollectionAssert.AreEqual(new[] { "a", "b" }, seen);
        }
        [Test]
        public void SameUpdateTime_DoesNotUpdateActiveEffectOrGimmickTwice()
        {
            int starts = 0, updates = 0, stops = 0;
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration("active", "Active", null, false, null, null,
                _ => new TrackingEffect(() => starts++, () => updates++, _ => stops++)));
            registry.RegisterEffect(new EffectRegistration(CallMusicGimmickEffect.TypeId,
                "Call", null, false, null, null, null));
            registry.RegisterGimmick(new MusicGimmickRegistration("counting", "Counting",
                () => new CountingMusicGimmick(), new[]
                {
                    new MusicGimmickCommandRegistration("touch", "Touch", null, false,
                        null, null, (g, c, p) => ((CountingMusicGimmick)g).CommandCount++)
                }));
            var snapshot = Snapshot(new ChartEffectEvent(0, "active", "active", "", 0),
                Call(0, "call", "touch", 1));
            var plan = EffectPreparation.Prepare(snapshot.EffectEvents, null, registry, "counting").Plan;
            var runner = new EffectRunner(plan,
                new EffectSessionContext("test", "normal", EffectExecutionMode.Preview));
            var gimmick = (CountingMusicGimmick)runner.Gimmick;

            Assert.That(runner.AdvanceTo(0), Is.True);
            Assert.That(runner.AdvanceTo(0), Is.True);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(updates, Is.EqualTo(1));
            Assert.That(gimmick.CommandCount, Is.EqualTo(1));
            Assert.That(gimmick.UpdateCount, Is.EqualTo(1));

            runner.Dispose();
            runner.Dispose();
            Assert.That(stops, Is.EqualTo(1));
            Assert.That(gimmick.DisposeCount, Is.EqualTo(1));
        }
        [TestCase(100, true)]
        [TestCase(10, false)]
        public void Gimmick_SharesStateAcrossCalls_ConditionalSkipKeepsConnection(double health, bool transitioned)
        {
            var snapshot = Snapshot(Call(0, "begin", "begin-section"), Call(240, "count", "count-success"),
                Call(480, "end", "end-section"), Call(720, "transition", "request-transition"));
            var values = new Dictionary<string, object> {
                ["begin"] = new SampleSectionParameters(30, 1.5), ["transition"] = new SongTransitionParameters("next", "hard") };
            var plan = EffectPreparation.Prepare(snapshot.EffectEvents, values, EffectRegistry.CreateDefault(), "sample").Plan;
            var services = new TestServices { CurrentHealth = health };
            using var runner = new EffectRunner(plan, new EffectSessionContext("test", "normal", EffectExecutionMode.Preview,
                services, services, services, services));
            runner.AdvanceTo(1000);
            Assert.That(runner.TriggeredCount, Is.EqualTo(4));
            Assert.That(runner.TransitionRequested, Is.EqualTo(transitioned));
            Assert.That(((SampleMusicGimmick)runner.Gimmick).SuccessCount, Is.EqualTo(transitioned ? 1 : 0));
            Assert.That(services.RequestCount, Is.EqualTo(transitioned ? 1 : 0));
            runner.AdvanceTo(2000);
            Assert.That(services.RequestCount, Is.EqualTo(transitioned ? 1 : 0));
        }
        [Test]
        public void Restart_FreshGimmick_AndSeekExplicitlyRejected()
        {
            var plan = EffectPreparation.Prepare(Snapshot(Call(0, "x", "count-success")).EffectEvents,
                null, EffectRegistry.CreateDefault(), "sample").Plan;
            using var a = new EffectRunner(plan, new EffectSessionContext("a", "b", EffectExecutionMode.Preview));
            using var b = new EffectRunner(plan, new EffectSessionContext("a", "b", EffectExecutionMode.Preview));
            Assert.That(a.Gimmick, Is.Not.SameAs(b.Gimmick));
            Assert.Throws<InvalidOperationException>(() => new EffectRunner(plan,
                new EffectSessionContext("a", "b", EffectExecutionMode.Preview), 1000));
        }
        [Test]
        public void CommonEffect_MustExplicitlyOptIntoSeek()
        {
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration("one-shot", "One shot", null,
                false, null, null, _ => new RecordingEffect(_ => { })));
            var plan = EffectPreparation.Prepare(
                Snapshot(new ChartEffectEvent(0, "one", "one-shot", "", 0)).EffectEvents,
                null, registry).Plan;
            var session = new EffectSessionContext("a", "b", EffectExecutionMode.Preview);

            Assert.That(plan.SupportsSeek, Is.False);
            Assert.Throws<InvalidOperationException>(() => new EffectRunner(plan, session, 1));
            Assert.That(session.CancellationToken.IsCancellationRequested, Is.True);
        }
        [Test]
        public void Camera_SeekAbsoluteTimeAndCleanup_RejectsLateSessionHandle()
        {
            var plan = EffectPreparation.Prepare(Snapshot(new ChartEffectEvent(0, "camera", "camera.offset", "", 0)).EffectEvents,
                new Dictionary<string, object> { ["camera"] = new CameraEffectParameters(400, 3, 0, 5) }, EffectRegistry.CreateDefault()).Plan;
            var services = new TestServices();
            var session = new EffectSessionContext("a", "b", EffectExecutionMode.Preview, services);
            using var runner = new EffectRunner(plan, session, 200);
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.AdvanceTo(199));
            Assert.That(runner.TriggerThrough(0, 200), Is.True);
            Assert.That(services.SetCount, Is.Zero);
            Assert.That(runner.AdvanceTo(200), Is.True);
            Assert.That(services.X, Is.EqualTo(3));
            Assert.That(services.SetCount, Is.EqualTo(1));
            runner.AdvanceTo(200);
            Assert.That(services.SetCount, Is.EqualTo(1));
            runner.AdvanceTo(400);
            Assert.That(services.X, Is.Zero);
            var handle = session.CreateCameraOffset("late");
            runner.Dispose();
            Assert.That(session.CancellationToken.IsCancellationRequested, Is.True);
            Assert.Throws<ObjectDisposedException>(() => handle.Set(1, 0, 0));
        }
        [Test]
        public void Failure_IsConsumedOnceAndCleansPartialSideEffects()
        {
            int calls = 0;
            var services = new TestServices();
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration("fail", "Fail", null, false, null, null,
                _ => new RecordingEffect(c => { calls++; c.Session.CreateCameraOffset("partial").Set(3, 0, 0); throw new Exception("sample error"); })));
            var plan = EffectPreparation.Prepare(Snapshot(new ChartEffectEvent(0, "bad", "fail", "", 0)).EffectEvents, null, registry).Plan;
            using var runner = new EffectRunner(plan, new EffectSessionContext("music", "hard", EffectExecutionMode.Preview, services));
            Assert.That(runner.AdvanceTo(0), Is.False);
            Assert.That(runner.AdvanceTo(100), Is.False);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(services.X, Is.Zero);
            StringAssert.Contains("effectId=bad", runner.Failure.Message);
        }
        [Test]
        public void ReentrantAdvance_FailsWithoutTriggeringLaterEffects()
        {
            int calls = 0;
            EffectRunner runner = null;
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration("reenter", "Reenter", null, false, null, null,
                _ => new RecordingEffect(c =>
                {
                    calls++;
                    runner.AdvanceTo(c.CurrentTimeMs);
                })));
            var snapshot = Snapshot(new ChartEffectEvent(0, "first", "reenter", "", 0),
                new ChartEffectEvent(240, "later", "reenter", "", 0));
            var plan = EffectPreparation.Prepare(snapshot.EffectEvents, null, registry).Plan;
            runner = new EffectRunner(plan,
                new EffectSessionContext("test", "normal", EffectExecutionMode.Preview));

            Assert.That(runner.AdvanceTo(1000), Is.False);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(runner.TriggeredCount, Is.EqualTo(1));
            StringAssert.Contains("recursively", runner.Failure.Exception.ToString());
            runner.Dispose();
        }
        [Test]
        public void ReentrantDispose_IsDeferredAndStopsTheCurrentFrame()
        {
            int startStops = 0, laterStarts = 0;
            EffectRunner startRunner = null;
            var startRegistry = new EffectRegistry();
            startRegistry.RegisterEffect(new EffectRegistration("dispose", "Dispose", null,
                false, null, null, _ => new TrackingEffect(
                    () => startRunner.Dispose(), () => { }, _ =>
                    {
                        startStops++;
                        throw new Exception("deferred cleanup failed");
                    })));
            startRegistry.RegisterEffect(new EffectRegistration("later", "Later", null,
                false, null, null, _ => new RecordingEffect(_ => laterStarts++)));
            var startPlan = EffectPreparation.Prepare(Snapshot(
                new ChartEffectEvent(0, "dispose", "dispose", "", 0),
                new ChartEffectEvent(0, "later", "later", "", 1)).EffectEvents,
                null, startRegistry).Plan;
            var startSession = new EffectSessionContext("test", "normal",
                EffectExecutionMode.Preview);
            startRunner = new EffectRunner(startPlan, startSession);

            Assert.That(startRunner.AdvanceTo(0), Is.False);
            Assert.That(startRunner.TriggeredCount, Is.EqualTo(1));
            Assert.That(laterStarts, Is.Zero);
            Assert.That(startStops, Is.EqualTo(1));
            Assert.That(startSession.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(startRunner.Failure, Is.Not.Null);
            StringAssert.Contains("deferred cleanup failed",
                startRunner.Failure.Exception.ToString());
            startRunner.Dispose();
            Assert.That(startStops, Is.EqualTo(1));

            int firstUpdates = 0, secondUpdates = 0;
            EffectRunner updateRunner = null;
            var updateRegistry = new EffectRegistry();
            updateRegistry.RegisterEffect(new EffectRegistration("first", "First", null,
                false, null, null, _ => new TrackingEffect(() => { }, () =>
                {
                    firstUpdates++;
                    updateRunner.Dispose();
                }, _ => { })));
            updateRegistry.RegisterEffect(new EffectRegistration("second", "Second", null,
                false, null, null, _ => new TrackingEffect(
                    () => { }, () => secondUpdates++, _ => { })));
            var updatePlan = EffectPreparation.Prepare(Snapshot(
                new ChartEffectEvent(0, "first", "first", "", 0),
                new ChartEffectEvent(0, "second", "second", "", 1)).EffectEvents,
                null, updateRegistry).Plan;
            updateRunner = new EffectRunner(updatePlan,
                new EffectSessionContext("test", "normal", EffectExecutionMode.Preview));

            Assert.That(updateRunner.AdvanceTo(0), Is.False);
            Assert.That(firstUpdates, Is.EqualTo(1));
            Assert.That(secondUpdates, Is.Zero);

            int gimmickUpdates = 0, gimmickDisposals = 0;
            EffectRunner gimmickRunner = null;
            var gimmickRegistry = new EffectRegistry();
            gimmickRegistry.RegisterEffect(new EffectRegistration(
                CallMusicGimmickEffect.TypeId, "Call", null, false, null, null, null));
            gimmickRegistry.RegisterGimmick(new MusicGimmickRegistration(
                "dispose", "Dispose", () => new CallbackGimmick(() =>
                {
                    gimmickUpdates++;
                    gimmickRunner.Dispose();
                }, () => gimmickDisposals++), new[]
                {
                    new MusicGimmickCommandRegistration("touch", "Touch", null,
                        false, null, null, (g, c, p) => { })
                }));
            var gimmickPlan = EffectPreparation.Prepare(
                Snapshot(Call(0, "call", "touch")).EffectEvents,
                null, gimmickRegistry, "dispose").Plan;
            gimmickRunner = new EffectRunner(gimmickPlan,
                new EffectSessionContext("test", "normal", EffectExecutionMode.Preview));

            Assert.That(gimmickRunner.AdvanceTo(0), Is.False);
            Assert.That(gimmickUpdates, Is.EqualTo(1));
            Assert.That(gimmickDisposals, Is.EqualTo(1));
            gimmickRunner.Dispose();
            Assert.That(gimmickDisposals, Is.EqualTo(1));
        }
        [Test]
        public void ExecutionAndCleanupFailures_ArePreservedAndCleanupIsIdempotent()
        {
            int stops = 0, resourceDisposals = 0;
            var registry = new EffectRegistry();
            registry.RegisterEffect(new EffectRegistration("fail-cleanup", "Fail cleanup", null,
                false, null, null, _ => new ThrowingCleanupEffect(() => stops++)));
            var plan = EffectPreparation.Prepare(
                Snapshot(new ChartEffectEvent(0, "bad", "fail-cleanup", "", 0)).EffectEvents,
                null, registry).Plan;
            var session = new EffectSessionContext("music", "hard", EffectExecutionMode.Preview);
            session.Own(new ThrowingDisposable(() => resourceDisposals++));
            var runner = new EffectRunner(plan, session);

            Assert.That(runner.AdvanceTo(0), Is.False);
            string failure = runner.Failure.Exception.ToString();
            StringAssert.Contains("start failed", failure);
            StringAssert.Contains("stop failed", failure);
            StringAssert.Contains("resource cleanup failed", failure);
            runner.Dispose();
            runner.Dispose();
            Assert.That(stops, Is.EqualTo(1));
            Assert.That(resourceDisposals, Is.EqualTo(1));
        }
        [Test]
        public void AcceptedTransition_CancelsOnceAndDoesNotTriggerSecondRequest()
        {
            var snapshot = Snapshot(Call(0, "begin", "begin-section"),
                Call(240, "count", "count-success"), Call(480, "end", "end-section"),
                Call(720, "transition-a", "request-transition", 0),
                Call(720, "transition-b", "request-transition", 1));
            var values = new Dictionary<string, object>
            {
                ["begin"] = new SampleSectionParameters(30, 1.5),
                ["transition-a"] = new SongTransitionParameters("next", "hard"),
                ["transition-b"] = new SongTransitionParameters("other", "normal")
            };
            var plan = EffectPreparation.Prepare(snapshot.EffectEvents, values,
                EffectRegistry.CreateDefault(), "sample").Plan;
            var services = new TestServices();
            var session = new EffectSessionContext("test", "normal", EffectExecutionMode.Preview,
                services, services, services, services);
            int cancellations = 0;
            var cancellationRegistration = session.CancellationToken.Register(() => cancellations++);
            var runner = new EffectRunner(plan, session);

            Assert.That(runner.AdvanceTo(1000), Is.False);
            Assert.That(runner.TriggeredCount, Is.EqualTo(4));
            Assert.That(services.RequestCount, Is.EqualTo(1));
            Assert.That(cancellations, Is.EqualTo(1));
            Assert.That(runner.AdvanceTo(2000), Is.False);
            runner.Dispose();
            runner.Dispose();
            Assert.That(services.RequestCount, Is.EqualTo(1));
            Assert.That(cancellations, Is.EqualTo(1));
            cancellationRegistration.Dispose();
        }
        private sealed class RecordingEffect : Effect
        {
            private readonly Action<EffectExecutionContext> action;
            public RecordingEffect(Action<EffectExecutionContext> action) { this.action = action; }
            protected override void OnStart(EffectExecutionContext context) { action(context); Complete(); }
        }
        private sealed class TrackingEffect : Effect
        {
            private readonly Action start;
            private readonly Action update;
            private readonly Action<bool> stop;
            public TrackingEffect(Action start, Action update, Action<bool> stop)
            { this.start = start; this.update = update; this.stop = stop; }
            protected override void OnStart(EffectExecutionContext context) { start(); }
            protected override void OnUpdate(EffectExecutionContext context) { update(); }
            protected override void OnStop(bool cancelled) { stop(cancelled); }
        }
        private sealed class CountingMusicGimmick : MusicGimmick
        {
            public int CommandCount;
            public int UpdateCount;
            public int DisposeCount;
            public override void Update(double chartTimeMs) { UpdateCount++; }
            protected override void OnDispose() { DisposeCount++; }
        }
        private sealed class CallbackGimmick : MusicGimmick
        {
            private readonly Action update;
            private readonly Action dispose;
            public CallbackGimmick(Action update, Action dispose)
            { this.update = update; this.dispose = dispose; }
            public override void Update(double chartTimeMs) { update(); }
            protected override void OnDispose() { dispose(); }
        }
        private sealed class ThrowingCleanupEffect : Effect
        {
            private readonly Action stopped;
            public ThrowingCleanupEffect(Action stopped) { this.stopped = stopped; }
            protected override void OnStart(EffectExecutionContext context) { throw new Exception("start failed"); }
            protected override void OnStop(bool cancelled)
            {
                stopped();
                throw new Exception("stop failed");
            }
        }
        private sealed class ThrowingDisposable : IDisposable
        {
            private readonly Action disposed;
            public ThrowingDisposable(Action disposed) { this.disposed = disposed; }
            public void Dispose()
            {
                disposed();
                throw new Exception("resource cleanup failed");
            }
        }
        private sealed class TestServices : IEffectCameraService, IEffectGameState, IEffectRuleService, IEffectTransitionService
        {
            public double CurrentHealth { get; set; } = 100;
            public double X;
            public int SetCount;
            public int RequestCount;
            public IEffectCameraOffset CreateOffset(string owner) => new Handle(this);
            public IEffectRuleHandle AddDamageMultiplier(string owner, double multiplier, double time) => new Handle(this);
            public bool CanTransitionTo(string music, string difficulty) => true;
            public bool RequestTransition(string music, string difficulty, EffectExecutionMode mode) { RequestCount++; return true; }
            private sealed class Handle : IEffectCameraOffset, IEffectRuleHandle
            {
                private readonly TestServices services;
                public Handle(TestServices services) { this.services = services; }
                public void Set(double x, double y, double roll) { services.SetCount++; services.X = x; }
                public void ReleaseAt(double time) { }
                public void Dispose() { services.X = 0; }
            }
        }
    }
}

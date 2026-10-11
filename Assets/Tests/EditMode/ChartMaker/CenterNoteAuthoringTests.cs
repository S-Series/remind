using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using REmind.Charting;
using REmind.Data;
using UnityEngine;

namespace REmind.ChartMaker.Tests
{
    public sealed class CenterNoteAuthoringTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void LegacyScratchPair_RemainsVersionOneAndKeepsBothLanes()
        {
            var holder = new ChartHolder(0, 1200);
            holder.noteTypes[4] = holder.noteTypes[5] = NoteType.Scratch;
            string text = ChartFileCodec.Serialize(new[] { holder }, 120, 0);
            ChartFile reopened = ChartFileCodec.Parse(text);
            Assert.That(reopened.FormatVersion, Is.EqualTo(1));
            Assert.That(text, Does.Not.Contain("centerNotes"));
            var built = ChartHolderDocumentAdapter.Build(reopened.chartDatas, 120, 4);
            Assert.That(built.Succeeded, Is.True);
            Assert.That(built.Document.Notes.Select(n => n.Lane), Is.EquivalentTo(new[] { 8, 9 }));
            Assert.That(built.Document.Notes.All(n => n.Kind == ChartNoteKind.Scratch), Is.True);
        }

        [Test]
        public void VersionTwo_RoundTripsKnobAndTapAlongsideLegacyWithoutMutatingSource()
        {
            ChartHolder[] source = Sample();
            source[4].knobInterpolation = ChartKnobInterpolation.Step;
            source[1].noteTypes[4] = NoteType.Scratch;
            source[1].noteTypes[5] = NoteType.Scratch;
            ChartFile reopened = ChartFileCodec.Parse(ChartFileCodec.Serialize(source, 120, 13));
            Assert.That(reopened.FormatVersion, Is.EqualTo(2));
            Assert.That(reopened.MusicStartCorrectionMs, Is.EqualTo(13));
            Assert.That(reopened.chartDatas.Select(h => h.knobX),
                Is.EqualTo(source.Select(h => h.knobX)));
            Assert.That(reopened.chartDatas.Select(h => h.knobPointKind),
                Is.EqualTo(source.Select(h => h.knobPointKind == ChartNotePointKind.End ? ChartNotePointKind.Mid : h.knobPointKind)));
            Assert.That(reopened.chartDatas.Select(h => h.knobInterpolation),
                Is.EqualTo(source.Select(h => h.knobInterpolation)));
            Assert.That(reopened.chartDatas[1].hasCenterTap, Is.True);
            Assert.That(reopened.chartDatas[1].noteTypes[4], Is.EqualTo(NoteType.Scratch));
            Assert.That(reopened.chartDatas[1].noteTypes[5], Is.EqualTo(NoteType.Scratch));
            Assert.That(source[0].knobX, Is.EqualTo(-0.5));
            Assert.That(source[0].knobPointKind, Is.EqualTo(ChartNotePointKind.Start));
        }

        [Test]
        public void VersionOne_RejectsNewCentralDataInsteadOfSilentlyDroppingIt()
        {
            string text = ChartFileCodec.Serialize(Sample(), 120, 0);
            text = System.Text.RegularExpressions.Regex.Replace(text,
                "\"formatVersion\"\\s*:\\s*2", "\"formatVersion\":1");
            Assert.Throws<FormatException>(() => ChartFileCodec.Parse(text));
        }

        [TestCase("{\"position\":0,\"kind\":\"knobStart\",\"x\":2}")]
        [TestCase("{\"position\":0.5,\"kind\":\"tap\"}")]
        [TestCase("{\"position\":0,\"kind\":\"unknown\"}")]
        [TestCase("{\"position\":0,\"kind\":\"knobStart\",\"x\":0,\"interpolation\":\"curve\"}")]
        [TestCase("{\"position\":0,\"kind\":\"tap\"},{\"position\":0,\"kind\":\"tap\"}")]
        public void VersionTwo_RejectsMalformedCentralRows(string rows)
        {
            string text = "{\"format\":\"REmindChart\",\"formatVersion\":2,\"baseBpm\":120," +
                "\"notes\":[],\"eventDictionary\":[],\"centerNotes\":[" + rows + "]}";
            Assert.Throws<FormatException>(() => ChartFileCodec.Parse(text));
        }

        [Test]
        public void Adapter_DerivesDirectionsAndAllowsAnInitialVerticalSegment()
        {
            var built = ChartHolderDocumentAdapter.Build(Sample(), 120, 4);
            Assert.That(built.Succeeded, Is.True);
            ChartDocumentNote knob = built.Document.Notes.Single(n => n.Kind == ChartNoteKind.Knob);
            Assert.That(knob.Points.Select(p => p.KnobDirection), Is.EqualTo(new[] {
                ChartKnobDirection.Right, ChartKnobDirection.None,
                ChartKnobDirection.Left, ChartKnobDirection.None }));
            ChartHolder[] flat = Sample();
            flat[2].knobX = flat[0].knobX;
            var vertical = ChartHolderDocumentAdapter.Build(flat, 120, 4);
            Assert.That(vertical.Succeeded, Is.True);
            Assert.That(ChartCompiler.Compile(vertical.Document).Succeeded, Is.True);
        }

        [Test]
        public void OlderVersionTwo_DefaultsToLinearAndStepUsesDestinationPoint()
        {
            string text = "{\"format\":\"REmindChart\",\"formatVersion\":2,\"baseBpm\":120," +
                "\"notes\":[],\"eventDictionary\":[],\"centerNotes\":[" +
                "{\"position\":0,\"kind\":\"knobStart\",\"x\":0.5}," +
                "{\"position\":2400,\"kind\":\"knobEnd\",\"x\":-0.5}] }";
            ChartHolder[] old = ChartFileCodec.Parse(text).chartDatas;
            Assert.That(old.All(h => h.knobInterpolation == ChartKnobInterpolation.Linear), Is.True);
            old[1].knobInterpolation = ChartKnobInterpolation.Step;
            var built = ChartHolderDocumentAdapter.Build(old, 120, 4);
            var compiled = ChartCompiler.Compile(built.Document);
            Assert.That(compiled.Succeeded, Is.True);
            var note = compiled.Snapshot.Notes.Single();
            Assert.That(note.Points[1].KnobInterpolation, Is.EqualTo(ChartKnobInterpolation.Step));
            Assert.That(KnobPathEvaluator.TargetX(note, compiled.Snapshot.TimingMap, 999), Is.EqualTo(1.25));
            Assert.That(KnobPathEvaluator.TargetX(note, compiled.Snapshot.TimingMap, 1000), Is.EqualTo(-1.25));
        }

        [Test]
        public void PackageAndPreview_UseTheSameCompiledTargets()
        {
            ChartHolder[] source = Sample();
            source[2].knobX = source[0].knobX;
            source[4].knobInterpolation = ChartKnobInterpolation.Step;
            string text = ChartFileCodec.Serialize(source, 120, 0);
            ChartFile reopened = ChartFileCodec.Parse(text);
            var built = ChartHolderDocumentAdapter.Build(reopened.chartDatas, 120, 4);
            var compiled = ChartCompiler.Compile(built.Document, 1d / ChartHolder.PositionUnitsPerWorldUnit);
            Assert.That(compiled.Succeeded, Is.True,
                string.Join("; ", compiled.Issues.Select(i => i.Message)));
            var knob = compiled.Snapshot.Notes.Single(n => n.Kind == ChartNoteKind.Knob);
            Assert.That(knob.KnobTargets.Count(t => t.Kind == JudgementTargetKind.KnobStart), Is.EqualTo(1));
            Assert.That(knob.KnobTargets.Count(t => t.Kind == JudgementTargetKind.KnobTurn), Is.EqualTo(1));
            Assert.That(knob.KnobTargets.Single(t => t.Kind == JudgementTargetKind.KnobTurn).TargetTimeMs,
                Is.EqualTo(knob.EndTimeMs.Value));
            Assert.That(knob.KnobTargets.Count(t => t.Kind == JudgementTargetKind.KnobTick),
                Is.EqualTo((int)(knob.EndTimeMs.Value / KnobJudgementRules.HoldTickIntervalMs) - 1));
            RuntimeChartPackage package = RuntimeChartPackageCodec.Import(
                ChartMakerRuntimePackageExporter.Export(text));
            Assert.That(package.Snapshot.JudgementTargets.Select(t => t.Id),
                Is.EqualTo(compiled.Snapshot.JudgementTargets.Select(t => t.Id)));
            Assert.That(package.Snapshot.JudgementTargets.Select(t => t.TargetTimeMs),
                Is.EqualTo(compiled.Snapshot.JudgementTargets.Select(t => t.TargetTimeMs)));
            var obj = new GameObject("Center Preview Test");
            obj.SetActive(false);
            try
            {
                var preview = obj.AddComponent<ChartTestPlay>();
                typeof(ChartTestPlay).GetField("snapshot", Hidden).SetValue(preview, compiled.Snapshot);
                typeof(ChartTestPlay).GetField("viewBindings", Hidden).SetValue(preview, built.ViewBindings);
                typeof(ChartTestPlay).GetMethod("BuildAutoTestQueue", Hidden).Invoke(preview, null);
                IList queue = (IList)typeof(ChartTestPlay).GetField("autoTestNotes", Hidden).GetValue(preview);
                Assert.That(queue.Count, Is.EqualTo(compiled.Snapshot.JudgementTargets.Count));
                for (int i = 0; i < queue.Count; i++)
                    Assert.That(queue[i].GetType().GetProperty("TargetTimeMs").GetValue(queue[i]),
                        Is.EqualTo(compiled.Snapshot.JudgementTargets[i].TargetTimeMs));
                var hide = queue[0].GetType().GetField("hideOnProcess", Hidden);
                for (int i = 0; i < queue.Count; i++)
                    if (compiled.Snapshot.JudgementTargets[i].NoteKind == ChartNoteKind.Knob)
                        Assert.That(hide.GetValue(queue[i]), Is.EqualTo(
                            compiled.Snapshot.JudgementTargets[i].TargetTimeMs == knob.EndTimeMs.Value));
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        [Test]
        public void UndoRedo_PreservesCentralDataThroughHolderCopies()
        {
            BindingFlags statics = BindingFlags.NonPublic | BindingFlags.Static;
            var liveRows = (IList)typeof(ChartManager).GetField("ChartHolderList", statics).GetValue(null);
            ChartHolder[] original = liveRows.Cast<ChartHolder>().ToArray();
            FieldInfo changedEvent = typeof(ChartManager).GetField("ChartChanged", statics);
            object listeners = changedEvent.GetValue(null);
            var metadata = ChartEffectDocumentState.CaptureEditable();
            var obj = new GameObject("Center History Test");
            obj.SetActive(false);
            Type history = typeof(ChartManager).Assembly.GetType("ChartEditHistory", true);
            BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            var undo = (IList)history.GetField("UndoChanges", statics).GetValue(null);
            var redo = (IList)history.GetField("RedoChanges", statics).GetValue(null);
            object[] originalUndo = undo.Cast<object>().ToArray();
            object[] originalRedo = redo.Cast<object>().ToArray();
            try
            {
                changedEvent.SetValue(null, null);
                undo.Clear(); redo.Clear(); liveRows.Clear();
                liveRows.Add(new ChartHolder(0, 0));
                object transaction = history.GetMethod("BeginChange", flags).Invoke(null, new object[] { new[] { 0 } });
                ChartHolder row = ChartManager.ChartHolders[0];
                row.hasCenterTap = true;
                row.hasKnobPoint = true;
                row.knobPointKind = ChartNotePointKind.Start;
                row.knobX = -0.75;
                row.knobInterpolation = ChartKnobInterpolation.Step;
                row.secondKnobPoint = new ChartHolder(0, 0) { hasKnobPoint = true,
                    knobPointKind = ChartNotePointKind.Mid, knobX = 0.5,
                    knobInterpolation = ChartKnobInterpolation.Step };
                history.GetMethod("CommitChange", flags).Invoke(null, new[] { transaction });
                var placement = obj.AddComponent<ChartPlacementController>();
                Assert.That(history.GetMethod("Undo", flags).Invoke(null, new object[] { placement }), Is.True);
                Assert.That(ChartManager.ChartHolders.Any(h => h.hasCenterTap || h.hasKnobPoint), Is.False);
                Assert.That(history.GetMethod("Redo", flags).Invoke(null, new object[] { placement }), Is.True);
                Assert.That(ChartManager.ChartHolders[0].hasCenterTap, Is.True);
                Assert.That(ChartManager.ChartHolders[0].knobX, Is.EqualTo(-0.75));
                Assert.That(ChartManager.ChartHolders[0].knobInterpolation, Is.EqualTo(ChartKnobInterpolation.Step));
                Assert.That(ChartManager.ChartHolders[0].secondKnobPoint.knobX, Is.EqualTo(0.5));
                Assert.That(ChartManager.ChartHolders[0].secondKnobPoint.knobInterpolation,
                    Is.EqualTo(ChartKnobInterpolation.Step));

                undo.Clear(); redo.Clear(); liveRows.Clear();
                var prefab = new GameObject("Knob Test Point");
                prefab.transform.SetParent(obj.transform, false);
                void Set(string field, object value) => typeof(ChartPlacementController)
                    .GetField(field, Hidden).SetValue(placement, value);
                Set("middleNoteField", obj.transform);
                Set("chartPreviewNoteField", obj.transform);
                Set("knobNotePrefab", prefab);
                Set("chartPreviewKnobNotePrefab", prefab);
                Set("currentNoteType", NoteType.Knob);
                Set("currentKnobPoint", ChartNotePointKind.Start);
                void Place(int position, float x)
                {
                    ChartHolder holder = ChartManager.GetOrCreateHolder(position / 4800, position % 4800);
                    typeof(ChartPlacementController).GetMethod("PlaceCenterNote", Hidden)
                        .Invoke(placement, new object[] { holder, new Vector3(x * 15, holder.WorldY, 0) });
                }
                Place(0, -0.5f);
                Place(2400, -0.5f);
                Assert.That(ChartManager.ChartHolders[1].knobX, Is.EqualTo(-0.5));
                Place(4800, 0.5f);
                // Legacy incoming Step metadata remains readable; new nodes use an outgoing curve.
                ChartManager.ChartHolders[2].knobInterpolation = ChartKnobInterpolation.Step;
                Assert.That(ChartManager.ChartHolders[2].knobPointKind, Is.EqualTo(ChartNotePointKind.Mid));
                Assert.That(ChartManager.ChartHolders[2].knobInterpolation, Is.EqualTo(ChartKnobInterpolation.Step));
                Set("insertKnobPoint", true);
                Set("currentKnobPoint", ChartNotePointKind.Mid);
                Place(1200, -0.5f);
                var inserted = ChartManager.ChartHolders[1];
                Assert.That(inserted.knobPointKind, Is.EqualTo(ChartNotePointKind.Mid));
                Assert.That(inserted.knobInterpolation, Is.EqualTo(ChartKnobInterpolation.Linear));
                GameObject selected = inserted.knobPointObjects[0];
                Assert.That(placement.TryEditCenterNote(selected, 1800, ChartNotePointKind.Mid,
                    -0.25, out string error), Is.True, error);
                Assert.That(placement.TryEditCenterNote(selected, 3000, ChartNotePointKind.Mid,
                    -0.25, out error), Is.False);
                Assert.That(error, Does.Contain("time order"));
                Assert.That(ChartManager.ChartHolders[1].AbsoluteChartPosition, Is.EqualTo(1800));
                // Undo restores data; the owning play-mode integration checks live object cleanup.
                foreach (ChartHolder holder in ChartManager.ChartHolders) holder.knobPointObjects = null;
                Set("knobNotePrefab", null);
                Set("chartPreviewKnobNotePrefab", null);
                Set("middleNoteField", null);
                Set("chartPreviewNoteField", null);
                Assert.That(history.GetMethod("Undo", flags).Invoke(null, new object[] { placement }), Is.True);
                Assert.That(ChartManager.ChartHolders[1].AbsoluteChartPosition, Is.EqualTo(1200));
                Assert.That(history.GetMethod("Redo", flags).Invoke(null, new object[] { placement }), Is.True);
                Assert.That(ChartManager.ChartHolders[1].AbsoluteChartPosition, Is.EqualTo(1800));
                Assert.That(ChartManager.ChartHolders[1].knobX, Is.EqualTo(-0.25));
            }
            finally
            {
                undo.Clear(); redo.Clear(); liveRows.Clear();
                foreach (object item in originalUndo) undo.Add(item);
                foreach (object item in originalRedo) redo.Add(item);
                foreach (ChartHolder row in original) liveRows.Add(row);
                ChartEffectDocumentState.RestoreEditable(metadata);
                changedEvent.SetValue(null, listeners);
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void SecondPoint_RoundTripsClonesAndExportsInFirstSecondOrder()
        {
            ChartHolder[] source = Sample();
            source[2].secondKnobPoint = new ChartHolder(0, 2400) { hasKnobPoint = true,
                knobPointKind = ChartNotePointKind.Mid, knobX = -0.25,
                knobInterpolation = ChartKnobInterpolation.Step };
            string text = ChartFileCodec.Serialize(source, 120, 0);
            Assert.That(text, Does.Contain("second"));
            ChartHolder[] reopened = ChartFileCodec.Parse(text).chartDatas;
            Assert.That(reopened[2].secondKnobPoint.knobX, Is.EqualTo(-0.25));
            var clone = typeof(ChartHolder).GetMethod("CloneData", Hidden).Invoke(source[2], null) as ChartHolder;
            Assert.That(clone.secondKnobPoint, Is.Not.SameAs(source[2].secondKnobPoint));
            Assert.That(clone.secondKnobPoint.knobX, Is.EqualTo(-0.25));
            var build = ChartHolderDocumentAdapter.Build(reopened, 120, 4);
            Assert.That(build.Succeeded, Is.True);
            var compiled = ChartCompiler.Compile(build.Document);
            Assert.That(compiled.Succeeded, Is.True, string.Join("; ", compiled.Issues.Select(i => i.Message)));
            var knob = compiled.Snapshot.Notes.Single(n => n.Kind == ChartNoteKind.Knob);
            Assert.That(knob.Points.Select(p => p.Position), Is.EqualTo(new[] { 0, 2400, 2400, 3600, 4800 }));
            var package = RuntimeChartPackageCodec.Import(ChartMakerRuntimePackageExporter.Export(text));
            Assert.That(package.Snapshot.Notes.Single(n => n.Kind == ChartNoteKind.Knob).Points.Select(p => p.KnobX),
                Is.EqualTo(knob.Points.Select(p => p.KnobX)));
        }

        [TestCase("{\"position\":0,\"kind\":\"knobMid\",\"x\":0.5,\"second\":true,\"interpolation\":\"step\"}")]
        [TestCase("{\"position\":0,\"kind\":\"knobStart\",\"x\":-0.5},{\"position\":0,\"kind\":\"knobMid\",\"x\":0.5}")]
        [TestCase("{\"position\":0,\"kind\":\"knobStart\",\"x\":-0.5},{\"position\":0,\"kind\":\"knobMid\",\"x\":0.5,\"second\":true,\"interpolation\":\"linear\"}")]
        [TestCase("{\"position\":0,\"kind\":\"knobStart\",\"x\":-0.5},{\"position\":0,\"kind\":\"knobMid\",\"x\":-0.5,\"second\":true,\"interpolation\":\"step\"}")]
        [TestCase("{\"position\":0,\"kind\":\"knobStart\",\"x\":-0.5},{\"position\":0,\"kind\":\"knobMid\",\"x\":0.5,\"second\":true,\"interpolation\":\"step\"},{\"position\":0,\"kind\":\"knobEnd\",\"x\":0,\"second\":true,\"interpolation\":\"step\"}")]
        public void SecondPoint_RejectsOrphansUnmarkedDuplicatesLinearCoincidentAndThirdPoints(string rows)
        {
            VersionTwo_RejectsMalformedCentralRows(rows);
        }

        [Test]
        public void NodeRules_SaveOnlyStartMiddleAndUseSourceCurveUntilNextStart()
        {
            var source = new[]
            {
                new ChartHolder(0, 0) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.Mid, knobX = 0.25 },
                new ChartHolder(0, 1200) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.Start,
                    knobX = -0.5, knobCurve = ChartKnobInterpolation.Sin },
                new ChartHolder(0, 2400) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.Mid,
                    knobX = 0.5, knobCurve = ChartKnobInterpolation.Cos },
                new ChartHolder(0, 3600) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.Mid, knobX = 0 },
                new ChartHolder(1, 0) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.Start, knobX = 0.5 },
                new ChartHolder(1, 1200) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.Mid, knobX = -0.5 }
            };
            string text = ChartFileCodec.Serialize(source, 120, 0);
            Assert.That(text, Does.Contain("knobMiddle").And.Not.Contain("knobEnd"));
            var reopened = ChartFileCodec.Parse(text).chartDatas;
            Assert.That(reopened[1].knobCurve, Is.EqualTo(ChartKnobInterpolation.Sin));
            Assert.That(reopened[2].knobCurve, Is.EqualTo(ChartKnobInterpolation.Cos));
            Assert.That(reopened[0].knobPointKind, Is.EqualTo(ChartNotePointKind.Mid));
            var build = ChartHolderDocumentAdapter.Build(reopened, 120, 4);
            Assert.That(build.Succeeded, Is.True);
            Assert.That(build.Document.Notes.Count, Is.EqualTo(2));
            Assert.That(build.Document.Notes[0].Points.Select(p => p.KnobInterpolation), Is.EqualTo(new[]
                { ChartKnobInterpolation.Linear, ChartKnobInterpolation.Sin, ChartKnobInterpolation.Cos }));
            Assert.That(build.Document.Notes[0].EndPosition, Is.EqualTo(3600));
            Assert.That(build.Document.Notes[1].StartPosition, Is.EqualTo(4800));
            Assert.That(ChartCompiler.Compile(build.Document).Succeeded, Is.True);
        }

        private static ChartHolder[] Sample() => new[]
        {
            new ChartHolder(0, 0) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.Start, knobX = -0.5 },
            new ChartHolder(0, 1200) { hasCenterTap = true },
            new ChartHolder(0, 2400) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.Mid, knobX = 0.5 },
            new ChartHolder(0, 3600) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.Mid, knobX = 0.5 },
            new ChartHolder(1, 0) { hasKnobPoint = true, knobPointKind = ChartNotePointKind.End, knobX = -0.5 }
        };
    }
}

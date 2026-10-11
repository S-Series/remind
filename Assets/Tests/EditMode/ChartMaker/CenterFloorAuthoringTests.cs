using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using REmind.Charting;
using REmind.Data;
using REmind.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace REmind.ChartMaker.Tests
{
    public sealed class CenterFloorAuthoringTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private bool enteredPlayMode;

        [UnityTearDown]
        public IEnumerator LeavePlayModeAfterFailure()
        {
            if (enteredPlayMode && Application.isPlaying)
                yield return new ExitPlayMode();
            enteredPlayMode = false;
        }

        [UnityTest]
        public IEnumerator FloorTools_PlaceCentralEndpointsAndAllowTapInsideAndAtEnd()
        {
            enteredPlayMode = true;
            yield return new EnterPlayMode();
            BindingFlags statics = BindingFlags.NonPublic | BindingFlags.Static;
            var live = (IList)typeof(ChartManager).GetField("ChartHolderList", statics).GetValue(null);
            object[] originalRows = live.Cast<object>().ToArray();
            var changed = typeof(ChartManager).GetField("ChartChanged", statics);
            object listeners = changed.GetValue(null);
            var metadata = ChartEffectDocumentState.CaptureEditable();
            Type history = typeof(ChartManager).Assembly.GetType("ChartEditHistory", true);
            var undo = (IList)history.GetField("UndoChanges", statics).GetValue(null);
            var redo = (IList)history.GetField("RedoChanges", statics).GetValue(null);
            object[] originalUndo = undo.Cast<object>().ToArray(), originalRedo = redo.Cast<object>().ToArray();
            var fixture = new GameObject("Floor Placement Test");
            fixture.SetActive(false);
            try
            {
                changed.SetValue(null, null);
                live.Clear(); undo.Clear(); redo.Clear();
                var placement = fixture.AddComponent<ChartPlacementController>();
                var makerField = new GameObject("Maker").transform;
                var previewField = new GameObject("Preview").transform;
                makerField.SetParent(fixture.transform, false);
                previewField.SetParent(fixture.transform, false);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Notes/Ingame/Floor.prefab");
                void Set(string field, object value) => typeof(ChartPlacementController).GetField(field, Hidden).SetValue(placement, value);
                Set("middleNoteField", makerField);
                Set("chartPreviewNoteField", previewField);
                foreach (string field in new[] { "centerTapNotePrefab", "centerHoldNotePrefab",
                    "chartPreviewCenterTapNotePrefab", "chartPreviewCenterHoldNotePrefab" }) Set(field, prefab);
                void Place(int position, NoteType type)
                {
                    Set("currentNoteType", type);
                    ChartHolder row = ChartManager.GetOrCreateHolder(position / 4800, position % 4800);
                    typeof(ChartPlacementController).GetMethod("PlaceCenterNote", Hidden)
                        .Invoke(placement, new object[] { row, new Vector3(0, row.WorldY, 0) });
                }
                Place(0, NoteType.CenterHold);
                GameObject[] first = ChartManager.ChartHolders[0].centerHoldObjects;
                LogAssert.Expect(LogType.Warning, "A Floor note of this type already occupies this position.");
                Place(0, NoteType.CenterHold);
                Assert.That(ChartManager.ChartHolders[0].centerHoldObjects, Is.SameAs(first));
                LogAssert.Expect(LogType.Warning, "Floor Tap and Long Floor Start cannot share the same position.");
                Place(0, NoteType.CenterTap);
                Assert.That(ChartManager.ChartHolders[0].hasCenterTap, Is.False);
                Place(1200, NoteType.CenterTap);
                Place(2400, NoteType.CenterHold);
                Place(2400, NoteType.CenterTap);
                Assert.That(ChartManager.ChartHolders[2].centerHoldPointKind, Is.EqualTo(ChartNotePointKind.End));
                Assert.That(ChartManager.ChartHolders[2].hasCenterTap, Is.True);
                foreach (ChartHolder row in ChartManager.ChartHolders)
                    foreach (GameObject obj in row.hasCenterHold ? row.centerHoldObjects : row.centerTapObjects)
                        Assert.That(obj.transform.localPosition.x, Is.Zero);
                Assert.That(first[0].GetComponent<BoxCollider2D>(), Is.Not.Null);
                object[] noteData = { first[0], null, 0, NoteType.Unknown, NoteHandleType.Unknown, false };
                Assert.That(typeof(ChartManager).GetMethod("TryGetNoteData", statics).Invoke(null, noteData), Is.True);
                Assert.That(noteData[2], Is.Zero);
                Assert.That(noteData[3], Is.EqualTo(NoteType.CenterHold));
                Assert.That(placement.TryEditCenterNote(first[0], 1200, ChartNotePointKind.Start, 0, out string error), Is.False);
                Assert.That(error, Does.Contain("cannot share"));
                Assert.That(ChartManager.ChartHolders[0].centerHoldObjects, Is.SameAs(first));
                Assert.DoesNotThrow(() => ChartFileCodec.Serialize(ChartManager.ChartHolders, 120, 0));

                fixture.SetActive(true);
                Physics2D.SyncTransforms();
                GameObject[] heads = ChartManager.ChartHolders.Where(r => r.hasCenterHold)
                    .Select(r => r.centerHoldObjects[0]).ToArray();
                Vector3[] headSizes = heads.Select(h => h.GetComponentInChildren<MeshRenderer>().bounds.size).ToArray();
                Vector3[] clickSizes = heads.Select(h => h.GetComponent<BoxCollider2D>().bounds.size).ToArray();
                Assert.That(clickSizes.All(size => size.x > 0 && size.y > 0), Is.True);
                foreach (float scale in new[] { 4f, 0.5f, 1f })
                {
                    makerField.localScale = new Vector3(1, scale, 1);
                    typeof(ChartPlacementController).GetMethod("HandleVerticalDisplayScaleChanged", Hidden)
                        .Invoke(placement, new object[] { scale });
                    Physics2D.SyncTransforms();
                    for (int i = 0; i < heads.Length; i++)
                    {
                        Assert.That(Vector3.Distance(heads[i].GetComponentInChildren<MeshRenderer>().bounds.size,
                            headSizes[i]), Is.LessThan(0.01f), "Long Floor heads must keep their world size after zoom.");
                        Assert.That(Vector3.Distance(heads[i].GetComponent<BoxCollider2D>().bounds.size,
                            clickSizes[i]), Is.LessThan(0.01f), "Long Floor click areas must keep their world size after zoom.");
                    }
                }
            }
            finally
            {
                live.Clear(); undo.Clear(); redo.Clear();
                foreach (object row in originalRows) live.Add(row);
                foreach (object change in originalUndo) undo.Add(change);
                foreach (object change in originalRedo) redo.Add(change);
                ChartEffectDocumentState.RestoreEditable(metadata);
                changed.SetValue(null, listeners);
                UnityEngine.Object.Destroy(fixture);
            }
            yield return null;
            Assert.That(fixture == null, Is.True);
            yield return new ExitPlayMode();
            enteredPlayMode = false;
        }

        [Test]
        public void FloorHold_RoundTripsAlongsideLegacyScratchAndKeepsSourceUnchanged()
        {
            ChartHolder[] rows = FloorRows();
            rows[0].noteTypes[4] = rows[0].noteTypes[5] = NoteType.Scratch;
            rows[0].scratchMotions[0] = new ScratchMotionData(23, ScratchMotionType.Instant);
            rows[0].scratchMotions[1] = new ScratchMotionData(47, ScratchMotionType.None);
            rows[1].noteTypes[4] = rows[2].noteTypes[4] = NoteType.LongScratch;
            rows[1].scratchPointTypes[0] = ScratchPointType.Start;
            rows[2].scratchPointTypes[0] = ScratchPointType.End;
            rows[1].scratchMotions[0] = new ScratchMotionData(32, ScratchMotionType.Gradual);
            rows[2].scratchMotions[0] = new ScratchMotionData(32, ScratchMotionType.Release);
            string text = ChartFileCodec.Serialize(rows, 120, 17);
            ChartFile file = ChartFileCodec.Parse(text);
            Assert.That(file.FormatVersion, Is.EqualTo(2));
            Assert.That(text, Does.Contain("holdStart").And.Contain("holdEnd"));
            Assert.That(file.chartDatas.Select(r => r.hasCenterHold), Is.EqualTo(new[] { true, false, true }));
            Assert.That(file.chartDatas[0].centerHoldPointKind, Is.EqualTo(ChartNotePointKind.Start));
            Assert.That(file.chartDatas[2].centerHoldPointKind, Is.EqualTo(ChartNotePointKind.End));
            Assert.That(file.chartDatas[1].hasCenterTap, Is.True);
            Assert.That(file.chartDatas[0].scratchMotions.Select(m => m.MoveAmount), Is.EqualTo(new[] { 23, 47 }));
            Assert.That(file.chartDatas[2].scratchMotions[0].MotionType, Is.EqualTo(ScratchMotionType.Release));
            var built = ChartHolderDocumentAdapter.Build(file.chartDatas, 120, 4);
            Assert.That(built.Succeeded, Is.True);
            ChartDocumentNote hold = built.Document.Notes.Single(n => n.Kind == ChartNoteKind.CenterHold);
            Assert.That(hold.Lane, Is.EqualTo((int)ChartLane.Center));
            Assert.That(hold.Points.Select(p => p.Kind), Is.EqualTo(new[] { ChartNotePointKind.Start, ChartNotePointKind.End }));
            Assert.That(built.Document.Notes.Where(n => n.Kind == ChartNoteKind.Scratch).Select(n => n.Lane),
                Is.EquivalentTo(new[] { 8, 9 }));
            Assert.That(built.Document.Notes.Single(n => n.Kind == ChartNoteKind.LongScratch).Lane, Is.EqualTo(8));
            Assert.That(rows[0].centerHoldObjects, Is.Null);
            Assert.That(rows[2].centerHoldPointKind, Is.EqualTo(ChartNotePointKind.End));
            var package = RuntimeChartPackageCodec.Import(ChartMakerRuntimePackageExporter.Export(text));
            Assert.That(package.Snapshot.Notes.Single(n => n.Kind == ChartNoteKind.CenterHold).Points.Count, Is.EqualTo(2));
        }

        [TestCase("{\"position\":0,\"kind\":\"holdStart\"}")]
        [TestCase("{\"position\":1200,\"kind\":\"holdEnd\"}")]
        [TestCase("{\"position\":0,\"kind\":\"holdStart\"},{\"position\":1200,\"kind\":\"holdStart\"},{\"position\":2400,\"kind\":\"holdEnd\"}")]
        [TestCase("{\"position\":0,\"kind\":\"holdStart\"},{\"position\":0,\"kind\":\"holdEnd\"}")]
        public void FloorHold_RejectsIncompleteOrDuplicateEndpoints(string rows)
        {
            Assert.Throws<FormatException>(() => ChartFileCodec.Parse(CenterJson(rows)));
        }

        [Test]
        public void Tap_OnlyConflictsWithHoldStartAtSamePosition()
        {
            ChartHolder[] rows = FloorRows();
            rows[2].hasCenterTap = true;
            Assert.DoesNotThrow(() => ChartFileCodec.Serialize(rows, 120, 0));
            rows[0].hasCenterTap = true;
            Assert.Throws<FormatException>(() => ChartFileCodec.Serialize(rows, 120, 0));
            Assert.That(ChartHolderDocumentAdapter.Build(rows, 120, 4).Succeeded, Is.False);
            Assert.That(rows[0].hasCenterHold && rows[0].hasCenterTap, Is.True);
        }

        [Test]
        public void UndoRedo_RestoresAnIncompleteHoldAndAllowsFinishingItAgain()
        {
            BindingFlags statics = BindingFlags.NonPublic | BindingFlags.Static;
            var live = (IList)typeof(ChartManager).GetField("ChartHolderList", statics).GetValue(null);
            object[] originalRows = live.Cast<object>().ToArray();
            var changed = typeof(ChartManager).GetField("ChartChanged", statics);
            object listeners = changed.GetValue(null);
            var metadata = ChartEffectDocumentState.CaptureEditable();
            Type history = typeof(ChartManager).Assembly.GetType("ChartEditHistory", true);
            var undo = (IList)history.GetField("UndoChanges", statics).GetValue(null);
            var redo = (IList)history.GetField("RedoChanges", statics).GetValue(null);
            object[] originalUndo = undo.Cast<object>().ToArray(), originalRedo = redo.Cast<object>().ToArray();
            BindingFlags api = BindingFlags.Public | BindingFlags.Static;
            var fixture = new GameObject("Floor History Test");
            fixture.SetActive(false);
            try
            {
                changed.SetValue(null, null);
                live.Clear(); undo.Clear(); redo.Clear();
                foreach (ChartHolder row in FloorRows()) live.Add(row);
                var placement = fixture.AddComponent<ChartPlacementController>();
                object Begin() => history.GetMethod("BeginChange", api).Invoke(null, new object[] { new[] { 2400 } });
                void Commit(object transaction) => history.GetMethod("CommitChange", api).Invoke(null, new[] { transaction });
                object change = Begin();
                ChartManager.ChartHolders.Single(r => r.AbsoluteChartPosition == 2400).hasCenterHold = false;
                Commit(change);
                Assert.Throws<FormatException>(() => ChartFileCodec.Serialize(ChartManager.ChartHolders, 120, 0));
                Assert.That(history.GetMethod("Undo", api).Invoke(null, new object[] { placement }), Is.True);
                Assert.DoesNotThrow(() => ChartFileCodec.Serialize(ChartManager.ChartHolders, 120, 0));
                Assert.That(history.GetMethod("Redo", api).Invoke(null, new object[] { placement }), Is.True);
                Assert.That(ChartManager.ChartHolders.Any(r => r.hasCenterHold && r.centerHoldPointKind == ChartNotePointKind.End), Is.False);
                change = Begin();
                ChartHolder end = ChartManager.GetOrCreateHolder(0, 2400);
                end.hasCenterHold = true;
                end.centerHoldPointKind = ChartNotePointKind.End;
                Commit(change);
                Assert.DoesNotThrow(() => ChartFileCodec.Serialize(ChartManager.ChartHolders, 120, 0));
                Assert.That(ChartManager.ChartHolders.Single(r => r.AbsoluteChartPosition == 1200).hasCenterTap, Is.True);
                var clone = (ChartHolder)typeof(ChartHolder).GetMethod("CloneData", Hidden).Invoke(end, null);
                Assert.That(clone.hasCenterHold && clone.HasChartData, Is.True);
                Assert.That(clone.centerHoldPointKind, Is.EqualTo(ChartNotePointKind.End));
                Assert.That(clone.centerHoldObjects, Is.Null);
            }
            finally
            {
                live.Clear(); undo.Clear(); redo.Clear();
                foreach (object row in originalRows) live.Add(row);
                foreach (object change in originalUndo) undo.Add(change);
                foreach (object change in originalRedo) redo.Add(change);
                ChartEffectDocumentState.RestoreEditable(metadata);
                changed.SetValue(null, listeners);
                UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        [Test]
        public void FloorMesh_LongSegmentPreservesWidthThicknessAndMaterialAndGetsClickableHead()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Notes/Ingame/Floor.prefab");
            Assert.That(prefab, Is.Not.Null);
            var obj = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Bounds original = FloorNoteGeometry.LocalBounds(obj);
                Material[] materials = obj.GetComponentInChildren<MeshRenderer>().sharedMaterials;
                FloorNoteGeometry.SetSegmentLength(obj, 120);
                Assert.That(obj.transform.localScale.x, Is.EqualTo(prefab.transform.localScale.x));
                Assert.That(obj.transform.localScale.z, Is.EqualTo(prefab.transform.localScale.z));
                Assert.That(FloorNoteGeometry.LocalBounds(obj).size.y * obj.transform.localScale.y,
                    Is.EqualTo(120).Within(0.001));
                Assert.That(obj.GetComponentInChildren<MeshRenderer>().sharedMaterials, Is.EqualTo(materials));
                Assert.That(FloorNoteGeometry.LocalBounds(obj).size.x, Is.EqualTo(original.size.x).Within(0.001));
                FloorNoteGeometry.EnsureClickArea(obj);
                Component view = obj.AddComponent(Type.GetType("NoteView, REmind.Common", true));
                var collider = obj.GetComponent<BoxCollider2D>();
                Assert.That(view.GetType().GetMethod("ContainsClickCollider").Invoke(view, new object[] { collider }), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        private static ChartHolder[] FloorRows() => new[]
        {
            new ChartHolder(0, 0) { hasCenterHold = true, centerHoldPointKind = ChartNotePointKind.Start },
            new ChartHolder(0, 1200) { hasCenterTap = true },
            new ChartHolder(0, 2400) { hasCenterHold = true, centerHoldPointKind = ChartNotePointKind.End }
        };

        private static string CenterJson(string rows) =>
            "{\"format\":\"REmindChart\",\"formatVersion\":2,\"baseBpm\":120," +
            "\"notes\":[],\"eventDictionary\":[],\"centerNotes\":[" + rows + "]}";
    }
}

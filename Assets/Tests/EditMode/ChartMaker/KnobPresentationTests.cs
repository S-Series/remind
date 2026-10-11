using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using REmind.Charting;
using REmind.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace REmind.ChartMaker.Tests
{
    public sealed class KnobPresentationTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private bool secondPointPlayModeEntered;

        [Test]
        public void Ribbon_PreservesAssignedMaterialAndUpdatesMeshWithoutCreatingCursor()
        {
            var obj = new GameObject("Knob Material Test");
            var texture = new Texture2D(2, 2);
            var material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = texture;
            material.color = Color.magenta;
            try
            {
                var view = obj.AddComponent<KnobPathView>();
                view.Configure(new[] { new KnobPathPoint(0, 0), new KnobPathPoint(2, 4) },
                    2, 0, 4, material);
                var renderer = obj.GetComponentInChildren<MeshRenderer>();
                Mesh mesh = obj.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                Assert.That(material.mainTexture, Is.SameAs(texture));
                Assert.That(material.color, Is.EqualTo(Color.magenta));
                Assert.That(obj.transform.childCount, Is.EqualTo(1));
                Assert.That(obj.GetComponentsInChildren<LineRenderer>(), Is.Empty);
                Assert.That(obj.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);

                view.SetPoints(new[] { new KnobPathPoint(0, 0),
                    new KnobPathPoint(2, 4, ChartKnobInterpolation.Step) }, 2);
                Assert.That(obj.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(mesh.vertexCount, Is.EqualTo(6));
                Assert.That(mesh.uv[2].y, Is.GreaterThan(0).And.LessThan(1));
                Object.DestroyImmediate(obj);
                Assert.That((bool)material, Is.True, "Destroying a view must not destroy its assigned asset.");
                Assert.That(material.mainTexture, Is.SameAs(texture));
            }
            finally
            {
                if (obj) Object.DestroyImmediate(obj);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void EditingPoint_IsOneByOneSpriteWithMatchingClickAreaAndScaleCompensation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Notes/Legacy/Preview/KnobPoint.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject point = Object.Instantiate(prefab);
            try
            {
                SpriteRenderer renderer = point.GetComponent<SpriteRenderer>();
                BoxCollider2D collider = point.GetComponent<BoxCollider2D>();
                Assert.That(renderer.sprite, Is.Not.Null);
                // Sliced display size owns the shape, not the source image's pixel ratio.
                Assert.That(renderer.drawMode, Is.EqualTo(SpriteDrawMode.Sliced));
                Assert.That(renderer.size.x, Is.GreaterThan(0));
                Assert.That(renderer.size.y, Is.EqualTo(renderer.size.x).Within(0.001));
                Assert.That(collider.size.x, Is.GreaterThan(0));
                Assert.That(collider.size.y, Is.EqualTo(collider.size.x).Within(0.001));
                float displayWidth = renderer.size.x, clickWidth = collider.size.x;
                void AssertDisplayAndClickBounds()
                {
                    Physics2D.SyncTransforms();
                    Assert.That(renderer.bounds.size.x, Is.EqualTo(displayWidth).Within(0.001));
                    Assert.That(renderer.bounds.size.y, Is.EqualTo(displayWidth).Within(0.001));
                    Assert.That(collider.bounds.size.x, Is.EqualTo(clickWidth).Within(0.001));
                    Assert.That(collider.bounds.size.y, Is.EqualTo(clickWidth).Within(0.001));
                    Assert.That(collider.bounds.center.x, Is.EqualTo(renderer.bounds.center.x).Within(0.001));
                    Assert.That(collider.bounds.center.y, Is.EqualTo(renderer.bounds.center.y).Within(0.001));
                }
                AssertDisplayAndClickBounds();
                Assert.That(point.transform.childCount, Is.Zero, "A point must not contain Floor/arrow visuals.");
                var parent = new GameObject("Editing Field");
                try
                {
                    point.transform.SetParent(parent.transform, false);
                    parent.transform.localScale = new Vector3(1, 4, 1);
                    Component noteView = point.GetComponent("NoteView");
                    noteView.GetType().GetMethod("SetVerticalDisplayScale").Invoke(noteView,
                        new object[] { 4f });
                    AssertDisplayAndClickBounds();
                    Assert.That(noteView.GetType().GetMethod("ContainsClickCollider").Invoke(noteView,
                        new object[] { collider }), Is.True);
                }
                finally { Object.DestroyImmediate(parent); }
            }
            finally { if (point) Object.DestroyImmediate(point); }
        }

        [Test]
        public void Path_FollowsDraggedSpritePointWithoutChangingAuthoredCoordinates()
        {
            var obj = new GameObject("Knob Point Connection Test");
            obj.SetActive(false);
            try
            {
                var placement = obj.AddComponent<ChartPlacementController>();
                var field = new GameObject("Editing Field");
                field.transform.SetParent(obj.transform, false);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Notes/Legacy/Preview/KnobPoint.prefab");
                GameObject first = Object.Instantiate(prefab, field.transform);
                GameObject second = Object.Instantiate(prefab, field.transform);
                var start = new ChartHolder(0, 0) { hasKnobPoint = true,
                    knobPointKind = ChartNotePointKind.Start, knobX = -0.5,
                    knobPointObjects = new[] { first } };
                var end = new ChartHolder(0, 2400) { hasKnobPoint = true,
                    knobPointKind = ChartNotePointKind.End, knobX = 0.5,
                    knobPointObjects = new[] { second } };
                first.transform.localPosition = new Vector3(-7.5f, start.WorldY, 0);
                second.transform.localPosition = new Vector3(3, end.WorldY + 5, 0);
                void Set(string name, object value) => typeof(ChartPlacementController)
                    .GetField(name, Hidden).SetValue(placement, value);
                Set("middleNoteField", field.transform);
                Set("chartPreviewKnobNotePrefab", prefab);
                Set("isDraggingNote", true);
                Set("draggedNoteType", REmind.Data.NoteType.Knob);
                Set("dragAnchor", second);
                Set("dragTargetPosition", second.transform.localPosition);
                typeof(ChartPlacementController).GetMethod("BuildKnobPaths", Hidden).Invoke(placement,
                    new object[] { new List<ChartHolder> { start, end }, new List<KnobPathPoint>() });

                Mesh mesh = field.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                Vector3[] vertices = mesh.vertices;
                Vector3 firstPoint = (vertices[0] + vertices[1]) * 0.5f;
                Assert.That(firstPoint.x, Is.EqualTo(first.transform.localPosition.x).Within(0.001));
                Assert.That(firstPoint.y, Is.EqualTo(first.transform.localPosition.y).Within(0.001));
                Vector3 last = (vertices[2] + vertices[3]) * 0.5f;
                Assert.That(last.x, Is.EqualTo(second.transform.localPosition.x).Within(0.001));
                Assert.That(last.y, Is.EqualTo(second.transform.localPosition.y).Within(0.001));
                Assert.That(end.knobX, Is.EqualTo(0.5), "A drag preview must not author X before commit.");
                Assert.That(end.AbsoluteChartPosition, Is.EqualTo(2400));
                Assert.That(field.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(2),
                    "The connection must not generate any additional note icons or cursor.");
                // Keep teardown independent of the drag-cancel path under test.
                Set("draggedNoteType", REmind.Data.NoteType.Unknown);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [UnityTest]
        public IEnumerator SecondPoint_PlacementEditingDeletionAndPreviewPreserveHorizontalOrder()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Is.Empty,
                "Run this fixture from the Test Runner bootstrap scene.");
            Assert.That(ChartManager.ChartHolders, Is.Empty,
                "Do not enter PlayMode with a live ChartMaker document.");
            yield return new EnterPlayMode();
            secondPointPlayModeEntered = true;
            Assert.That(Application.isPlaying, Is.True);
            var statics = BindingFlags.NonPublic | BindingFlags.Static;
            var rows = (IList)typeof(ChartManager).GetField("ChartHolderList", statics).GetValue(null);
            object[] original = rows.Cast<object>().ToArray();
            var eventField = typeof(ChartManager).GetField("ChartChanged", statics);
            object listeners = eventField.GetValue(null);
            var history = typeof(ChartManager).Assembly.GetType("ChartEditHistory", true);
            var undo = (IList)history.GetField("UndoChanges", statics).GetValue(null);
            var redo = (IList)history.GetField("RedoChanges", statics).GetValue(null);
            object[] oldUndo = undo.Cast<object>().ToArray(), oldRedo = redo.Cast<object>().ToArray();
            var obj = new GameObject("Second Point Test");
            obj.SetActive(false);
            try
            {
                rows.Clear(); undo.Clear(); redo.Clear(); eventField.SetValue(null, null);
                var placement = obj.AddComponent<ChartPlacementController>();
                var field = new GameObject("Preview");
                field.transform.SetParent(obj.transform, false);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Notes/Legacy/Preview/KnobPoint.prefab");
                void Set(string name, object value) => typeof(ChartPlacementController)
                    .GetField(name, Hidden).SetValue(placement, value);
                Set("middleNoteField", field.transform);
                Set("chartPreviewNoteField", field.transform);
                Set("previewField", field.transform);
                Set("knobNotePrefab", prefab);
                Set("chartPreviewKnobNotePrefab", prefab);
                placement.SelectKnobPath();
                void Place(int position, float x)
                {
                    var row = ChartManager.GetOrCreateHolder(position / 4800, position % 4800);
                    typeof(ChartPlacementController).GetMethod("PlaceCenterNote", Hidden).Invoke(placement,
                        new object[] { row, new Vector3(x * 15, row.WorldY, 0) });
                }
                Place(0, -0.5f);
                Place(1200, -0.5f);
                Place(1200, 0.5f);
                var pair = ChartManager.ChartHolders[1];
                Assert.That(pair.secondKnobPoint, Is.Not.Null);
                var second = pair.secondKnobPoint;
                Assert.That(second.knobX, Is.EqualTo(0.5));
                Assert.That(second.knobInterpolation, Is.EqualTo(ChartKnobInterpolation.Step));
                Place(1200, 0.75f);
                Assert.That(pair.secondKnobPoint, Is.SameAs(second));
                Assert.That(second.knobX, Is.EqualTo(0.5));
                Place(2400, 0.5f);
                var built = ChartHolderDocumentAdapter.Build(ChartManager.ChartHolders, 120, 4);
                var compiled = ChartCompiler.Compile(built.Document);
                Assert.That(compiled.Succeeded, Is.True);
                typeof(ChartPlacementController).GetMethod("RefreshKnobPaths", Hidden).Invoke(placement,
                    new object[] { compiled.Snapshot });
                Transform path = field.transform.Cast<Transform>().Last(t => t.name == "Knob Path" && t.gameObject.activeSelf);
                Mesh mesh = path.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                Vector3 Center(int index) => (mesh.vertices[index * 2] + mesh.vertices[index * 2 + 1]) * 0.5f;
                Assert.That(mesh.vertexCount, Is.EqualTo(8));
                Assert.That(Center(1).y, Is.EqualTo(Center(2).y).Within(0.001));
                Assert.That(Center(1).x, Is.EqualTo(-7.5).Within(0.001));
                Assert.That(Center(2).x, Is.EqualTo(7.5).Within(0.001));
                Assert.That(built.ViewBindings.Values.Single().Points.Count, Is.EqualTo(4));
                GameObject secondIcon = second.knobPointObjects[0];
                ChartHolder next = ChartManager.ChartHolders.Single(h => h.AbsoluteChartPosition == 2400);
                next.knobInterpolation = ChartKnobInterpolation.Step;
                Assert.That(placement.TryEditCenterNote(secondIcon, 1200, ChartNotePointKind.Mid,
                    0.5, out string curveError, curve: ChartKnobInterpolation.Sin), Is.True, curveError);
                Assert.That(second.knobInterpolation, Is.EqualTo(ChartKnobInterpolation.Step));
                Assert.That(next.knobInterpolation, Is.EqualTo(ChartKnobInterpolation.Linear));
                var curved = ChartHolderDocumentAdapter.Build(ChartManager.ChartHolders, 120, 4);
                Assert.That(curved.Document.Notes.Single().Points.Last().KnobInterpolation,
                    Is.EqualTo(ChartKnobInterpolation.Sin));
                object change = undo[undo.Count - 1];
                ChartHolder Captured(string state) => ((IEnumerable)change.GetType().GetProperty(state)
                    .GetValue(change)).Cast<object>().Select(item => (ChartHolder)item.GetType()
                        .GetProperty("Holder").GetValue(item)).Single(h => h.AbsoluteChartPosition == 2400);
                Assert.That(Captured("Before").knobInterpolation, Is.EqualTo(ChartKnobInterpolation.Step));
                Assert.That(Captured("After").knobInterpolation, Is.EqualTo(ChartKnobInterpolation.Linear));
                Assert.That(placement.TryEditCenterNote(secondIcon, 1200, ChartNotePointKind.Mid,
                    0.75, out string error), Is.True, error);
                Assert.That(pair.knobX, Is.EqualTo(-0.5));
                Assert.That(second.knobX, Is.EqualTo(0.75));
                Assert.That(placement.TryEditCenterNote(secondIcon, 1200, ChartNotePointKind.Mid,
                    -0.5, out error), Is.False);
                Assert.That(placement.TryEditCenterNote(secondIcon, 1800, ChartNotePointKind.Mid,
                    0.75, out error), Is.True, error);
                Assert.That(pair.secondKnobPoint, Is.Null);
                Assert.That(ChartManager.ChartHolders.Single(h => h.AbsoluteChartPosition == 1800).knobX,
                    Is.EqualTo(0.75));
                GameObject[] deletedSecond = ChartManager.ChartHolders.Single(h => h.AbsoluteChartPosition == 1800)
                    .knobPointObjects.ToArray();
                Assert.That(ChartManager.DeleteNote(secondIcon), Is.True);
                yield return null;
                Assert.That(deletedSecond.All(view => !view), Is.True);
                Place(1200, 0.75f);
                secondIcon = pair.secondKnobPoint.knobPointObjects[0];
                GameObject[] deletedFirst = pair.knobPointObjects.ToArray();
                Assert.That(ChartManager.DeleteNote(pair.knobPointObjects[0]), Is.True);
                Assert.That(pair.secondKnobPoint, Is.Null);
                Assert.That(pair.knobX, Is.EqualTo(0.75));
                Assert.That(pair.knobPointObjects[0], Is.SameAs(secondIcon));
                yield return null;
                Assert.That(deletedFirst.All(view => !view), Is.True);
                Assert.That((bool)secondIcon, Is.True, "Deleting First must preserve the promoted Second icon.");
                GameObject[] deletedRemaining = pair.knobPointObjects.ToArray();
                Assert.That(ChartManager.DeleteNote(secondIcon), Is.True);
                Assert.That(pair.hasKnobPoint, Is.False);
                yield return null;
                Assert.That(deletedRemaining.All(view => !view), Is.True);
            }
            finally
            {
                rows.Clear(); undo.Clear(); redo.Clear();
                foreach (object row in original) rows.Add(row);
                foreach (object item in oldUndo) undo.Add(item);
                foreach (object item in oldRedo) redo.Add(item);
                eventField.SetValue(null, listeners);
                Object.Destroy(obj);
            }
            yield return null;
            Assert.That((bool)obj, Is.False);
            yield return new ExitPlayMode();
            secondPointPlayModeEntered = false;
        }

        [UnityTearDown]
        public IEnumerator ExitPlayModeAfterSecondPointCheck()
        {
            if (secondPointPlayModeEntered && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                yield return null;
                yield return new ExitPlayMode();
            }
            secondPointPlayModeEntered = false;
        }

        [Test]
        public void MakerHover_ConnectsLastPointSupportsVerticalAndStepAndHidesOnExit()
        {
            var statics = BindingFlags.NonPublic | BindingFlags.Static;
            var rows = (IList)typeof(ChartManager).GetField("ChartHolderList",
                statics).GetValue(null);
            object[] original = rows.Cast<object>().ToArray();
            var eventField = typeof(ChartManager).GetField("ChartChanged", statics);
            object listeners = eventField.GetValue(null);
            var history = typeof(ChartManager).Assembly.GetType("ChartEditHistory", true);
            var undo = (IList)history.GetField("UndoChanges", statics).GetValue(null);
            var redo = (IList)history.GetField("RedoChanges", statics).GetValue(null);
            object[] oldUndo = undo.Cast<object>().ToArray(), oldRedo = redo.Cast<object>().ToArray();
            var obj = new GameObject("Knob Hover Test");
            obj.SetActive(false);
            try
            {
                rows.Clear(); undo.Clear(); redo.Clear(); eventField.SetValue(null, null);
                rows.Add(new ChartHolder(0, 0) { hasKnobPoint = true,
                    knobPointKind = ChartNotePointKind.Start, knobX = -0.5 });
                var placement = obj.AddComponent<ChartPlacementController>();
                var field = new GameObject("Placement Field");
                field.transform.SetParent(obj.transform, false);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Notes/Legacy/Preview/KnobPoint.prefab");
                void Set(string name, object value) => typeof(ChartPlacementController)
                    .GetField(name, Hidden).SetValue(placement, value);
                void Invoke(string name, params object[] values) => typeof(ChartPlacementController)
                    .GetMethod(name, Hidden).Invoke(placement, values);
                Set("previewField", field.transform);
                Set("chartPreviewKnobNotePrefab", prefab);
                Set("isHovering", true);
                var router = obj.AddComponent<ChartMakerInputRouter>();
                router.ToolSelected += placement.SetCurrentTool;
                router.SelectKnob();
                Assert.That(router.CurrentTool, Is.EqualTo(ChartToolType.Knob));
                Assert.That(placement.CurrentTool, Is.EqualTo(ChartToolType.Knob));
                var selection = obj.AddComponent<ChartNoteSelectionController>();
                typeof(ChartNoteSelectionController).GetField("placementController", Hidden).SetValue(selection, placement);
                selection.EditSelectedNote(true);
                Assert.That(typeof(ChartPlacementController).GetField("currentKnobPoint", Hidden).GetValue(placement),
                    Is.EqualTo(ChartNotePointKind.Mid));
                selection.EditSelectedNote(false);
                Assert.That(typeof(ChartPlacementController).GetField("currentKnobCurve", Hidden).GetValue(placement),
                    Is.EqualTo(ChartKnobInterpolation.Sin));
                selection.EditSelectedNote(false);
                Assert.That(typeof(ChartPlacementController).GetField("currentKnobCurve", Hidden).GetValue(placement),
                    Is.EqualTo(ChartKnobInterpolation.Cos));
                selection.EditSelectedNote(false);
                Assert.That(typeof(ChartPlacementController).GetField("currentKnobCurve", Hidden).GetValue(placement),
                    Is.EqualTo(ChartKnobInterpolation.Linear));
                Invoke("HandleNormalizedPositionChanged", null, new Vector2(0.75f, 0.4f), null);
                var preview = (GameObject)typeof(ChartPlacementController).GetField("previewNote", Hidden)
                    .GetValue(placement);
                Assert.That(preview.GetComponent<SpriteRenderer>().sprite,
                    Is.SameAs(prefab.GetComponent<SpriteRenderer>().sprite));
                var path = (KnobPathView)typeof(ChartPlacementController)
                    .GetField("knobPlacementPreview", Hidden).GetValue(placement);
                Assert.That(path, Is.Not.Null);
                Mesh mesh = path.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                Vector3 Midpoint(int index) => (mesh.vertices[index * 2] + mesh.vertices[index * 2 + 1]) * 0.5f;
                Assert.That(Midpoint(0).x, Is.EqualTo(-7.5f).Within(0.001));
                Assert.That(Midpoint(1).x, Is.EqualTo(preview.transform.localPosition.x).Within(0.001));

                Invoke("HandleNormalizedPositionChanged", null, new Vector2(0.75f, 0f), null);
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                Assert.That(Midpoint(0).y, Is.EqualTo(Midpoint(1).y).Within(0.001));
                Assert.That(Midpoint(1).x, Is.EqualTo(7.5f).Within(0.001));
                Invoke("HandleNormalizedPositionChanged", null, new Vector2(0.75f, 0.4f), null);

                ((ChartHolder)rows[0]).knobCurve = ChartKnobInterpolation.Sin;
                placement.SelectKnobSin();
                Assert.That(mesh.vertexCount, Is.EqualTo(130));
                Assert.That(Midpoint(32).x, Is.EqualTo(-7.5 + 15 * System.Math.Sin(System.Math.PI / 4)).Within(0.001));
                ((ChartHolder)rows[0]).knobCurve = ChartKnobInterpolation.Cos;
                placement.SelectKnobCos();
                Assert.That(Midpoint(32).x, Is.EqualTo(-7.5 + 15 * (1 - System.Math.Cos(System.Math.PI / 4))).Within(0.001));
                Assert.That(rows.Count, Is.EqualTo(1), "Hovering must not author chart points.");
                GameObject icon = Object.Instantiate(prefab, field.transform);
                ((ChartHolder)rows[0]).knobPointObjects = new[] { icon };
                var selected = (IList)typeof(ChartNoteSelectionController).GetField("selectedNoteObjects", Hidden).GetValue(selection);
                selected.Add(icon);
                selection.EditSelectedNote(true);
                Assert.That(((ChartHolder)rows[0]).knobPointKind, Is.EqualTo(ChartNotePointKind.Mid));
                Invoke("RefreshPreviewPosition");
                Assert.That(path.gameObject.activeSelf, Is.False, "An orphan Middle must have no incoming line.");
                Invoke("RefreshKnobPaths", (object)null);
                Assert.That(typeof(ChartPlacementController).GetField("centerPaths", Hidden).GetValue(placement), Is.Empty);
                var orphan = ChartHolderDocumentAdapter.Build(new[] { (ChartHolder)rows[0] }, 120, 4);
                Assert.That(orphan.Succeeded, Is.True);
                Assert.That(orphan.Document.Notes, Is.Empty);
                Assert.That(orphan.ViewBindings.Count, Is.EqualTo(1));
                selection.EditSelectedNote(true);
                Assert.That(((ChartHolder)rows[0]).knobPointKind, Is.EqualTo(ChartNotePointKind.Start));
                selection.EditSelectedNote(false);
                Assert.That(((ChartHolder)rows[0]).knobCurve, Is.EqualTo(ChartKnobInterpolation.Linear));

                Invoke("SetHovering", false);
                Assert.That(path.gameObject.activeSelf, Is.False);
                Assert.That(preview.activeSelf, Is.False);
            }
            finally
            {
                rows.Clear(); undo.Clear(); redo.Clear();
                foreach (object row in original) rows.Add(row);
                foreach (object item in oldUndo) undo.Add(item);
                foreach (object item in oldRedo) redo.Add(item);
                eventField.SetValue(null, listeners);
                Object.DestroyImmediate(obj);
            }
        }
    }
}

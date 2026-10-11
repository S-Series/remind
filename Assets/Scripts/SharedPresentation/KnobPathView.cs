using System;
using System.Collections.Generic;
using REmind.Charting;
using UnityEngine;

namespace REmind.Presentation
{
    public readonly struct KnobPathPoint
    {
        public KnobPathPoint(float x, float y,
            ChartKnobInterpolation incoming = ChartKnobInterpolation.Linear)
        {
            X = x;
            Y = y;
            Incoming = incoming;
        }

        public float X { get; }
        public float Y { get; }
        public ChartKnobInterpolation Incoming { get; }
    }

    [DisallowMultipleComponent]
    public sealed class KnobPathView : MonoBehaviour
    {
        private const float RibbonHalfWidth = 0.55f;

        private GameObject ribbonObject;
        private Mesh ribbonMesh;
        private Material fallbackMaterial;

        public void Configure(IReadOnlyList<KnobPathPoint> points,
            float verticalScale, int sortingLayerId, int sortingOrder,
            Material sourceMaterial = null)
        {
            if (points == null || points.Count < 2)
                throw new ArgumentException(
                    "A knob path needs at least two points.", nameof(points));
            if (float.IsNaN(verticalScale) ||
                float.IsInfinity(verticalScale) || verticalScale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(verticalScale));

            ClearVisuals();
            List<Vector2> spine = BuildSpine(points);
            if (spine.Count < 2)
                throw new ArgumentException(
                    "A knob path needs two distinct positions.", nameof(points));

            Material material = sourceMaterial;
            if (!material)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (!shader)
                    throw new InvalidOperationException(
                        "A sprite material is required for the knob ribbon.");
                fallbackMaterial = new Material(shader);
                fallbackMaterial.mainTexture = Texture2D.whiteTexture;
                material = fallbackMaterial;
            }

            ribbonObject = new GameObject("Knob Ribbon",
                typeof(MeshFilter), typeof(MeshRenderer));
            ribbonObject.transform.SetParent(transform, false);
            ribbonMesh = new Mesh { name = "Knob Ribbon Mesh" };
            ribbonObject.GetComponent<MeshFilter>().sharedMesh = ribbonMesh;
            MeshRenderer renderer = ribbonObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            BuildRibbonMesh(spine, verticalScale);
        }

        public void SetPoints(IReadOnlyList<KnobPathPoint> points, float verticalScale)
        {
            if (points == null || points.Count < 2)
                throw new ArgumentException(
                    "A knob path needs at least two points.", nameof(points));
            if (float.IsNaN(verticalScale) ||
                float.IsInfinity(verticalScale) || verticalScale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(verticalScale));
            if (!ribbonMesh)
                throw new InvalidOperationException("Configure the knob path before updating it.");
            List<Vector2> spine = BuildSpine(points);
            if (spine.Count < 2)
                throw new ArgumentException(
                    "A knob path needs two distinct positions.", nameof(points));
            BuildRibbonMesh(spine, verticalScale);
        }

        public static List<KnobPathPoint> BuildPlayablePoints(PlayableNoteSnapshot note,
            PlayableChartSnapshot chart, float halfWidth)
        {
            var result = new List<KnobPathPoint>();
            PlayableNotePoint first = note.Points[0];
            result.Add(new KnobPathPoint((float)first.KnobX * halfWidth, (float)first.FloorPosition));
            for (int index = 1; index < note.Points.Count; index++)
            {
                PlayableNotePoint start = note.Points[index - 1], end = note.Points[index];
                var positions = new SortedSet<double>();
                if (KnobPathEvaluator.IsCurve(end.KnobInterpolation))
                    for (int sample = 1; sample < KnobPathEvaluator.CurveSegments; sample++)
                        positions.Add(start.Position + (end.Position - start.Position) *
                            (double)sample / KnobPathEvaluator.CurveSegments);
                foreach (ScrollPoint boundary in chart.ScrollMap.Points)
                    if (boundary.StartTimeMs > start.TimeMs && boundary.StartTimeMs < end.TimeMs)
                        positions.Add(chart.TimingMap.PositionAtTime(boundary.StartTimeMs));
                foreach (double position in positions)
                {
                    double x = KnobPathEvaluator.Interpolate(start.KnobX, end.KnobX,
                        (position - start.Position) / (end.Position - start.Position), end.KnobInterpolation);
                    result.Add(new KnobPathPoint((float)x * halfWidth,
                        (float)chart.ScrollMap.FloorPositionAtChartPosition(position)));
                }
                result.Add(new KnobPathPoint((float)end.KnobX * halfWidth, (float)end.FloorPosition,
                    end.KnobInterpolation == ChartKnobInterpolation.Step ? ChartKnobInterpolation.Step : ChartKnobInterpolation.Linear));
            }
            return result;
        }

        private static List<Vector2> BuildSpine(
            IReadOnlyList<KnobPathPoint> points)
        {
            var result = new List<Vector2>(points.Count * 2);
            AddDistinct(result, new Vector2(points[0].X, points[0].Y));
            for (int i = 1; i < points.Count; i++)
            {
                KnobPathPoint previous = points[i - 1];
                KnobPathPoint current = points[i];
                if (current.Incoming == ChartKnobInterpolation.Step)
                    AddDistinct(result,
                        new Vector2(previous.X, current.Y));
                if (KnobPathEvaluator.IsCurve(current.Incoming))
                    for (int sample = 1; sample < KnobPathEvaluator.CurveSegments; sample++)
                    {
                        double fraction = (double)sample / KnobPathEvaluator.CurveSegments;
                        AddDistinct(result, new Vector2(
                            (float)KnobPathEvaluator.Interpolate(previous.X, current.X, fraction, current.Incoming),
                            Mathf.Lerp(previous.Y, current.Y, (float)fraction)));
                    }
                AddDistinct(result, new Vector2(current.X, current.Y));
            }
            return result;
        }

        private static void AddDistinct(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 ||
                (points[points.Count - 1] - point).sqrMagnitude > 0.000001f)
                points.Add(point);
        }

        private void BuildRibbonMesh(IReadOnlyList<Vector2> spine,
            float verticalScale)
        {
            int count = spine.Count;
            var display = new Vector2[count];
            var distances = new float[count];
            for (int i = 0; i < count; i++)
            {
                display[i] = new Vector2(
                    spine[i].x, spine[i].y * verticalScale);
                if (i > 0)
                    distances[i] = distances[i - 1] +
                        Vector2.Distance(display[i - 1], display[i]);
            }

            var vertices = new Vector3[count * 2];
            var uvs = new Vector2[count * 2];
            var triangles = new int[(count - 1) * 6];
            float uvLength = Mathf.Max(0.0001f, distances[count - 1]);
            for (int i = 0; i < count; i++)
            {
                Vector2 next = i + 1 < count
                    ? (display[i + 1] - display[i]).normalized
                    : (display[i] - display[i - 1]).normalized;
                Vector2 previous = i > 0
                    ? (display[i] - display[i - 1]).normalized
                    : next;
                Vector2 nextNormal = new(-next.y, next.x);
                Vector2 previousNormal =
                    new(-previous.y, previous.x);
                Vector2 sum = nextNormal + previousNormal;
                Vector2 offset;
                if (i == 0 || i == count - 1 ||
                    sum.sqrMagnitude < 0.000001f)
                    offset = nextNormal * RibbonHalfWidth;
                else
                {
                    Vector2 miter = sum.normalized;
                    float projection = Mathf.Max(
                        0.25f, Vector2.Dot(miter, nextNormal));
                    offset = miter * Mathf.Min(
                        RibbonHalfWidth / projection,
                        RibbonHalfWidth * 2f);
                }

                Vector2 left = display[i] + offset;
                Vector2 right = display[i] - offset;
                vertices[i * 2] =
                    new Vector3(left.x, left.y / verticalScale, 0f);
                vertices[i * 2 + 1] =
                    new Vector3(right.x, right.y / verticalScale, 0f);
                float v = distances[i] / uvLength;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);

                if (i == count - 1) continue;
                int index = i * 6;
                int vertex = i * 2;
                triangles[index] = vertex;
                triangles[index + 1] = vertex + 2;
                triangles[index + 2] = vertex + 3;
                triangles[index + 3] = vertex;
                triangles[index + 4] = vertex + 3;
                triangles[index + 5] = vertex + 1;
            }

            ribbonMesh.Clear();
            ribbonMesh.vertices = vertices;
            ribbonMesh.uv = uvs;
            ribbonMesh.triangles = triangles;
            ribbonMesh.RecalculateBounds();
        }

        private void ClearVisuals()
        {
            if (ribbonObject)
            {
                ribbonObject.SetActive(false);
                DestroyViewObject(ribbonObject);
            }
            if (ribbonMesh) DestroyViewObject(ribbonMesh);
            if (fallbackMaterial) DestroyViewObject(fallbackMaterial);
            ribbonObject = null;
            ribbonMesh = null;
            fallbackMaterial = null;
        }

        private void OnDestroy()
        {
            if (ribbonMesh) DestroyViewObject(ribbonMesh);
            if (fallbackMaterial) DestroyViewObject(fallbackMaterial);
        }

        private static void DestroyViewObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}

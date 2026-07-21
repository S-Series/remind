using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace REmind.Common.UI
{
    [AddComponentMenu("REmind/Common UI/Four Point Graphic")]
    [DisallowMultipleComponent]
    public sealed class FourPointGraphic : MaskableGraphic
    {
        [SerializeField] private Sprite sourceSprite;
        [SerializeField] private Vector2 bottomLeft = new Vector2(-50f, -50f);
        [SerializeField] private Vector2 topLeft = new Vector2(-50f, 50f);
        [SerializeField] private Vector2 topRight = new Vector2(50f, 50f);
        [SerializeField] private Vector2 bottomRight = new Vector2(50f, -50f);

        public override Texture mainTexture => sourceSprite != null ? sourceSprite.texture : s_WhiteTexture;

        public Sprite SourceSprite => sourceSprite;

        public Vector2 GetPoint(int index)
        {
            switch (index)
            {
                case 0:
                    return bottomLeft;
                case 1:
                    return topLeft;
                case 2:
                    return topRight;
                case 3:
                    return bottomRight;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        public void SetPoint(int index, Vector2 point)
        {
            switch (index)
            {
                case 0:
                    bottomLeft = point;
                    break;
                case 1:
                    topLeft = point;
                    break;
                case 2:
                    topRight = point;
                    break;
                case 3:
                    bottomRight = point;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(index));
            }

            SetVerticesDirty();
        }

        public void Configure(Sprite sprite, Color tint)
        {
            sourceSprite = sprite;
            color = tint;
            ResetPointsToSpriteBounds();
            SetAllDirty();
        }

        public void ResetPointsToSpriteBounds()
        {
            if (sourceSprite != null)
            {
                var bounds = sourceSprite.bounds;
                SetPoints(
                    new Vector2(bounds.min.x, bounds.min.y),
                    new Vector2(bounds.min.x, bounds.max.y),
                    new Vector2(bounds.max.x, bounds.max.y),
                    new Vector2(bounds.max.x, bounds.min.y));
                return;
            }

            var rect = rectTransform.rect;
            SetPoints(
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMax, rect.yMin));
        }

        public void SetPoints(Vector2 newBottomLeft, Vector2 newTopLeft, Vector2 newTopRight, Vector2 newBottomRight)
        {
            bottomLeft = newBottomLeft;
            topLeft = newTopLeft;
            topRight = newTopRight;
            bottomRight = newBottomRight;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            var outerUv = sourceSprite != null
                ? DataUtility.GetOuterUV(sourceSprite)
                : new Vector4(0f, 0f, 1f, 1f);

            AddVertex(vertexHelper, bottomLeft, new Vector2(outerUv.x, outerUv.y));
            AddVertex(vertexHelper, topLeft, new Vector2(outerUv.x, outerUv.w));
            AddVertex(vertexHelper, topRight, new Vector2(outerUv.z, outerUv.w));
            AddVertex(vertexHelper, bottomRight, new Vector2(outerUv.z, outerUv.y));

            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(0, 2, 3);
        }

        private void AddVertex(VertexHelper vertexHelper, Vector2 position, Vector2 uv)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertex.uv0 = uv;
            vertexHelper.AddVert(vertex);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(FourPointGraphic))]
    public sealed class FourPointGraphicEditor : Editor
    {
        private static readonly string[] PointLabels = { "BL", "TL", "TR", "BR" };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!GUILayout.Button("Reset Points To Sprite Bounds"))
            {
                return;
            }

            var graphic = (FourPointGraphic)target;
            Undo.RecordObject(graphic, "Reset Four Point Graphic");
            graphic.ResetPointsToSpriteBounds();
            EditorUtility.SetDirty(graphic);
        }

        private void OnSceneGUI()
        {
            var graphic = (FourPointGraphic)target;
            var graphicTransform = graphic.rectTransform;
            var worldPoints = new Vector3[4];

            for (var i = 0; i < worldPoints.Length; i++)
            {
                worldPoints[i] = graphicTransform.TransformPoint(graphic.GetPoint(i));
            }

            Handles.color = Color.cyan;
            Handles.DrawAAPolyLine(3f, worldPoints[0], worldPoints[1], worldPoints[2], worldPoints[3], worldPoints[0]);

            for (var i = 0; i < worldPoints.Length; i++)
            {
                var handleSize = HandleUtility.GetHandleSize(worldPoints[i]) * 0.08f;

                EditorGUI.BeginChangeCheck();
                var movedWorldPoint = Handles.FreeMoveHandle(
                    worldPoints[i],
                    handleSize,
                    Vector3.zero,
                    Handles.DotHandleCap);

                Handles.Label(worldPoints[i], PointLabels[i]);

                if (!EditorGUI.EndChangeCheck())
                {
                    continue;
                }

                Undo.RecordObject(graphic, "Move Four Point Graphic Corner");
                var movedLocalPoint = graphicTransform.InverseTransformPoint(movedWorldPoint);
                graphic.SetPoint(i, new Vector2(movedLocalPoint.x, movedLocalPoint.y));
                EditorUtility.SetDirty(graphic);
            }
        }
    }

    internal static class FourPointGraphicConverter
    {
        private const string ConvertSelectedPath = "Tools/REmind/Convert Selected Sprite To Four Point Graphic";

        [MenuItem(ConvertSelectedPath)]
        private static void ConvertSelected()
        {
            Convert(Selection.activeGameObject.GetComponent<SpriteRenderer>());
        }

        [MenuItem(ConvertSelectedPath, true)]
        private static bool CanConvertSelected()
        {
            var selected = Selection.activeGameObject;
            return selected != null
                   && selected.GetComponent<SpriteRenderer>() != null
                   && selected.GetComponentInParent<Canvas>() != null;
        }

        private static void Convert(SpriteRenderer spriteRenderer)
        {
            var targetObject = spriteRenderer.gameObject;
            var sourceSprite = spriteRenderer.sprite;
            var sourceColor = spriteRenderer.color;

            var graphic = Undo.AddComponent<FourPointGraphic>(targetObject);
            graphic.raycastTarget = false;
            graphic.Configure(sourceSprite, sourceColor);

            Undo.DestroyObjectImmediate(spriteRenderer);
            EditorSceneManager.MarkSceneDirty(targetObject.scene);
            Selection.activeGameObject = targetObject;
            SceneView.RepaintAll();
        }
    }
#endif
}

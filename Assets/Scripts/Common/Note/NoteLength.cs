using System;
using UnityEngine;
using UnityEngine.Sprites;

[DisallowMultipleComponent]
public sealed class NoteLength : MonoBehaviour
{
    [Serializable]
    public struct RibbonPoint
    {
        public float x1;
        public float x2;
        public float y;

        public RibbonPoint(float x1, float x2, float y)
        {
            this.x1 = x1;
            this.x2 = x2;
            this.y = y;
        }
    }

    [Header("Ribbon Body")]
    [SerializeField] private Sprite baseSprite;
    [SerializeField] private RibbonPoint[] ribbonPoints =
        Array.Empty<RibbonPoint>();

    private float currentLength;
    private GameObject ribbonObject;
    private MeshFilter ribbonMeshFilter;
    private MeshRenderer ribbonMeshRenderer;
    private PolygonCollider2D ribbonCollider;
    private Mesh ribbonMesh;
    private Material ribbonMaterial;
    private NoteView noteView;
    private bool ribbonColliderEnabled = true;

    /// <summary>선택한 Sprite가 차지하는 로컬 폭의 절반입니다.</summary>
    public float DefaultHalfWidth => baseSprite
        ? baseSprite.bounds.extents.x
        : 0f;

    /// <summary>시작점에서 위쪽으로 표시할 로컬 Y 길이를 적용합니다.</summary>
    public void SetLength(float length)
    {
        if (ribbonPoints == null || ribbonPoints.Length < 2)
        {
            SetStraightLength(length);
            return;
        }

        currentLength = Mathf.Max(0f, length);
        RefreshRibbon();
    }

    /// <summary>선택한 Sprite 폭으로 곧은 리본을 만들어 길이를 적용합니다.</summary>
    public void SetStraightLength(float length)
    {
        float halfWidth = DefaultHalfWidth;
        SetRibbon(
            length,
            new[]
            {
                new RibbonPoint(-halfWidth, halfWidth, 0f),
                new RibbonPoint(-halfWidth, halfWidth, 1f)
            });
    }

    /// <summary>런타임 리본 단면 배열과 표시 길이를 한 번에 적용합니다.</summary>
    public void SetRibbon(float length, RibbonPoint[] points)
    {
        currentLength = Mathf.Max(0f, length);
        ribbonPoints = ClonePoints(points);
        RefreshRibbon();
    }

    /// <summary>런타임에서 리본 단면 배열을 교체하고 현재 길이로 다시 만듭니다.</summary>
    public void SetRibbonPoints(RibbonPoint[] points)
    {
        ribbonPoints = ClonePoints(points);
        RefreshRibbon();
    }

    /// <summary>표시 전용 노트 등에서 생성 리본의 판정 사용 여부를 지정합니다.</summary>
    public void SetRibbonColliderEnabled(bool enabled)
    {
        ribbonColliderEnabled = enabled;

        if (ribbonCollider)
        {
            ribbonCollider.enabled = enabled &&
                currentLength > Mathf.Epsilon;
        }
    }

    private void Awake()
    {
        noteView = GetComponent<NoteView>();
        RefreshRibbon();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            RefreshRibbon();
        }
    }

    private void OnDestroy()
    {
        if (ribbonMesh)
        {
            Destroy(ribbonMesh);
        }

        if (ribbonMaterial)
        {
            Destroy(ribbonMaterial);
        }
    }

    private void RefreshRibbon()
    {
        if (!CanBuildRibbon() || !EnsureRibbonRenderer())
        {
            SetRibbonVisible(false);
            return;
        }

        BuildRibbonMesh();
        SetRibbonVisible(true);
        noteView?.RefreshClickColliders();
    }

    private bool CanBuildRibbon()
    {
        if (!baseSprite || ribbonPoints == null || ribbonPoints.Length < 2)
        {
            return false;
        }

        float startY = ribbonPoints[0].y;
        float endY = ribbonPoints[ribbonPoints.Length - 1].y;

        if (!IsFinite(startY) ||
            !IsFinite(endY) ||
            Mathf.Abs(endY - startY) <= Mathf.Epsilon)
        {
            return false;
        }

        for (int i = 0; i < ribbonPoints.Length; i++)
        {
            RibbonPoint point = ribbonPoints[i];

            if (!IsFinite(point.x1) ||
                !IsFinite(point.x2) ||
                !IsFinite(point.y))
            {
                return false;
            }
        }

        return true;
    }

    private bool EnsureRibbonRenderer()
    {
        if (ribbonObject)
        {
            UpdateRibbonMaterial();
            return ribbonMeshFilter &&
                   ribbonMeshRenderer &&
                   ribbonCollider &&
                   ribbonMesh &&
                   ribbonMaterial;
        }

        SpriteRenderer styleSource =
            GetComponentInChildren<SpriteRenderer>(true);
        ribbonObject = new GameObject(
            "Generated Note Length Ribbon",
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(PolygonCollider2D));
        ribbonObject.layer = styleSource
            ? styleSource.gameObject.layer
            : gameObject.layer;
        ribbonObject.transform.SetParent(transform, false);

        ribbonMeshFilter = ribbonObject.GetComponent<MeshFilter>();
        ribbonMeshRenderer = ribbonObject.GetComponent<MeshRenderer>();
        ribbonCollider = ribbonObject.GetComponent<PolygonCollider2D>();
        ribbonCollider.isTrigger = true;
        ribbonMesh = new Mesh
        {
            name = $"{name} Note Length Ribbon"
        };
        ribbonMesh.MarkDynamic();
        ribbonMeshFilter.sharedMesh = ribbonMesh;

        Material sourceMaterial = styleSource
            ? styleSource.sharedMaterial
            : null;
        Shader fallbackShader = Shader.Find("Sprites/Default");

        if (!sourceMaterial && !fallbackShader)
        {
            Debug.LogError(
                "NoteLength could not find a material for its ribbon body.",
                this);
            return false;
        }

        ribbonMaterial = sourceMaterial
            ? new Material(sourceMaterial)
            : new Material(fallbackShader);
        ribbonMaterial.name = $"{name} Note Length Ribbon (Runtime)";
        ribbonMeshRenderer.sharedMaterial = ribbonMaterial;

        if (styleSource)
        {
            ribbonMeshRenderer.sortingLayerID =
                styleSource.sortingLayerID;
            ribbonMeshRenderer.sortingOrder = styleSource.sortingOrder - 1;

            if (ribbonMaterial.HasProperty("_Color"))
            {
                ribbonMaterial.color = styleSource.color;
            }
        }

        UpdateRibbonMaterial();
        return true;
    }

    private void UpdateRibbonMaterial()
    {
        if (ribbonMaterial)
        {
            ribbonMaterial.mainTexture = baseSprite
                ? baseSprite.texture
                : null;
        }
    }

    private void BuildRibbonMesh()
    {
        int segmentCount = ribbonPoints.Length - 1;
        Vector3[] vertices = new Vector3[segmentCount * 4];
        Vector2[] uvs = new Vector2[segmentCount * 4];
        int[] triangles = new int[segmentCount * 6];
        float startY = ribbonPoints[0].y;
        float profileLength =
            ribbonPoints[ribbonPoints.Length - 1].y - startY;
        float lengthScale = currentLength / profileLength;
        Vector4 spriteUv = DataUtility.GetOuterUV(baseSprite);

        for (int i = 0; i < segmentCount; i++)
        {
            RibbonPoint start = ribbonPoints[i];
            RibbonPoint end = ribbonPoints[i + 1];
            float scaledStartY = (start.y - startY) * lengthScale;
            float scaledEndY = (end.y - startY) * lengthScale;
            int vertexIndex = i * 4;
            int triangleIndex = i * 6;

            vertices[vertexIndex] =
                new Vector3(start.x1, scaledStartY, 0f);
            vertices[vertexIndex + 1] =
                new Vector3(end.x1, scaledEndY, 0f);
            vertices[vertexIndex + 2] =
                new Vector3(end.x2, scaledEndY, 0f);
            vertices[vertexIndex + 3] =
                new Vector3(start.x2, scaledStartY, 0f);

            uvs[vertexIndex] = new Vector2(spriteUv.x, spriteUv.y);
            uvs[vertexIndex + 1] = new Vector2(spriteUv.x, spriteUv.w);
            uvs[vertexIndex + 2] = new Vector2(spriteUv.z, spriteUv.w);
            uvs[vertexIndex + 3] = new Vector2(spriteUv.z, spriteUv.y);

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;
            triangles[triangleIndex + 3] = vertexIndex;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        ribbonMesh.Clear();
        ribbonMesh.vertices = vertices;
        ribbonMesh.uv = uvs;
        ribbonMesh.triangles = triangles;
        ribbonMesh.RecalculateBounds();
        BuildRibbonCollider(startY, lengthScale);
    }

    private void BuildRibbonCollider(float startY, float lengthScale)
    {
        Vector2[] outline = new Vector2[ribbonPoints.Length * 2];

        for (int i = 0; i < ribbonPoints.Length; i++)
        {
            RibbonPoint point = ribbonPoints[i];
            float scaledY = (point.y - startY) * lengthScale;
            outline[i] = new Vector2(point.x1, scaledY);
            outline[outline.Length - 1 - i] =
                new Vector2(point.x2, scaledY);
        }

        ribbonCollider.pathCount = 1;
        ribbonCollider.SetPath(0, outline);
        ribbonCollider.enabled = ribbonColliderEnabled &&
            currentLength > Mathf.Epsilon;
    }

    private void SetRibbonVisible(bool visible)
    {
        if (ribbonMeshRenderer)
        {
            ribbonMeshRenderer.enabled = visible;
        }

        if (ribbonCollider)
        {
            ribbonCollider.enabled = visible &&
                ribbonColliderEnabled &&
                currentLength > Mathf.Epsilon;
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static RibbonPoint[] ClonePoints(RibbonPoint[] points)
    {
        return points == null
            ? Array.Empty<RibbonPoint>()
            : (RibbonPoint[])points.Clone();
    }
}

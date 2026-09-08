using UnityEngine;

/// <summary>
/// Air_Game.png 아틀라스의 1·2·3번 영역을 앞·위·좌우 면에 매핑한
/// Air Note 공용 Mesh를 제공합니다. 바닥과 뒷면은 생성하지 않습니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class AirNoteFourFaceView : MonoBehaviour
{
    private const string SharedMeshName = "Air Note Four Face Mesh";
    private const float AtlasSize = 1024f;

    private static Mesh sharedMesh;

    private void Awake()
    {
        ApplySharedGeometry();
    }

    private void OnEnable()
    {
        ApplySharedGeometry();
    }

    private void OnValidate()
    {
        ApplySharedGeometry();
    }

    private void ApplySharedGeometry()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        BoxCollider2D clickCollider = GetComponent<BoxCollider2D>();

        meshFilter.sharedMesh = GetOrCreateSharedMesh();
        meshRenderer.sortingLayerName = "Notes";
        meshRenderer.sortingOrder = 4;
        float localScaleY = Mathf.Abs(transform.localScale.y);
        float localScaleZ = Mathf.Abs(transform.localScale.z);
        float rotatedHeight = localScaleY > Mathf.Epsilon
            ? localScaleZ / localScaleY
            : 1f;
        clickCollider.size = new Vector2(1f, rotatedHeight);
        clickCollider.offset = Vector2.zero;
        clickCollider.isTrigger = true;
    }

    private static Mesh GetOrCreateSharedMesh()
    {
        if (sharedMesh)
        {
            return sharedMesh;
        }

        sharedMesh = new Mesh
        {
            name = SharedMeshName,
            hideFlags = HideFlags.HideAndDontSave
        };

        Vector3[] vertices =
        {
            // Front (-Z)
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            // Top (+Y)
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            // Left (-X)
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            // Right (+X)
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f)
        };
        int[] triangles =
        {
             0,  1,  2,  0,  2,  3,
             4,  5,  6,  4,  6,  7,
             8,  9, 10,  8, 10, 11,
            12, 13, 14, 12, 14, 15
        };
        Vector2[] uvs = new Vector2[vertices.Length];
        SetFaceUvs(
            uvs,
            0,
            CreatePixelRect(12, 812, 1000, 200));
        SetFaceUvs(
            uvs,
            4,
            CreatePixelRect(12, 512, 1000, 250));
        Rect sideUv = CreatePixelRect(387, 262, 250, 200);
        SetLeftFaceUvs(uvs, 8, sideUv);
        SetFaceUvs(uvs, 12, sideUv);
        Color[] colors = new Color[vertices.Length];
        SetFaceColors(colors, Color.white);

        sharedMesh.vertices = vertices;
        sharedMesh.triangles = triangles;
        sharedMesh.uv = uvs;
        sharedMesh.colors = colors;
        sharedMesh.RecalculateNormals();
        sharedMesh.RecalculateBounds();
        // Material 정점 회전은 Unity가 계산한 원래 Mesh bounds 밖으로 나갈 수
        // 있으므로, 회전된 비균일 직육면체까지 포함하는 여유 bounds를 둡니다.
        sharedMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return sharedMesh;
    }

    /// <summary>
    /// Bilinear filtering이 인접 아틀라스 영역을 읽지 않도록 첫 픽셀과 마지막
    /// 픽셀의 중심을 UV 경계로 사용합니다. 좌표 원점은 Texture UV와 같은
    /// 왼쪽 아래입니다.
    /// </summary>
    private static Rect CreatePixelRect(
        int x,
        int y,
        int width,
        int height)
    {
        float minimumU = (x + 0.5f) / AtlasSize;
        float minimumV = (y + 0.5f) / AtlasSize;
        float maximumU = (x + width - 0.5f) / AtlasSize;
        float maximumV = (y + height - 0.5f) / AtlasSize;
        return Rect.MinMaxRect(
            minimumU,
            minimumV,
            maximumU,
            maximumV);
    }

    private static void SetFaceUvs(
        Vector2[] uvs,
        int startIndex,
        Rect rect)
    {
        uvs[startIndex] = new Vector2(rect.xMin, rect.yMin);
        uvs[startIndex + 1] = new Vector2(rect.xMin, rect.yMax);
        uvs[startIndex + 2] = new Vector2(rect.xMax, rect.yMax);
        uvs[startIndex + 3] = new Vector2(rect.xMax, rect.yMin);
    }

    private static void SetLeftFaceUvs(
        Vector2[] uvs,
        int startIndex,
        Rect rect)
    {
        uvs[startIndex] = new Vector2(rect.xMin, rect.yMin);
        uvs[startIndex + 1] = new Vector2(rect.xMax, rect.yMin);
        uvs[startIndex + 2] = new Vector2(rect.xMax, rect.yMax);
        uvs[startIndex + 3] = new Vector2(rect.xMin, rect.yMax);
    }

    private static void SetFaceColors(Color[] colors, Color color)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = color;
        }
    }
}

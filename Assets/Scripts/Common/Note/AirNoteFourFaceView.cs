using UnityEngine;

/// <summary>
/// 텍스처 없이 앞·위·좌·우 네 면만 가진 Air Note 공용 Mesh를 제공합니다.
/// 면별 명암은 Vertex Color에 저장하고 전체 색상은 공용 Material이 결정합니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class AirNoteFourFaceView : MonoBehaviour
{
    private const string SharedMeshName = "Air Note Four Face Mesh";
    private static readonly Color FrontColor =
        new Color(0.88f, 0.94f, 1f, 1f);
    private static readonly Color TopColor = Color.white;
    private static readonly Color LeftColor =
        new Color(0.52f, 0.68f, 0.78f, 1f);
    private static readonly Color RightColor =
        new Color(0.68f, 0.82f, 0.9f, 1f);

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
        clickCollider.size = Vector2.one;
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
        Vector2[] uvs =
        {
            Vector2.zero, Vector2.up, Vector2.one, Vector2.right,
            Vector2.zero, Vector2.up, Vector2.one, Vector2.right,
            Vector2.zero, Vector2.up, Vector2.one, Vector2.right,
            Vector2.zero, Vector2.up, Vector2.one, Vector2.right
        };
        Color[] colors = new Color[vertices.Length];
        SetFaceColors(colors, 0, FrontColor);
        SetFaceColors(colors, 4, TopColor);
        SetFaceColors(colors, 8, LeftColor);
        SetFaceColors(colors, 12, RightColor);

        sharedMesh.vertices = vertices;
        sharedMesh.triangles = triangles;
        sharedMesh.uv = uvs;
        sharedMesh.colors = colors;
        sharedMesh.RecalculateNormals();
        sharedMesh.RecalculateBounds();
        return sharedMesh;
    }

    private static void SetFaceColors(
        Color[] colors,
        int startIndex,
        Color color)
    {
        for (int i = 0; i < 4; i++)
        {
            colors[startIndex + i] = color;
        }
    }
}

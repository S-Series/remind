using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Renders all six faces with directional material texture fixtures.</summary>
public static class BoxNoteSurfaceValidation
{
    public static void Run()
    {
        BoxNotePrototypeBuilder.Build();
        Scene scene = EditorSceneManager.NewPreviewScene();
        Texture2D[] textures = new Texture2D[3];
        Material material = null;
        RenderTexture target = new(128, 128, 24, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        Texture2D image = new(128, 128, TextureFormat.RGBA32, false, true);
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            for (int index = 0; index < 3; index++)
            {
                Texture2D texture = new(32, 32, TextureFormat.RGBA32, false, true)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                textures[index] = texture;
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color color = new(0.05f, 0.05f, 0.05f, 1f);
                    color[index] = 0.7f;
                    if (x >= 24) color = Color.white;
                    else if (y >= 24) color = Color.black;
                    texture.SetPixel(x, y, color);
                }
                texture.Apply();
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Notes/New/BoxNote.prefab");
            GameObject note = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            note.transform.Find("Temporary Star").gameObject.SetActive(false);
            Transform body = note.transform.Find("Body");
            body.localRotation = Quaternion.identity;
            body.localScale = Vector3.one * 2f;
            MeshRenderer renderer = body.GetComponent<MeshRenderer>();
            Material sharedMaterial = renderer.sharedMaterial;
            Mesh sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;

            // Remove procedural highlights only on a disposable test material so that
            // pixel checks measure the texture, independent of the user's visual tuning.
            material = UnityEngine.Object.Instantiate(sharedMaterial);
            material.SetTexture("_TopMap", textures[0]);
            material.SetTexture("_FrontMap", textures[1]);
            material.SetTexture("_SideMap", textures[2]);
            material.SetColor("_TopColor", Color.white);
            material.SetColor("_SideTopColor", Color.white);
            material.SetColor("_SideBottomColor", Color.white);
            material.SetFloat("_BorderWidth", 0.001f);
            material.SetFloat("_GlowWidth", 0.001f);
            material.SetFloat("_EdgeIntensity", 0f);
            renderer.sharedMaterial = material;

            Camera camera = new GameObject("Box Surface Validation Camera", typeof(Camera))
                .GetComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.targetTexture = target;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Vector3[] directions = { Vector3.up, Vector3.down, Vector3.back,
                Vector3.forward, Vector3.left, Vector3.right };
            string[] names = { "Top", "Bottom", "Front", "Back", "Left", "Right" };
            string output = Path.GetFullPath("Logs/BoxNotePreview/Surfaces");
            Directory.CreateDirectory(output);
            for (int index = 0; index < directions.Length; index++)
            {
                camera.transform.position = directions[index] * 4f;
                camera.transform.LookAt(Vector3.zero,
                    index < 2 ? Vector3.forward : Vector3.up);
                Capture(camera, target, image);
                CheckFace(image, index / 2, names[index]);
                File.WriteAllBytes(Path.Combine(output, names[index] + ".png"), image.EncodeToPNG());
            }

            material.SetTexture("_TopMap", textures[1]);
            camera.transform.position = Vector3.up * 4f;
            camera.transform.LookAt(Vector3.zero, Vector3.forward);
            Capture(camera, target, image);
            CheckFace(image, 1, "Material texture replacement");

            material.SetTexture("_TopMap", null);
            Capture(camera, target, image);
            Color empty = image.GetPixel(64, 64);
            if (Mathf.Min(empty.r, Mathf.Min(empty.g, empty.b)) < 0.8f)
                throw new InvalidOperationException("Empty texture did not use the white fallback.");
            if (body.GetComponent<MeshFilter>().sharedMesh != sharedMesh ||
                renderer.HasPropertyBlock() || note.GetComponentsInChildren<MonoBehaviour>().Length != 0)
                throw new InvalidOperationException("Box Note must render without scripts or property blocks.");
            if (ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("Box Note surface shader has compilation errors.");
            Debug.Log("BOX_NOTE_SURFACES_VERIFIED: all 6 faces, material texture slots, UV orientation, " +
                "texture replacement, white fallback, no runtime scripts/property blocks. " + output);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
            foreach (Texture2D texture in textures) UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void Capture(Camera camera, RenderTexture target, Texture2D image)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        if (!RenderPipeline.SupportsRenderRequest(camera, request))
            throw new InvalidOperationException("URP single-camera validation is unavailable.");
        RenderPipeline.SubmitRenderRequest(camera, request);
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
    }

    public static void RunBorderScaleValidation()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        Material material = null;
        RenderTexture target = new(512, 512, 24, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        Texture2D image = new(512, 512, TextureFormat.RGBA32, false, true);
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Notes/New/BoxNote.prefab");
            GameObject note = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            note.transform.Find("Temporary Star").gameObject.SetActive(false);
            Transform body = note.transform.Find("Body");
            MeshRenderer renderer = body.GetComponent<MeshRenderer>();
            material = UnityEngine.Object.Instantiate(renderer.sharedMaterial);
            renderer.sharedMaterial = material;
            material.SetTexture("_TopMap", null);
            material.SetColor("_TopColor", Color.black);
            material.SetColor("_EdgeColor", Color.white);
            material.SetFloat("_EdgeIntensity", 1f);
            material.SetFloat("_BorderWidth", 0.05f);
            material.SetFloat("_GlowWidth", 0.001f);

            Camera camera = new GameObject("Box Border Validation Camera", typeof(Camera))
                .GetComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.targetTexture = target;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            for (int variant = 0; variant < 4; variant++)
            {
                note.transform.localScale = variant < 2 ? Vector3.one :
                    new Vector3(2f, 0.75f, 0.5f);
                body.localScale = variant == 0 ? new Vector3(1f, 0.5f, 2f) :
                    new Vector3(3f, 0.5f, 2f);
                body.localRotation = variant == 3 ? Quaternion.Euler(25f, 35f, 15f) :
                    Quaternion.Euler(-90f, 0f, 0f);
                Vector3 axisX = body.TransformVector(Vector3.right);
                Vector3 axisZ = body.TransformVector(Vector3.forward);
                Vector3 normal = Vector3.Cross(axisZ, axisX).normalized;
                Vector3 center = body.TransformPoint(Vector3.up * 0.5f);
                camera.transform.position = center + normal * 10f;
                camera.transform.LookAt(center, axisZ);
                Capture(camera, target, image);
                for (int edge = 0; edge < 4; edge++)
                {
                    Vector3 across = edge < 2 ? axisX : axisZ;
                    Vector3 along = edge < 2 ? axisZ : axisX;
                    float sign = edge % 2 == 0 ? 1f : -1f;
                    Vector3 inward = -sign * Vector3.ProjectOnPlane(across, along).normalized;
                    Vector3 midpoint = center + sign * across * 0.5f;
                    // Probe on either side of the 0.05 world-unit border, far enough
                    // from its antialiasing transition at this render resolution.
                    foreach (float distance in new[] { 0.02f, 0.08f })
                    {
                        Vector3 pixel = camera.WorldToViewportPoint(midpoint + inward * distance);
                        Color color = image.GetPixel(Mathf.FloorToInt(pixel.x * image.width),
                            Mathf.FloorToInt(pixel.y * image.height));
                        if (distance < 0.05f ? color.r < 0.85f : color.r > 0.15f)
                            throw new InvalidOperationException($"World border changed: variant {variant}, " +
                                $"edge {edge}, distance {distance}, color {color}.");
                    }
                }
            }
            if (ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("Box Note border shader has compilation errors.");
            Debug.Log("BOX_NOTE_WORLD_BORDER_VERIFIED: body scales, parent nonuniform scale, " +
                "parent scale with rotated body, all four top edges at 0.05 world units.");
        }
        finally
        {
            RenderTexture.active = previousTarget;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void CheckFace(Texture2D image, int channel, string face)
    {
        // Left/middle samples check face selection; white right and black upper bands
        // catch mirrored or rotated UVs when a face is viewed from outside the box.
        foreach (int x in new[] { 26, 64 })
        {
            Color color = image.GetPixel(x, 64);
            if (color[channel] < 0.4f || color[(channel + 1) % 3] > 0.25f ||
                color[(channel + 2) % 3] > 0.25f)
                throw new InvalidOperationException(face + " sampled the wrong face texture: " + color);
        }
        Color right = image.GetPixel(100, 64);
        Color upper = image.GetPixel(64, 100);
        if (Mathf.Min(right.r, Mathf.Min(right.g, right.b)) < 0.6f ||
            Mathf.Max(upper.r, Mathf.Max(upper.g, upper.b)) > 0.15f)
            throw new InvalidOperationException(face + " texture UV orientation is incorrect.");
    }
}

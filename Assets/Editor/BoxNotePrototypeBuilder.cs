using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Creates an isolated note visual without replacing the legacy prefabs.</summary>
public static class BoxNotePrototypeBuilder
{
    private const string Folder = "Assets/Prefabs/Notes/New/";
    private const string PrefabPath = Folder + "BoxNote.prefab";
    private const string StarPath = "Assets/Art/UI/SettingsUI/small_star.png";

    [MenuItem("ReMind/Notes/Create Box Note Prototype")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return;
        }

        Material bodyMaterial = CreateMaterial(Folder + "BoxNote.mat",
            "REmind/Notes/Box Note");
        Material starMaterial = CreateMaterial(Folder + "BoxNoteStar.mat",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default");
        starMaterial.SetColor("_Color", new Color(3f, 3f, 4f, 1f));
        Sprite starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StarPath);
        if (!starSprite)
            throw new InvalidOperationException("Temporary star sprite is missing: " + StarPath);

        Scene buildScene = EditorSceneManager.NewPreviewScene();
        GameObject root = new("BoxNote");
        SceneManager.MoveGameObjectToScene(root, buildScene);
        try
        {
            SortingGroup group = root.AddComponent<SortingGroup>();
            group.sortingLayerName = "Notes";
            group.sortingOrder = 4;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            body.transform.localScale = new Vector3(6.5f, 0.28f, 1.6f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            MeshRenderer bodyRenderer = body.GetComponent<MeshRenderer>();
            bodyRenderer.sharedMaterial = bodyMaterial;
            bodyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            bodyRenderer.receiveShadows = false;
            bodyRenderer.lightProbeUsage = LightProbeUsage.Off;
            bodyRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            bodyRenderer.sortingLayerName = "Notes";

            GameObject star = new("Temporary Star", typeof(SpriteRenderer));
            star.transform.SetParent(root.transform, false);
            star.transform.localPosition = new Vector3(0f, 0f, -0.155f);
            star.transform.localScale = Vector3.one * 0.85f;
            SpriteRenderer starRenderer = star.GetComponent<SpriteRenderer>();
            starRenderer.sprite = starSprite;
            starRenderer.sharedMaterial = starMaterial;
            starRenderer.sortingLayerName = "Notes";
            starRenderer.sortingOrder = 1;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (!prefab)
                throw new InvalidOperationException("Box Note prefab could not be saved.");
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log("BOX_NOTE_CREATED: " + PrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(buildScene);
        }
    }

    private static Material CreateMaterial(string path, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material)
            return material;
        Shader shader = Shader.Find(shaderName);
        if (!shader)
            throw new InvalidOperationException("Shader is missing: " + shaderName);
        material = new Material(shader) { enableInstancing = true };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    [MenuItem("ReMind/Notes/Capture Box Note Prototype")]
    public static void BuildAndCapture()
    {
        Build();
        Scene preview = EditorSceneManager.NewPreviewScene();
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        RenderTexture target = new(1200, 900, 24, RenderTextureFormat.ARGB32);
        Texture2D screenshot = new(1200, 900, TextureFormat.RGB24, false);
        RenderTexture previousTarget = RenderTexture.active;
        VolumeStack previousVolumeStack = null;
        VolumeStack previewVolumeStack = null;
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Mesh mesh = null;
            Material material = null;
            for (int index = 0; index < 3; index++)
            {
                GameObject note = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                note.transform.localPosition = new Vector3(0f, 2.6f - index * 2.6f, 0f);
                // Width variants exercise the shader's world-space border calculation.
                note.transform.localScale = new Vector3(0.75f + index * 0.325f, 1f, 1f);
                Transform star = note.transform.Find("Temporary Star");
                Vector3 starScale = star.localScale;
                starScale.x /= note.transform.localScale.x;
                star.localScale = starScale;
                MeshFilter filter = note.GetComponentInChildren<MeshFilter>();
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (index > 0 && (filter.sharedMesh != mesh || renderer.sharedMaterial != material))
                    throw new InvalidOperationException("Box notes must share their mesh and material.");
                mesh = filter.sharedMesh;
                material = renderer.sharedMaterial;
            }

            Camera camera = new GameObject("Box Note Preview Camera", typeof(Camera))
                .GetComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, preview);
            camera.scene = preview;
            camera.enabled = false;
            camera.transform.position = new Vector3(0f, -5f, -16f);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true;
            camera.orthographicSize = 4.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.012f, 0.019f, 0.045f, 1f);
            camera.allowHDR = true;
            camera.targetTexture = target;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1 << 31;

            GameObject volumeObject = new("Box Note Preview Bloom");
            SceneManager.MoveGameObjectToScene(volumeObject, preview);
            // Volume registers its layer in OnEnable, before the first editor Update.
            volumeObject.layer = 31;
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = profile;
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.65f);
            bloom.scatter.Override(0.65f);

            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new InvalidOperationException("URP single-camera previews are unavailable.");
            // A SingleCameraRequest does not update the Volume framework itself.
            previousVolumeStack = VolumeManager.instance.stack;
            previewVolumeStack = VolumeManager.instance.CreateStack();
            VolumeManager.instance.stack = previewVolumeStack;
            VolumeManager.instance.Update(previewVolumeStack, null, cameraData.volumeLayerMask);
            RenderPipeline.SubmitRenderRequest(camera, request);
            if (ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("Box Note shader compilation failed.");
            if (VolumeManager.instance.stack.GetComponent<Bloom>().intensity.value <= 0f)
                throw new InvalidOperationException("Preview Bloom volume was not applied.");
            RenderTexture.active = target;
            screenshot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
            screenshot.Apply();
            string output = Path.GetFullPath("Logs/BoxNotePreview/preview.png");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, screenshot.EncodeToPNG());
            Debug.Log("BOX_NOTE_VERIFIED: shared mesh/material, shader, width variants. " + output);
        }
        finally
        {
            if (previewVolumeStack != null)
            {
                VolumeManager.instance.stack = previousVolumeStack;
                VolumeManager.instance.DestroyStack(previewVolumeStack);
            }
            RenderTexture.active = previousTarget;
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(target);
            foreach (VolumeComponent component in profile.components)
                UnityEngine.Object.DestroyImmediate(component);
            UnityEngine.Object.DestroyImmediate(profile);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}

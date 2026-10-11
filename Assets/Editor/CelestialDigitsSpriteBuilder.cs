using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>Builds a TMP sprite font from the authored transparent digit PNGs.</summary>
public static class CelestialDigitsSpriteBuilder
{
    private const string Source = "Assets/Art/UI/CommonUI/CelestialDigits/";
    private const string Generated = Source + "Generated/";
    private const string AtlasPath = Generated + "CelestialDigitsAtlas.png";
    private const string MaterialPath = Generated + "CelestialDigitsSprite.mat";
    private const string AssetPath = Generated + "CelestialDigitsSprite.asset";
    private const string PreviewPath = Generated + "CelestialDigitsPreview.prefab";
    private const string FontMaterialPath = Generated + "CelestialDigitsFont.mat";
    private const string FontAssetPath = Generated + "CelestialDigitsFont.asset";
    private const string FontPreviewPath = Generated + "CelestialDigitsFontPreview.prefab";
    private const string PreviewFontPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const int SourceWidth = 480;
    private const int SourceHeight = 380;
    private const int CropX = 72;
    private const int GlyphWidth = 336;
    private const int GlyphHeight = SourceHeight;
    private const int Padding = 8;
    private const int Columns = 5;
    private const int Rows = 2;
    private const int PitchX = GlyphWidth + Padding * 2;
    private const int PitchY = GlyphHeight + Padding * 2;

    [MenuItem("ReMind/Build Celestial TMP Digits")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath))
            throw new InvalidOperationException(
                "CelestialDigitsSprite.asset has been curated in Unity. " +
                "The legacy builder must not overwrite its atlas or glyph metrics.");
        Directory.CreateDirectory(Generated);
        BuildAtlas();
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        if (!atlas || atlas.width != Columns * PitchX ||
            atlas.height != Rows * PitchY)
            throw new InvalidOperationException("Celestial digit atlas import failed.");
        Material material = BuildMaterial(atlas);
        TMP_SpriteAsset spriteAsset = BuildSpriteAsset(atlas, material);
        BuildPreviewPrefab(spriteAsset);
        TMP_FontAsset fontAsset = BuildFontAsset(atlas);
        BuildFontPreviewPrefab(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("CELESTIAL_DIGITS_BUILT: 10 digits, fixed advance " +
            GlyphWidth + ", atlas " + atlas.width + "x" + atlas.height);
    }

    public static void BuildAndCapture()
    {
        Build();
        CapturePreview();
    }

    private static void BuildAtlas()
    {
        int width = Columns * PitchX;
        int height = Rows * PitchY;
        Color32[] pixels = new Color32[width * height];
        for (int digit = 0; digit < 10; digit++)
        {
            string path = Source + "digit_" + digit + ".png";
            if (!File.Exists(path))
                throw new FileNotFoundException("Digit source is missing", path);
            Texture2D source = new(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(path), false) ||
                    source.width != SourceWidth ||
                    source.height != SourceHeight)
                    throw new InvalidOperationException(
                        "Expected a 480x380 transparent PNG: " + path);
                Color32[] sourcePixels = source.GetPixels32();
                int opaque = 0;
                for (int y = 0; y < SourceHeight; y++)
                    for (int x = 0; x < SourceWidth; x++)
                    {
                        if (sourcePixels[y * SourceWidth + x].a == 0)
                            continue;
                        opaque++;
                        if (x < CropX || x >= CropX + GlyphWidth)
                            throw new InvalidOperationException(
                                "Digit would be cropped: " + path);
                    }
                if (opaque == 0)
                    throw new InvalidOperationException("Digit is empty: " + path);

                int originX = Padding + digit % Columns * PitchX;
                int originY = Padding + digit / Columns * PitchY;
                for (int y = 0; y < GlyphHeight; y++)
                    Array.Copy(sourcePixels, y * SourceWidth + CropX,
                        pixels, (originY + y) * width + originX,
                        GlyphWidth);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        Texture2D output = new(width, height, TextureFormat.RGBA32,
            false, false);
        try
        {
            output.SetPixels32(pixels);
            output.Apply(false, false);
            File.WriteAllBytes(AtlasPath, output.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(output);
        }
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(
            AtlasPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    private static Material BuildMaterial(Texture2D atlas)
    {
        Shader shader = Shader.Find("TextMeshPro/Sprite");
        if (!shader)
            throw new InvalidOperationException(
                "TextMeshPro/Sprite shader is unavailable.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            MaterialPath);
        if (!material)
        {
            material = new Material(shader) { name = "CelestialDigitsSprite" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.shader = shader;
        material.mainTexture = atlas;
        material.color = Color.white;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static TMP_SpriteAsset BuildSpriteAsset(Texture2D atlas,
        Material material)
    {
        TMP_SpriteAsset asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(
            AssetPath);
        if (!asset)
        {
            asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "CelestialDigitsSprite";
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        SerializedObject serialized = new(asset);
        serialized.FindProperty("m_Version").stringValue = "1.1.0";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);
        asset.spriteSheet = atlas;
        asset.material = material;
        asset.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(
            material.name);
        asset.faceInfo = new FaceInfo
        {
            familyName = "Celestial Digits",
            styleName = "Regular",
            pointSize = GlyphHeight,
            scale = 1f,
            lineHeight = GlyphHeight,
            ascentLine = GlyphHeight,
            capLine = GlyphHeight,
            meanLine = GlyphHeight * 0.7f,
            baseline = 0f,
            descentLine = 0f
        };
        asset.spriteGlyphTable.Clear();
        asset.spriteCharacterTable.Clear();
        for (int digit = 0; digit < 10; digit++)
        {
            int x = Padding + digit % Columns * PitchX;
            int y = Padding + digit / Columns * PitchY;
            TMP_SpriteGlyph glyph = new((uint)digit,
                new GlyphMetrics(GlyphWidth, GlyphHeight, 0f,
                    GlyphHeight, GlyphWidth),
                new GlyphRect(x, y, GlyphWidth, GlyphHeight), 1f, 0);
            asset.spriteGlyphTable.Add(glyph);
            TMP_SpriteCharacter character = new((uint)('0' + digit),
                asset, glyph)
            {
                name = "digit_" + digit
            };
            asset.spriteCharacterTable.Add(character);
        }
        asset.UpdateLookupTables();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static TMP_FontAsset BuildFontAsset(Texture2D atlas)
    {
        Shader shader = Shader.Find("TextMeshPro/Bitmap Custom Atlas");
        if (!shader)
            throw new InvalidOperationException(
                "TextMeshPro/Bitmap Custom Atlas shader is unavailable.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            FontMaterialPath);
        if (!material)
        {
            material = new Material(shader) { name = "CelestialDigitsFont" };
            AssetDatabase.CreateAsset(material, FontMaterialPath);
        }
        material.shader = shader;
        material.mainTexture = atlas;
        material.SetTexture("_FaceTex", Texture2D.whiteTexture);
        material.SetColor("_FaceColor", Color.white);
        EditorUtility.SetDirty(material);

        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            FontAssetPath);
        if (!asset)
        {
            asset = ScriptableObject.CreateInstance<TMP_FontAsset>();
            asset.name = "CelestialDigitsFont";
            AssetDatabase.CreateAsset(asset, FontAssetPath);
        }
        SerializedObject serialized = new(asset);
        serialized.FindProperty("m_Version").stringValue = "1.1.0";
        serialized.FindProperty("m_AtlasWidth").intValue = atlas.width;
        serialized.FindProperty("m_AtlasHeight").intValue = atlas.height;
        serialized.FindProperty("m_AtlasPadding").intValue = Padding;
        serialized.FindProperty("m_AtlasRenderMode").intValue =
            (int)GlyphRenderMode.RASTER;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        asset.atlasTextures = new[] { atlas };
        asset.material = material;
        asset.faceInfo = new FaceInfo
        {
            familyName = "Celestial Digits",
            styleName = "Regular",
            pointSize = GlyphHeight,
            scale = 1f,
            lineHeight = GlyphHeight,
            ascentLine = GlyphHeight,
            capLine = GlyphHeight,
            meanLine = GlyphHeight * 0.7f,
            baseline = 0f,
            descentLine = 0f
        };
        asset.glyphTable.Clear();
        asset.characterTable.Clear();
        for (int digit = 0; digit < 10; digit++)
        {
            int x = Padding + digit % Columns * PitchX;
            int y = Padding + digit / Columns * PitchY;
            Glyph glyph = new((uint)(digit + 1),
                new GlyphMetrics(GlyphWidth, GlyphHeight, 0f,
                    GlyphHeight, GlyphWidth),
                new GlyphRect(x, y, GlyphWidth, GlyphHeight), 1f, 0);
            asset.glyphTable.Add(glyph);
            asset.characterTable.Add(new TMP_Character(
                (uint)('0' + digit), asset, glyph));
        }
        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            PreviewFontPath) ?? TMP_Settings.defaultFontAsset;
        if (!fallback)
            throw new InvalidOperationException("TMP fallback font is missing.");
        asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>
        {
            fallback
        };
        asset.ReadFontAssetDefinition();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void BuildFontPreviewPrefab(TMP_FontAsset font)
    {
        GameObject root = new("CelestialDigitsFontPreview",
            typeof(RectTransform), typeof(Image));
        try
        {
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1600, 850);
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.025f, 0.042f, 0.105f, 1f);
            background.raycastTarget = false;
            Label(rect, "Heading", "CELESTIAL DIGITS  /  PLAIN TEXT",
                font, null, 46, new Vector2(0, 340),
                new Vector2(1450, 80));
            Label(rect, "AllDigits", "0123456789", font,
                null, 122, new Vector2(0, 165),
                new Vector2(1450, 155));
            Label(rect, "LeadingZeroes", "00123456789", font,
                null, 112, new Vector2(0, -45),
                new Vector2(1450, 150));
            Label(rect, "ScoreExample", "SCORE   00123456789",
                font, null, 94, new Vector2(0, -255),
                new Vector2(1450, 135));
            PrefabUtility.SaveAsPrefabAsset(root, FontPreviewPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildPreviewPrefab(TMP_SpriteAsset spriteAsset)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            PreviewFontPath) ?? TMP_Settings.defaultFontAsset;
        if (!font)
            throw new InvalidOperationException("TMP preview font is missing.");
        GameObject root = new("CelestialDigitsPreview",
            typeof(RectTransform), typeof(Image));
        try
        {
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1600, 850);
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.025f, 0.042f, 0.105f, 1f);
            background.raycastTarget = false;
            Label(rect, "Heading", "CELESTIAL DIGITS  /  TMP SPRITE ASSET",
                font, spriteAsset, 46, new Vector2(0, 340),
                new Vector2(1450, 80));
            Label(rect, "AllDigits", Tags("0123456789"), font,
                spriteAsset, 122, new Vector2(0, 165),
                new Vector2(1450, 155));
            Label(rect, "LeadingZeroes", Tags("00123456789"), font,
                spriteAsset, 112, new Vector2(0, -45),
                new Vector2(1450, 150));
            Label(rect, "ScoreExample", "SCORE   " + Tags("00123456789"),
                font, spriteAsset, 94, new Vector2(0, -255),
                new Vector2(1450, 135));
            PrefabUtility.SaveAsPrefabAsset(root, PreviewPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Label(RectTransform parent, string name, string value,
        TMP_FontAsset font, TMP_SpriteAsset spriteAsset, int fontSize,
        Vector2 position, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.spriteAsset = spriteAsset;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.richText = true;
        label.raycastTarget = false;
        label.text = value;
    }

    private static string Tags(string digits)
    {
        StringBuilder markup = new(digits.Length * 24);
        foreach (char digit in digits)
        {
            if (digit < '0' || digit > '9')
                throw new ArgumentException("Only digits can be tagged.",
                    nameof(digits));
            markup.Append("<sprite name=\"digit_");
            markup.Append(digit);
            markup.Append("\">");
        }
        return markup.ToString();
    }

    private static void CapturePreview()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            NewSceneMode.Single);
        GameObject cameraObject = new("CelestialDigitsPreviewCamera",
            typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.005f, 0.01f, 0.025f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 540f;
        camera.transform.position = new Vector3(0, 0, -1000);
        GameObject canvasObject = new("CelestialDigitsPreviewCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100f;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        string outputDirectory = Path.GetFullPath("Logs/CelestialDigits");
        Directory.CreateDirectory(outputDirectory);
        CapturePrefab(camera, canvasObject.transform, PreviewPath,
            Path.Combine(outputDirectory, "preview.png"),
            Path.Combine(outputDirectory, "preview_transparent.png"),
            null);
        TMP_FontAsset digitFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            FontAssetPath);
        CapturePrefab(camera, canvasObject.transform, FontPreviewPath,
            Path.Combine(outputDirectory, "font_preview.png"),
            Path.Combine(outputDirectory, "font_preview_transparent.png"),
            digitFont);
        Debug.Log("CELESTIAL_DIGITS_PREVIEW: " + outputDirectory);
    }

    private static void CapturePrefab(Camera camera, Transform parent,
        string prefabPath, string opaquePath, string transparentPath,
        TMP_FontAsset expectedDigitFont)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            prefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
            prefab, SceneManager.GetActiveScene());
        try
        {
            instance.transform.SetParent(parent, false);
            foreach (TMP_Text label in instance.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                if (!expectedDigitFont)
                    continue;
                TMP_TextInfo info = label.textInfo;
                for (int i = 0; i < info.characterCount; i++)
                {
                    TMP_CharacterInfo character = info.characterInfo[i];
                    if (character.character >= '0' && character.character <= '9' &&
                        character.fontAsset != expectedDigitFont)
                        throw new InvalidOperationException(
                            "Plain digit used the fallback font in " + label.name);
                }
            }
            Canvas.ForceUpdateCanvases();
            camera.backgroundColor = new Color(0.005f, 0.01f, 0.025f, 1f);
            Render(camera, opaquePath);
            instance.GetComponent<Image>().enabled = false;
            camera.backgroundColor = Color.clear;
            Render(camera, transparentPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void Render(Camera camera, string path)
    {
        RenderTexture target = new(1920, 1080, 24,
            RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Texture2D screenshot = new(1920, 1080,
                TextureFormat.RGBA32, false);
            try
            {
                screenshot.ReadPixels(new Rect(0, 0, 1920, 1080),
                    0, 0);
                screenshot.Apply();
                File.WriteAllBytes(path, screenshot.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(screenshot);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}

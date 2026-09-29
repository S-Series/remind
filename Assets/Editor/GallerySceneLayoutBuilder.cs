using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using REmind.Common.UI;
using REmind.Gameplay.Demo;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates an editable, grouped gallery layout in the ReMind scene.</summary>
public static class GallerySceneLayoutBuilder
{
    private const string ScenePath = "Assets/Scenes/ReMind.unity";
    private const string ArtRoot = "Assets/Art/UI/GalleryUI/";
    private static readonly Color Ivory = Hex("E9EDFF");
    private static readonly Color Muted = Hex("A9B7DD");
    private static readonly Color Blue = Hex("9AB1FF");
    private static readonly Color Deep = new Color(.025f, .038f, .092f, .76f);
    private static readonly Dictionary<Transform, Vector2> Origins = new Dictionary<Transform, Vector2>();
    private static TMP_FontAsset font;
    private static Sprite[] temporaryArt;

    [MenuItem("REmind/Build Gallery Scene Layout")]
    public static void Build()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            Canvas canvas = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                canvas = root.GetComponent<Canvas>();
                if (canvas) break;
            }
            if (!canvas) throw new InvalidOperationException("ReMind scene has no Canvas.");
            Transform oldLayout = canvas.transform.Find("GalleryLayout");
            if (oldLayout) UnityEngine.Object.DestroyImmediate(oldLayout.gameObject);
            Transform temporary = canvas.transform.Find("TemporaryMenuRoot");
            if (temporary) UnityEngine.Object.DestroyImmediate(temporary.gameObject);

            canvas.gameObject.name = "GalleryCanvas";
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Art/Fonts/NanumMyeongjo SDF.asset");
            temporaryArt = new[]
            {
                Load("Assets/Art/UI/MusicSelectUI/StellarPromise_Jacket.png"),
                Load("Assets/Art/Sprites/AI Generated/HomeBackground.png")
            };
            if (!font || !temporaryArt[0] || !temporaryArt[1])
                throw new InvalidOperationException("Gallery layout fonts or temporary art are missing.");

            Origins.Clear();
            RectTransform layout = Rect("GalleryLayout", canvas.transform, 0, 0, 1920, 1080);
            layout.anchorMin = Vector2.zero;
            layout.anchorMax = Vector2.one;
            layout.offsetMin = Vector2.zero;
            layout.offsetMax = Vector2.zero;
            Origins.Add(layout, Vector2.zero);

            RectTransform backdrop = Group("00_Backdrop", layout, 0, 0, 1920, 1080);
            Image("Temp_Background", backdrop, 0, 0, 1920, 1080,
                new Color(.48f, .51f, .76f, 1f), temporaryArt[1]);
            Image("Night_Veil", backdrop, 0, 0, 1920, 1080,
                new Color(.012f, .019f, .061f, .69f));
            Image("Left_Veil", backdrop, 0, 0, 340, 1080,
                new Color(.018f, .026f, .078f, .68f));
            Image("Grid_Veil", backdrop, 350, 143, 994, 855,
                new Color(.022f, .033f, .091f, .24f));

            RectTransform header = Group("01_Header", layout, 28, 22, 1864, 108);
            Icon("Header_Compass_1x1_SpriteSlot", header, 30, 26, 78,
                Load("Assets/Art/UI/CommonUI/header_compass.png"));
            Text("Gallery_Title", header, "G A L L E R Y", 134, 33, 340, 53, 34, Ivory);
            Text("Gallery_Subtitle", header, "기억의 조각들을 다시,", 137, 82, 320, 34, 19, Muted);
            Text("Header_Motto", header, "THE STARS REMEMBER.\nAND SO DO WE.",
                1510, 29, 380, 54, 15, Muted, TextAlignmentOptions.Right);
            Image("Header_Rule", header, 466, 94, 842, 1,
                new Color(.75f, .81f, 1f, .30f));

            RectTransform sidebar = Group("02_Sidebar", layout, 32, 153, 294, 783);
            Image("Sidebar_Rule", sidebar, 40, 160, 2, 688,
                new Color(.75f, .82f, 1f, .47f));
            string[] categories = { "STORY", "ILLUSTRATION", "MUSIC", "MOVIE", "EXTRA" };
            string[] subtitles = { "스토리", "일러스트", "음악", "영상", "기타" };
            string[] categoryIcons =
            {
                "sidebar_story.png", "sidebar_illustration.png", "sidebar_music.png",
                "sidebar_movie.png", "sidebar_extra.png"
            };
            for (int index = 0; index < categories.Length; index++)
            {
                int y = 178 + index * 105;
                RectTransform item = Group(index == 0 ? "Story_Selected" : categories[index],
                    sidebar, 44, y, 278, 91);
                if (index == 0)
                {
                    Image("Selected_Glass", item, 44, y, 278, 91,
                        Color.white, Art("sidebar_selected_tab.png"));
                    Image("Selected_Edge", item, 44, y, 2, 91, Blue);
                }
                Icon("Icon_1x1_SpriteSlot", item, 70, y + 21, 48,
                    Art(categoryIcons[index]),
                    index == 0 ? Ivory : new Color(.64f, .69f, .86f, 1f));
                Text("Category_Label", item, categories[index], 140, y + 17,
                    174, 34, index == 0 ? 24 : 22, index == 0 ? Ivory : Muted);
                Text("Category_Subtitle", item, subtitles[index], 141, y + 52,
                    170, 29, 18, index == 0 ? Ivory : Muted);
            }
            Text("Sidebar_Motto", sidebar, "언젠가, 다시.", 88, 971, 230, 34, 19, Ivory);
            Text("Sidebar_Motto_English", sidebar, "Someday, again.",
                89, 1004, 230, 29, 15, Muted);

            RectTransform chapters = Group("03_Chapters", layout, 365, 73, 962, 116);
            Image("Chapter_TopRule", chapters, 365, 96, 955, 1,
                new Color(.74f, .81f, 1f, .31f));
            Image("Chapter_BottomRule", chapters, 365, 178, 955, 1,
                new Color(.74f, .81f, 1f, .43f));
            string[] chapterNames = { "ALL", "Prologue", "Chapter 1", "Chapter 2", "Chapter 3", "Chapter 4" };
            string[] chapterSubtitles = { "", "프롤로그", "푸른 파도 아래", "흩어지는 별빛", "잊혀가는 이름", "다시, 여기" };
            for (int index = 0; index < chapterNames.Length; index++)
            {
                int x = 380 + index * 158;
                RectTransform tab = Group("Chapter_" + index, chapters, x, 86, 148, 87);
                Icon("Star_1x1_SpriteSlot", tab, x + 65, 87, 21,
                    Art("chapter_star.png"), index == 0 ? Blue : Muted);
                Text("Chapter_Label", tab, chapterNames[index], x, 112, 148, 30,
                    index == 0 ? 20 : 18, index == 0 ? Ivory : Muted,
                    TextAlignmentOptions.Center);
                if (index > 0)
                    Text("Chapter_Subtitle", tab, chapterSubtitles[index], x, 143,
                        148, 27, 16, Muted, TextAlignmentOptions.Center);
                if (index == 0)
                    Image("Selected_Underline", tab, x + 45, 172, 58, 2, Blue);
            }

            RectTransform grid = Group("04_StoryGrid", layout, 365, 207, 963, 766);
            string[] titles =
            {
                "처음의 약속", "낯선 전학생", "바다 위의 학원", "작은 소원들",
                "엇갈리는 시간", "흩어지는 기억", "남겨진 사람들", "그리고, 다시",
                "별이 머무는 곳", "... 언젠가.", "비어 있음", "비어 있음"
            };
            for (int index = 0; index < 12; index++)
            {
                int column = index % 3;
                int row = index / 3;
                int x = 366 + column * 321;
                int y = 209 + row * 190;
                RectTransform card = Group("StoryCard_" + (index + 1).ToString("00"),
                    grid, x, y, 302, 174);
                Sprite artwork = temporaryArt[index % temporaryArt.Length];
                Image("Temp_Artwork", card, x + 5, y + 5, 292, 164,
                    index % 4 < 2 ? new Color(.77f, .82f, 1f, 1f) :
                        new Color(.66f, .71f, .94f, 1f), artwork);
                Image("Artwork_Veil", card, x + 5, y + 5, 292, 164,
                    new Color(.05f, .08f, .19f, index >= 10 ? .76f : .20f));
                Image("Caption_Strip", card, x + 5, y + 122, 292, 47,
                    new Color(.055f, .076f, .16f, .88f));
                Image("Frame", card, x, y, 302, 174, Color.white,
                    Art(index == 0 ? "thumbnail_frame_selected.png" :
                        "thumbnail_frame_normal.png"));
                Text("Number", card, (index + 1).ToString("00"),
                    x + 17, y + 124, 52, 39, 31,
                    index >= 10 ? Muted : Ivory);
                Text("Title", card, titles[index], x + 70, y + 134,
                    174, 29, 18, index >= 10 ? Muted : Ivory);
                if (index >= 10)
                {
                    Image("Locked_Veil", card, x + 3, y + 3, 296, 166,
                        new Color(.04f, .055f, .13f, .60f));
                    Icon("Lock_1x1_SpriteSlot", card, x + 129, y + 64, 44,
                        Art("card_lock.png"));
                }
                else
                    Icon("Play_1x1_SpriteSlot", card, x + 255, y + 127, 32,
                        Art("card_play.png"));
            }

            RectTransform detail = Group("05_Detail", layout, 1350, 178, 538, 792);
            Image("Panel", detail, 1350, 178, 538, 792, Deep);
            Image("Panel_Top", detail, 1350, 178, 538, 1,
                new Color(.8f, .86f, 1f, .5f));
            Image("Panel_Left", detail, 1350, 178, 1, 792,
                new Color(.8f, .86f, 1f, .36f));
            Image("Panel_Right", detail, 1887, 178, 1, 792,
                new Color(.8f, .86f, 1f, .30f));
            Image("Panel_Bottom", detail, 1350, 969, 538, 1,
                new Color(.8f, .86f, 1f, .35f));
            RectTransform feature = Group("FeaturedArtwork", detail, 1371, 205, 496, 277);
            Image("Temp_Featured_Artwork", feature, 1378, 212, 482, 263,
                Color.white, temporaryArt[0]);
            Image("Image_Frame", feature, 1371, 205, 496, 277,
                Color.white, Art("detail_image_frame.png"));
            Text("Selected_Number", detail, "01", 1378, 500, 91, 65, 52, Ivory);
            Text("Selected_Title", detail, "처음의 약속", 1461, 505, 391, 58, 41, Ivory);
            Text("Selected_Chapter", detail, "Prologue", 1467, 558,
                356, 32, 21, Muted);
            Image("Title_Divider", detail, 1378, 601, 483, 1,
                new Color(.76f, .83f, 1f, .40f));
            Text("Description", detail,
                "처음 너를 만난 날,\n하늘은 평소보다 조금 더 가까워 보였고,\n바다는 유난히 조용했다.\n\n아마도 그때부터였을 거야.\n모든 이야기가 이 작은 약속에서 시작된 것은.",
                1380, 630, 478, 205, 21, Ivory);
            Image("Action_Divider", detail, 1378, 834, 483, 1,
                new Color(.76f, .83f, 1f, .36f));
            RectTransform replay = Group("Replay_Button", detail, 1468, 866, 303, 69);
            Image buttonFace = Image("Button_Glass", replay, 1468, 866, 303, 69,
                new Color(.32f, .42f, .75f, .75f),
                Art("replay_button.png"));
            buttonFace.raycastTarget = true;
            Text("Replay_Label", replay, "▶  다시 보기", 1509, 884,
                221, 34, 22, Ivory, TextAlignmentOptions.Center);
            Button replayButton = replay.gameObject.AddComponent<Button>();
            replayButton.targetGraphic = buttonFace;

            RectTransform footer = Group("06_Footer", layout, 37, 1000, 1852, 69);
            Image("Footer_Rule", footer, 40, 1042, 76, 1,
                new Color(.77f, .84f, 1f, .43f));
            Text("Footer_English", footer, "A city where stars meet again.",
                1483, 1021, 352, 33, 16, Muted, TextAlignmentOptions.Right);
            Icon("Footer_Star_1x1_SpriteSlot", footer, 1849, 1009, 39,
                Art("star_flare.png"));

            RectTransform navigation = Group("07_Navigation", layout, 365, 1001, 194, 59);
            RectTransform back = Group("Back_Button", navigation, 365, 1006, 184, 50);
            Image backFace = Image("Button_Glass", back, 365, 1006, 184, 50,
                new Color(.05f, .07f, .15f, .65f));
            backFace.raycastTarget = true;
            Text("Back_Label", back, "←  HOME", 387, 1014, 152, 31,
                19, Muted);
            Button backButton = back.gameObject.AddComponent<Button>();
            backButton.targetGraphic = backFace;
            TemporaryMenuActions actions = navigation.gameObject.AddComponent<TemporaryMenuActions>();
            UnityEventTools.AddPersistentListener(backButton.onClick, actions.ReturnHome);

            MenuNavigationController controller = null;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                controller = sceneRoot.GetComponent<MenuNavigationController>();
                if (controller) break;
            }
            if (controller)
            {
                CanvasGroup canvasGroup = layout.gameObject.AddComponent<CanvasGroup>();
                NavigationScope scope = layout.gameObject.AddComponent<NavigationScope>();
                NavigationNode node = back.gameObject.AddComponent<NavigationNode>();
                var nodeData = new SerializedObject(node);
                nodeData.FindProperty("selectable").objectReferenceValue = backButton;
                nodeData.FindProperty("selectionGraphics").arraySize = 0;
                nodeData.ApplyModifiedPropertiesWithoutUndo();
                var scopeData = new SerializedObject(scope);
                scopeData.FindProperty("controller").objectReferenceValue = controller;
                scopeData.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
                scopeData.FindProperty("initialSelection").objectReferenceValue = node;
                SerializedProperty nodes = scopeData.FindProperty("nodes");
                nodes.arraySize = 1;
                nodes.GetArrayElementAtIndex(0).objectReferenceValue = node;
                scopeData.ApplyModifiedPropertiesWithoutUndo();
                var cancel = (UnityEvent)typeof(NavigationScope)
                    .GetField("onCancel", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(scope);
                UnityEventTools.AddPersistentListener(cancel, actions.ReturnHome);
                var controllerData = new SerializedObject(controller);
                controllerData.FindProperty("initialScope").objectReferenceValue = scope;
                controllerData.ApplyModifiedPropertiesWithoutUndo();
            }

            ValidateSquareSlots(layout);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save ReMind scene.");
            CapturePreview(canvas);
            Debug.Log("Gallery scene layout saved: " + ScenePath);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void CapturePreview(Canvas canvas)
    {
        var cameraObject = new GameObject("Gallery Preview Camera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Hex("0B1022");
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 540;
        var target = new RenderTexture(1920, 1080, 24);
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        RenderTexture oldTarget = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath,
                "../Library/GalleryPreview.png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        finally
        {
            RenderTexture.active = oldTarget;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y,
        float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        Vector2 origin = Origins.TryGetValue(parent, out Vector2 value)
            ? value : Vector2.zero;
        rect.anchoredPosition = new Vector2(x - origin.x, origin.y - y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform Group(string name, Transform parent, float x, float y,
        float width, float height)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        Origins.Add(rect, new Vector2(x, y));
        return rect;
    }

    private static Image Image(string name, Transform parent, float x, float y,
        float width, float height, Color tint, Sprite sprite = null)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        rect.gameObject.AddComponent<CanvasRenderer>();
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }

    private static Image Icon(string name, Transform parent, float x, float y,
        float size, Sprite sprite, Color? tint = null)
    {
        Image image = Image(name, parent, x, y, size, size, tint ?? Color.white, sprite);
        image.preserveAspect = true;
        return image;
    }

    private static TMP_Text Text(string name, Transform parent, string value,
        float x, float y, float width, float height, float size, Color tint,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        rect.gameObject.AddComponent<CanvasRenderer>();
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.color = tint;
        label.text = value;
        label.alignment = align;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static void ValidateSquareSlots(Transform root)
    {
        int count = 0;
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (!image.name.EndsWith("_1x1_SpriteSlot", StringComparison.Ordinal)) continue;
            Vector2 size = image.rectTransform.sizeDelta;
            if (Mathf.Abs(size.x - size.y) > .01f || !image.preserveAspect)
                throw new InvalidOperationException("Gallery sprite slot is not square: " + image.name);
            count++;
        }
        if (count < 20)
            throw new InvalidOperationException("Gallery icon slots are incomplete: " + count);
    }

    private static Sprite Art(string relativePath) => Load(ArtRoot + relativePath);
    private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    private static Color Hex(string rgb)
    {
        ColorUtility.TryParseHtmlString("#" + rgb, out Color color);
        return color;
    }
}

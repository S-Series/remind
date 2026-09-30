using System;
using System.Collections.Generic;
using System.IO;
using REmind.Common.UI;
using REmind.Gameplay;
using REmind.Gameplay.Demo;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors the persistent Character selection presentation.</summary>
public static class CharacterOverlayPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/CharacterOverlay.prefab";
    private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
    private const string BackgroundPath =
        "Assets/Art/UI/CharacterUI/character_select_background.png";
    private const string PortraitPath =
        "Assets/Art/UI/CharacterUI/ame_hina_portraits.png";
    private const string SpritePath = "Assets/Art/UI/CharacterUI/";

    private static readonly Color Glass = new(0.015f, 0.03f, 0.075f, 0.94f);
    private static readonly Color Silver = new(0.83f, 0.88f, 1f, 1f);
    private static TMP_FontAsset font;

    [MenuItem("ReMind/Build Character Overlay Prefab")]
    public static void Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Art/Fonts/NanumMyeongjo SDF.asset") ??
            TMP_Settings.defaultFontAsset;
        Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(
            BackgroundPath);
        Texture2D portraits = AssetDatabase.LoadAssetAtPath<Texture2D>(
            PortraitPath);
        if (!font || !background || !portraits)
            throw new InvalidOperationException(
                "Character overlay font or artwork is missing.");
        RequireSprites();

        var nodes = new List<NavigationNode>();
        GameObject root = new("CharacterOverlay", typeof(RectTransform),
            typeof(Image), typeof(CanvasGroup), typeof(NavigationScope),
            typeof(CharacterOverlayController));
        Stretch((RectTransform)root.transform);
        Image blocker = root.GetComponent<Image>();
        blocker.color = Color.black;
        blocker.raycastTarget = true;
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        NavigationScope scope = root.GetComponent<NavigationScope>();
        CharacterOverlayController controller =
            root.GetComponent<CharacterOverlayController>();

        RectTransform backdrop = Empty(root.transform, "01_Backdrop",
            Vector2.zero, new Vector2(1920, 1080));
        Raw(backdrop, "MoonlitAcademyArtwork", background,
            Vector2.zero, new Vector2(1920, 1080), new Rect(0, 0, 1, 1));
        Image orbit = Decor(backdrop, "CharacterOrbit",
            Sprite("character_orbit"), new Vector2(-390, 188),
            new Vector2(680, 595));
        orbit.color = new Color(1f, 1f, 1f, 0.42f);
        Box(backdrop, "RightReadingShade", new Vector2(485, 0),
            new Vector2(950, 1080), new Color(0.015f, 0.025f, 0.07f, 0.49f));
        Box(backdrop, "BottomReadingShade", new Vector2(0, -430),
            new Vector2(1920, 220), new Color(0.015f, 0.025f, 0.07f, 0.35f));

        RectTransform header = Empty(root.transform, "02_Header",
            Vector2.zero, new Vector2(1920, 1080));
        BuildHeader(header);
        RectTransform leftNarrative = Empty(root.transform,
            "03_LeftNarrative", Vector2.zero, new Vector2(1920, 1080));
        BuildLeftNarrative(leftNarrative);

        RectTransform right = Empty(root.transform, "04_CharacterDetails",
            new Vector2(460, 85), new Vector2(880, 850));
        RectTransform identity = Empty(right, "Identity",
            Vector2.zero, new Vector2(880, 850));
        BuildIdentity(identity);
        RectTransform tabBar = Empty(right, "TabBar",
            Vector2.zero, new Vector2(880, 850));
        Button[] tabs = new Button[3];
        string[] tabNames = { "Profile", "Story", "Voice" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int tab = i;
            tabs[i] = ButtonAt(tabBar, tabNames[i] + "Tab",
                tabNames[i], new Vector2(-280 + i * 280, -100),
                new Vector2(274, 47), 0, i, nodes,
                button => UnityEventTools.AddIntPersistentListener(
                    button.onClick, controller.SelectTab, tab));
            Image tabImage = tabs[i].GetComponent<Image>();
            tabImage.sprite = Sprite(i == 0 ? "tab_selected" : "tab_idle");
            tabImage.color = Color.white;
            tabs[i].transition = Selectable.Transition.None;
            if (i == 0)
                tabs[i].GetComponentInChildren<TMP_Text>().color =
                    new Color(0.15f, 0.23f, 0.52f);
        }

        RectTransform tabContent = Empty(right, "TabContent",
            Vector2.zero, new Vector2(880, 850));
        GameObject[] pages = new GameObject[3];
        pages[0] = BuildProfile(tabContent).gameObject;
        pages[1] = BuildPendingPage(tabContent, "StoryPage", "STORY",
            "이야기는 추후 공개됩니다.").gameObject;
        pages[2] = BuildPendingPage(tabContent, "VoicePage", "VOICE",
            "보이스는 추후 공개됩니다.").gameObject;
        pages[1].SetActive(false);
        pages[2].SetActive(false);

        RectTransform roster = Empty(root.transform, "05_RosterArea",
            Vector2.zero, new Vector2(1920, 1080));
        BuildRoster(roster, background, portraits);
        RectTransform actions = Empty(root.transform, "06_Actions",
            Vector2.zero, new Vector2(1920, 1080));
        Button begin = ButtonAt(actions, "BeginButton", "BEGIN  →",
            new Vector2(744, -410), new Vector2(285, 90),
            2, 3, nodes, button => UnityEventTools.AddPersistentListener(
                button.onClick, controller.Begin), true);
        begin.GetComponent<Image>().sprite = Sprite("begin_button");
        begin.GetComponent<Image>().color = Color.white;
        ButtonAt(actions, "CloseButton", "X",
            new Vector2(884, 405), new Vector2(50, 50),
            0, 3, nodes, button => UnityEventTools.AddPersistentListener(
                button.onClick, controller.CloseOverlay));
        RectTransform footer = Empty(root.transform, "07_Footer",
            Vector2.zero, new Vector2(1920, 1080));
        Decor(footer, "FooterCompass", Sprite("icon_compass"),
            new Vector2(-901, -486), new Vector2(47, 47));
        Label(footer, "FooterCredit",
            "PROJECT ReMind    ·    MEMORIES SHAPE A KINDER TOMORROW.",
            15, new Vector2(613, -512), new Vector2(585, 28),
            TextAlignmentOptions.Right).color = Silver;

        SerializedObject scopeData = new(scope);
        scopeData.FindProperty("canvasGroup").objectReferenceValue = group;
        scopeData.FindProperty("initialSelection").objectReferenceValue =
            tabs[0].GetComponent<NavigationNode>();
        SerializedProperty nodeList = scopeData.FindProperty("nodes");
        nodeList.arraySize = nodes.Count;
        for (int i = 0; i < nodes.Count; i++)
            nodeList.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
        scopeData.ApplyModifiedPropertiesWithoutUndo();
        UnityEvent onCancel = (UnityEvent)typeof(NavigationScope)
            .GetField("onCancel", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).GetValue(scope);
        UnityEventTools.AddPersistentListener(onCancel, controller.CloseOverlay);

        SerializedObject data = new(controller);
        data.FindProperty("scope").objectReferenceValue = scope;
        SetArray(data, "tabPages", pages);
        SetArray(data, "tabBackgrounds",
            Array.ConvertAll(tabs, tab => tab.GetComponent<Image>()));
        SetArray(data, "tabLabels",
            Array.ConvertAll(tabs, tab =>
                tab.GetComponentInChildren<TMP_Text>()));
        data.FindProperty("selectedTabSprite").objectReferenceValue =
            Sprite("tab_selected");
        data.FindProperty("idleTabSprite").objectReferenceValue =
            Sprite("tab_idle");
        data.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        BindBootstrap();
        AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_OVERLAY_PREFAB_BUILT");
    }

    [MenuItem("ReMind/Capture Character Overlay Preview")]
    public static void CapturePreview()
    {
        Scene scene = EditorSceneManager.OpenScene(BootstrapPath,
            OpenSceneMode.Single);
        AppRoot appRoot = null;
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.TryGetComponent(out appRoot)) break;
        if (!appRoot)
            throw new InvalidOperationException("Bootstrap AppRoot is missing.");
        Transform overlay = appRoot.transform.Find("CharacterOverlay");
        if (!overlay)
            throw new InvalidOperationException("Character overlay is missing.");

        overlay.gameObject.SetActive(true);
        overlay.GetComponent<CharacterOverlayController>().SelectTab(0);
        Canvas canvas = appRoot.GetComponent<Canvas>();
        GameObject cameraObject = new("CharacterPreviewCamera",
            typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 540;
        camera.transform.position = new Vector3(0, 0, -1000);
        RenderTexture target = new(1920, 1080, 24,
            RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100;
        Canvas.ForceUpdateCanvases();
        RenderTexture.active = target;
        camera.Render();
        Texture2D pixels = new(1920, 1080, TextureFormat.RGBA32,
            false);
        pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        pixels.Apply();
        string path = Path.GetFullPath(
            "Logs/CharacterOverlay/preview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(pixels);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        Debug.Log("CHARACTER_OVERLAY_PREVIEW: " + path);
    }

    private static void BuildHeader(Transform root)
    {
        RectTransform title = Empty(root, "TitleBlock",
            Vector2.zero, new Vector2(1920, 1080));
        Decor(title, "HeaderStar", Sprite("icon_compass"),
            new Vector2(-908, 484),
            new Vector2(52, 52));
        Decor(title, "TitleOrnament", Sprite("ornament_star"),
            new Vector2(-330, 484), new Vector2(19, 19));
        Label(title, "HeaderTitle", "CHARACTER SELECT", 45,
            new Vector2(-546, 482), new Vector2(695, 68),
            TextAlignmentOptions.Left);
        Label(title, "HeaderKorean", "캐 릭 터   선 택", 22,
            new Vector2(-686, 429), new Vector2(395, 36),
            TextAlignmentOptions.Left);
        Rule(title, "HeaderRule", new Vector2(-696, 407),
            new Vector2(486, 1));
        RectTransform brand = Empty(root, "ProjectBrand",
            Vector2.zero, new Vector2(1920, 1080));
        Label(brand, "ProjectMark", "Project\nReMind", 31,
            new Vector2(757, 465), new Vector2(265, 85),
            TextAlignmentOptions.Right);
    }

    private static void BuildLeftNarrative(Transform root)
    {
        Label(root, "LeftMotto",
            "S O M E   M E M O R I E S\nS T I L L   S H I N E,\nE V E N   I F   W E   F O R G E T.",
            17, new Vector2(-780, 334), new Vector2(300, 100),
            TextAlignmentOptions.Left);
        Label(root, "LeftQuote",
            "…언젠가,\n다시 기억할 수 있기를.", 29,
            new Vector2(-751, 34), new Vector2(360, 95),
            TextAlignmentOptions.Center).color = new Color(0.18f, 0.29f, 0.65f);
    }

    private static void BuildIdentity(RectTransform right)
    {
        RectTransform nameBlock = Empty(right, "NameBlock",
            Vector2.zero, new Vector2(880, 850));
        Label(nameBlock, "Motto", "Still, with you.", 28,
            new Vector2(-245, 338), new Vector2(355, 52),
            TextAlignmentOptions.Left).fontStyle = FontStyles.Italic;
        Label(nameBlock, "CharacterName", "NON", 86,
            new Vector2(-214, 270), new Vector2(430, 105),
            TextAlignmentOptions.Left);
        Decor(nameBlock, "NameRule", Sprite("divider_starline"),
            new Vector2(-14, 225), new Vector2(750, 22));
        Label(nameBlock, "Academy", "Selenite Academy", 30,
            new Vector2(-237, 183), new Vector2(420, 48),
            TextAlignmentOptions.Left);
        Label(nameBlock, "AcademyKorean", "셀레나이트 아카데미", 19,
            new Vector2(-237, 145), new Vector2(420, 32),
            TextAlignmentOptions.Left);
        RectTransform biography = Empty(right, "Biography",
            Vector2.zero, new Vector2(880, 850));
        Label(biography, "CharacterQuote",
            "「 … 잊혀진 마음도,\n    어딘가에 빛나고 있을 거야. 」",
            25, new Vector2(-213, 65), new Vector2(470, 85),
            TextAlignmentOptions.Left);
        RectTransform metadata = Empty(right, "Metadata",
            Vector2.zero, new Vector2(880, 850));
        Label(metadata, "Affiliation",
            "Affiliation     Selenite Academy\nTheme            Memory / Moon / Support\nWeapon          ???",
            20, new Vector2(236, 31), new Vector2(390, 115),
            TextAlignmentOptions.Left);
        Decor(metadata, "MoonMark", Sprite("icon_crescent"),
            new Vector2(224, 156), new Vector2(90, 90));
    }

    private static RectTransform BuildProfile(RectTransform right)
    {
        RectTransform page = Empty(right, "ProfilePage",
            new Vector2(0, -255), new Vector2(850, 250));
        RectTransform stats = SpriteBox(page, "StatsPanel",
            new Vector2(-215, 0), new Vector2(416, 237),
            Sprite("stats_panel_frame"));
        Label(stats, "StatsHeading", "Stats", 24,
            new Vector2(-86, 96), new Vector2(200, 39),
            TextAlignmentOptions.Left);
        Decor(stats, "HeadingLine", Sprite("divider_starline"),
            new Vector2(53, 96), new Vector2(165, 14));
        string[] titles = { "Memory", "Resonance", "Focus", "Support" };
        string[] icons = { "stat_icon_memory", "stat_icon_resonance",
            "stat_icon_focus", "stat_icon_support" };
        int[] values = { 82, 68, 75, 91 };
        for (int i = 0; i < titles.Length; i++)
        {
            float y = 49 - i * 45;
            RectTransform row = Empty(stats, titles[i] + "Row",
                new Vector2(0, y), new Vector2(416, 45));
            Decor(row, "Icon", Sprite(icons[i]),
                new Vector2(-175, 0), new Vector2(35, 35));
            Label(row, "Label", titles[i], 19,
                new Vector2(-81, 0), new Vector2(135, 33),
                TextAlignmentOptions.Left);
            SpriteBox(row, "Track", new Vector2(65, 0),
                new Vector2(160, 15), Sprite("progress_track"));
            SpriteBox(row, "Fill",
                new Vector2(-15 + values[i] * 0.8f, 0),
                new Vector2(160 * values[i] / 100f, 13),
                Sprite("progress_fill"));
            Label(row, "Value", values[i].ToString(), 19,
                new Vector2(176, 0), new Vector2(45, 31),
                TextAlignmentOptions.Right);
        }

        RectTransform skill = SpriteBox(page, "SkillPanel",
            new Vector2(214, 0), new Vector2(410, 237),
            Sprite("skill_panel_frame"));
        Label(skill, "SkillHeading", "Skill", 24,
            new Vector2(-80, 96), new Vector2(180, 39),
            TextAlignmentOptions.Left);
        Rule(skill, "HeadingLine", new Vector2(54, 96),
            new Vector2(155, 1));
        RectTransform iconGroup = Empty(skill, "SkillArtwork",
            new Vector2(-124, -20), new Vector2(105, 105));
        Box(iconGroup, "SkillIconBackdrop", Vector2.zero,
            new Vector2(98, 98), new Color(0.13f, 0.18f, 0.32f));
        SpriteBox(iconGroup, "SkillIconPanel", Vector2.zero,
            new Vector2(105, 105), Sprite("skill_icon_frame"));
        Decor(iconGroup, "SkillIcon", Sprite("skill_icon_moon"),
            Vector2.zero, new Vector2(78, 85));
        RectTransform copyGroup = Empty(skill, "SkillCopy",
            Vector2.zero, new Vector2(410, 237));
        Label(copyGroup, "SkillName", "달이 기억하는 것", 23,
            new Vector2(65, 38), new Vector2(237, 38),
            TextAlignmentOptions.Left);
        Label(copyGroup, "SkillEnglish", "Lunar Recollection", 17,
            new Vector2(67, 9), new Vector2(240, 29),
            TextAlignmentOptions.Left).color = Silver;
        Label(copyGroup, "SkillDescription",
            "잠든 기억을 비추어,\n잊힌 것도 다시 이어지게 한다.", 18,
            new Vector2(64, -55), new Vector2(240, 90),
            TextAlignmentOptions.Left);
        return page;
    }

    private static RectTransform BuildPendingPage(Transform parent,
        string name, string title, string status)
    {
        RectTransform page = Box(parent, name,
            new Vector2(0, -255), new Vector2(845, 237), Glass);
        Frame(page.gameObject);
        Label(page, "Heading", title, 29, new Vector2(0, 54),
            new Vector2(740, 50), TextAlignmentOptions.Center);
        Label(page, "Status", status, 23, new Vector2(0, -22),
            new Vector2(740, 65), TextAlignmentOptions.Center);
        return page;
    }

    private static void BuildRoster(Transform root,
        Texture2D background, Texture2D portraits)
    {
        RectTransform strip = Empty(root, "CharacterRoster",
            new Vector2(185, -415), new Vector2(805, 170));
        string[] names = { "NON", "AME", "HINA", "???", "???" };
        for (int i = 0; i < names.Length; i++)
        {
            RectTransform card = Empty(strip, "CharacterCard" + i,
                new Vector2(-314 + i * 160, 0), new Vector2(148, 143));
            if (i < 3)
            {
                Texture2D texture = i == 0 ? background : portraits;
                Rect crop = i == 0
                    ? new Rect(0.26f, 0.60f, 0.20f, 0.31f)
                    : new Rect(i == 1 ? 0.03f : 0.53f,
                        0.20f, 0.44f, 0.75f);
                Raw(card, "Portrait", texture,
                    new Vector2(0, 17), new Vector2(136, 100), crop);
            }
            else
            {
                Image locked = SpriteBox(card, "LockedOverlay",
                    Vector2.zero, new Vector2(148, 143),
                    Sprite("thumbnail_locked_overlay"))
                    .GetComponent<Image>();
                locked.color = new Color(1f, 1f, 1f, 0.8f);
            }
            SpriteBox(card, "FrameOverlay", Vector2.zero,
                new Vector2(148, 143),
                Sprite(i == 0 ? "thumbnail_frame_selected" :
                    "thumbnail_frame_idle"));
            if (i >= 3)
                Decor(card, "IconLock", Sprite("icon_lock"),
                    new Vector2(0, 15), new Vector2(27, 33));
            Label(card, "Caption", names[i], 20,
                new Vector2(0, -54), new Vector2(136, 28),
                TextAlignmentOptions.Center);
        }
        Decor(strip, "NavigationChevron", Sprite("navigation_chevron"),
            new Vector2(412, 0), new Vector2(23, 39));
    }

    private static void BindBootstrap()
    {
        Scene scene = EditorSceneManager.OpenScene(BootstrapPath,
            OpenSceneMode.Single);
        AppRoot appRoot = null;
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.TryGetComponent(out appRoot)) break;
        if (!appRoot)
            throw new InvalidOperationException("Bootstrap AppRoot is missing.");

        Transform existing = appRoot.transform.Find("CharacterOverlay");
        GameObject instance;
        bool sceneChanged = false;
        if (existing) instance = existing.gameObject;
        else
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            instance.transform.SetParent(appRoot.transform, false);
            Stretch((RectTransform)instance.transform);
            instance.transform.SetSiblingIndex(1);
            instance.SetActive(false);
            sceneChanged = true;
        }
        SerializedObject data = new(appRoot);
        SerializedProperty binding = data.FindProperty("characterOverlay");
        CharacterOverlayController overlay =
            instance.GetComponent<CharacterOverlayController>();
        if (binding.objectReferenceValue != overlay)
        {
            binding.objectReferenceValue = overlay;
            data.ApplyModifiedPropertiesWithoutUndo();
            sceneChanged = true;
        }
        if (sceneChanged)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, BootstrapPath))
                throw new InvalidOperationException(
                    "Could not save Bootstrap scene.");
        }
    }

    private static RectTransform Empty(Transform parent, string name,
        Vector2 position, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        Place(rect, position, size);
        return rect;
    }

    private static RectTransform Box(Transform parent, string name,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        Place(rect, position, size);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static RectTransform SpriteBox(Transform parent, string name,
        Vector2 position, Vector2 size, Sprite sprite)
    {
        RectTransform rect = Box(parent, name, position, size, Color.white);
        rect.GetComponent<Image>().sprite = sprite;
        return rect;
    }

    private static RawImage Raw(Transform parent, string name,
        Texture2D texture, Vector2 position, Vector2 size, Rect uv)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, position, size);
        RawImage image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.uvRect = uv;
        image.raycastTarget = false;
        return image;
    }

    private static Image Decor(Transform parent, string name,
        Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, position, size);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Label(Transform parent, string name,
        string caption, int size, Vector2 position, Vector2 dimensions,
        TextAlignmentOptions alignment)
    {
        GameObject go = new(name, typeof(RectTransform),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, position, dimensions);
        TMP_Text label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = caption;
        label.fontSize = size;
        label.color = new Color(0.95f, 0.96f, 1f);
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Button ButtonAt(Transform parent, string name,
        string caption, Vector2 position, Vector2 size, int row, int column,
        List<NavigationNode> nodes, Action<Button> bind,
        bool highlighted = false)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(NavigationNode));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, position, size);
        Image background = go.GetComponent<Image>();
        background.color = highlighted
            ? new Color(0.88f, 0.92f, 1f, 0.98f)
            : new Color(0.035f, 0.055f, 0.11f, 0.76f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = background;
        TMP_Text label = Label(go.transform, "Label", caption,
            highlighted ? 31 : 23, Vector2.zero,
            size - new Vector2(14, 0), TextAlignmentOptions.Center);
        if (highlighted) label.color = new Color(0.16f, 0.26f, 0.56f);
        NavigationNode node = go.GetComponent<NavigationNode>();
        SerializedObject data = new(node);
        data.FindProperty("selectable").objectReferenceValue = button;
        data.FindProperty("row").intValue = row;
        data.FindProperty("column").intValue = column;
        data.ApplyModifiedPropertiesWithoutUndo();
        nodes.Add(node);
        bind(button);
        return button;
    }

    private static void Frame(GameObject go) => Frame(go, Silver);

    private static void Frame(GameObject go, Color color)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(color.r, color.g, color.b, 0.73f);
        outline.effectDistance = new Vector2(2, -2);
    }

    private static void Rule(Transform parent, string name,
        Vector2 position, Vector2 size) =>
        Box(parent, name, position, size,
            new Color(0.75f, 0.83f, 1f, 0.75f));

    private static Sprite Sprite(string name) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath + name + ".png") ??
        throw new InvalidOperationException(
            "Character overlay sprite is missing: " + name);

    private static void RequireSprites()
    {
        string[] names = {
            "icon_compass", "ornament_star", "character_orbit",
            "icon_crescent", "divider_starline", "tab_selected", "tab_idle",
            "stats_panel_frame", "skill_panel_frame", "stat_icon_memory",
            "stat_icon_resonance", "stat_icon_focus", "stat_icon_support",
            "progress_track", "progress_fill", "skill_icon_frame",
            "skill_icon_moon", "thumbnail_frame_selected",
            "thumbnail_frame_idle", "thumbnail_locked_overlay", "icon_lock",
            "navigation_chevron", "begin_button"
        };
        foreach (string name in names) Sprite(name);
    }

    private static void SetArray<T>(SerializedObject data, string name,
        T[] values) where T : UnityEngine.Object
    {
        SerializedProperty property = data.FindProperty(name);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void Place(RectTransform rect, Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

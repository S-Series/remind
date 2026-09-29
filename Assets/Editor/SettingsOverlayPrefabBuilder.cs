using System;
using System.Collections.Generic;
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

/// <summary>Authors the persistent Game settings modal.</summary>
public static class SettingsOverlayPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/SettingsOverlay.prefab";
    private const string BootstrapScene = "Assets/Scenes/Bootstrap.unity";
    private static readonly Color PanelColor = new(0.055f, 0.075f, 0.14f, 0.96f);
    private static readonly Color InnerColor = new(0.075f, 0.10f, 0.18f, 0.91f);
    private static readonly Color ButtonColor = new(0.13f, 0.17f, 0.29f, 0.98f);
    private static readonly Color Accent = new(0.65f, 0.70f, 0.98f, 1f);
    private static TMP_FontAsset font;

    [MenuItem("ReMind/Build Settings Overlay Prefab")]
    public static void Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Art/Fonts/NanumMyeongjo SDF.asset") ??
            TMP_Settings.defaultFontAsset;
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

        var nodes = new List<NavigationNode>();
        GameObject root = new("SettingsOverlay", typeof(RectTransform),
            typeof(Image), typeof(CanvasGroup), typeof(NavigationScope),
            typeof(SettingsMenuController));
        RectTransform rootRect = (RectTransform)root.transform;
        Stretch(rootRect);
        Image blocker = root.GetComponent<Image>();
        blocker.color = new Color(0.005f, 0.012f, 0.035f, 0.78f);
        blocker.raycastTarget = true;
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        NavigationScope scope = root.GetComponent<NavigationScope>();
        SettingsMenuController controller =
            root.GetComponent<SettingsMenuController>();

        RectTransform panel = Box(rootRect, "SettingsPanel",
            Vector2.zero, new Vector2(1440, 850), PanelColor);
        AddOutline(panel.gameObject, Accent, new Vector2(2, -2));
        Label(panel, "Heading", "SETTINGS  ·  설정", 48,
            new Vector2(-420, 365), new Vector2(610, 65),
            TextAlignmentOptions.Left);
        Label(panel, "Motto", "SAME STARS. DIFFERENT TOMORROWS.", 15,
            new Vector2(445, 365), new Vector2(530, 36),
            TextAlignmentOptions.Right).color = Accent;
        Box(panel, "HeaderRule", new Vector2(0, 326),
            new Vector2(1360, 2), Accent).GetComponent<Image>().raycastTarget = false;

        RectTransform sidebar = Box(panel, "Categories",
            new Vector2(-557, -13), new Vector2(280, 654), InnerColor);
        RectTransform[] pages = new RectTransform[8];
        TMP_Text[] categoryLabels = new TMP_Text[8];
        string[] names = { "General", "Graphics", "Audio", "Rhythm",
            "Story", "Controls", "Language", "Accessibility" };
        string[] korean = { "일반", "그래픽", "오디오", "리듬", "스토리",
            "조작", "언어", "접근성" };
        NavigationNode firstTab = null;
        for (int i = 0; i < names.Length; i++)
        {
            int category = i;
            Button tab = ButtonAt(sidebar, names[i] + "Tab", "✧  " +
                names[i] + "    " + korean[i], new Vector2(0, 264 - i * 70),
                new Vector2(250, 54), i, 0, nodes,
                value => UnityEventTools.AddIntPersistentListener(
                    value.onClick, controller.SelectCategory, category));
            categoryLabels[i] = tab.GetComponentInChildren<TMP_Text>();
            if (i == 2) firstTab = tab.GetComponent<NavigationNode>();
            pages[i] = Box(panel, names[i] + "Page",
                new Vector2(170, -12), new Vector2(1050, 654), InnerColor);
            pages[i].gameObject.SetActive(i == 2);
        }

        BuildAudio(pages[2], controller, nodes,
            out Slider master, out Slider bgm, out Slider sfx,
            out Slider voice, out TMP_Text[] audioValues,
            out TMP_Text[] hitLabels, out Toggle spatial,
            out Toggle mute, out Toggle reduce, out Toggle keep);
        BuildRhythm(pages[3], controller, nodes, out TMP_Text offsetValue);
        BuildControls(pages[5], controller, nodes, out TMP_Text[] laneValues);
        for (int i = 0; i < pages.Length; i++)
        {
            if (i == 2 || i == 3 || i == 5) continue;
            Label(pages[i], "PageTitle", names[i].ToUpperInvariant() +
                "  ·  " + korean[i], 31, new Vector2(-18, 267),
                new Vector2(925, 50), TextAlignmentOptions.Left);
            Label(pages[i], "Status",
                "이 영역의 세부 설정은 콘텐츠와 시스템이 연결될 때 추가됩니다.",
                23, new Vector2(-15, 65), new Vector2(890, 110),
                TextAlignmentOptions.Center).color = Accent;
        }

        TMP_Text status = Label(panel, "SaveStatus",
            "변경 사항은 즉시 저장됩니다.", 18,
            new Vector2(-180, -369), new Vector2(560, 35),
            TextAlignmentOptions.Left);
        ButtonAt(panel, "ResetKeys", "RESET KEYS  ·  키 초기화",
            new Vector2(-497, -375), new Vector2(275, 48),
            12, 0, nodes, value => UnityEventTools.AddPersistentListener(
                value.onClick, controller.ResetBindings));
        ButtonAt(panel, "Close", "CLOSE  ·  닫기",
            new Vector2(343, -375), new Vector2(240, 48),
            12, 1, nodes, value => UnityEventTools.AddPersistentListener(
                value.onClick, controller.ReturnHome));
        ButtonAt(panel, "Apply", "APPLY  ·  적용",
            new Vector2(596, -375), new Vector2(205, 48),
            12, 2, nodes, value => UnityEventTools.AddPersistentListener(
                value.onClick, controller.ReturnHome));

        SerializedObject scopeData = new(scope);
        scopeData.FindProperty("canvasGroup").objectReferenceValue = group;
        scopeData.FindProperty("initialSelection").objectReferenceValue = firstTab;
        SerializedProperty nodeList = scopeData.FindProperty("nodes");
        nodeList.arraySize = nodes.Count;
        for (int i = 0; i < nodes.Count; i++)
            nodeList.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
        scopeData.ApplyModifiedPropertiesWithoutUndo();
        UnityEvent onCancel = (UnityEvent)typeof(NavigationScope)
            .GetField("onCancel", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).GetValue(scope);
        UnityEventTools.AddPersistentListener(onCancel, controller.ReturnHome);

        SerializedObject data = new(controller);
        data.FindProperty("scope").objectReferenceValue = scope;
        data.FindProperty("volumeValue").objectReferenceValue = audioValues[1];
        data.FindProperty("offsetValue").objectReferenceValue = offsetValue;
        data.FindProperty("status").objectReferenceValue = status;
        data.FindProperty("masterSlider").objectReferenceValue = master;
        data.FindProperty("bgmSlider").objectReferenceValue = bgm;
        data.FindProperty("sfxSlider").objectReferenceValue = sfx;
        data.FindProperty("voiceSlider").objectReferenceValue = voice;
        data.FindProperty("spatialToggle").objectReferenceValue = spatial;
        data.FindProperty("muteUnfocusedToggle").objectReferenceValue = mute;
        data.FindProperty("reduceBgmToggle").objectReferenceValue = reduce;
        data.FindProperty("keepBackgroundToggle").objectReferenceValue = keep;
        SetArray(data, "audioValues", audioValues);
        SetArray(data, "hitStyleLabels", hitLabels);
        SetArray(data, "laneValues", laneValues);
        SetArray(data, "categoryPages", Array.ConvertAll(pages,
            page => page.gameObject));
        SetArray(data, "categoryLabels", categoryLabels);
        data.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        BindBootstrap();
        AssetDatabase.SaveAssets();
        Debug.Log("SETTINGS_OVERLAY_PREFAB_BUILT");
    }

    private static void BindBootstrap()
    {
        Scene scene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Single);
        AppRoot appRoot = null;
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.TryGetComponent(out appRoot)) break;
        if (!appRoot)
            throw new InvalidOperationException("Bootstrap AppRoot is missing.");

        Canvas canvas = appRoot.GetComponent<Canvas>() ??
            appRoot.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        CanvasScaler scaler = appRoot.GetComponent<CanvasScaler>() ??
            appRoot.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        if (!appRoot.GetComponent<GraphicRaycaster>())
            appRoot.gameObject.AddComponent<GraphicRaycaster>();

        Transform existing = appRoot.transform.Find("SettingsOverlay");
        GameObject instance;
        if (existing) instance = existing.gameObject;
        else
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            instance.transform.SetParent(appRoot.transform, false);
            Stretch((RectTransform)instance.transform);
            instance.transform.SetAsLastSibling();
        }
        instance.SetActive(false);
        var data = new SerializedObject(appRoot);
        data.FindProperty("settingsOverlay").objectReferenceValue =
            instance.GetComponent<SettingsMenuController>();
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, BootstrapScene))
            throw new InvalidOperationException("Could not save Bootstrap scene.");
    }

    private static void BuildAudio(RectTransform page,
        SettingsMenuController controller, List<NavigationNode> nodes,
        out Slider master, out Slider bgm, out Slider sfx,
        out Slider voice, out TMP_Text[] values, out TMP_Text[] hitLabels,
        out Toggle spatial, out Toggle mute, out Toggle reduce,
        out Toggle keep)
    {
        Label(page, "AudioHeading", "Audio Settings  ·  오디오 설정", 32,
            new Vector2(-15, 279), new Vector2(950, 48),
            TextAlignmentOptions.Left);
        Box(page, "AudioRule", new Vector2(-15, 246),
            new Vector2(940, 2), Accent);
        string[] volumeNames = { "Master Volume", "BGM Volume",
            "SFX Volume", "Voice Volume" };
        string[] volumeKorean = { "전체 볼륨", "배경 음악",
            "효과음", "음성" };
        values = new TMP_Text[4];
        Slider[] sliders = new Slider[4];
        UnityAction<float>[] actions = { controller.SetMasterVolume,
            controller.SetBgmVolume, controller.SetSfxVolume,
            controller.SetVoiceVolume };
        for (int i = 0; i < 4; i++)
        {
            float y = 194 - i * 49;
            Label(page, "VolumeLabel" + i,
                volumeNames[i] + "  " + volumeKorean[i], 20,
                new Vector2(-298, y), new Vector2(300, 35),
                TextAlignmentOptions.Left);
            sliders[i] = SliderAt(page, "VolumeSlider" + i,
                new Vector2(-7, y), new Vector2(250, 16), i + 2, 1,
                nodes, actions[i]);
            values[i] = Label(page, "VolumeValue" + i, "100", 20,
                new Vector2(154, y), new Vector2(65, 32),
                TextAlignmentOptions.Right);
        }
        master = sliders[0]; bgm = sliders[1];
        sfx = sliders[2]; voice = sliders[3];

        Label(page, "HitLabel", "Hit Sound  ·  히트 사운드", 20,
            new Vector2(-297, -18), new Vector2(300, 35),
            TextAlignmentOptions.Left);
        hitLabels = new TMP_Text[4];
        string[] styles = { "Default", "Soft", "Sharp", "None" };
        for (int i = 0; i < 4; i++)
        {
            int style = i;
            Button button = ButtonAt(page, "HitStyle" + i, styles[i],
                new Vector2(-100 + i * 93, -18), new Vector2(91, 34),
                6, i + 1, nodes,
                value => UnityEventTools.AddIntPersistentListener(
                    value.onClick, controller.SetHitSoundStyle, style));
            hitLabels[i] = button.GetComponentInChildren<TMP_Text>();
        }
        Label(page, "OutputLabel", "Output Device  ·  출력 장치", 20,
            new Vector2(-297, -72), new Vector2(300, 35),
            TextAlignmentOptions.Left);
        ButtonAt(page, "OutputDevice", "System Default  ▾",
            new Vector2(15, -72), new Vector2(328, 36), 7, 1, nodes,
            value => UnityEventTools.AddPersistentListener(value.onClick,
                controller.OpenSystemSoundSettings));
        Label(page, "SpatialLabel", "Spatial Audio  ·  공간 음향", 20,
            new Vector2(-297, -123), new Vector2(300, 35),
            TextAlignmentOptions.Left);
        spatial = ToggleAt(page, "SpatialAudio", new Vector2(-65, -123),
            8, 1, nodes, controller.SetSpatialAudio);
        Label(page, "OtherHeading", "Other Audio Options  ·  기타 설정",
            23, new Vector2(-112, -170), new Vector2(670, 38),
            TextAlignmentOptions.Left);
        Box(page, "OtherRule", new Vector2(-116, -192),
            new Vector2(640, 2), Accent);
        string[] options = { "Mute When Unfocused  ·  비활성화 시 음소거",
            "Reduce BGM During Voice  ·  음성 중 BGM 감소",
            "Keep Audio in Background  ·  백그라운드 재생" };
        UnityAction<bool>[] set = { controller.SetMuteWhenUnfocused,
            controller.SetReduceBgmDuringVoice,
            controller.SetKeepAudioInBackground };
        Toggle[] toggles = new Toggle[3];
        for (int i = 0; i < 3; i++)
        {
            float y = -224 - i * 39;
            Label(page, "OtherLabel" + i, options[i], 18,
                new Vector2(-127, y), new Vector2(625, 30),
                TextAlignmentOptions.Left);
            toggles[i] = ToggleAt(page, "OtherToggle" + i,
                new Vector2(215, y), 9 + i, 1, nodes, set[i]);
        }
        mute = toggles[0]; reduce = toggles[1]; keep = toggles[2];

        RectTransform quote = Box(page, "MemoryCard",
            new Vector2(380, 16), new Vector2(275, 570),
            new Color(0.10f, 0.14f, 0.25f, 0.95f));
        AddOutline(quote.gameObject,
            new Color(0.63f, 0.69f, 0.95f, 0.55f), Vector2.one);
        Label(quote, "MemoryScript", "기억은\n언제나 어디선가\n다시 흐른다.", 28,
            new Vector2(0, 75), new Vector2(235, 180),
            TextAlignmentOptions.Center);
        Label(quote, "MemoryEnglish", "MEMORIES\nALWAYS FIND A WAY\nTO RESONATE AGAIN.",
            15, new Vector2(0, -117), new Vector2(230, 100),
            TextAlignmentOptions.Center).color = Accent;
        Label(quote, "Star", "✦", 44, new Vector2(0, -234),
            new Vector2(70, 70), TextAlignmentOptions.Center).color = Accent;
    }

    private static void BuildRhythm(RectTransform page,
        SettingsMenuController controller, List<NavigationNode> nodes,
        out TMP_Text offsetValue)
    {
        Label(page, "RhythmHeading", "Rhythm  ·  리듬", 32,
            new Vector2(-15, 270), new Vector2(930, 52),
            TextAlignmentOptions.Left);
        Label(page, "OffsetDescription",
            "판정 보정은 -200 ms부터 +200 ms까지 5 ms씩 조절합니다.",
            23, new Vector2(0, 125), new Vector2(900, 55),
            TextAlignmentOptions.Center);
        ButtonAt(page, "TimingMinus", "− 5 ms",
            new Vector2(-230, 7), new Vector2(160, 58), 3, 1, nodes,
            value => UnityEventTools.AddIntPersistentListener(
                value.onClick, controller.ChangeJudgementOffset, -1));
        offsetValue = Label(page, "TimingValue", "0 ms", 35,
            new Vector2(0, 7), new Vector2(220, 58),
            TextAlignmentOptions.Center);
        ButtonAt(page, "TimingPlus", "+ 5 ms",
            new Vector2(230, 7), new Vector2(160, 58), 3, 2, nodes,
            value => UnityEventTools.AddIntPersistentListener(
                value.onClick, controller.ChangeJudgementOffset, 1));
        Label(page, "OffsetNote",
            "양수는 입력을 더 이르게 판정합니다. 다음 플레이부터 적용됩니다.",
            20, new Vector2(0, -120), new Vector2(900, 50),
            TextAlignmentOptions.Center).color = Accent;
    }

    private static void BuildControls(RectTransform page,
        SettingsMenuController controller, List<NavigationNode> nodes,
        out TMP_Text[] laneValues)
    {
        Label(page, "ControlsHeading", "Controls  ·  조작", 32,
            new Vector2(-15, 270), new Vector2(930, 52),
            TextAlignmentOptions.Left);
        Label(page, "ControlsDescription",
            "변경할 레인을 선택한 뒤 새 키를 누르세요. ESC로 취소합니다.",
            20, new Vector2(0, 208), new Vector2(930, 45),
            TextAlignmentOptions.Center);
        laneValues = new TMP_Text[10];
        for (int i = 0; i < laneValues.Length; i++)
        {
            int lane = i;
            Button button = ButtonAt(page, "Lane" + (i + 1),
                "Lane " + (i + 1),
                new Vector2(i < 5 ? -225 : 225,
                    145 - i % 5 * 72),
                new Vector2(350, 56), 5 + i % 5, i < 5 ? 1 : 2, nodes,
                value => UnityEventTools.AddIntPersistentListener(
                    value.onClick, controller.BeginRebind, lane));
            laneValues[i] = button.GetComponentInChildren<TMP_Text>();
        }
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
        label.color = new Color(0.93f, 0.94f, 1f);
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Button ButtonAt(Transform parent, string name,
        string caption, Vector2 position, Vector2 size, int row, int column,
        List<NavigationNode> nodes, Action<Button> bind)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(NavigationNode));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, position, size);
        Image background = go.GetComponent<Image>();
        background.color = ButtonColor;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = background;
        Label(go.transform, "Label", caption, 19, Vector2.zero,
            size - new Vector2(12, 0), TextAlignmentOptions.Center);
        Register(go.GetComponent<NavigationNode>(), button,
            row, column, nodes);
        bind(button);
        return button;
    }

    private static Slider SliderAt(Transform parent, string name,
        Vector2 position, Vector2 size, int row, int column,
        List<NavigationNode> nodes, UnityAction<float> bind)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image),
            typeof(Slider), typeof(NavigationNode));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, position, size);
        Image track = go.GetComponent<Image>();
        track.color = new Color(0.18f, 0.22f, 0.34f, 1f);
        RectTransform fill = Box(go.transform, "Fill", Vector2.zero,
            size, Accent);
        RectTransform handle = Box(go.transform, "Handle", Vector2.zero,
            new Vector2(20, 26),
            new Color(0.89f, 0.91f, 1f, 1f));
        Slider slider = go.GetComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        Register(go.GetComponent<NavigationNode>(), slider,
            row, column, nodes);
        UnityEventTools.AddPersistentListener(slider.onValueChanged, bind);
        return slider;
    }

    private static Toggle ToggleAt(Transform parent, string name,
        Vector2 position, int row, int column,
        List<NavigationNode> nodes, UnityAction<bool> bind)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image),
            typeof(Toggle), typeof(NavigationNode));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, position, new Vector2(58, 30));
        Image background = go.GetComponent<Image>();
        background.color = ButtonColor;
        RectTransform check = Box(go.transform, "Checkmark", Vector2.zero,
            new Vector2(24, 24), Accent);
        Toggle toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = check.GetComponent<Image>();
        Register(go.GetComponent<NavigationNode>(), toggle,
            row, column, nodes);
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, bind);
        return toggle;
    }

    private static void Register(NavigationNode node, Selectable selectable,
        int row, int column, List<NavigationNode> nodes)
    {
        var data = new SerializedObject(node);
        data.FindProperty("selectable").objectReferenceValue = selectable;
        data.FindProperty("row").intValue = row;
        data.FindProperty("column").intValue = column;
        data.ApplyModifiedPropertiesWithoutUndo();
        nodes.Add(node);
    }

    private static void SetArray<T>(SerializedObject data, string name,
        T[] values) where T : UnityEngine.Object
    {
        SerializedProperty property = data.FindProperty(name);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void AddOutline(GameObject go, Color color, Vector2 distance)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
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

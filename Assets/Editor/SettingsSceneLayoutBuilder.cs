using System;
using System.Collections.Generic;
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

/// <summary>Replaces the Settings placeholder with a functional scene-authored menu.</summary>
public static class SettingsSceneLayoutBuilder
{
    private const string ScenePath = "Assets/Scenes/Settings.unity";
    private static readonly Color Background = new Color(0.025f, 0.035f,
        0.075f, 1f);
    private static readonly Color ButtonColor = new Color(0.12f, 0.16f,
        0.27f, 1f);

    [MenuItem("ReMind/Build Settings Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        Canvas canvas = null;
        MenuNavigationController navigation = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!canvas) canvas = root.GetComponentInChildren<Canvas>(true);
            if (!navigation)
                navigation = root.GetComponentInChildren<
                    MenuNavigationController>(true);
        }
        if (!canvas || !navigation)
            throw new InvalidOperationException(
                "Settings scene needs its Canvas and EventSystem.");

        Transform old = canvas.transform.Find("TemporaryMenuRoot");
        if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        old = canvas.transform.Find("SettingsRoot");
        if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);

        GameObject rootObject = new GameObject("SettingsRoot",
            typeof(RectTransform), typeof(CanvasGroup), typeof(Image),
            typeof(NavigationScope), typeof(SettingsMenuController));
        rootObject.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = (RectTransform)rootObject.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        Image background = rootObject.GetComponent<Image>();
        background.color = Background;
        background.raycastTarget = false;
        NavigationScope scope = rootObject.GetComponent<NavigationScope>();
        SettingsMenuController controller =
            rootObject.GetComponent<SettingsMenuController>();
        var nodes = new List<NavigationNode>();

        CreateText(rootRect, "Title", "SETTINGS", 44,
            new Vector2(0, 289), new Vector2(600, 58));
        CreateText(rootRect, "Subtitle",
            "Settings apply to the next play. Positive timing shifts input earlier.", 20,
            new Vector2(0, 244), new Vector2(800, 30));

        CreateText(rootRect, "MusicLabel", "MUSIC VOLUME", 25,
            new Vector2(-310, 176), new Vector2(270, 42));
        TMP_Text volumeValue = CreateText(rootRect, "MusicValue", "100%", 28,
            new Vector2(150, 176), new Vector2(130, 42));
        AddButton(rootRect, "VolumeMinus", "-", new Vector2(30, 176),
            new Vector2(52, 42), 0, 0, nodes,
            button => UnityEventTools.AddIntPersistentListener(
                button.onClick, controller.ChangeMusicVolume, -1));
        AddButton(rootRect, "VolumePlus", "+", new Vector2(270, 176),
            new Vector2(52, 42), 0, 1, nodes,
            button => UnityEventTools.AddIntPersistentListener(
                button.onClick, controller.ChangeMusicVolume, 1));

        CreateText(rootRect, "TimingLabel", "JUDGEMENT OFFSET", 25,
            new Vector2(-310, 113), new Vector2(270, 42));
        TMP_Text offsetValue = CreateText(rootRect, "TimingValue", "0 ms", 28,
            new Vector2(150, 113), new Vector2(130, 42));
        AddButton(rootRect, "TimingMinus", "-", new Vector2(30, 113),
            new Vector2(52, 42), 1, 0, nodes,
            button => UnityEventTools.AddIntPersistentListener(
                button.onClick, controller.ChangeJudgementOffset, -1));
        AddButton(rootRect, "TimingPlus", "+", new Vector2(270, 113),
            new Vector2(52, 42), 1, 1, nodes,
            button => UnityEventTools.AddIntPersistentListener(
                button.onClick, controller.ChangeJudgementOffset, 1));

        CreateText(rootRect, "KeysLabel", "LANE KEYS", 25,
            new Vector2(0, 49), new Vector2(420, 38));
        var laneValues = new TMP_Text[10];
        for (int lane = 0; lane < laneValues.Length; lane++)
        {
            int index = lane;
            Vector2 position = new Vector2(lane < 5 ? -225 : 225,
                4 - (lane % 5) * 48);
            Button button = AddButton(rootRect, "Lane" + (lane + 1),
                "Lane " + (lane + 1), position, new Vector2(280, 39),
                2 + lane % 5, lane < 5 ? 0 : 1, nodes,
                value => UnityEventTools.AddIntPersistentListener(
                    value.onClick, controller.BeginRebind, index));
            laneValues[lane] = button.GetComponentInChildren<TMP_Text>();
        }

        TMP_Text status = CreateText(rootRect, "Status",
            "Changes are saved immediately.", 18,
            new Vector2(0, -253), new Vector2(960, 36));
        AddButton(rootRect, "ResetKeys", "RESET KEYS",
            new Vector2(-225, -307), new Vector2(280, 42), 7, 0,
            nodes, button => UnityEventTools.AddPersistentListener(
                button.onClick, controller.ResetBindings));
        AddButton(rootRect, "Back", "BACK",
            new Vector2(225, -307), new Vector2(280, 42), 7, 1,
            nodes, button => UnityEventTools.AddPersistentListener(
                button.onClick, controller.ReturnHome));

        var scopeData = new SerializedObject(scope);
        scopeData.FindProperty("controller").objectReferenceValue = navigation;
        scopeData.FindProperty("canvasGroup").objectReferenceValue =
            rootObject.GetComponent<CanvasGroup>();
        scopeData.FindProperty("initialSelection").objectReferenceValue =
            nodes[0];
        SerializedProperty nodeList = scopeData.FindProperty("nodes");
        nodeList.arraySize = nodes.Count;
        for (int i = 0; i < nodes.Count; i++)
            nodeList.GetArrayElementAtIndex(i).objectReferenceValue = nodes[i];
        scopeData.ApplyModifiedPropertiesWithoutUndo();
        var onCancel = (UnityEvent)typeof(NavigationScope)
            .GetField("onCancel", BindingFlags.Instance |
                BindingFlags.NonPublic).GetValue(scope);
        UnityEventTools.AddPersistentListener(onCancel, controller.ReturnHome);

        var menuData = new SerializedObject(navigation);
        menuData.FindProperty("initialScope").objectReferenceValue = scope;
        menuData.ApplyModifiedPropertiesWithoutUndo();
        var controllerData = new SerializedObject(controller);
        controllerData.FindProperty("navigation").objectReferenceValue = navigation;
        controllerData.FindProperty("scope").objectReferenceValue = scope;
        controllerData.FindProperty("volumeValue").objectReferenceValue =
            volumeValue;
        controllerData.FindProperty("offsetValue").objectReferenceValue =
            offsetValue;
        controllerData.FindProperty("status").objectReferenceValue = status;
        SerializedProperty laneList = controllerData.FindProperty("laneValues");
        laneList.arraySize = laneValues.Length;
        for (int i = 0; i < laneValues.Length; i++)
            laneList.GetArrayElementAtIndex(i).objectReferenceValue =
                laneValues[i];
        controllerData.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("SETTINGS_SCENE_BUILT");
    }

    private static TMP_Text CreateText(Transform parent, string name,
        string value, float fontSize, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = value;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Button AddButton(Transform parent, string name,
        string caption, Vector2 position, Vector2 size, int row, int column,
        List<NavigationNode> nodes, Action<Button> bind)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(NavigationNode));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = ButtonColor;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        CreateText(rect, "Label", caption, 20, Vector2.zero, size);
        NavigationNode node = go.GetComponent<NavigationNode>();
        var nodeData = new SerializedObject(node);
        nodeData.FindProperty("selectable").objectReferenceValue = button;
        nodeData.FindProperty("row").intValue = row;
        nodeData.FindProperty("column").intValue = column;
        nodeData.ApplyModifiedPropertiesWithoutUndo();
        nodes.Add(node);
        bind(button);
        return button;
    }
}

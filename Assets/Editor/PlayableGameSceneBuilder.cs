using System;
using REmind.Charting;
using REmind.Gameplay;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Input.Judgement;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlayableGameSceneBuilder
{
    [MenuItem("REmind/Build Playable Game Scene")]
    public static void Build()
    {
        const string scenePath = "Assets/Scenes/Game.unity";
        var chart = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/Data/Music/i/charts/hard.rmp.json");
        if (!chart) throw new InvalidOperationException(
            "Export the playable sample package first.");
        var songAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/Data/Music/i/data.json");
        if (!songAsset) throw new InvalidOperationException(
            "I song data.json is missing.");
        SongContent song = SongContentCodec.Parse(songAsset.text);
        if (song.FindChart("hard") == null)
            throw new InvalidOperationException("I song hard chart is not listed.");

        // Rebind the authored Game scene. Recreating it from DemoPlay would
        // discard the menu objects and Inspector connections in its hierarchy.
        Scene scene = EditorSceneManager.OpenScene(
            scenePath, OpenSceneMode.Single);
        DemoPlayController presenter = UnityEngine.Object
            .FindFirstObjectByType<DemoPlayController>();
        GameplayChartSessionController chartSession = UnityEngine.Object
            .FindFirstObjectByType<GameplayChartSessionController>();
        NoteJudgementSystem judgement = UnityEngine.Object
            .FindFirstObjectByType<NoteJudgementSystem>();
        GameplaySessionState state = UnityEngine.Object
            .FindFirstObjectByType<GameplaySessionState>();
        GameManager manager = UnityEngine.Object
            .FindFirstObjectByType<GameManager>();
        Test legacyUi = UnityEngine.Object.FindFirstObjectByType<Test>();
        if (!presenter || !chartSession || !judgement || !state ||
            !manager || !legacyUi)
            throw new InvalidOperationException(
                "DemoPlay is missing a required Game component.");

        var chartFields = new SerializedObject(chartSession);
        chartFields.FindProperty("chartAsset").objectReferenceValue = chart;
        chartFields.FindProperty("bundledMusicId").stringValue = "i";
        chartFields.ApplyModifiedPropertiesWithoutUndo();

        var presenterFields = new SerializedObject(presenter);
        presenterFields.FindProperty("playOnReady").boolValue = false;
        SetPrefab(presenterFields, "scratchNotePrefab", "Scratch");
        SetPrefab(presenterFields, "longTapNotePrefab", "Long Tap");
        SetPrefab(presenterFields, "longScratchNotePrefab", "Long Scratch");
        SetPrefab(presenterFields, "airNotePrefab", "Air");
        presenterFields.ApplyModifiedPropertiesWithoutUndo();

        var judgementFields = new SerializedObject(judgement);
        SerializedProperty profiles = judgementFields.FindProperty(
            "noteWindowProfiles");
        profiles.arraySize = 5;
        for (int index = 0; index < 5; index++)
            profiles.GetArrayElementAtIndex(index).objectReferenceValue =
                GetOrCreateWindowProfile((ChartNoteKind)index);
        judgementFields.ApplyModifiedPropertiesWithoutUndo();

        var oldFields = new SerializedObject(legacyUi);
        DisableObject(oldFields, "playButton");
        DisableObject(oldFields, "resetButton");
        DisableObject(oldFields, "autoToggle");
        DisableObject(oldFields, "timeText");
        legacyUi.enabled = false;

        var flow = legacyUi.gameObject.GetComponent<GameFlowController>() ??
            legacyUi.gameObject.AddComponent<GameFlowController>();
        var flowFields = new SerializedObject(flow);
        flowFields.FindProperty("gameManager").objectReferenceValue = manager;
        flowFields.FindProperty("notePresenter").objectReferenceValue = presenter;
        flowFields.FindProperty("chartSession").objectReferenceValue = chartSession;
        flowFields.FindProperty("sessionState").objectReferenceValue = state;
        flowFields.FindProperty("judgementSystem").objectReferenceValue =
            judgement;
        flowFields.FindProperty("songTitle").stringValue = song.Title;
        flowFields.ApplyModifiedPropertiesWithoutUndo();

        if (!EditorSceneManager.SaveScene(scene, scenePath))
            throw new InvalidOperationException("Could not save Game scene.");
        AssetDatabase.SaveAssets();
        Debug.Log("Playable Game scene created: " + scenePath);
    }

    private static void SetPrefab(SerializedObject owner, string field,
        string prefabName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Notes/InGame/" + prefabName + ".prefab");
        if (!prefab) throw new InvalidOperationException(
            "Missing note prefab: " + prefabName);
        owner.FindProperty(field).objectReferenceValue = prefab;
    }

    private static void DisableObject(SerializedObject owner, string field)
    {
        Component component = owner.FindProperty(field)
            .objectReferenceValue as Component;
        if (component) component.gameObject.SetActive(false);
    }

    private static NoteJudgeWindowProfile GetOrCreateWindowProfile(
        ChartNoteKind kind)
    {
        const string folder = "Assets/GameRules/JudgementWindows";
        if (!AssetDatabase.IsValidFolder("Assets/GameRules"))
            AssetDatabase.CreateFolder("Assets", "GameRules");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/GameRules",
                "JudgementWindows");
        string path = folder + "/" + kind + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<NoteJudgeWindowProfile>(
            path);
        if (profile) return profile;
        profile = ScriptableObject.CreateInstance<NoteJudgeWindowProfile>();
        var fields = new SerializedObject(profile);
        fields.FindProperty("noteKind").enumValueIndex = (int)kind;
        fields.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }
}

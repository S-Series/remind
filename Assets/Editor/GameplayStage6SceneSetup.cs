using System;
using System.Collections.Generic;
using REmind.Gameplay;
using REmind.Gameplay.Chart;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Effects;
using REmind.Gameplay.Input.Judgement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reproducible, idempotent wiring for the existing DemoPlay scene.  It does not
/// create song selection, loading screens, or scene-transition policy.
/// </summary>
public static class GameplayStage6SceneSetup
{
    private const string UndoName = "Connect Effect Sample to DemoPlay";
    private const string ScenePath = "Assets/Scenes/DemoPlay.unity";
    private const string ChartPath =
        "Assets/Chart/EffectGameplaySample.rd";
    private const string ParameterPath =
        "Assets/Chart/effect.effect_gameplay_sample.demo.json";
    private const string SampleMusicId = "effect_gameplay_sample";

    [MenuItem("REmind/Gameplay/Connect Effect Sample to DemoPlay")]
    public static void Install()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        AssetDatabase.ImportAsset(
            ChartPath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(
            ParameterPath,
            ImportAssetOptions.ForceSynchronousImport |
            ImportAssetOptions.ForceUpdate);

        TextAsset chartAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
            ChartPath);
        TextAsset parameterAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
            ParameterPath);
        if (!chartAsset || !parameterAsset)
        {
            throw new InvalidOperationException(
                "The bundled gameplay chart pair could not be imported.");
        }

        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);
        RemoveLegacyTempLoaders(scene);
        GameManager manager = FindSingle<GameManager>(scene);
        NoteJudgementSystem judgement =
            FindSingle<NoteJudgementSystem>(scene);
        DemoPlayController demo = FindSingle<DemoPlayController>(scene);
        Camera camera = FindSingle<Camera>(scene);
        AudioClip song = manager.GamePlay.CurrentSong;
        if (!song)
        {
            song = GetReference<AudioClip>(
                manager.GamePlay,
                "initialSong");
        }
        if (!song)
        {
            throw new InvalidOperationException(
                "DemoPlay requires an assigned gameplay AudioClip.");
        }

        Transform cameraBase = EnsureChildlessRoot(
            scene,
            "Camera Base Motion");
        Transform effectPivot = EnsureChild(
            cameraBase,
            "Camera Effect Pivot");
        ConnectCameraRig(camera.transform, cameraBase, effectPivot);

        GameplaySessionState state = GetOrAdd<GameplaySessionState>(
            manager.gameObject);
        GameplayChartEffectController effects =
            GetOrAdd<GameplayChartEffectController>(manager.gameObject);
        GameplayChartSessionController chartSession =
            GetOrAdd<GameplayChartSessionController>(manager.gameObject);

        SetReference(state, "gameManager", manager);
        SetReference(state, "judgementSystem", judgement);
        SetReference(state, "gameRule", manager.GameRule);

        SetReference(effects, "gameManager", manager);
        SetReference(effects, "judgementSystem", judgement);
        SetReference(effects, "cameraEffectPivot", effectPivot);
        SetReference(effects, "gameStateProvider", state);

        SetReference(chartSession, "chartAsset", chartAsset);
        SetReference(
            chartSession,
            "effectParameterAsset",
            parameterAsset);
        SetInteger(chartSession, "beatsPerMeasure", 4);
        SetString(chartSession, "bundledMusicId", SampleMusicId);
        SetReference(chartSession, "bundledSong", song);
        SetFloat(chartSession, "bundledSongVolume", 0.5f);
        SetReference(chartSession, "gameManager", manager);
        SetReference(chartSession, "judgementSystem", judgement);
        SetReference(chartSession, "effectController", effects);
        SetReference(chartSession, "sessionState", state);

        SetReference(demo, "chartSession", chartSession);
        SetReference(demo, "cameraTransform", cameraBase);

        EditorUtility.SetDirty(manager.gameObject);
        EditorUtility.SetDirty(demo);
        EditorUtility.SetDirty(cameraBase.gameObject);
        EditorUtility.SetDirty(effectPivot.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new InvalidOperationException(
                "DemoPlay scene could not be saved.");
        }

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log(
            "DemoPlay now uses the shared .rd/Effect JSON gameplay pipeline.");
    }

    private static void RemoveLegacyTempLoaders(Scene scene)
    {
        var loaders = new List<TempLoader>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            loaders.AddRange(root.GetComponentsInChildren<TempLoader>(true));
        }

        foreach (TempLoader loader in loaders)
        {
            GameObject owner = loader.gameObject;
            Component[] components = owner.GetComponents<Component>();
            if (components.Length == 2 && owner.transform.childCount == 0)
            {
                Undo.DestroyObjectImmediate(owner);
            }
            else
            {
                Undo.DestroyObjectImmediate(loader);
            }
        }
    }

    private static void ConnectCameraRig(
        Transform camera,
        Transform cameraBase,
        Transform effectPivot)
    {
        Undo.RecordObjects(
            new UnityEngine.Object[] { camera, cameraBase, effectPivot },
            UndoName);

        bool alreadyConnected = camera.parent == effectPivot &&
            effectPivot.parent == cameraBase;
        if (alreadyConnected)
        {
            effectPivot.localPosition = Vector3.zero;
            effectPivot.localRotation = Quaternion.identity;
            effectPivot.localScale = Vector3.one;
            return;
        }

        Vector3 position = camera.position;
        Quaternion rotation = camera.rotation;
        Vector3 scale = camera.localScale;

        cameraBase.SetPositionAndRotation(position, rotation);
        cameraBase.localScale = Vector3.one;
        effectPivot.SetParent(cameraBase, false);
        effectPivot.localPosition = Vector3.zero;
        effectPivot.localRotation = Quaternion.identity;
        effectPivot.localScale = Vector3.one;
        camera.SetParent(effectPivot, false);
        camera.localPosition = Vector3.zero;
        camera.localRotation = Quaternion.identity;
        camera.localScale = scale;
    }

    private static Transform EnsureChildlessRoot(
        Scene scene,
        string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root.transform;
            }
        }

        var created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, UndoName);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created.transform;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing)
        {
            return existing;
        }

        var created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, UndoName);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static T GetOrAdd<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component ? component : Undo.AddComponent<T>(target.gameObject);
    }

    private static T FindSingle<T>(Scene scene) where T : Component
    {
        var matches = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            matches.AddRange(root.GetComponentsInChildren<T>(true));
        }

        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"DemoPlay requires exactly one {typeof(T).Name}; found " +
                $"{matches.Count}.");
        }

        return matches[0];
    }

    private static T GetReference<T>(
        UnityEngine.Object target,
        string fieldName)
        where T : UnityEngine.Object
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName) ??
            throw new MissingFieldException(target.GetType().Name, fieldName);
        return property.objectReferenceValue as T;
    }

    private static void SetReference(
        UnityEngine.Object target,
        string fieldName,
        UnityEngine.Object value)
    {
        Undo.RecordObject(target, UndoName);
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName) ??
            throw new MissingFieldException(target.GetType().Name, fieldName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetInteger(
        UnityEngine.Object target,
        string fieldName,
        int value)
    {
        Undo.RecordObject(target, UndoName);
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName) ??
            throw new MissingFieldException(target.GetType().Name, fieldName);
        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetString(
        UnityEngine.Object target,
        string fieldName,
        string value)
    {
        Undo.RecordObject(target, UndoName);
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName) ??
            throw new MissingFieldException(target.GetType().Name, fieldName);
        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetFloat(
        UnityEngine.Object target,
        string fieldName,
        float value)
    {
        Undo.RecordObject(target, UndoName);
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName) ??
            throw new MissingFieldException(target.GetType().Name, fieldName);
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}

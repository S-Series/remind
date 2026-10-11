using System;
using REmind.Gameplay.Demo;
using REmind.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Adds modal motion to authored prefabs without rebuilding their UI.</summary>
public static class OverlayPresentationPrefabBinder
{
    private const string CharacterPath =
        "Assets/Prefabs/UI/CharacterOverlay.prefab";
    private const string SettingsPath =
        "Assets/Prefabs/UI/SettingsOverlay.prefab";

    [MenuItem("ReMind/Bind Overlay Presentation")]
    public static void Bind()
    {
        BindPrefab<CharacterOverlayController>(CharacterPath,
            "04_CharacterDetails");
        BindPrefab<SettingsMenuController>(SettingsPath,
            "SettingsPanel");
        AssetDatabase.SaveAssets();
        Debug.Log("OVERLAY_PRESENTATION_PREFABS_BOUND");
    }

    private static void BindPrefab<TController>(string path, string panelPath)
        where TController : MonoBehaviour
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (!root)
            throw new InvalidOperationException("Overlay prefab is missing: " + path);
        try
        {
            RectTransform panel = root.transform.Find(panelPath)
                as RectTransform;
            TController controller = root.GetComponent<TController>();
            if (!panel || !controller)
                throw new InvalidOperationException(
                    "Overlay root, panel or controller is missing: " + path);
            if (Configure(root, panel, controller))
                PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static bool Configure(GameObject root, RectTransform panel,
        MonoBehaviour controller)
    {
        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        if (!canvasGroup || !panel || !controller)
            throw new InvalidOperationException(
                "Overlay motion needs a CanvasGroup, panel and controller.");
        OverlayPresentation presentation =
            root.GetComponent<OverlayPresentation>();
        bool changed = false;
        if (!presentation)
        {
            presentation = root.AddComponent<OverlayPresentation>();
            changed = true;
        }

        SerializedObject motionData = new(presentation);
        changed |= SetReference(motionData, "canvasGroup", canvasGroup);
        changed |= SetReference(motionData, "panel", panel);
        motionData.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerData = new(controller);
        changed |= SetReference(controllerData, "presentation",
            presentation);
        controllerData.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    private static bool SetReference(SerializedObject data,
        string name, UnityEngine.Object value)
    {
        SerializedProperty property = data.FindProperty(name);
        if (property == null)
            throw new InvalidOperationException(
                data.targetObject.GetType().Name + " is missing " + name);
        if (property.objectReferenceValue == value) return false;
        property.objectReferenceValue = value;
        return true;
    }
}

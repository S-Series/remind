using System;
using REmind.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Connects the authored AppRoot animation to a persistent UI overlay.</summary>
public static class BootstrapTransitionBinder
{
    private const string ScenePath = "Assets/Scenes/Bootstrap.unity";

    [MenuItem("REmind/Bind Music Selected Transition")]
    public static void Bind()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath,
            OpenSceneMode.Single);
        AppRoot root = null;
        foreach (GameObject candidate in scene.GetRootGameObjects())
            if (candidate.TryGetComponent(out root)) break;
        if (!root) throw new InvalidOperationException("Bootstrap AppRoot is missing.");
        Transform animationRoot = root.transform.Find("Animator");
        if (!animationRoot ||
            !animationRoot.TryGetComponent(out Animator animator) ||
            !animationRoot.TryGetComponent(out SpriteRenderer source))
            throw new InvalidOperationException(
                "AppRoot/Animator needs Animator and SpriteRenderer.");

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Art/Animations/MusicSelected.anim");
        Sprite firstFrame = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/ai/ReMind_transition_001-048_no_text/" +
            "ReMind_transition_001.png");
        if (!clip || !firstFrame)
            throw new InvalidOperationException(
                "MusicSelected clip or first frame is missing.");

        Transform overlayRoot = animationRoot.Find("ScreenTransitionOverlay");
        if (!overlayRoot)
        {
            var objectRoot = new GameObject("ScreenTransitionOverlay",
                typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(GraphicRaycaster));
            objectRoot.transform.SetParent(animationRoot, false);
            overlayRoot = objectRoot.transform;
        }
        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        CanvasGroup group = overlayRoot.GetComponent<CanvasGroup>();
        if (!canvas || !group ||
            !overlayRoot.TryGetComponent(out GraphicRaycaster _))
            throw new InvalidOperationException(
                "ScreenTransitionOverlay components are incomplete.");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Transform frameRoot = overlayRoot.Find("Frame");
        if (!frameRoot)
        {
            var frame = new GameObject("Frame",
                typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(overlayRoot, false);
            frameRoot = frame.transform;
        }
        RectTransform bounds = (RectTransform)frameRoot;
        bounds.anchorMin = Vector2.zero;
        bounds.anchorMax = Vector2.one;
        bounds.offsetMin = Vector2.zero;
        bounds.offsetMax = Vector2.zero;
        Image image = frameRoot.GetComponent<Image>();
        if (!image) throw new InvalidOperationException(
            "Transition Frame needs an Image.");
        image.sprite = firstFrame;
        image.color = Color.white;
        image.raycastTarget = true;
        image.preserveAspect = false;
        canvas.enabled = false;
        source.enabled = false;

        SceneTransitionController transition = animationRoot
            .GetComponent<SceneTransitionController>() ??
            animationRoot.gameObject.AddComponent<SceneTransitionController>();
        var serialized = new SerializedObject(transition);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("frameSource").objectReferenceValue = source;
        serialized.FindProperty("musicSelected").objectReferenceValue = clip;
        serialized.FindProperty("firstFrame").objectReferenceValue = firstFrame;
        serialized.FindProperty("overlayCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("overlayGroup").objectReferenceValue = group;
        serialized.FindProperty("overlayImage").objectReferenceValue = image;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var serializedRoot = new SerializedObject(root);
        serializedRoot.FindProperty("sceneTransition").objectReferenceValue =
            transition;
        serializedRoot.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Could not save Bootstrap scene.");
        Debug.Log("Music Selected transition bound to Bootstrap AppRoot.");
    }
}

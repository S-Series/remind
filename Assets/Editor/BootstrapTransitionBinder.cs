using System;
using REmind.Gameplay;
using REmind.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Connects the two authored transition prefabs to the persistent AppRoot.</summary>
public static class BootstrapTransitionBinder
{
    private const string ScenePath = "Assets/Scenes/Bootstrap.unity";

    [MenuItem("REmind/Bind Crystal Scene Transitions %#k")]
    public static void Bind()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            AppRoot root = null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.TryGetComponent(out root)) break;
            if (!root) throw new InvalidOperationException("Bootstrap AppRoot is missing.");
            Transform transitionRoot = root.transform.Find("Animator");
            if (!transitionRoot)
                throw new InvalidOperationException("AppRoot/Animator is missing.");
            var overlay = transitionRoot.Find("CrystalOverlayTransition")
                ?.GetComponent<CrystalTransitionPlayer>();
            var crystal = transitionRoot.Find("CrystalTransition")
                ?.GetComponent<CrystalTransitionPlayer>();
            if (!overlay || !crystal || !overlay.clip || !overlay.loopClip ||
                !overlay.outroClip || !crystal.clip)
                throw new InvalidOperationException("Both transition prefabs must be assigned under AppRoot/Animator.");

            Transform fadeRoot = transitionRoot.Find("SceneFadeCover");
            if (!fadeRoot)
            {
                var go = new GameObject("SceneFadeCover", typeof(RectTransform),
                    typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
                go.transform.SetParent(transitionRoot, false);
                fadeRoot = go.transform;
            }
            var canvas = fadeRoot.GetComponent<Canvas>();
            var group = fadeRoot.GetComponent<CanvasGroup>();
            if (!canvas || !group || !fadeRoot.GetComponent<GraphicRaycaster>())
                throw new InvalidOperationException("SceneFadeCover components are incomplete.");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 29999;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Transform imageRoot = fadeRoot.Find("Frame");
            if (!imageRoot)
            {
                var go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(fadeRoot, false);
                imageRoot = go.transform;
            }
            var bounds = (RectTransform)imageRoot;
            bounds.anchorMin = Vector2.zero;
            bounds.anchorMax = Vector2.one;
            bounds.offsetMin = Vector2.zero;
            bounds.offsetMax = Vector2.zero;
            var image = imageRoot.GetComponent<Image>();
            if (!image) throw new InvalidOperationException("SceneFadeCover/Frame needs an Image.");
            image.color = new Color(0.016f, 0.024f, 0.046f, 1f);
            image.raycastTarget = true;
            canvas.enabled = false;

            var transition = transitionRoot.GetComponent<SceneTransitionController>();
            if (!transition)
                throw new InvalidOperationException("SceneTransitionController is missing.");
            var serialized = new SerializedObject(transition);
            serialized.FindProperty("crystalOverlayTransition").objectReferenceValue = overlay;
            serialized.FindProperty("crystalTransition").objectReferenceValue = crystal;
            serialized.FindProperty("fadeCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("fadeGroup").objectReferenceValue = group;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save Bootstrap scene.");
            Debug.Log("Crystal scene transitions bound to Bootstrap AppRoot.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }
}

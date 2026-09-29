using System;
using System.IO;
using REmind.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace REmind.EditorTools
{
    /// <summary>Owns an isolated preview scene. Never samples or saves the user's open scene.</summary>
    internal sealed class CrystalPreviewRender : IDisposable
    {
        private readonly Scene scene;
        private readonly Camera camera;
        internal readonly CrystalTransitionPlayer player;
        internal readonly RenderTexture output;

        internal CrystalPreviewRender(int width = 1280, int height = 720, string prefabPath = null, bool transparent = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath ?? CrystalTransitionBuilder.PrefabPath);
            if (!prefab) throw new InvalidOperationException("Create the crystal transition assets first.");
            scene = EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player = instance.GetComponent<CrystalTransitionPlayer>();
            var cameraObject = new GameObject("Isolated Crystal Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 540;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = transparent ? Color.clear : new Color(.016f, .024f, .046f, 1f);
            camera.allowHDR = false; camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            output = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            output.name = "Crystal Transition Preview"; output.Create();
            camera.targetTexture = output;
            var canvas = instance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1f;
            // CanvasScaler does not tick in a preview scene. Use the same Expand fit explicitly.
            instance.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = Mathf.Min(width / 1920f, height / 1080f);
        }

        internal void Render(float seconds, AnimationClip phaseClip = null)
        {
            if (phaseClip)
            {
                player.PlayPhase(phaseClip, phaseClip.length,
                    phaseClip == player.loopClip, phaseClip != player.clip);
                player.Pause();
            }
            player.Seek(seconds);
            Canvas.ForceUpdateCanvases();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = output };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new InvalidOperationException("The current render pipeline does not support URP single-camera previews.");
            RenderPipeline.SubmitRenderRequest(camera, request);
        }

        internal Texture2D ReadPixels()
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = output;
                var image = new Texture2D(output.width, output.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0);
                image.Apply(); return image;
            }
            finally { RenderTexture.active = previous; }
        }

        public void Dispose()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (output) { output.Release(); UnityEngine.Object.DestroyImmediate(output); }
        }
    }

    public sealed class CrystalTransitionPreview : EditorWindow
    {
        private enum OverlayPhase { Intro, Loop, Outro }
        private CrystalPreviewRender render;
        private bool playing;
        private bool loop = true;
        private float seconds = .65f;
        private double lastUpdate;
        private bool needsRender = true;
        private string error;
        [SerializeField] private bool transparentOverlay;
        [SerializeField] private OverlayPhase overlayPhase;

        private AnimationClip SelectedClip => !render?.player ? null :
            !transparentOverlay ? render.player.clip : overlayPhase == OverlayPhase.Loop
                ? render.player.loopClip : overlayPhase == OverlayPhase.Outro
                    ? render.player.outroClip : render.player.clip;
        private float SelectedDuration => SelectedClip ? SelectedClip.length :
            transparentOverlay ? overlayPhase == OverlayPhase.Loop ? .8f : 1.2f : 1.5f;

        [MenuItem("REmind/Crystal Transition/Preview %&t")]
        public static void Open() => Open(false);
        [MenuItem("REmind/Crystal Transition/Preview Transparent Overlay")]
        public static void OpenOverlay() => Open(true);
        private static void Open(bool overlay)
        {
            var window = GetWindow<CrystalTransitionPreview>("Crystal Preview");
            window.transparentOverlay = overlay;
            window.overlayPhase = OverlayPhase.Intro;
            window.render?.Dispose(); window.render = null; window.error = null; window.needsRender = true;
            window.seconds = .65f;
        }
        private void OnEnable()
        {
            EditorApplication.update += Tick;
            lastUpdate = EditorApplication.timeSinceStartup;
            minSize = new Vector2(540, 360);
            hasUnsavedChanges = false;
        }
        private void OnDisable()
        { EditorApplication.update -= Tick; render?.Dispose(); render = null; }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing && render != null)
            {
                seconds += (float)(now - lastUpdate);
                float tail = transparentOverlay && overlayPhase == OverlayPhase.Loop ? 0f : .65f;
                if (seconds > SelectedDuration + tail)
                {
                    if (loop) seconds = 0; else { seconds = SelectedDuration; playing = false; }
                }
                needsRender = true; Repaint();
            }
            lastUpdate = now;
        }
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(playing ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(65)))
                { if (!playing && seconds >= SelectedDuration) seconds = 0; playing = !playing; lastUpdate = EditorApplication.timeSinceStartup; }
                if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(65)))
                { seconds = 0; playing = true; needsRender = true; }
                loop = GUILayout.Toggle(loop, "Loop", EditorStyles.toolbarButton, GUILayout.Width(55));
                if (transparentOverlay)
                {
                    EditorGUI.BeginChangeCheck();
                    overlayPhase = (OverlayPhase)EditorGUILayout.EnumPopup(overlayPhase,
                        EditorStyles.toolbarPopup, GUILayout.Width(75));
                    if (EditorGUI.EndChangeCheck())
                    { seconds = 0f; playing = false; needsRender = true; }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Select Prefab", EditorStyles.toolbarButton)) Select(transparentOverlay ? CrystalOverlayBuilder.PrefabPath : CrystalTransitionBuilder.PrefabPath);
                if (GUILayout.Button("Select Clip", EditorStyles.toolbarButton))
                    Select(!transparentOverlay ? CrystalTransitionBuilder.ClipPath :
                        overlayPhase == OverlayPhase.Loop ? CrystalOverlayPhaseBuilder.LoopPath :
                        overlayPhase == OverlayPhase.Outro ? CrystalOverlayPhaseBuilder.OutroPath :
                        CrystalOverlayPhaseBuilder.IntroPath);
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
                {
                    render?.Dispose(); render = null; needsRender = true; error = null;
                }
            }
            EditorGUI.BeginChangeCheck();
            float selectedTime = EditorGUILayout.Slider("Time (seconds)", Mathf.Min(seconds, SelectedDuration), 0f, SelectedDuration);
            if (EditorGUI.EndChangeCheck()) { seconds = selectedTime; playing = false; needsRender = true; }
            int lastFrame = Mathf.Max(0, Mathf.RoundToInt(SelectedDuration * 60) - 1);
            EditorGUILayout.LabelField($"Frame {Mathf.Clamp(Mathf.FloorToInt(seconds / SelectedDuration * (lastFrame + 1)), 0, lastFrame):00} / {lastFrame}   •   1920 × 1080   •   {(transparentOverlay ? "Transparent overlay / " + overlayPhase : "Whiteout")}");
            if (error != null) { EditorGUILayout.HelpBox(error, MessageType.Error); return; }
            try
            {
                if (render == null)
                {
                    render = new CrystalPreviewRender(1280, 720, transparentOverlay ? CrystalOverlayBuilder.PrefabPath : CrystalTransitionBuilder.PrefabPath, transparentOverlay);
                    needsRender = true;
                }
                if (needsRender && Event.current.type == EventType.Repaint)
                { render.Render(seconds, SelectedClip); needsRender = false; }
                Rect rect = GUILayoutUtility.GetRect(320, 4000, 180, 4000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (transparentOverlay) EditorGUI.DrawTextureTransparent(rect, render.output, ScaleMode.ScaleToFit);
                else EditorGUI.DrawPreviewTexture(rect, render.output, null, ScaleMode.ScaleToFit);
            }
            catch (ExitGUIException) { throw; }
            catch (Exception ex) { error = ex.Message; Debug.LogException(ex); }
            EditorGUILayout.HelpBox("프리팹의 Artwork를 Inspector에서 수정하고, 움직임은 Animation 창에서 편집하세요. 저장 후 Reload를 누르면 미리보기에 반영됩니다.", MessageType.Info);
        }
        private static void Select(string path)
        { Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path); EditorGUIUtility.PingObject(Selection.activeObject); }
    }

    [CustomEditor(typeof(CrystalTransitionPlayer))]
    public sealed class CrystalTransitionPlayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Open isolated animation preview"))
            {
                if (((CrystalTransitionPlayer)target).hideOnComplete) CrystalTransitionPreview.OpenOverlay();
                else CrystalTransitionPreview.Open();
            }
            if (!Application.isPlaying && GUILayout.Button("Show assembled pose for editing"))
            {
                var player = (CrystalTransitionPlayer)target;
                Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Show crystal editing pose");
                player.Seek(player.Duration * 39f / 90f);
                EditorUtility.SetDirty(player);
                SceneView.RepaintAll();
            }
            if (Application.isPlaying)
            {
                var player = (CrystalTransitionPlayer)target;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play / Restart")) player.Play();
                    if (GUILayout.Button("Pause")) player.Pause();
                    if (GUILayout.Button("Resume")) player.Resume();
                    if (GUILayout.Button("Hide")) player.Hide();
                }
            }
        }
    }
}

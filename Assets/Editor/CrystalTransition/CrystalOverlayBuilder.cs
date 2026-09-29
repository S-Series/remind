using System;
using System.Collections.Generic;
using System.IO;
using REmind.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace REmind.EditorTools
{
    /// <summary>Authors a separate transparent entrance/exit clip; never modifies the source prefab or clip.</summary>
    public static class CrystalOverlayBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Transitions/CrystalOverlayTransition.prefab";
        public const string ClipPath = "Assets/Art/Animations/ReMind_CrystalOverlay.anim";
        public const string ScenePath = "Assets/Scenes/prev/CrystalOverlayPreview.unity";
        public const float Duration = 2.4f;

        [MenuItem("REmind/Crystal Transition/Create Transparent Overlay")]
        public static void Build()
        {
            if (File.Exists(PrefabPath) || File.Exists(ClipPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("Transparent overlay already exists. Edit its prefab/clip directly; it will not be overwritten.");
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                throw new InvalidOperationException("Save the current untitled scene before creating the overlay demo.");
            var root = PrefabUtility.LoadPrefabContents(CrystalTransitionBuilder.PrefabPath);
            try
            {
                root.name = "ReMind_CrystalOverlay";
                var player = root.GetComponent<CrystalTransitionPlayer>();
                player.clip.SampleAnimation(root, .65f);
                foreach (string name in new[] { "00_Background", "05_White_Bloom", "06_White_Cover" })
                {
                    var child = root.transform.Find(name);
                    if (child) UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                player.whiteCover = null; player.duration = Duration; player.starSize = .5f;
                player.orbitOpacity = .85f; player.particleOpacity = .85f;
                player.hideOnComplete = true; player.blockInputDuringPlayback = false; player.playOnStart = false;
                player.covered = new UnityEngine.Events.UnityEvent();
                player.completed = new UnityEngine.Events.UnityEvent();
                var oldAnimation = root.GetComponent<Animation>();
                if (oldAnimation) UnityEngine.Object.DestroyImmediate(oldAnimation);

                var envelope = Group("00_Elements_Envelope", root.transform);
                var children = new List<Transform>();
                foreach (Transform t in root.transform) if (t != envelope) children.Add(t);
                foreach (var t in children) t.SetParent(envelope, false);
                var clip = new AnimationClip { name = "ReMind_CrystalOverlay", frameRate = 60,
                    legacy = true, wrapMode = WrapMode.ClampForever };
                var author = new Curves(root.transform, clip);
                author.Alpha(envelope, 0, 0, .18f, 1, 1.65f, 1, 2.36f, 0, Duration, 0);

                var star = player.starSizeRoot.Find("Animated_Star");
                author.Scale(star, 0, .9f, .72f, 1, 1.5f, 1.025f, Duration, 1.12f);
                author.Alpha(star, 0, 0, .58f, 1, 1.52f, 1, 2.25f, 0, Duration, 0);
                string[] parts = { "04_Center_Motion", "03_Vertical_Motion", "02_Horizontal_Motion", "01_Diagonal_Motion" };
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = star.Find(parts[i]); float start = i * .09f;
                    author.Alpha(part, 0, 0, start + .04f, 0, start + .28f, 1, Duration, 1);
                    if (i == 1)
                    { author.Axis(part, "x", 0, .65f, .60f, 1, Duration, 1); author.Axis(part, "y", 0, .03f, .60f, 1, Duration, 1); }
                    else if (i == 2)
                    { author.Axis(part, "x", 0, .03f, .66f, 1, Duration, 1); author.Axis(part, "y", 0, .65f, .66f, 1, Duration, 1); }
                    else author.Scale(part, 0, .1f, start + .4f, 1, Duration, 1);
                }
                var halo = star.Find("00_Soft_Halo");
                halo.localScale = Vector3.one * .78f;
                halo.GetComponent<CanvasGroup>().alpha = .42f;
                player.orbitBack.transform.localScale = Vector3.one * .7f;
                player.orbitFront.transform.localScale = Vector3.one * .7f;

                for (int side = 0; side < 2; side++)
                {
                    Transform parent = side == 0 ? player.orbitBack.transform : player.orbitFront.transform;
                    for (int i = 0; i < 3; i++)
                    {
                        var orbit = parent.Find("Orbit_" + (i + 1) + "_Motion");
                        float rx = i == 0 ? 250 : i == 1 ? 275 : 210;
                        float ry = i == 0 ? 65 : i == 1 ? 100 : 210;
                        float tilt = i == 0 ? 27 : i == 1 ? -18 : 0;
                        author.Scale(orbit, 0, .48f, .82f, .78f, 1.6f, .79f, Duration, .87f);
                        author.Curve(orbit, typeof(Transform), "localEulerAnglesRaw.z", 0, tilt - 6, Duration, tilt + 9);
                        float opacity = side == 0 ? .38f : .85f;
                        author.Alpha(orbit, 0, 0, .20f + .05f * i, 0, .80f, opacity, 1.55f, opacity, 2.3f, 0, Duration, 0);
                        foreach (var arc in orbit.GetComponentsInChildren<CrystalOrbitGraphic>())
                        {
                            arc.radius = new Vector2(rx, ry);
                            author.Curve(arc.transform, typeof(CrystalOrbitGraphic), "reveal", 0, 0, .20f + .05f * i, 0, .85f + .05f * i, 1, Duration, 1);
                        }
                        var dot = orbit.Find("Orbiting_Light");
                        int orbitIndex = i; bool front = side == 1;
                        Func<float, float> phase = t => orbitIndex * 1.8f + t * 1.4f;
                        author.Sample(dot, typeof(RectTransform), "m_AnchoredPosition.x", t => Mathf.Cos(phase(t)) * rx);
                        author.Sample(dot, typeof(RectTransform), "m_AnchoredPosition.y", t => Mathf.Sin(phase(t)) * ry);
                        author.Sample(dot, typeof(CanvasGroup), "m_Alpha", t => (Mathf.Sin(phase(t)) <= 0) == front ? 1 : 0);
                    }
                }

                var particles = player.particles.transform;
                var rays = particles.Find("03_Light_Rays");
                if (rays) UnityEngine.Object.DestroyImmediate(rays.gameObject);
                var random = new System.Random(280926);
                var shards = particles.Find("01_Crystal_Shards");
                for (int i = 0; i < shards.childCount; i++)
                {
                    var motion = shards.GetChild(i);
                    float jitterSample = (float)random.NextDouble();
                    float radiusSample = (float)random.NextDouble();
                    int slot = i * 7 % shards.childCount;
                    float angle = (slot + .5f) * Mathf.PI * 2 / shards.childCount +
                                  (jitterSample - .5f) * 8f * Mathf.Deg2Rad;
                    float radius = 350 + radiusSample * 360;
                    float start = .18f + (float)random.NextDouble() * .4f;
                    Vector2 p = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .7f);
                    Vector2 end = p * 1.18f + new Vector2(12, 28);
                    motion.localScale = Vector3.one * (.7f + (float)random.NextDouble() * .5f);
                    author.Position(motion, p * .92f, end);
                    author.Curve(motion, typeof(Transform), "localEulerAnglesRaw.z", 0, angle * Mathf.Rad2Deg - 40, Duration, angle * Mathf.Rad2Deg - 25);
                    author.Alpha(motion, 0, 0, start, 0, start + .4f, .72f, 1.5f, .72f, 2.32f, 0, Duration, 0);
                }
                var dust = particles.Find("02_Stardust");
                for (int i = 0; i < dust.childCount; i++)
                {
                    var motion = dust.GetChild(i);
                    Vector2 p = new Vector2((float)random.NextDouble() * 1580 - 790, (float)random.NextDouble() * 820 - 410);
                    float start = .2f + (float)random.NextDouble() * .45f;
                    motion.localScale = Vector3.one; motion.localRotation = Quaternion.identity;
                    author.Position(motion, p, p + new Vector2(10, 35));
                    author.Alpha(motion, 0, 0, start, 0, start + .4f, .5f, 1.65f, .3f, 2.3f, 0, Duration, 0);
                }

                var axes = Group("05_Fine_Cross_Light", envelope);
                Line(axes, "Horizontal_Core", Vector2.zero, new Vector2(1440, 1), .23f);
                Line(axes, "Horizontal_Glow", Vector2.zero, new Vector2(1440, 4), .035f);
                Line(axes, "Vertical_Core", Vector2.zero, new Vector2(1, 880), .19f);
                author.Scale(axes, 0, .03f, .65f, 1, 1.5f, 1, Duration, 1.1f);
                author.Alpha(axes, 0, 0, .6f, 1, 1.4f, .8f, 2.05f, 0, Duration, 0);

                var ornaments = Group("06_Star_Ornaments", envelope);
                var source = star.Find("04_Center_Motion/Artwork_EDIT_Size_Color_Texture").GetComponent<RawImage>();
                Spark(ornaments, "Bottom_Center", source, new Vector2(0, -285), 360);
                Line(ornaments, "Bottom_Line_Left", new Vector2(-100, -285), new Vector2(150, 1), .4f);
                Line(ornaments, "Bottom_Line_Right", new Vector2(100, -285), new Vector2(150, 1), .4f);
                for (int i = 0; i < 6; i++)
                {
                    float x = (i < 3 ? -1 : 1) * (65 + i % 3 * 60);
                    Spark(ornaments, "Bottom_Spark_" + i, source, new Vector2(x, -285), 65);
                }
                Spark(ornaments, "Upper_Right_Star", source, new Vector2(790, 310), 190);
                Line(ornaments, "Upper_Right_Thread", new Vector2(790, 420), new Vector2(1, 210), .35f);
                Spark(ornaments, "Lower_Left_Star", source, new Vector2(-740, -305), 140);
                Line(ornaments, "Lower_Left_Thread", new Vector2(-840, -305), new Vector2(180, 1), .3f);
                author.Alpha(ornaments, 0, 0, .4f, 0, .95f, .9f, 1.55f, .9f, 2.25f, 0, Duration, 0);

                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                var raycaster = root.GetComponent<GraphicRaycaster>();
                if (raycaster) UnityEngine.Object.DestroyImmediate(raycaster);
                AssetDatabase.CreateAsset(clip, ClipPath); player.clip = clip;
                var animation = root.AddComponent<Animation>(); animation.AddClip(clip, clip.name);
                animation.clip = clip; animation.playAutomatically = false; animation.enabled = false;
                player.Seek(0); player.Hide();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                instance.GetComponent<CrystalTransitionPlayer>().playOnStart = false;
                // The Animation window samples the clip, but does not change the unkeyed root visibility.
                // Keep the preview instance visible so its animated children can be seen while scrubbing.
                instance.GetComponent<CanvasGroup>().alpha = 1f;
                instance.GetComponent<Animation>().enabled = true;
                var camera = new GameObject("Preview Camera - background belongs to scene", typeof(Camera)).GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .045f, .07f, 1);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
        }

        private static RectTransform Group(string name, Transform parent)
        { var r = CrystalTransitionBuilder.Rect(name, parent); r.gameObject.AddComponent<CanvasGroup>(); return r; }
        private static void Line(Transform parent, string name, Vector2 position, Vector2 size, float alpha)
        {
            var r = CrystalTransitionBuilder.Rect(name, parent, size); r.anchoredPosition = position;
            var image = r.gameObject.AddComponent<Image>(); image.color = new Color(.72f, .78f, 1, alpha); image.raycastTarget = false;
        }
        private static void Spark(Transform parent, string name, RawImage source, Vector2 position, float size)
        {
            var r = CrystalTransitionBuilder.Rect(name, parent, Vector2.one * size);
            r.anchoredPosition = position; r.pivot = source.rectTransform.pivot;
            var image = r.gameObject.AddComponent<RawImage>(); image.texture = source.texture; image.raycastTarget = false;
        }
        private sealed class Curves
        {
            private readonly Transform root; private readonly AnimationClip clip;
            internal Curves(Transform root, AnimationClip clip) { this.root = root; this.clip = clip; }
            internal void Curve(Transform t, Type type, string property, params float[] pairs)
            {
                var keys = new Keyframe[pairs.Length / 2];
                for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(pairs[i * 2], pairs[i * 2 + 1], 0, 0);
                Set(t, type, property, new AnimationCurve(keys));
            }
            private void Set(Transform t, Type type, string property, AnimationCurve curve) =>
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(t, root), type, property), curve);
            internal void Sample(Transform t, Type type, string property, Func<float, float> f)
            {
                var keys = new Keyframe[145];
                for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(i / 60f, f(i / 60f));
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                Set(t, type, property, curve);
            }
            internal void Alpha(Transform t, params float[] pairs) => Curve(t, typeof(CanvasGroup), "m_Alpha", pairs);
            internal void Axis(Transform t, string axis, params float[] pairs) => Curve(t, typeof(Transform), "m_LocalScale." + axis, pairs);
            internal void Scale(Transform t, params float[] pairs) { Axis(t, "x", pairs); Axis(t, "y", pairs); }
            internal void Position(Transform t, Vector2 start, Vector2 end)
            {
                Curve(t, typeof(RectTransform), "m_AnchoredPosition.x", 0, start.x, Duration, end.x);
                Curve(t, typeof(RectTransform), "m_AnchoredPosition.y", 0, start.y, Duration, end.y);
            }
        }
    }
}

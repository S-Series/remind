using System;
using System.IO;
using REmind.Gameplay.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace REmind.EditorTools
{
    public static class CrystalTransitionBuilder
    {
        public const string Folder = "Assets/Art/Transitions/CrystalCompass";
        public const string PrefabPath = "Assets/Prefabs/Transitions/CrystalTransition.prefab";
        public const string ClipPath = "Assets/Art/Animations/ReMind_CrystalTransition.anim";
        public const string ScenePath = "Assets/Scenes/prev/CrystalPreview.unity";
        public const string MaterialPath = "Assets/Art/Shaders/RadialHalo.mat";
        private const string Sources = "Assets/Art/TransitionFX/GuidedWarp60/Sprites/";
        private static readonly Color Ice = new Color(0.66f, 0.72f, 1f, 1f);

        [MenuItem("REmind/Crystal Transition/Create editable assets")]
        public static void Build()
        {
            // Refuse to replace an artist's edits. The source builder is only for the initial asset creation.
            if (File.Exists(PrefabPath) || File.Exists(ClipPath) || File.Exists(ScenePath))
                throw new InvalidOperationException("Crystal assets already exist. Edit them directly; creation will not overwrite them.");
            foreach (string file in Directory.GetFiles(Folder + "/Textures", "*.png"))
            {
                string path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var root = Rect("ReMind_CrystalTransition", null);
                var canvas = root.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
                var scaler = root.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                root.gameObject.AddComponent<GraphicRaycaster>();
                var player = root.gameObject.AddComponent<CrystalTransitionPlayer>();
                player.visibility = root.gameObject.AddComponent<CanvasGroup>();
                var clip = new AnimationClip { name = "ReMind_CrystalTransition", frameRate = 60, legacy = true,
                    wrapMode = WrapMode.ClampForever };
                var author = new Author(root, clip);
                var background = Rect("00_Background", root);
                Stretch(background);
                var bg = background.gameObject.AddComponent<Image>();
                bg.color = new Color(0.016f, 0.024f, 0.046f, 1f);
                bg.raycastTarget = true;

                var back = Group("01_Orbit_Back", root);
                player.orbitBack = back.GetComponent<CanvasGroup>();
                var starSize = Rect("02_Star_Size", root);
                player.starSizeRoot = starSize;
                var starMotion = Group("Animated_Star", starSize);
                author.Scale(starMotion, f => 1f + 4f * Mathf.Pow(Smooth(49, 66, f), 3f));
                author.Alpha(starMotion, f => 1f - Smooth(53, 66, f));

                var halo = Group("00_Soft_Halo", starMotion);
                var haloArt = Rect("Artwork", halo, new Vector2(740, 740));
                var haloGraphic = haloArt.gameObject.AddComponent<CrystalHaloGraphic>();
                haloGraphic.raycastTarget = false;
                haloGraphic.color = new Color(0.30f, 0.40f, 0.95f, 0.35f);
                author.Alpha(halo, f => Smooth(0, 24, f) * (0.85f + 0.15f * Mathf.Sin(f * 0.09f)));
                author.Scale(halo, f => Mathf.Lerp(0.10f, 1f, Smooth(0, 30, f)));

                Star(author, starMotion, "01_Diagonal", "Star_Diagonal.png", 0.33f, 627, 627, 18, 29, 0);
                Star(author, starMotion, "02_Horizontal", "Star_Horizontal.png", 0.366f, 627, 647, 13, 25, 1);
                Star(author, starMotion, "03_Vertical", "Star_Vertical.png", 0.423f, 627, 627, 10, 24, 2);
                Star(author, starMotion, "04_Center", "Star_Center.png", 0.46f, 628, 626, 0, 13, 0);

                var front = Group("03_Orbit_Front", root);
                player.orbitFront = front.GetComponent<CanvasGroup>();
                for (int i = 0; i < 3; i++)
                {
                    float rx = 260 + i * 36, ry = i == 0 ? 78 : i == 1 ? 115 : 240;
                    float tilt = i == 0 ? 26 : i == 1 ? -19 : 0;
                    Orbit(author, back, i, rx, ry, tilt, false);
                    Orbit(author, front, i, rx, ry, tilt, true);
                }

                var particles = Group("04_Particles_And_Warp", root);
                player.particles = particles.GetComponent<CanvasGroup>();
                var shards = Rect("01_Crystal_Shards", particles);
                var dust = Rect("02_Stardust", particles);
                var rays = Rect("03_Light_Rays", particles);
                var random = new System.Random(280926);
                string[] shardNames = { "shard_sliver", "shard_triangle", "shard_diamond", "shard_kite" };
                string[] rayNames = { "ray_straight", "ray_curve", "ray_dotted", "ray_double" };
                for (int i = 0; i < 18; i++)
                {
                    float a = (float)random.NextDouble() * Mathf.PI * 2;
                    float radius = 220 + (float)random.NextDouble() * 330;
                    float onset = 25 + (float)random.NextDouble() * 15;
                    float size = 35 + (float)random.NextDouble() * 80;
                    var motion = Group("Shard_" + (i + 1).ToString("00") + "_Motion", shards);
                    Texture2D tex = Texture(Sources + "Shards/" + shardNames[i % 4] + ".png");
                    Raw("Artwork", motion, tex, new Vector2(size * tex.width / tex.height, size), new Vector2(.5f, .5f));
                    author.Position(motion, f => Polar(a + 0.12f * Smooth(20, 50, f), radius * (1f + 4f * Mathf.Pow(Smooth(45, 75, f), 2.7f))));
                    author.Rotation(motion, f => a * Mathf.Rad2Deg - 40 + f * 0.15f);
                    author.Scale(motion, f => 0.55f + 2f * Smooth(45, 73, f));
                    author.Alpha(motion, f => Smooth(onset, onset + 10, f) * (1f - Smooth(66, 76, f)) * 0.85f);
                }
                for (int i = 0; i < 44; i++)
                {
                    float a = (float)random.NextDouble() * Mathf.PI * 2;
                    float radius = 100 + (float)random.NextDouble() * 520;
                    float size = 1.3f + (float)random.NextDouble() * 3;
                    float onset = 18 + (float)random.NextDouble() * 27;
                    var motion = Group("Dust_" + (i + 1).ToString("00") + "_Motion", dust);
                    var art = Rect("Artwork", motion, new Vector2(size, size));
                    var image = art.gameObject.AddComponent<Image>(); image.color = Ice; image.raycastTarget = false;
                    art.localRotation = Quaternion.Euler(0, 0, 45);
                    author.Position(motion, f => Polar(a, radius * (1f + 5f * Mathf.Pow(Smooth(44, 76, f), 2.8f))));
                    author.Rotation(motion, f => a * Mathf.Rad2Deg);
                    author.ScaleXY(motion, f => new Vector2(1f + 20f * Smooth(47, 72, f), 1f));
                    author.Alpha(motion, f => Smooth(onset, onset + 9, f) * (1f - Smooth(65, 76, f)) * .55f);
                }
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12 + .08f;
                    float onset = 40 + i % 4 * 2;
                    var motion = Group("Ray_" + (i + 1).ToString("00") + "_Motion", rays);
                    Texture2D tex = Texture(Sources + "Rays/" + rayNames[i % 4] + ".png");
                    Raw("Artwork", motion, tex, new Vector2(640, 640f * tex.height / tex.width), new Vector2(.10f, .5f));
                    author.Position(motion, f => Polar(a, 90 + 560f * Mathf.Pow(Smooth(47, 77, f), 2.3f)));
                    author.Rotation(motion, f => a * Mathf.Rad2Deg);
                    author.ScaleXY(motion, f => new Vector2(.2f + 1.8f * Smooth(43, 73, f), .22f + .5f * Smooth(45, 71, f)));
                    author.Alpha(motion, f => Smooth(onset, onset + 12, f) * (1f - Smooth(68, 79, f)) * .55f);
                }

                var bloom = Group("05_White_Bloom", root);
                var bloomArt = Rect("Artwork", bloom, new Vector2(1600, 1600));
                var bloomGraphic = bloomArt.gameObject.AddComponent<CrystalHaloGraphic>();
                bloomGraphic.color = new Color(.84f, .88f, 1f, 1); bloomGraphic.raycastTarget = false;
                author.Scale(bloom, f => .05f + 3.5f * Mathf.Pow(Smooth(49, 79, f), 2));
                author.Alpha(bloom, f => Smooth(50, 75, f));
                var cover = Group("06_White_Cover", root); Stretch(cover);
                player.whiteCover = cover.GetComponent<CanvasGroup>();
                var white = cover.gameObject.AddComponent<Image>(); white.color = Color.white; white.raycastTarget = false;
                author.Alpha(cover, f => Smooth(66, 79, f));
                Material haloMaterial = GetHaloMaterial();
                foreach (var graphic in root.GetComponentsInChildren<CrystalHaloGraphic>(true)) graphic.material = haloMaterial;
                AssetDatabase.CreateAsset(clip, ClipPath);
                player.clip = clip;
                var animation = root.gameObject.AddComponent<Animation>();
                animation.AddClip(clip, clip.name); animation.clip = clip;
                animation.playAutomatically = false; animation.enabled = false;
                player.Seek(0f); player.Hide();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                UnityEngine.Object.DestroyImmediate(root.gameObject);
                CrystalTransitionCurveCleanup.Simplify();
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                instance.GetComponent<CrystalTransitionPlayer>().playOnStart = false;
                var camera = new GameObject("Preview Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 540;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.016f, .024f, .046f);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("Crystal transition: editable prefab, clip and demo scene created.");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        internal static Material GetHaloMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material) return material;
            Shader shader = Shader.Find("ReMind/UI/Radial Halo");
            if (!shader) throw new InvalidOperationException("Radial Halo shader is missing.");
            material = new Material(shader) { name = "RadialHalo" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void Star(Author author, RectTransform parent, string name, string file,
            float scale, float pivotX, float pivotY, float start, float end, int stretchAxis)
        {
            var motion = Group(name + "_Motion", parent);
            var texture = Texture(Folder + "/Textures/" + file);
            Raw("Artwork_EDIT_Size_Color_Texture", motion, texture,
                new Vector2(texture.width, texture.height) * scale,
                new Vector2(pivotX / texture.width, 1f - pivotY / texture.height));
            author.Alpha(motion, f => Smooth(start, start + 7, f));
            author.ScaleXY(motion, f =>
            {
                float p = Smooth(start, end, f);
                if (stretchAxis == 1) return new Vector2(Mathf.Lerp(.03f, 1f, p), Mathf.Lerp(.6f, 1f, p));
                if (stretchAxis == 2) return new Vector2(Mathf.Lerp(.6f, 1f, p), Mathf.Lerp(.03f, 1f, p));
                return Vector2.one * Mathf.Lerp(.05f, 1f, p);
            });
        }

        private static void Orbit(Author author, RectTransform parent, int index, float rx, float ry, float tilt, bool front)
        {
            var motion = Group("Orbit_" + (index + 1) + "_Motion", parent);
            author.Rotation(motion, f => tilt + 10f * Smooth(23, 50, f) + 38f * Mathf.Pow(Smooth(48, 70, f), 2));
            author.Scale(motion, f => .7f + .3f * Smooth(19, 34, f) + 2f * Mathf.Pow(Smooth(49, 72, f), 2));
            author.Alpha(motion, f => Smooth(19 + index * 3, 30 + index * 3, f) * (1f - Smooth(55, 70, f)) * (front ? .9f : .4f));
            for (int light = 0; light < 3; light++)
            {
                var arc = Rect(light == 2 ? "01_Crisp_Arc" : "00_Glow_" + light, motion);
                var g = arc.gameObject.AddComponent<CrystalOrbitGraphic>(); g.raycastTarget = false;
                g.radius = new Vector2(rx, ry); g.startDegrees = front ? 180 : 0;
                g.lineWidth = light == 0 ? 10 : light == 1 ? 4 : 1.25f;
                g.color = new Color(Ice.r, Ice.g, Ice.b, light == 0 ? .055f : light == 1 ? .14f : 1f);
                author.Curve(arc, typeof(CrystalOrbitGraphic), "reveal", f => Smooth(18 + index * 4, 34 + index * 4, f));
            }
            var dot = Group("Orbiting_Light", motion);
            var glow = Rect("Glow", dot, new Vector2(38, 38));
            var halo = glow.gameObject.AddComponent<CrystalHaloGraphic>(); halo.color = Ice; halo.raycastTarget = false;
            var point = Rect("Point", dot, new Vector2(4, 4));
            point.localRotation = Quaternion.Euler(0, 0, 45);
            var img = point.gameObject.AddComponent<Image>(); img.color = Color.white; img.raycastTarget = false;
            Func<float, float> phase = f => index * 1.6f + f * .045f + Mathf.Pow(Mathf.Max(0, f - 43), 2f) * .0018f;
            author.Position(dot, f => new Vector2(Mathf.Cos(phase(f)) * rx, Mathf.Sin(phase(f)) * ry));
            author.Alpha(dot, f => (Mathf.Sin(phase(f)) <= 0) == front ? 1f : 0f);
        }

        internal static RectTransform Rect(string name, Transform parent, Vector2? size = null)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (parent) r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = size ?? new Vector2(1920, 1080);
            return r;
        }
        private static RectTransform Group(string name, Transform parent)
        {
            var r = Rect(name, parent); r.gameObject.AddComponent<CanvasGroup>(); return r;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static Texture2D Texture(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!t) throw new InvalidOperationException("Missing source: " + path);
            return t;
        }
        private static void Raw(string name, Transform parent, Texture texture, Vector2 size, Vector2 pivot)
        {
            var r = Rect(name, parent, size); r.pivot = pivot;
            var img = r.gameObject.AddComponent<RawImage>(); img.texture = texture; img.raycastTarget = false;
        }
        private static Vector2 Polar(float a, float r) => new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * .64f);
        private static float Smooth(float a, float b, float f)
        {
            float t = Mathf.InverseLerp(a, b, f); return t * t * (3f - 2f * t);
        }

        private sealed class Author
        {
            private readonly Transform root; private readonly AnimationClip clip;
            internal Author(Transform root, AnimationClip clip) { this.root = root; this.clip = clip; }
            internal void Curve(Transform target, Type type, string property, Func<float, float> evaluate)
            {
                var keys = new Keyframe[91];
                for (int f = 0; f <= 90; f++) keys[f] = new Keyframe(f / 60f, evaluate(f));
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                    AnimationUtility.CalculateTransformPath(target, root), type, property), curve);
            }
            internal void Alpha(Transform t, Func<float, float> f) => Curve(t, typeof(CanvasGroup), "m_Alpha", f);
            internal void Rotation(Transform t, Func<float, float> f) => Curve(t, typeof(Transform), "localEulerAnglesRaw.z", f);
            internal void Position(Transform t, Func<float, Vector2> f)
            {
                Curve(t, typeof(RectTransform), "m_AnchoredPosition.x", n => f(n).x);
                Curve(t, typeof(RectTransform), "m_AnchoredPosition.y", n => f(n).y);
            }
            internal void Scale(Transform t, Func<float, float> f) => ScaleXY(t, n => Vector2.one * f(n));
            internal void ScaleXY(Transform t, Func<float, Vector2> f)
            {
                Curve(t, typeof(Transform), "m_LocalScale.x", n => f(n).x);
                Curve(t, typeof(Transform), "m_LocalScale.y", n => f(n).y);
            }
        }
    }
}

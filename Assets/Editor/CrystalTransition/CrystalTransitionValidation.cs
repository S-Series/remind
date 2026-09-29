using System;
using System.IO;
using System.Linq;
using REmind.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace REmind.EditorTools
{
    public static class CrystalTransitionValidation
    {
        public const string ReportFolder = "Library/CrystalTransitionQA";

        [MenuItem("REmind/Crystal Transition/Validate and render contact frames")]
        public static void Run()
        {
            Directory.CreateDirectory(ReportFolder);
            using (var render = new CrystalPreviewRender(1920, 1080))
            {
                var player = render.player;
                Require(Mathf.Abs(player.clip.length - 1.5f) < .001f, "Clip must be 1.5 seconds.");
                Require(player.GetComponentsInChildren<RawImage>(true).Length == 34, "All four star parts, 18 shards and 12 rays must exist.");
                Require(player.GetComponentsInChildren<RawImage>(true).All(x => x.texture), "Missing texture reference.");
                Require(player.GetComponentsInChildren<CrystalOrbitGraphic>(true).Length == 18, "Three front/back orbits with separate glow layers.");
                Require(player.GetComponentsInChildren<Graphic>(true).All(x => x.GetComponent<CanvasRenderer>()), "Every graphic needs a CanvasRenderer.");
                int covered = 0, complete = 0;
                player.covered.AddListener(() => covered++); player.completed.AddListener(() => complete++);
                // Edit-mode validation must not dirty ProjectSettings/TimeManager.asset.
                // Advance is the same explicit clock entry point used by Update with unscaledDeltaTime.
                try
                {
                    player.Play(); player.Advance(.65f);
                    var moving = player.transform.Find("04_Particles_And_Warp/01_Crystal_Shards/Shard_01_Motion");
                    Vector3 at39 = moving.localPosition;
                    player.Advance(.35f); Require((moving.localPosition - at39).magnitude > 10, "Warp must move actual sprite layers.");
                    player.Seek(.65f); Require((moving.localPosition - at39).sqrMagnitude < .001f, "Reverse scrubbing must be deterministic.");
                    player.Pause(); float paused = player.Elapsed; player.Advance(.3f);
                    Require(player.Elapsed == paused, "Pause must stop the clock.");
                    player.Resume(); player.Advance(5f); player.Advance(5f);
                    Require(covered == 1 && complete == 1 && player.IsCovered && !player.IsPlaying, "Completion must fire once and hold white.");
                    player.Hide(); Require(player.visibility.alpha == 0 && !player.visibility.blocksRaycasts, "Hide must release input.");
                    player.duration = 2f; player.Play(); player.Advance(1f);
                    Require(Mathf.Abs(player.Elapsed - 1f) < .0001f && !player.IsCovered, "Duration scaling midpoint.");
                    player.Advance(1f); Require(player.IsCovered && complete == 2, "Duration scaling completion.");
                    player.duration = 1.5f;
                    player.starSize = 1.35f; player.Seek(.65f);
                    Require(Mathf.Abs(player.starSizeRoot.localScale.x - 1.35f) < .001f, "Star size control is connected.");
                    player.starSize = 1f;
                }
                finally { player.Pause(); }

                render.Render(.65f);
                var withOrbits = render.ReadPixels();
                player.orbitOpacity = 0;
                render.Render(.65f);
                var withoutOrbits = render.ReadPixels();
                try
                {
                    var a = withOrbits.GetPixels32(); var b = withoutOrbits.GetPixels32();
                    int different = 0;
                    for (int i = 0; i < a.Length; i++)
                        if (Mathf.Abs(a[i].b - b[i].b) > 8) different++;
                    Require(different > 1000, "Orbit graphics must contribute visible pixels, not only exist in the hierarchy.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(withOrbits);
                    UnityEngine.Object.DestroyImmediate(withoutOrbits);
                    player.orbitOpacity = 1f;
                }

                int[] frames = { 0, 10, 24, 39, 51, 61, 72, 79, 89 };
                foreach (int frame in frames)
                {
                    render.Render(frame / 60f);
                    Texture2D image = render.ReadPixels();
                    try
                    {
                        Color32[] pixels = image.GetPixels32();
                        if (frame >= 79) Require(pixels.All(c => c.r == 255 && c.g == 255 && c.b == 255 && c.a == 255), "White cover must be RGB 255, opaque across the whole frame.");
                        if (frame == 39) Require(pixels.Count(c => c.r > 100 && c.b > 150) > 500, "Rendered star and orbits must be visible.");
                        File.WriteAllBytes(ReportFolder + "/F" + frame.ToString("000") + ".png", image.EncodeToPNG());
                    }
                    finally { UnityEngine.Object.DestroyImmediate(image); }
                }
                for (int frame = 79; frame <= 89; frame++)
                { player.Seek(frame / 60f); Require(player.whiteCover.alpha == 1f, "White hold at frame " + frame); }
                int bindings = AnimationUtility.GetCurveBindings(player.clip).Length;
                File.WriteAllText(ReportFolder + "/result.txt", "PASS\nUnity " + Application.unityVersion + "\n34 source image layers; 18 orbit graphics; " + bindings +
                    " editable animation curves.\nExplicit clock, pause/resume, restart, deterministic seek, 2-second duration, size control, input release, complete-once, visible orbit pixel contribution and opaque-white endpoints passed.\n9 actual URP renders at 1920x1080.\nRuntime Update uses Time.unscaledDeltaTime; this suite exercises Advance directly in Edit Mode.\n");
            }
            Debug.Log("Crystal transition validation PASS: " + ReportFolder);
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}

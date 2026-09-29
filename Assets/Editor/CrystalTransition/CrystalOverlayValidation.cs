using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using REmind.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace REmind.EditorTools
{
    public static class CrystalOverlayValidation
    {
        public const string ReportFolder = "Library/CrystalOverlayQA";
        public static void BuildAndValidate()
        {
            string prefabHash = Hash(CrystalTransitionBuilder.PrefabPath);
            string clipHash = Hash(CrystalTransitionBuilder.ClipPath);
            if (!File.Exists(CrystalOverlayBuilder.PrefabPath)) CrystalOverlayBuilder.Build();
            Require(prefabHash == Hash(CrystalTransitionBuilder.PrefabPath) && clipHash == Hash(CrystalTransitionBuilder.ClipPath), "Original assets were modified.");
            Run();
        }

        [MenuItem("REmind/Crystal Transition/Validate Transparent Overlay")]
        public static void Run()
        {
            Directory.CreateDirectory(ReportFolder);
            using (var render = new CrystalPreviewRender(1920, 1080, CrystalOverlayBuilder.PrefabPath, true))
            {
                var p = render.player;
                Require(!p.whiteCover && p.hideOnComplete && !p.blockInputDuringPlayback, "Overlay must hide at completion and pass input through.");
                Require(Mathf.Abs(p.orbitBack.transform.localScale.x - .7f) < .001f &&
                        Mathf.Abs(p.orbitFront.transform.localScale.x - .7f) < .001f,
                    "Both orbit groups must use the reduced size.");
                Require(!p.GetComponent<GraphicRaycaster>(), "Decorative overlay must not intercept clicks.");
                Require(p.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget && g.GetComponent<CanvasRenderer>()), "Graphics must be renderable and non-interactive.");
                Require(p.GetComponentsInChildren<RawImage>(true).All(g => g.texture), "Missing artwork.");
                Require(!p.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Background") || t.name.Contains("White_Cover") || t.name.Contains("White_Bloom")), "Background or full-screen cover found.");
                var intro = p.clip;
                var loop = p.loopClip;
                var outro = p.outroClip;
                Require(intro && loop && outro &&
                        Mathf.Abs(intro.length - 1.2f) < .001f &&
                        Mathf.Abs(loop.length - .8f) < .001f &&
                        Mathf.Abs(outro.length - 1.2f) < .001f,
                    "The Intro, Loop and Outro clips must be assigned with their authored lengths.");
                CheckPhaseSeams(intro, loop, outro);
                var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrystalOverlayBuilder.ClipPath);
                Require(source, "The original authored clip must remain available.");
                p.clip = source;
                Require(Mathf.Abs(p.clip.length - 2.4f) < .001f, "Clip duration.");
                int covered = 0, completed = 0;
                p.covered.AddListener(() => covered++); p.completed.AddListener(() => completed++);
                p.Play(); p.Advance(1);
                var shardRoot = p.particles.transform.Find("01_Crystal_Shards");
                Require(shardRoot && shardRoot.childCount == 18, "The authored shard group is missing.");
                int[] sectors = new int[8];
                float nearest = float.MaxValue, farthest = 0f;
                foreach (Transform motion in shardRoot)
                {
                    Vector3 point = motion.localPosition;
                    float angle = Mathf.Atan2(point.y / .7f, point.x);
                    float radius = new Vector2(point.x, point.y / .7f).magnitude;
                    nearest = Mathf.Min(nearest, radius);
                    farthest = Mathf.Max(farthest, radius);
                    int sector = Mathf.FloorToInt(Mathf.Repeat(angle, Mathf.PI * 2f) /
                        (Mathf.PI * 2f) * sectors.Length) % sectors.Length;
                    sectors[sector]++;
                }
                Require(sectors.All(count => count > 0), "Crystal shards must cover all eight sectors.");
                Require(nearest < 400f && farthest > 650f && farthest - nearest > 270f,
                    "Shard distances must vary visibly around the center.");
                var shard = p.particles.transform.GetChild(0).GetChild(0);
                Vector3 pose = shard.localPosition;
                p.Advance(.3f); Require(Vector3.Distance(pose, shard.localPosition) > 1, "Shards must move.");
                p.Seek(1); Require(Vector3.Distance(pose, shard.localPosition) < .001f, "Scrubbing must be deterministic.");
                p.Pause(); p.Advance(.3f); Require(p.Elapsed == 1, "Pause.");
                p.Resume(); p.Advance(5); p.Advance(5);
                Require(completed == 1 && covered == 0 && !p.IsPlaying && p.visibility.alpha == 0 && !p.visibility.blocksRaycasts, "Complete once without a covered event; disappear.");
                p.duration = 3; p.Play(); p.Advance(1.5f);
                Require(p.visibility.alpha == 1 && !p.visibility.blocksRaycasts, "Scaled duration midpoint.");
                p.Advance(1.5f); Require(completed == 2 && p.visibility.alpha == 0, "Replay and scaled duration.");
                p.duration = 2.4f;
                p.Play(); p.Pause();
                foreach (float time in new[] { 0f, .4f, 1f, 1.6f, 2.1f, 2.4f })
                {
                    render.Render(time);
                    var image = render.ReadPixels();
                    try
                    {
                        var pixels = image.GetPixels32();
                        if (time == 0 || time == 2.4f) Require(pixels.All(c => c.a == 0), "Start/end must be fully transparent, including halo.");
                        if (time == 1)
                        {
                            Require(pixels.Count(c => c.a > 30) > 1000, "Elements must be visible.");
                            Require(pixels.Count(c => c.a == 0) > pixels.Length * .5f, "Most of frame must remain transparent.");
                            Require(pixels[0].a == 0 && pixels[pixels.Length - 1].a == 0, "Transparent corners.");
                        }
                        File.WriteAllBytes(ReportFolder + "/T" + Mathf.RoundToInt(time * 100).ToString("000") + ".png", image.EncodeToPNG());
                    }
                    finally { UnityEngine.Object.DestroyImmediate(image); }
                }
                render.Render(.2f, loop);
                var loopFrame = render.ReadPixels();
                try
                {
                    Require(loopFrame.GetPixels32().Count(c => c.a > 30) > 1000,
                        "The loading Loop must remain visible.");
                    File.WriteAllBytes(ReportFolder + "/Loop_020.png", loopFrame.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(loopFrame); }
                render.Render(.6f, outro);
                var outroFrame = render.ReadPixels();
                try
                {
                    Require(outroFrame.GetPixels32().Count(c => c.a > 30) > 1000,
                        "The Outro must remain visible before its fade.");
                    File.WriteAllBytes(ReportFolder + "/Outro_060.png", outroFrame.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(outroFrame); }
                p.clip = intro;
                p.PlayPhase(intro, 1f, false, false);
                p.Advance(1f);
                Require(completed == 3 && !p.IsPlaying && p.visibility.alpha == 1f,
                    "Intro must finish once and hold its assembled pose.");
                p.PlayPhase(loop, loop.length, true);
                p.Advance(2.1f);
                Require(p.IsPlaying && completed == 3 && p.visibility.alpha == 1f,
                    "Loading loop must repeat without completing or hiding.");
                p.StopAfterCurrentLoop();
                p.Advance(1f);
                Require(!p.IsPlaying && completed == 3 && p.visibility.alpha == 1f,
                    "Loading loop must stop at its matching end pose.");
                p.PlayPhase(outro, 1f);
                p.Advance(1f);
                Require(!p.IsPlaying && completed == 4 && p.visibility.alpha == 0f,
                    "Outro must finish and hide once.");
            }
            using (var original = new CrystalPreviewRender(320, 180))
            {
                var p = original.player;
                Require(!p.hideOnComplete && p.blockInputDuringPlayback, "Original behavior defaults changed.");
                p.Play(); p.Advance(p.Duration + 1);
                Require(p.IsCovered && p.visibility.alpha == 1 && p.visibility.blocksRaycasts, "Original must hold its white cover.");
            }
            File.WriteAllText(ReportFolder + "/PASS.txt", "PASS: source preservation, Intro/Loop/Outro clip lengths and seams, loop lifecycle, transparent endpoints and corners, visible elements, input passthrough, pause/replay/seek/duration/events, original whiteout regression. Eight 1920x1080 URP frames.");
            Debug.Log("Crystal overlay validation PASS");
        }
        private static void CheckPhaseSeams(AnimationClip intro, AnimationClip loop, AnimationClip outro)
        {
            var introBindings = AnimationUtility.GetCurveBindings(intro);
            Require(introBindings.Length > 0 &&
                    AnimationUtility.GetCurveBindings(loop).Length == introBindings.Length &&
                    AnimationUtility.GetCurveBindings(outro).Length == introBindings.Length,
                "Every phase must animate the same set of properties.");
            bool loopMoves = false;
            foreach (var binding in introBindings)
            {
                var entering = AnimationUtility.GetEditorCurve(intro, binding);
                var holding = AnimationUtility.GetEditorCurve(loop, binding);
                var leaving = AnimationUtility.GetEditorCurve(outro, binding);
                Require(entering != null && holding != null && leaving != null,
                    "Missing phase curve: " + binding.path + "/" + binding.propertyName);
                float pose = entering.Evaluate(intro.length);
                Require(Mathf.Abs(pose - holding.Evaluate(0f)) < .001f &&
                        Mathf.Abs(pose - holding.Evaluate(loop.length)) < .001f &&
                        Mathf.Abs(pose - leaving.Evaluate(0f)) < .001f,
                    "Phase seam differs: " + binding.path + "/" + binding.propertyName);
                if (Mathf.Abs(holding.Evaluate(.2f) - pose) > .1f) loopMoves = true;
            }
            Require(loopMoves, "Loading loop must contain visible motion.");
        }
        private static string Hash(string path)
        { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}

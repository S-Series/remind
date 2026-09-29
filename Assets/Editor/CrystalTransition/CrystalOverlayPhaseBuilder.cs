using System;
using System.Collections.Generic;
using System.IO;
using REmind.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

namespace REmind.EditorTools
{
    /// <summary>One-time split of the authored overlay. The three resulting clips are edited independently.</summary>
    public static class CrystalOverlayPhaseBuilder
    {
        public const string IntroPath = "Assets/Art/Animations/ReMind_CrystalOverlay_Intro.anim";
        public const string LoopPath = "Assets/Art/Animations/ReMind_CrystalOverlay_Loop.anim";
        public const string OutroPath = "Assets/Art/Animations/ReMind_CrystalOverlay_Outro.anim";
        private const float SplitTime = 1.2f;
        private const float LoopSeconds = 0.8f;

        [MenuItem("REmind/Crystal Transition/Split Transparent Overlay Into Phases")]
        public static void Build()
        {
            if (File.Exists(IntroPath) || File.Exists(LoopPath) || File.Exists(OutroPath))
                throw new InvalidOperationException("Phase clips already exist. Edit them directly; this tool will not overwrite them.");
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrystalOverlayBuilder.ClipPath);
            if (!source || !File.Exists(CrystalOverlayBuilder.PrefabPath))
                throw new InvalidOperationException("Create the transparent overlay before splitting it.");

            var intro = Slice(source, 0f, SplitTime, "ReMind_CrystalOverlay_Intro");
            var loop = MakeLoop(source);
            var outro = Slice(source, SplitTime, source.length, "ReMind_CrystalOverlay_Outro");
            AssetDatabase.CreateAsset(intro, IntroPath);
            AssetDatabase.CreateAsset(loop, LoopPath);
            AssetDatabase.CreateAsset(outro, OutroPath);

            var root = PrefabUtility.LoadPrefabContents(CrystalOverlayBuilder.PrefabPath);
            try
            {
                var player = root.GetComponent<CrystalTransitionPlayer>();
                var animation = root.GetComponent<Animation>();
                if (!player || !animation) throw new InvalidOperationException("Overlay prefab is missing its animation components.");
                player.clip = intro;
                player.loopClip = loop;
                player.outroClip = outro;
                animation.RemoveClip(source.name);
                animation.AddClip(intro, intro.name);
                animation.AddClip(loop, loop.name);
                animation.AddClip(outro, outro.name);
                animation.clip = intro;
                EditorUtility.SetDirty(player);
                EditorUtility.SetDirty(animation);
                PrefabUtility.SaveAsPrefabAsset(root, CrystalOverlayBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Crystal overlay phases created: Intro, Loop, Outro.");
        }

        [MenuItem("REmind/Crystal Transition/Split Transparent Overlay Into Phases", true)]
        private static bool CanBuild() =>
            File.Exists(CrystalOverlayBuilder.ClipPath) &&
            File.Exists(CrystalOverlayBuilder.PrefabPath) &&
            !File.Exists(IntroPath) && !File.Exists(LoopPath) && !File.Exists(OutroPath);

        private static AnimationClip Slice(AnimationClip source, float start, float end, string name)
        {
            var result = NewClip(name, WrapMode.ClampForever);
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                var keys = new List<Keyframe> { new Keyframe(0f, curve.Evaluate(start), 0f, 0f) };
                foreach (var sourceKey in curve.keys)
                {
                    if (sourceKey.time <= start + 0.0001f || sourceKey.time >= end - 0.0001f) continue;
                    var key = sourceKey;
                    key.time -= start;
                    keys.Add(key);
                }
                keys.Add(new Keyframe(end - start, curve.Evaluate(end), 0f, 0f));
                AnimationUtility.SetEditorCurve(result, binding, new AnimationCurve(keys.ToArray()));
            }
            return result;
        }

        private static AnimationClip MakeLoop(AnimationClip source)
        {
            var result = NewClip("ReMind_CrystalOverlay_Loop", WrapMode.Loop);
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
                float pose = sourceCurve.Evaluate(SplitTime);
                float sway = binding.propertyName == "localEulerAnglesRaw.z" &&
                    binding.path.Contains("Orbit_") && binding.path.Contains("_Motion")
                    ? (binding.path.Contains("Orbit_2_Motion") ? -4f : 4f) : 0f;
                var keys = sway == 0f
                    ? new[] { new Keyframe(0f, pose), new Keyframe(LoopSeconds, pose) }
                    : new[] { new Keyframe(0f, pose), new Keyframe(.2f, pose + sway),
                        new Keyframe(.4f, pose), new Keyframe(.6f, pose - sway),
                        new Keyframe(LoopSeconds, pose) };
                AnimationUtility.SetEditorCurve(result, binding, new AnimationCurve(keys));
            }
            return result;
        }

        private static AnimationClip NewClip(string name, WrapMode wrap) =>
            new AnimationClip { name = name, frameRate = 60f, legacy = true, wrapMode = wrap };
    }
}

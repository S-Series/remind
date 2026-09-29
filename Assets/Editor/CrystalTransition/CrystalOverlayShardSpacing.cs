using System;
using REmind.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

namespace REmind.EditorTools
{
    /// <summary>Stores one varied radial arrangement across the prefab and every authored phase.</summary>
    public static class CrystalOverlayShardSpacing
    {
        private const string ShardRoot =
            "00_Elements_Envelope/04_Particles_And_Warp/01_Crystal_Shards";
        private const float VerticalCompression = .7f;
        private const float ReferenceTime = 1.2f;

        [MenuItem("REmind/Crystal Transition/Apply Varied Shard Distances")]
        public static void Apply()
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrystalOverlayBuilder.ClipPath);
            var intro = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrystalOverlayPhaseBuilder.IntroPath);
            var loop = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrystalOverlayPhaseBuilder.LoopPath);
            var outro = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrystalOverlayPhaseBuilder.OutroPath);
            if (!source || !intro || !loop || !outro)
                throw new InvalidOperationException("All four overlay clips are required.");

            var root = PrefabUtility.LoadPrefabContents(CrystalOverlayBuilder.PrefabPath);
            try
            {
                var player = root.GetComponent<CrystalTransitionPlayer>();
                var shards = root.transform.Find(ShardRoot);
                if (!player || !shards || shards.childCount != 18)
                    throw new InvalidOperationException("The overlay prefab must contain all 18 shards.");

                float[] targets = TargetRadii();
                AnimationClip[] clips = { source, intro, loop, outro };
                for (int i = 0; i < targets.Length; i++)
                {
                    string name = "Shard_" + (i + 1).ToString("00") + "_Motion";
                    string path = ShardRoot + "/" + name;
                    var x = PositionBinding(path, "x");
                    var y = PositionBinding(path, "y");
                    var sourceX = AnimationUtility.GetEditorCurve(source, x);
                    var sourceY = AnimationUtility.GetEditorCurve(source, y);
                    var rect = shards.Find(name) as RectTransform;
                    if (sourceX == null || sourceY == null || !rect)
                        throw new InvalidOperationException("Missing shard position: " + name);
                    float px = sourceX.Evaluate(ReferenceTime);
                    float py = sourceY.Evaluate(ReferenceTime) / VerticalCompression;
                    float currentRadius = Mathf.Sqrt(px * px + py * py);
                    if (currentRadius < 1f)
                        throw new InvalidOperationException("Invalid shard radius: " + name);
                    float factor = targets[i] / currentRadius;
                    foreach (var clip in clips)
                    {
                        ScaleCurve(clip, x, factor);
                        ScaleCurve(clip, y, factor);
                        EditorUtility.SetDirty(clip);
                    }
                    rect.anchoredPosition = rect.anchoredPosition * factor;
                }
                PrefabUtility.SaveAsPrefabAsset(root, CrystalOverlayBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Crystal overlay shard distances saved to the prefab and all four clips.");
        }

        private static EditorCurveBinding PositionBinding(string path, string axis) =>
            EditorCurveBinding.FloatCurve(path, typeof(RectTransform),
                "m_AnchoredPosition." + axis);

        private static void ScaleCurve(AnimationClip clip, EditorCurveBinding binding, float factor)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
                throw new InvalidOperationException("Missing curve: " + clip.name + "/" + binding.path);
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value *= factor;
                keys[i].inTangent *= factor;
                keys[i].outTangent *= factor;
            }
            curve.keys = keys;
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static float[] TargetRadii()
        {
            var random = new System.Random(290926);
            var radii = new float[18];
            for (int i = 0; i < radii.Length; i++)
                radii[i] = Mathf.Round(350f + 360f * i / (radii.Length - 1) +
                    random.Next(-9, 10));
            for (int i = radii.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (radii[i], radii[j]) = (radii[j], radii[i]);
            }
            return radii;
        }
    }
}

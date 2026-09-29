using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace REmind.EditorTools
{
    /// <summary>Removes sampled keys that do not contribute to the transition.</summary>
    public static class CrystalTransitionCurveCleanup
    {
        public static void Simplify()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CrystalTransitionBuilder.ClipPath);
            if (!clip) throw new InvalidOperationException("Crystal transition clip is missing.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalTransitionBuilder.PrefabPath);
            if (!prefab) throw new InvalidOperationException("Crystal transition prefab is missing.");

            var root = PrefabUtility.LoadPrefabContents(CrystalTransitionBuilder.PrefabPath);
            try
            {
                int removedCurves = 0;
                int beforeKeys = 0;
                int afterKeys = 0;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null) continue;
                    var keys = curve.keys;
                    beforeKeys += keys.Length;
                    if (IsConstant(keys) && BakeConstant(root.transform, binding, keys[0].value))
                    {
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                        removedCurves++;
                        continue;
                    }

                    var reduced = Reduce(keys, Tolerance(binding.propertyName));
                    afterKeys += reduced.length;
                    if (reduced.length != keys.Length)
                    {
                        reduced.preWrapMode = curve.preWrapMode;
                        reduced.postWrapMode = curve.postWrapMode;
                        AnimationUtility.SetEditorCurve(clip, binding, reduced);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, CrystalTransitionBuilder.PrefabPath);
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
                Debug.Log($"Crystal transition curves simplified: {removedCurves} constant tracks removed; " +
                          $"{beforeKeys} -> {afterKeys} keys in remaining tracks.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool IsConstant(Keyframe[] keys)
        {
            if (keys.Length == 0) return false;
            float value = keys[0].value;
            foreach (var key in keys)
                if (Mathf.Abs(key.value - value) > 0.00001f) return false;
            return true;
        }

        private static bool BakeConstant(Transform root, EditorCurveBinding binding, float value)
        {
            var target = string.IsNullOrEmpty(binding.path) ? root : root.Find(binding.path);
            if (!target) throw new InvalidOperationException("Animation target is missing: " + binding.path);
            if (binding.type == typeof(Transform) && binding.propertyName == "localEulerAnglesRaw.z")
            {
                var angles = target.localEulerAngles;
                target.localRotation = Quaternion.Euler(angles.x, angles.y, value);
                return true;
            }
            if (binding.type == typeof(Transform) && binding.propertyName == "m_LocalScale.y")
            {
                var scale = target.localScale;
                scale.y = value;
                target.localScale = scale;
                return true;
            }
            return false;
        }

        private static float Tolerance(string property)
        {
            if (property.StartsWith("m_AnchoredPosition.", StringComparison.Ordinal)) return 0.5f;
            if (property.StartsWith("localEulerAngles", StringComparison.Ordinal)) return 0.15f;
            return 0.005f;
        }

        private static AnimationCurve Reduce(Keyframe[] keys, float tolerance)
        {
            if (keys.Length <= 2) return new AnimationCurve(keys);
            var keep = new bool[keys.Length];
            keep[0] = keep[keys.Length - 1] = true;
            var segments = new Stack<Vector2Int>();
            segments.Push(new Vector2Int(0, keys.Length - 1));
            while (segments.Count > 0)
            {
                var segment = segments.Pop();
                int start = segment.x;
                int end = segment.y;
                float worst = tolerance;
                int worstIndex = -1;
                float span = keys[end].time - keys[start].time;
                for (int i = start + 1; i < end; i++)
                {
                    float t = span > 0f ? (keys[i].time - keys[start].time) / span : 0f;
                    float estimate = Mathf.LerpUnclamped(keys[start].value, keys[end].value, t);
                    float error = Mathf.Abs(estimate - keys[i].value);
                    if (error > worst)
                    {
                        worst = error;
                        worstIndex = i;
                    }
                }
                if (worstIndex < 0) continue;
                keep[worstIndex] = true;
                segments.Push(new Vector2Int(start, worstIndex));
                segments.Push(new Vector2Int(worstIndex, end));
            }

            var result = new List<Keyframe>();
            for (int i = 0; i < keys.Length; i++)
                if (keep[i]) result.Add(new Keyframe(keys[i].time, keys[i].value));
            var reduced = new AnimationCurve(result.ToArray());
            for (int i = 0; i < reduced.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(reduced, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(reduced, i, AnimationUtility.TangentMode.Linear);
            }
            return reduced;
        }
    }
}

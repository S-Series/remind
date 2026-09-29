using UnityEngine;
using UnityEngine.UI;

namespace REmind.Gameplay.Presentation
{
    /// <summary>An editable ellipse arc in canvas pixels. Front/back arcs share the same ellipse.</summary>
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class CrystalOrbitGraphic : MaskableGraphic
    {
        public CrystalOrbitGraphic() { useLegacyMeshGeneration = false; }
        public Vector2 radius = new Vector2(270f, 95f);
        public float startDegrees;
        public float sweepDegrees = 180f;
        [Range(0f, 1f)] public float reveal = 1f;
        [Min(0.1f)] public float lineWidth = 1.2f;
        [Range(16, 256)] public int segments = 120;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int count = Mathf.Max(2, Mathf.CeilToInt(segments * Mathf.Clamp01(reveal)));
            if (reveal <= 0f) return;
            for (int i = 0; i <= count; i++)
            {
                float angle = (startDegrees + sweepDegrees * reveal * i / count) * Mathf.Deg2Rad;
                Vector2 p = new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                Vector2 normal = new Vector2(Mathf.Cos(angle) / Mathf.Max(1f, radius.x),
                    Mathf.Sin(angle) / Mathf.Max(1f, radius.y)).normalized * lineWidth * 0.5f;
                vh.AddVert(p - normal, color, Vector2.zero);
                vh.AddVert(p + normal, color, Vector2.zero);
                if (i == 0) continue;
                int j = i * 2;
                vh.AddTriangle(j - 2, j - 1, j);
                vh.AddTriangle(j - 1, j + 1, j);
            }
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            SetVerticesDirty();
        }
    }
}

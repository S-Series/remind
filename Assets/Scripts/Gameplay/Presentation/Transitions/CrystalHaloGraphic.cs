using UnityEngine;
using UnityEngine.UI;

namespace REmind.Gameplay.Presentation
{
    /// <summary>Analytic radial light on a quad; assign the Radial Halo material. No bitmap or post processing.</summary>
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class CrystalHaloGraphic : MaskableGraphic
    {
        public CrystalHaloGraphic() { useLegacyMeshGeneration = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, new Vector2(0, 0));
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, new Vector2(0, 1));
            vh.AddVert(new Vector3(r.xMax, r.yMax), color, new Vector2(1, 1));
            vh.AddVert(new Vector3(r.xMax, r.yMin), color, new Vector2(1, 0));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
        }
    }
}

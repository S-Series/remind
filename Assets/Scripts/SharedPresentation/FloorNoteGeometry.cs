using UnityEngine;

namespace REmind.Presentation
{
    public static class FloorNoteGeometry
    {
        public static Bounds LocalBounds(GameObject obj)
        {
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>(true))
            {
                Bounds source = renderer.localBounds;
                Matrix4x4 matrix = obj.transform.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                            (corner & 4) == 0 ? -1 : 1)));
                    if (found) bounds.Encapsulate(point);
                    else { bounds = new Bounds(point, Vector3.zero); found = true; }
                }
            }
            return bounds;
        }

        public static void SetSegmentLength(GameObject obj, float length)
        {
            Bounds bounds = LocalBounds(obj);
            if (bounds.size.y <= 0f) return;
            Vector3 scale = obj.transform.localScale;
            scale.y = length / bounds.size.y;
            obj.transform.localScale = scale;
        }

        public static void EnsureClickArea(GameObject obj)
        {
            if (obj.GetComponentInChildren<Collider2D>(true)) return;
            Bounds bounds = LocalBounds(obj);
            var collider = obj.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(bounds.center.x, bounds.center.y);
            collider.size = new Vector2(Mathf.Max(1f, bounds.size.x), Mathf.Max(5f, bounds.size.y));
        }
    }
}

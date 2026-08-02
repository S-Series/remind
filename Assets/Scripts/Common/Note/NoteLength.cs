using UnityEngine;

public class NoteLength : MonoBehaviour
{
    [SerializeField] Transform bodyTransform;
    [SerializeField] Transform endCapTransform;

    private static readonly Vector2 normalScale = new Vector2(0.6000001f, 15.72f);

    

    public void SetLength(float height)
    {
        Vector2 prev = transform.position;
        endCapTransform.position = new Vector2(prev.x, height);

        float scaleY = normalScale.y * height / 160;
        bodyTransform.localScale = new Vector3(normalScale.x, scaleY, 1);
    }
}

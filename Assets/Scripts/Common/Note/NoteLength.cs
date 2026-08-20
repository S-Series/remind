using UnityEngine;

[DisallowMultipleComponent]
public sealed class NoteLength : MonoBehaviour
{
    [SerializeField] private Transform bodyTransform;

    private const float ReferenceLength = ChartHolder.WorldUnitsPerMeasure;
    private static readonly Vector2 NormalScale =
        new Vector2(1.23f, 64f);

    /// <summary>시작점에서 위쪽으로 표시할 로컬 Y 길이를 적용합니다.</summary>
    public void SetLength(float length)
    {
        if (!bodyTransform) return;

        float clampedLength = Mathf.Max(0f, length);
        float scaleY = NormalScale.y * clampedLength / ReferenceLength;
        bodyTransform.localScale = new Vector3(NormalScale.x, scaleY, 1f);
    }
}

using REmind.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScratchArrowPattern : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private SpriteRenderer patternRenderer;
    [SerializeField] private Sprite leftArrowSprite;
    [SerializeField] private Sprite rightArrowSprite;
    [SerializeField, Min(0f)] private float horizontalPadding = 1.2f;

    /// <summary>Scratch 방향과 이동 상태에 맞는 반복 화살표를 표시합니다.</summary>
    public void Configure(
        NoteHandleType side,
        ScratchMotionType motionType)
    {
        if (!patternRenderer)
        {
            return;
        }

        bool pointsRight = side == NoteHandleType.Right;

        if (motionType == ScratchMotionType.Release)
        {
            pointsRight = !pointsRight;
        }

        patternRenderer.sprite = pointsRight
            ? rightArrowSprite
            : leftArrowSprite;
        patternRenderer.enabled =
            motionType != ScratchMotionType.None &&
            patternRenderer.sprite;
        RefreshPatternSize();
    }

    /// <summary>가로 폭이 바뀐 렌더러에 화살표 반복 영역을 다시 맞춥니다.</summary>
    public void RefreshPatternSize()
    {
        if (!backgroundRenderer ||
            !backgroundRenderer.sprite ||
            !patternRenderer ||
            !patternRenderer.sprite)
        {
            return;
        }

        float backgroundWidth = backgroundRenderer.drawMode ==
            SpriteDrawMode.Simple
                ? backgroundRenderer.sprite.bounds.size.x
                : backgroundRenderer.size.x;
        float patternWidth = Mathf.Max(
            0f,
            backgroundWidth - horizontalPadding * 2f);
        patternRenderer.drawMode = SpriteDrawMode.Tiled;
        patternRenderer.size = new Vector2(
            patternWidth,
            patternRenderer.sprite.bounds.size.y);
    }

}

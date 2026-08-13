using REmind.Data;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChartNoteSelectable : MonoBehaviour
{
    [SerializeField] private Color selectedTint =
        new Color(1f, 0.72f, 0.12f, 1f);
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.7f;

    private SpriteRenderer[] spriteRenderers;
    private Color[] normalColors;
    private GameObject[] linkedNoteObjects;

    public bool IsSelected { get; private set; }
    public NoteType NoteType { get; private set; }
    internal GameObject[] LinkedNoteObjects => linkedNoteObjects;

    private void Awake()
    {
        CacheRendererColors();
    }

    /// <summary>같은 채보 노트를 표현하는 중앙·손 필드 복제본을 연결합니다.</summary>
    internal void Configure(
        NoteType noteType,
        GameObject[] noteObjects)
    {
        NoteType = noteType;
        linkedNoteObjects = noteObjects;
    }

    /// <summary>원래 색상을 보존하면서 선택 강조 표시를 전환합니다.</summary>
    internal void SetSelected(bool selected)
    {
        if (spriteRenderers == null || normalColors == null)
        {
            CacheRendererColors();
        }

        IsSelected = selected;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (!spriteRenderer)
            {
                continue;
            }

            Color color = selected
                ? Color.Lerp(normalColors[i], selectedTint, tintStrength)
                : normalColors[i];
            color.a = normalColors[i].a;
            spriteRenderer.color = color;
        }
    }

    private void CacheRendererColors()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        normalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            normalColors[i] = spriteRenderers[i].color;
        }
    }
}

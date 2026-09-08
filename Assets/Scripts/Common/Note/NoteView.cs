using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NoteView : MonoBehaviour
{
    [SerializeField] private Collider2D[] clickColliders;

    private Vector3 baseLocalScale;
    private NoteLength noteLength;
    private bool displayScaleInitialized;

    private void Awake()
    {
        if (clickColliders == null || clickColliders.Length == 0)
        {
            RefreshClickColliders();
        }
    }

    /// <summary>클릭으로 검출된 Collider가 이 노트에 등록된 것인지 확인합니다.</summary>
    public bool ContainsClickCollider(Collider2D target)
    {
        return TryGetClickPriority(target, out _);
    }

    /// <summary>배열 앞쪽 Collider일수록 높은 클릭 우선순위를 반환합니다.</summary>
    public bool TryGetClickPriority(Collider2D target, out int priority)
    {
        priority = -1;

        if (!target || clickColliders == null)
        {
            return false;
        }

        for (int i = 0; i < clickColliders.Length; i++)
        {
            if (clickColliders[i] == target)
            {
                priority = clickColliders.Length - i;
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Refresh Click Colliders")]
    public void RefreshClickColliders()
    {
        clickColliders = GetComponentsInChildren<Collider2D>(true);
    }

    /// <summary>
    /// 부모의 세로 간격이 확대되어도 노트 머리의 화면 크기는 유지합니다.
    /// Long Note의 리본 길이는 NoteLength에서 별도로 복원합니다.
    /// </summary>
    public void SetVerticalDisplayScale(float displayScale)
    {
        if (float.IsNaN(displayScale) ||
            float.IsInfinity(displayScale) ||
            displayScale <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayScale),
                displayScale,
                "Display scale must be finite and greater than zero.");
        }

        if (!displayScaleInitialized)
        {
            baseLocalScale = transform.localScale;
            noteLength = GetComponent<NoteLength>();
            displayScaleInitialized = true;
        }

        Vector3 compensatedScale = baseLocalScale;
        compensatedScale.y /= displayScale;
        transform.localScale = compensatedScale;
        noteLength?.SetVerticalDisplayScale(displayScale);
    }

    private void Reset()
    {
        RefreshClickColliders();
    }
}

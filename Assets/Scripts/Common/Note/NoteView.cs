using UnityEngine;

[DisallowMultipleComponent]
public sealed class NoteView : MonoBehaviour
{
    [SerializeField] private BoxCollider2D[] clickColliders;

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
    private void RefreshClickColliders()
    {
        clickColliders = GetComponentsInChildren<BoxCollider2D>(true);
    }

    private void Reset()
    {
        RefreshClickColliders();
    }
}

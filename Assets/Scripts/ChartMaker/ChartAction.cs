using System;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class ChartAction : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler
{
    private enum PositionCorrectionMode
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    [SerializeField] private Camera inputCamera;
    [SerializeField] private ChartScroll chartScroll;
    [SerializeField] private PositionCorrectionMode positionCorrectionMode;

    private BoxCollider2D chartCollider;

    public Vector2 LastNormalizedPosition { get; private set; }
    public bool IsHovered { get; private set; }
    public bool? PositionCorrection => positionCorrectionMode switch
    {
        PositionCorrectionMode.Left => false,
        PositionCorrectionMode.Right => true,
        _ => null
    };

    public event Action PointerEntered;
    public event Action PointerExited;
    public event Action<Vector2, bool?> NormalizedPositionChanged;
    public event Action<Vector2, bool?> PositionClicked;
    public event Action<Vector2, bool?> DragStarted;
    public event Action<Vector2, bool?> PositionDragged;
    public event Action<Vector2, bool?> DragEnded;

    private void Awake()
    {
        chartCollider = GetComponent<BoxCollider2D>();

        if (!inputCamera)
        {
            inputCamera = Camera.main;
        }

        if (!chartScroll)
        {
            chartScroll = FindFirstObjectByType<ChartScroll>();
        }

        if (!inputCamera)
        {
            Debug.LogError("ChartAction requires an input camera.", this);
            enabled = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!TryUpdatePosition(eventData.position))
        {
            return;
        }

        IsHovered = true;
        NotifyPositionChanged();
        PointerEntered?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovered = false;
        PointerExited?.Invoke();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!IsHovered || !TryUpdatePosition(eventData.position))
        {
            return;
        }

        NotifyPositionChanged();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        TryReadPosition(eventData.position);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            !TryUpdatePosition(eventData.position, true))
        {
            return;
        }

        DragStarted?.Invoke(LastNormalizedPosition, PositionCorrection);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            !TryUpdatePosition(eventData.position, false))
        {
            return;
        }

        PositionDragged?.Invoke(LastNormalizedPosition, PositionCorrection);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        TryUpdatePosition(eventData.position, false);
        DragEnded?.Invoke(LastNormalizedPosition, PositionCorrection);
    }

    public void OnScroll(PointerEventData eventData)
    {
        chartScroll?.RequestScroll(eventData.scrollDelta);
        eventData.Use();
    }

    private void OnDisable()
    {
        if (!IsHovered)
        {
            return;
        }

        IsHovered = false;
        PointerExited?.Invoke();
    }

    /// <summary>
    /// 화면 좌표를 이 입력 영역 내부의 0~1 좌표로 변환하고 클릭 이벤트를 보냅니다.
    /// </summary>
    public bool TryReadPosition(Vector2 screenPosition)
    {
        if (!TryUpdatePosition(screenPosition))
        {
            return false;
        }

        PositionClicked?.Invoke(
            LastNormalizedPosition,
            PositionCorrection);
        return true;
    }

    private void NotifyPositionChanged()
    {
        NormalizedPositionChanged?.Invoke(
            LastNormalizedPosition,
            PositionCorrection);
    }

    private bool TryUpdatePosition(
        Vector2 screenPosition,
        bool requireColliderOverlap = true)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        Plane colliderPlane = new Plane(transform.forward, transform.position);

        if (!colliderPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        Vector3 worldPosition = ray.GetPoint(distance);
        if (requireColliderOverlap &&
            !chartCollider.OverlapPoint(worldPosition))
        {
            return false;
        }

        // 월드 좌표를 콜라이더 중심 기준으로 바꾼 뒤 각 축을 0~1로 정규화합니다.
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        Vector2 colliderPosition = (Vector2)localPosition - chartCollider.offset;
        Vector2 halfSize = chartCollider.size * 0.5f;

        LastNormalizedPosition = new Vector2(
            Mathf.InverseLerp(-halfSize.x, halfSize.x, colliderPosition.x),
            Mathf.InverseLerp(-halfSize.y, halfSize.y, colliderPosition.y));
        return true;
    }
}

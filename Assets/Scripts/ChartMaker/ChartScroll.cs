using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ChartScroll : MonoBehaviour
{
    private const float BoundsEpsilon = 0.001f;

    [SerializeField] private RectTransform scrollTrans;
    [SerializeField, Min(1f)] private float scrollPower = 40f;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.08f;
    [SerializeField, Min(0.0001f)] private float snapThreshold = 0.01f;

    [Header("Camera Scrolling")]
    [SerializeField] private Transform scrollCameraTransform;
    [SerializeField] private Transform previewCameraTransform;
    [SerializeField] private RectTransform[] cameraFollowRects =
        Array.Empty<RectTransform>();

    private ScrollRect scrollRect;
    private EventTrigger eventTrigger;
    private EventTrigger.Entry scrollEntry;
    private EventTrigger.Entry beginDragEntry;
    private EventTrigger.Entry endDragEntry;
    private Vector2 targetPosition;
    private Vector2 smoothVelocity;
    private bool isDragging;
    private bool ignoreNextScrollRectCallback;
    private bool ownsEventTrigger;
    private bool externalTimelineControl;
    private Vector3 cameraBasePosition;
    private Vector3 previewCameraBasePosition;
    private Vector2 scrollViewportBasePosition;
    private Vector2[] cameraFollowBasePositions = Array.Empty<Vector2>();
    private GuideGenerate guideGenerate;
    private bool cameraScrollingReady;

    public event Action<float> ScrollYChanged;
    public event Action<Vector2> ScrollPositionChanged;

    public Vector2 ScrollPosition => scrollRect != null
        ? scrollRect.content.anchoredPosition
        : Vector2.zero;
    public float ScrollY => ScrollPosition.y;
    public float CameraY => scrollCameraTransform
        ? scrollCameraTransform.position.y
        : 0f;

    private void Awake()
    {
        if (!scrollTrans || !scrollTrans.TryGetComponent(out scrollRect))
        {
            Debug.LogError("ChartScroll requires a ScrollRect on Scroll Field.", this);
            enabled = false;
            return;
        }

        ApplySettings();
        ConfigurePointerEvents();
        targetPosition = ScrollPosition;
        InitializeCameraScrolling();
        ApplyCameraScrolling(ScrollY);
    }

    private void OnEnable()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(HandleScrollChanged);
            targetPosition = ScrollPosition;
        }
    }

    private void OnDisable()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(HandleScrollChanged);
        }
    }

    private void Update()
    {
        if (scrollRect == null || isDragging || externalTimelineControl)
        {
            return;
        }

        Vector2 currentPosition = ScrollPosition;
        float thresholdSquared = snapThreshold * snapThreshold;

        if ((currentPosition - targetPosition).sqrMagnitude <= thresholdSquared)
        {
            smoothVelocity = Vector2.zero;

            if (currentPosition != targetPosition)
            {
                SetScrollPosition(targetPosition);
            }

            return;
        }

        Vector2 nextPosition = Vector2.SmoothDamp(
            currentPosition,
            targetPosition,
            ref smoothVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        SetScrollPosition(nextPosition);
    }

    private void OnDestroy()
    {
        RemovePointerEvents();
    }

    private void ApplySettings()
    {
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0f;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        // Wheel input is accumulated by ApplyScrollDelta for smooth movement.
        // Disable ScrollRect's immediate wheel movement to avoid applying it twice.
        scrollRect.scrollSensitivity = 0f;
    }

    /// <summary>스크롤 목표 Y를 설정하고 필요하면 현재 위치에 즉시 반영합니다.</summary>
    public void SetScrollY(float positionY, bool smooth = false)
    {
        if (scrollRect == null)
        {
            return;
        }

        Vector2 requestedPosition = targetPosition;
        requestedPosition.x = ScrollPosition.x;
        requestedPosition.y = positionY;
        targetPosition = ClampContentPosition(requestedPosition);

        if (smooth)
        {
            return;
        }

        smoothVelocity = Vector2.zero;
        SetScrollPosition(targetPosition);
    }

    /// <summary>
    /// 테스트 재생이 타임라인을 제어하는 동안 스크롤 입력과 가이드 기준 갱신을 잠급니다.
    /// </summary>
    public void SetExternalTimelineControl(bool active)
    {
        if (externalTimelineControl == active)
        {
            return;
        }

        externalTimelineControl = active;

        if (scrollRect == null)
        {
            return;
        }

        scrollRect.StopMovement();
        scrollRect.enabled = !active;
        isDragging = false;
        ignoreNextScrollRectCallback = false;
        smoothVelocity = Vector2.zero;
        targetPosition = ScrollPosition;

        if (!active)
        {
            GuideGenerate.SetReferenceFromScrollY(ScrollY);
        }
    }

    /// <summary>테스트 타임라인의 채보 Y를 일반 스크롤과 같은 카메라 좌표로 적용합니다.</summary>
    public void SetExternalChartY(float chartY)
    {
        if (!externalTimelineControl ||
            float.IsNaN(chartY) ||
            float.IsInfinity(chartY))
        {
            return;
        }

        GuideGenerate guideGenerate = GuideGenerate.Instance;

        if (!guideGenerate ||
            guideGenerate.ScrollToChartRatio <= Mathf.Epsilon)
        {
            return;
        }

        Vector2 externalPosition = ScrollPosition;
        externalPosition.y = -chartY / guideGenerate.ScrollToChartRatio;
        targetPosition = externalPosition;
        smoothVelocity = Vector2.zero;
        SetScrollPosition(externalPosition);
        GuideGenerate.SetReferenceY(chartY);
    }

    /// <summary>현재 스크롤 위치를 가장 가까운 마디선으로 부드럽게 이동합니다.</summary>
    public void ClampToNearestMeasure()
    {
        GuideGenerate guideGenerate = GuideGenerate.Instance;

        if (scrollRect == null || !guideGenerate)
        {
            Debug.LogWarning(
                "ChartScroll requires GuideGenerate to clamp to a measure.",
                this);
            return;
        }

        float scrollToChartRatio = guideGenerate.ScrollToChartRatio;

        if (scrollToChartRatio <= Mathf.Epsilon)
        {
            return;
        }

        float chartPositionY = -ScrollY * scrollToChartRatio;
        float nearestMeasureY = Mathf.Round(
            chartPositionY / guideGenerate.MeasureHeight) *
            guideGenerate.MeasureHeight;
        float targetScrollY = -nearestMeasureY / scrollToChartRatio;

        scrollRect.StopMovement();
        smoothVelocity = Vector2.zero;
        SetScrollY(targetScrollY, smooth: true);
    }

    /// <summary>외부 입력 영역에서 받은 휠 이동량을 현재 스크롤 목표에 더합니다.</summary>
    public void RequestScroll(Vector2 scrollDelta)
    {
        ApplyScrollDelta(scrollDelta);
    }

    private void HandleScrollChanged(Vector2 _)
    {
        if (ignoreNextScrollRectCallback)
        {
            ignoreNextScrollRectCallback = false;
            return;
        }

        targetPosition = ScrollPosition;
        smoothVelocity = Vector2.zero;
        NotifyScrollPositionChanged();
    }

    private void ConfigurePointerEvents()
    {
        eventTrigger = scrollTrans.GetComponent<EventTrigger>();

        if (!eventTrigger)
        {
            eventTrigger = scrollTrans.gameObject.AddComponent<EventTrigger>();
            ownsEventTrigger = true;
        }

        eventTrigger.triggers ??= new List<EventTrigger.Entry>();
        scrollEntry = AddPointerEvent(EventTriggerType.Scroll, HandlePointerScroll);
        beginDragEntry = AddPointerEvent(
            EventTriggerType.BeginDrag,
            HandleBeginDrag);
        endDragEntry = AddPointerEvent(EventTriggerType.EndDrag, HandleEndDrag);
    }

    private EventTrigger.Entry AddPointerEvent(
        EventTriggerType eventType,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(callback);
        eventTrigger.triggers.Add(entry);
        return entry;
    }

    private void RemovePointerEvents()
    {
        if (!eventTrigger)
        {
            return;
        }

        if (ownsEventTrigger)
        {
            Destroy(eventTrigger);
            return;
        }

        eventTrigger.triggers.Remove(scrollEntry);
        eventTrigger.triggers.Remove(beginDragEntry);
        eventTrigger.triggers.Remove(endDragEntry);
    }

    private void HandlePointerScroll(BaseEventData eventData)
    {
        if (!isActiveAndEnabled || eventData is not PointerEventData pointerEvent)
        {
            return;
        }

        ApplyScrollDelta(pointerEvent.scrollDelta);
        pointerEvent.Use();
    }

    private void ApplyScrollDelta(Vector2 scrollDelta)
    {
        if (!isActiveAndEnabled ||
            scrollRect == null ||
            externalTimelineControl)
        {
            return;
        }

        Vector2 delta = scrollDelta;
        delta.y *= -1f;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            delta.y = delta.x;
        }

        delta.x = 0f;
        targetPosition = ClampContentPosition(
            targetPosition + delta * scrollPower);
    }

    private void HandleBeginDrag(BaseEventData _)
    {
        if (!isActiveAndEnabled || externalTimelineControl)
        {
            return;
        }

        isDragging = true;
        targetPosition = ScrollPosition;
        smoothVelocity = Vector2.zero;
    }

    private void HandleEndDrag(BaseEventData _)
    {
        if (!isActiveAndEnabled || externalTimelineControl)
        {
            return;
        }

        isDragging = false;
        targetPosition = ScrollPosition;
        smoothVelocity = Vector2.zero;
    }

    private Vector2 ClampContentPosition(Vector2 requestedPosition)
    {
        // ScrollRect의 내부 Bounds 계산과 같은 좌표계를 사용해 콘텐츠가
        // 뷰포트 밖으로 완전히 빠져나가지 않도록 목표 위치를 제한합니다.
        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport
            ? scrollRect.viewport
            : scrollTrans;
        Vector2 originalPosition = content.anchoredPosition;
        requestedPosition.x = originalPosition.x;
        content.anchoredPosition = requestedPosition;

        Bounds viewBounds = new Bounds(viewport.rect.center, viewport.rect.size);
        Bounds contentBounds =
            RectTransformUtility.CalculateRelativeRectTransformBounds(
                viewport,
                content);
        float minOffset = viewBounds.min.y - contentBounds.min.y;
        float maxOffset = viewBounds.max.y - contentBounds.max.y;

        if (minOffset < -BoundsEpsilon)
        {
            requestedPosition.y += minOffset;
        }
        else if (maxOffset > BoundsEpsilon)
        {
            requestedPosition.y += maxOffset;
        }

        content.anchoredPosition = originalPosition;
        return requestedPosition;
    }

    private void SetScrollPosition(Vector2 position)
    {
        if (ScrollPosition == position)
        {
            return;
        }

        ignoreNextScrollRectCallback = true;
        scrollRect.content.anchoredPosition = position;
        NotifyScrollPositionChanged();
    }

    private void NotifyScrollPositionChanged()
    {
        Vector2 position = ScrollPosition;
        ApplyCameraScrolling(position.y);

        if (!externalTimelineControl)
        {
            GuideGenerate.SetReferenceFromScrollY(position.y);
        }

        ScrollPositionChanged?.Invoke(position);
        ScrollYChanged?.Invoke(position.y);
    }

    private void InitializeCameraScrolling()
    {
        if (!scrollCameraTransform && Camera.main)
        {
            scrollCameraTransform = Camera.main.transform;
        }

        guideGenerate = GuideGenerate.Instance;

        if (!guideGenerate)
        {
            guideGenerate = FindFirstObjectByType<GuideGenerate>();
        }

        if (!scrollCameraTransform || !scrollTrans.parent)
        {
            Debug.LogWarning(
                "ChartScroll camera scrolling requires a camera transform " +
                "and a parent for Scroll Field.",
                this);
            return;
        }

        float initialViewportOffsetY = -ScrollY;
        Vector3 initialWorldOffset =
            GetCameraWorldOffset(initialViewportOffsetY);
        cameraBasePosition =
            scrollCameraTransform.position - initialWorldOffset;

        if (previewCameraTransform && guideGenerate)
        {
            previewCameraBasePosition =
                previewCameraTransform.position -
                Vector3.forward * GetPreviewCameraZOffset(
                    initialViewportOffsetY);
        }
        else if (previewCameraTransform)
        {
            Debug.LogWarning(
                "Preview Camera scrolling requires GuideGenerate.",
                this);
        }

        scrollViewportBasePosition =
            scrollTrans.anchoredPosition -
            Vector2.up * initialViewportOffsetY;
        cameraFollowBasePositions =
            new Vector2[cameraFollowRects.Length];

        for (int i = 0; i < cameraFollowRects.Length; i++)
        {
            RectTransform followRect = cameraFollowRects[i];

            if (followRect)
            {
                cameraFollowBasePositions[i] =
                    followRect.anchoredPosition -
                    Vector2.up * initialViewportOffsetY;
            }
        }

        cameraScrollingReady = true;
    }

    private void ApplyCameraScrolling(float scrollY)
    {
        if (!cameraScrollingReady)
        {
            return;
        }

        float viewportOffsetY = -scrollY;
        // Content의 로컬 스크롤을 뷰포트 이동으로 상쇄해 채보는 월드에 고정합니다.
        scrollTrans.anchoredPosition =
            scrollViewportBasePosition + Vector2.up * viewportOffsetY;

        for (int i = 0; i < cameraFollowRects.Length; i++)
        {
            RectTransform followRect = cameraFollowRects[i];

            if (followRect)
            {
                followRect.anchoredPosition =
                    cameraFollowBasePositions[i] +
                    Vector2.up * viewportOffsetY;
            }
        }

        scrollCameraTransform.position =
            cameraBasePosition + GetCameraWorldOffset(viewportOffsetY);

        if (previewCameraTransform && guideGenerate)
        {
            previewCameraTransform.position =
                previewCameraBasePosition +
                Vector3.forward * GetPreviewCameraZOffset(viewportOffsetY);
        }
    }

    private Vector3 GetCameraWorldOffset(float viewportOffsetY)
    {
        return scrollTrans.parent.TransformVector(
            Vector3.up * viewportOffsetY);
    }

    private float GetPreviewCameraZOffset(float viewportOffsetY)
    {
        return viewportOffsetY * guideGenerate.ScrollToChartRatio;
    }
}

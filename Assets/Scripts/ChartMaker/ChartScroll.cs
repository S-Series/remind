using System;
using System.Collections.Generic;
using REmind.Charting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ChartScroll : MonoBehaviour
{
    private const float BoundsEpsilon = 0.001f;

    /// <summary>
    /// Preview Camera 회전의 기준 X입니다. Camera Note 컴파일/표시 계층이 이 값을
    /// 갱신하며, 양수 X는 음수 Z 회전으로 변환됩니다.
    /// </summary>
    public static float PreviewCameraRotationReferenceX;
    public static float PreviewCameraSpinDegrees;
    public static float PreviewCameraScratchTiltDegrees;

    [SerializeField] private RectTransform scrollTrans;
    [SerializeField, Min(1f)] private float scrollPower = 40f;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.08f;
    [SerializeField, Min(0.0001f)] private float snapThreshold = 0.01f;

    [Header("Camera Scrolling")]
    [SerializeField] private Transform scrollCameraTransform;
    [SerializeField] private Transform previewCameraTransform;
    [SerializeField] private RectTransform[] cameraFollowRects =
        Array.Empty<RectTransform>();

    [Header("Preview Camera Line Following")]
    [SerializeField] private ChartPreviewFloorRenderer previewFloorRenderer;
    [SerializeField]
    private bool previewCameraUseOnlyPoweredKickX;
    [SerializeField] private float previewCameraTiltDegreesPerXUnit = 0.6f;
    [SerializeField, Min(0f)]
    private float previewCameraSpinCounterMoveX = 3.5f;
    [SerializeField, Min(0f)]
    private float previewCameraReferenceTiltCounterMoveX = 2f;
    [SerializeField, Min(0f)]
    private float previewCameraPoweredKickMaxX = 7.5f;
    [SerializeField, Min(0f)]
    private float previewCameraPoweredYawMaxDegrees = 2.5f;

    [Header("Vertical Zoom")]
    [SerializeField, Min(0.1f)] private float minimumVerticalZoom = 0.5f;
    [SerializeField, Min(0.1f)] private float maximumVerticalZoom = 4f;
    [SerializeField, Min(1.01f)] private float verticalZoomMultiplier = 1.1f;

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
    private Vector3 previewCameraBaseLocalPosition;
    private Quaternion previewCameraBaseLocalRotation;
    private Vector2 scrollViewportBasePosition;
    private Vector2[] cameraFollowBasePositions = Array.Empty<Vector2>();
    private GuideGenerate guideGenerate;
    private bool cameraScrollingReady;
    private float previewHighSpeedScale = 1f;
    private Vector3 previewCameraLineLocalOffset;
    private float previewCameraPoweredKickLocalX;
    private float previewCameraPoweredYawDegrees;
    private Vector2 effectCameraOffset;
    private float effectCameraRollDegrees;

    public void SetEffectCameraOffset(Vector2 offset, float rollDegrees)
    {
        effectCameraOffset = offset;
        effectCameraRollDegrees = rollDegrees;
        if (cameraScrollingReady && previewCameraTransform && guideGenerate)
            ApplyPreviewCameraPosition(-ScrollY);
    }
    private Vector3 contentBaseScale;
    private float verticalZoom = 1f;

    public event Action<float> ScrollYChanged;
    public event Action<Vector2> ScrollPositionChanged;

    public Vector2 ScrollPosition => scrollRect != null
        ? scrollRect.content.anchoredPosition
        : Vector2.zero;
    public float ScrollY => ScrollPosition.y;
    public float CameraY => scrollCameraTransform
        ? scrollCameraTransform.position.y
        : 0f;
    public float PreviewHighSpeedScale => previewHighSpeedScale;
    public float VerticalZoom => verticalZoom;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        PreviewCameraRotationReferenceX = 0f;
        PreviewCameraSpinDegrees = 0f;
        PreviewCameraScratchTiltDegrees = 0f;
    }

    private void Awake()
    {
        if (!scrollTrans || !scrollTrans.TryGetComponent(out scrollRect))
        {
            Debug.LogError("ChartScroll requires a ScrollRect on Scroll Field.", this);
            enabled = false;
            return;
        }

        minimumVerticalZoom = Mathf.Max(0.1f, minimumVerticalZoom);
        maximumVerticalZoom = Mathf.Max(
            minimumVerticalZoom,
            maximumVerticalZoom);
        verticalZoomMultiplier = Mathf.Max(1.01f, verticalZoomMultiplier);
        contentBaseScale = scrollRect.content.localScale;
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

    private void LateUpdate()
    {
        UpdatePreviewCameraLineFollowing();
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

    /// <summary>
    /// Preview 노트 간격과 같은 배율로 Preview Camera의 타임라인 이동량을 조절합니다.
    /// </summary>
    public void SetPreviewHighSpeedScale(float scale)
    {
        if (float.IsNaN(scale) ||
            float.IsInfinity(scale) ||
            scale <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                scale,
                "Preview high-speed scale must be finite and greater than zero.");
        }

        if (Mathf.Approximately(previewHighSpeedScale, scale))
        {
            return;
        }

        previewHighSpeedScale = scale;
        previewFloorRenderer?.SetDisplayScale(scale);

        if (cameraScrollingReady)
        {
            ApplyCameraScrolling(ScrollY);
        }
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

    /// <summary>
    /// 외부 입력 영역에서 받은 휠을 스크롤 또는 Ctrl 세로 확대 입력으로 처리합니다.
    /// </summary>
    public void RequestScroll(Vector2 scrollDelta)
    {
        RequestScroll(scrollDelta, 0.5f);
    }

    /// <summary>포인터 위치를 기준으로 휠 입력을 처리합니다.</summary>
    public void RequestPointerScroll(PointerEventData pointerEvent)
    {
        if (pointerEvent == null)
        {
            return;
        }

        Camera eventCamera = pointerEvent.enterEventCamera
            ? pointerEvent.enterEventCamera
            : pointerEvent.pressEventCamera;
        float anchorY = GetViewportNormalizedY(
            pointerEvent.position,
            eventCamera);
        RequestScroll(pointerEvent.scrollDelta, anchorY);
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

        RequestPointerScroll(pointerEvent);
        pointerEvent.Use();
    }

    private void RequestScroll(Vector2 scrollDelta, float anchorY)
    {
        if (Keyboard.current?.ctrlKey.isPressed == true)
        {
            ApplyVerticalZoom(scrollDelta, anchorY);
            return;
        }

        ApplyScrollDelta(scrollDelta);
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

    private void ApplyVerticalZoom(Vector2 scrollDelta, float anchorY)
    {
        if (!isActiveAndEnabled ||
            scrollRect == null ||
            externalTimelineControl)
        {
            return;
        }

        float wheelDelta = Mathf.Abs(scrollDelta.y) >= Mathf.Abs(scrollDelta.x)
            ? scrollDelta.y
            : scrollDelta.x;

        if (Mathf.Approximately(wheelDelta, 0f))
        {
            return;
        }

        float zoomFactor = wheelDelta > 0f
            ? verticalZoomMultiplier
            : 1f / verticalZoomMultiplier;
        float requestedZoom = Mathf.Clamp(
            verticalZoom * zoomFactor,
            minimumVerticalZoom,
            maximumVerticalZoom);

        if (Mathf.Approximately(requestedZoom, verticalZoom))
        {
            return;
        }

        anchorY = Mathf.Clamp01(anchorY);
        float oldZoom = verticalZoom;
        float oldReferenceY = guideGenerate
            ? -ScrollY * guideGenerate.ScrollToChartRatio
            : 0f;
        float baseVisibleChartHeight = GetBaseVisibleChartHeight();
        float anchoredChartY = oldReferenceY +
            baseVisibleChartHeight * anchorY / oldZoom;

        verticalZoom = requestedZoom;
        Vector3 contentScale = contentBaseScale;
        contentScale.y *= verticalZoom;
        scrollRect.content.localScale = contentScale;

        if (guideGenerate)
        {
            guideGenerate.SetVerticalDisplayScale(verticalZoom);
        }

        float newReferenceY = anchoredChartY -
            baseVisibleChartHeight * anchorY / verticalZoom;
        Vector2 requestedPosition = ScrollPosition;

        if (guideGenerate &&
            guideGenerate.ScrollToChartRatio > Mathf.Epsilon)
        {
            requestedPosition.y =
                -newReferenceY / guideGenerate.ScrollToChartRatio;
        }

        scrollRect.StopMovement();
        smoothVelocity = Vector2.zero;
        targetPosition = ClampContentPosition(requestedPosition);

        if (ScrollPosition == targetPosition)
        {
            NotifyScrollPositionChanged();
        }
        else
        {
            SetScrollPosition(targetPosition);
        }
    }

    private float GetViewportNormalizedY(
        Vector2 screenPosition,
        Camera eventCamera)
    {
        RectTransform viewport = scrollRect.viewport
            ? scrollRect.viewport
            : scrollTrans;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                screenPosition,
                eventCamera,
                out Vector2 localPosition))
        {
            return 0.5f;
        }

        return Mathf.InverseLerp(
            viewport.rect.yMin,
            viewport.rect.yMax,
            localPosition.y);
    }

    private float GetBaseVisibleChartHeight()
    {
        RectTransform viewport = scrollRect.viewport
            ? scrollRect.viewport
            : scrollTrans;
        float baseScaleY = Mathf.Abs(contentBaseScale.y);

        return baseScaleY > Mathf.Epsilon
            ? viewport.rect.height / baseScaleY
            : ChartHolder.WorldUnitsPerMeasure;
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

        if (!previewFloorRenderer)
        {
            previewFloorRenderer =
                FindFirstObjectByType<ChartPreviewFloorRenderer>();
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
            // Preview 루트가 화면 배치를 소유하고 Camera는 로컬 원점에서
            // 움직입니다. 월드 위치를 기준값으로 저장하면 부모의 화면 배치값이
            // 카메라 이동 계산에 섞이므로 로컬 좌표만 사용합니다.
            previewCameraBaseLocalPosition =
                previewCameraTransform.localPosition -
                Vector3.forward * GetPreviewCameraZOffset(
                    initialViewportOffsetY);
            previewCameraBaseLocalRotation =
                previewCameraTransform.localRotation;
            previewCameraLineLocalOffset = Vector3.zero;
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
            ApplyPreviewCameraPosition(viewportOffsetY);
        }
    }

    private void UpdatePreviewCameraLineFollowing()
    {
        if (!cameraScrollingReady ||
            !previewCameraTransform ||
            !guideGenerate ||
            !previewFloorRenderer)
        {
            return;
        }

        float chartY = -ScrollY * guideGenerate.ScrollToChartRatio;

        if (!externalTimelineControl)
        {
            UpdateEditorPreviewCameraMotion(chartY);
        }

        if (previewCameraUseOnlyPoweredKickX)
        {
            previewCameraLineLocalOffset = Vector3.zero;
        }
        else
        {
            Vector3 worldLineOffset =
                previewFloorRenderer.EvaluateWorldCenterOffset(chartY);
            previewCameraLineLocalOffset = previewCameraTransform.parent
                ? previewCameraTransform.parent.InverseTransformVector(
                    worldLineOffset)
                : worldLineOffset;
        }

        UpdatePreviewCameraPoweredKickX();
        ApplyPreviewCameraPosition(-ScrollY);
    }

    private void UpdateEditorPreviewCameraMotion(float chartY)
    {
        if (!previewFloorRenderer.TryEvaluateCameraMotionAtChartY(
                chartY,
                out CameraMotionState cameraState,
                out float scratchTiltDegrees))
        {
            PreviewCameraRotationReferenceX = 0f;
            PreviewCameraSpinDegrees = 0f;
            PreviewCameraScratchTiltDegrees = 0f;
            return;
        }

        PreviewCameraRotationReferenceX =
            previewFloorRenderer.EvaluateCameraReferenceX(cameraState);
        PreviewCameraSpinDegrees = (float)cameraState.SpinDegrees;
        PreviewCameraScratchTiltDegrees = scratchTiltDegrees;
    }

    private void ApplyPreviewCameraPosition(float viewportOffsetY)
    {
        Vector3 nonPoweredHorizontalOffset =
            previewCameraUseOnlyPoweredKickX
                ? Vector3.zero
                : previewCameraLineLocalOffset +
                  Vector3.right * GetPreviewCameraRotationCounterMoveX();

        // 확인 모드에서는 Line 및 다른 회전의 X 보정을 제외합니다.
        previewCameraTransform.localPosition =
            previewCameraBaseLocalPosition +
            Vector3.forward * GetPreviewCameraZOffset(viewportOffsetY) +
            nonPoweredHorizontalOffset;

        // Powered Scratch의 카메라 킥은 Line 추적에 섞이지 않도록 마지막에
        // 독립 변수로 더합니다.
        Vector3 kickedLocalPosition = previewCameraTransform.localPosition;
        kickedLocalPosition.x += previewCameraPoweredKickLocalX;
        kickedLocalPosition += new Vector3(effectCameraOffset.x, effectCameraOffset.y, 0f);
        previewCameraTransform.localPosition = kickedLocalPosition;

        float tiltDegrees =
            -PreviewCameraRotationReferenceX *
            previewCameraTiltDegreesPerXUnit +
            PreviewCameraSpinDegrees +
            PreviewCameraScratchTiltDegrees + effectCameraRollDegrees;
        previewCameraTransform.localRotation =
            previewCameraBaseLocalRotation *
            Quaternion.Euler(
                0f,
                previewCameraPoweredYawDegrees,
                tiltDegrees);
    }

    /// <summary>
    /// Left 회전은 Right(+X), Right 회전은 Left(-X)로 이동시켜 회전감을
    /// 강조합니다. 360도 Spin은 시작과 끝에서 0, 중간에서 최대가 됩니다.
    /// Camera Note 기준 기울기를 사용합니다. Powered Scratch의 X 이동은 Line
    /// 추적 뒤에 별도 변수로 적용합니다.
    /// </summary>
    private float GetPreviewCameraRotationCounterMoveX()
    {
        float spinProgress = Mathf.Clamp01(
            Mathf.Abs(PreviewCameraSpinDegrees) / 360f);
        float spinEnvelope = Mathf.Sin(spinProgress * Mathf.PI);
        float spinOffsetX = Mathf.Sign(PreviewCameraSpinDegrees) *
            spinEnvelope *
            previewCameraSpinCounterMoveX;
        float referenceTiltDegrees =
            -PreviewCameraRotationReferenceX *
            previewCameraTiltDegreesPerXUnit;
        float referenceTiltProgress = Mathf.Clamp(
            referenceTiltDegrees /
            (float)ScratchCameraTiltMap.PeakTiltDegrees,
            -1f,
            1f);
        float referenceTiltOffsetX = referenceTiltProgress *
            previewCameraReferenceTiltCounterMoveX;
        return spinOffsetX + referenceTiltOffsetX;
    }

    private void UpdatePreviewCameraPoweredKickX()
    {
        // Powered Scratch의 200ms Z 롤을 독립된 X 카메라 킥으로 변환합니다.
        float poweredTiltProgress = Mathf.Clamp(
            PreviewCameraScratchTiltDegrees /
            (float)ScratchCameraTiltMap.PeakTiltDegrees,
            -1f,
            1f);
        previewCameraPoweredKickLocalX =
            poweredTiltProgress * previewCameraPoweredKickMaxX;
        previewCameraPoweredYawDegrees =
            -poweredTiltProgress * previewCameraPoweredYawMaxDegrees;
    }

    private Vector3 GetCameraWorldOffset(float viewportOffsetY)
    {
        return scrollTrans.parent.TransformVector(
            Vector3.up * viewportOffsetY);
    }

    private float GetPreviewCameraZOffset(float viewportOffsetY)
    {
        return viewportOffsetY *
            guideGenerate.ScrollToChartRatio *
            previewHighSpeedScale;
    }
}

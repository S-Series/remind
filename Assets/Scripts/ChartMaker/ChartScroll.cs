using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChartScroll : MonoBehaviour
{
    private const float BoundsEpsilon = 0.001f;

    [SerializeField] private RectTransform scrollTrans;
    [SerializeField, Min(1f)] private float scrollPower = 40f;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.08f;
    [SerializeField, Min(0.0001f)] private float snapThreshold = 0.01f;

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

    public event Action<float> ScrollYChanged;
    public event Action<Vector2> ScrollPositionChanged;

    public Vector2 ScrollPosition => scrollRect != null
        ? scrollRect.content.anchoredPosition
        : Vector2.zero;
    public float ScrollY => ScrollPosition.y;

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
        if (scrollRect == null || isDragging)
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
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;
        scrollRect.scrollSensitivity = 0f;
    }

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

        Vector2 delta = pointerEvent.scrollDelta;
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
        if (!isActiveAndEnabled)
        {
            return;
        }

        isDragging = true;
        targetPosition = ScrollPosition;
        smoothVelocity = Vector2.zero;
    }

    private void HandleEndDrag(BaseEventData _)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        isDragging = false;
        targetPosition = ScrollPosition;
        smoothVelocity = Vector2.zero;
    }

    private Vector2 ClampContentPosition(Vector2 requestedPosition)
    {
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
        GuideGenerate.SetReferenceFromScrollY(position.y);
        ScrollPositionChanged?.Invoke(position);
        ScrollYChanged?.Invoke(position.y);
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

public class ChartScroll : MonoBehaviour
{
    [SerializeField] private RectTransform scrollTrans;
    [SerializeField, Min(1f)] private float scrollPower = 40f;

    private ScrollRect scrollRect;

    public event Action<float> ScrollYChanged;

    public float ScrollY => scrollRect != null
        ? scrollRect.content.anchoredPosition.y
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
    }

    private void OnEnable()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(HandleScrollChanged);
        }
    }

    private void OnDisable()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(HandleScrollChanged);
        }
    }

    private void ApplySettings()
    {
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = scrollPower;
    }

    private void HandleScrollChanged(Vector2 _)
    {
        ScrollYChanged?.Invoke(ScrollY);
    }
}

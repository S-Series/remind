using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using REmind.Gameplay.Chart.Data;

[RequireComponent(typeof(BoxCollider2D))]
public class ChartInput : MonoBehaviour
{
    [SerializeField] private Camera inputCamera;

    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    private BoxCollider2D chartCollider;
    private Vector2 pointerPressPosition;
    private bool isPointerPressed;

    public Vector2 LastLocalPosition { get; private set; }
    public Vector2 LastColliderPosition { get; private set; }
    public Vector2 LastNormalizedPosition { get; private set; }

    public event Action<Vector2> PositionClicked;

    private void Awake()
    {
        chartCollider = GetComponent<BoxCollider2D>();

        if (!inputCamera)
        {
            inputCamera = Camera.main;
        }

        if (!inputCamera)
        {
            Debug.LogError("ChartInput requires an input camera.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isPointerPressed = true;
            pointerPressPosition = Mouse.current.position.ReadValue();
            return;
        }

        if (!isPointerPressed || !Mouse.current.leftButton.wasReleasedThisFrame)
        {
            return;
        }

        isPointerPressed = false;
        Vector2 screenPosition = Mouse.current.position.ReadValue();
        float dragThreshold = EventSystem.current != null
            ? EventSystem.current.pixelDragThreshold
            : 10f;

        if (Vector2.Distance(pointerPressPosition, screenPosition) > dragThreshold)
        {
            return;
        }

        TryReadPosition(screenPosition);
    }

    public bool TryReadPosition(Vector2 screenPosition)
    {
        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        Plane colliderPlane = new Plane(transform.forward, transform.position);

        if (!colliderPlane.Raycast(ray, out float distance))
        {
            return false;
        }

        Vector3 worldPosition = ray.GetPoint(distance);
        if (!chartCollider.OverlapPoint(worldPosition))
        {
            return false;
        }

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        Vector2 colliderPosition = (Vector2)localPosition - chartCollider.offset;
        Vector2 halfSize = chartCollider.size * 0.5f;

        LastLocalPosition = localPosition;
        LastColliderPosition = colliderPosition;
        LastNormalizedPosition = new Vector2(
            Mathf.InverseLerp(-halfSize.x, halfSize.x, colliderPosition.x),
            Mathf.InverseLerp(-halfSize.y, halfSize.y, colliderPosition.y));

        Debug.Log(
            $"ChartInput local={LastLocalPosition}, " +
            $"collider={LastColliderPosition}, normalized={LastNormalizedPosition}",
            this);

        PositionClicked?.Invoke(LastColliderPosition);
        return true;
    }

    public void GenerateNote(NoteType type)
    {
        
    }
}

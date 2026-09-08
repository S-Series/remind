using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ChartScroll))]
public sealed class ChartMarkerNavigationController : MonoBehaviour
{
    private const double PositionComparisonEpsilon = 0.001d;

    [SerializeField] private bool wrapAround = true;

    private ChartScroll chartScroll;

    private void Awake()
    {
        chartScroll = GetComponent<ChartScroll>();
    }

    /// <summary>현재 화면 기준 위치보다 앞에 있는 Marker로 이동합니다.</summary>
    public void JumpToPreviousMarker()
    {
        JumpToAdjacentMarker(-1);
    }

    /// <summary>현재 화면 기준 위치에서 가장 가까운 Marker로 이동합니다.</summary>
    public void JumpToNearestMarker()
    {
        if (!TryGetCurrentAbsolutePosition(out double currentPosition))
        {
            return;
        }

        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;
        int targetPosition = -1;
        double nearestDistance = double.MaxValue;

        for (int i = 0; i < holders.Count; i++)
        {
            ChartHolder holder = holders[i];

            if (holder == null || !holder.isMarker)
            {
                continue;
            }

            int markerPosition = holder.AbsoluteChartPosition;
            double distance = Math.Abs(markerPosition - currentPosition);

            if (distance + PositionComparisonEpsilon < nearestDistance ||
                Math.Abs(distance - nearestDistance) <=
                PositionComparisonEpsilon &&
                (targetPosition < 0 || markerPosition < targetPosition))
            {
                targetPosition = markerPosition;
                nearestDistance = distance;
            }
        }

        if (targetPosition < 0)
        {
            LogNoMarkers();
            return;
        }

        JumpToAbsolutePosition(targetPosition);
    }

    /// <summary>현재 화면 기준 위치보다 뒤에 있는 Marker로 이동합니다.</summary>
    public void JumpToNextMarker()
    {
        JumpToAdjacentMarker(1);
    }

    private void JumpToAdjacentMarker(int direction)
    {
        if (!TryGetCurrentAbsolutePosition(out double currentPosition))
        {
            return;
        }

        IReadOnlyList<ChartHolder> holders = ChartManager.ChartHolders;
        int firstMarkerPosition = -1;
        int lastMarkerPosition = -1;
        int targetPosition = -1;

        for (int i = 0; i < holders.Count; i++)
        {
            ChartHolder holder = holders[i];

            if (holder == null || !holder.isMarker)
            {
                continue;
            }

            int markerPosition = holder.AbsoluteChartPosition;

            if (firstMarkerPosition < 0)
            {
                firstMarkerPosition = markerPosition;
            }

            lastMarkerPosition = markerPosition;

            if (direction < 0 &&
                markerPosition < currentPosition - PositionComparisonEpsilon)
            {
                targetPosition = markerPosition;
            }
            else if (direction > 0 &&
                     targetPosition < 0 &&
                     markerPosition >
                     currentPosition + PositionComparisonEpsilon)
            {
                targetPosition = markerPosition;
            }
        }

        if (firstMarkerPosition < 0)
        {
            LogNoMarkers();
            return;
        }

        if (targetPosition < 0 && wrapAround)
        {
            targetPosition = direction < 0
                ? lastMarkerPosition
                : firstMarkerPosition;
        }

        if (targetPosition >= 0)
        {
            JumpToAbsolutePosition(targetPosition);
        }
    }

    private bool TryGetCurrentAbsolutePosition(out double absolutePosition)
    {
        absolutePosition = 0d;
        GuideGenerate guideGenerate = GuideGenerate.Instance;

        if (!chartScroll || !guideGenerate ||
            guideGenerate.ScrollToChartRatio <= Mathf.Epsilon)
        {
            Debug.LogWarning(
                "Marker navigation requires ChartScroll and GuideGenerate.",
                this);
            return false;
        }

        float chartY = Mathf.Clamp(
            -chartScroll.ScrollY * guideGenerate.ScrollToChartRatio,
            0f,
            ChartHolder.AbsolutePositionToWorldY(
                ChartHolder.MaximumAbsolutePosition));
        absolutePosition = chartY * ChartHolder.PositionUnitsPerWorldUnit;
        return true;
    }

    private void JumpToAbsolutePosition(int absolutePosition)
    {
        GuideGenerate guideGenerate = GuideGenerate.Instance;

        if (!chartScroll || !guideGenerate ||
            guideGenerate.ScrollToChartRatio <= Mathf.Epsilon)
        {
            return;
        }

        float chartY = ChartHolder.AbsolutePositionToWorldY(
            absolutePosition);
        float scrollY = -chartY / guideGenerate.ScrollToChartRatio;

        // 즉시 이동해야 빠르게 연속 클릭해도 같은 Marker에 머물지 않습니다.
        chartScroll.SetScrollY(scrollY);
    }

    private void LogNoMarkers()
    {
        Debug.LogWarning("There are no Markers in the current chart.", this);
    }
}

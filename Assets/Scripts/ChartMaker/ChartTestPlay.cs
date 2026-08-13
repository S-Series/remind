using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class ChartTestPlay : MonoBehaviour
{
    private const double MillisecondsPerMinute = 60000d;
    private const double ChartPositionPerMeasure = 160d;
    private const double CameraPositionPerMeasure = 102.64006d;

    [Header("Movement")]
    [FormerlySerializedAs("moveCameraTranform")]
    [SerializeField] private Transform moveCameraTransform;
    [SerializeField] private ChartScroll chartScroll;
    [SerializeField, Min(1)] private int beatsPerMeasure = 4;

    private ChartCore chartCore;

    public float ChartPositionY { get; private set; }
    public float CameraPositionY { get; private set; }

    private void Start()
    {
        chartCore = ChartCore.Instance;

        if (chartCore == null)
        {
            Debug.LogError("ChartTestPlay requires ChartCore in the scene.", this);
            enabled = false;
            return;
        }

        if (moveCameraTransform == null)
        {
            Debug.LogError("ChartTestPlay requires a Move Transform.", this);
            enabled = false;
            return;
        }

        if (!chartScroll)
        {
            chartScroll = FindFirstObjectByType<ChartScroll>();
        }

        if (!chartScroll)
        {
            Debug.LogError("ChartTestPlay requires ChartScroll in the scene.", this);
            enabled = false;
            return;
        }

        chartCore.TestMsChanged += HandleTestMsChanged;
        chartCore.BpmChanged += HandleBpmChanged;
        chartCore.TestPlaybackChanged += HandleTestPlaybackChanged;

        if (chartCore.IsTestPlaying)
        {
            ApplyTimelinePosition(chartCore.TestMs);
        }
        else
        {
            ResetTestView();
        }
    }

    private void OnDestroy()
    {
        if (chartCore != null)
        {
            chartCore.TestMsChanged -= HandleTestMsChanged;
            chartCore.BpmChanged -= HandleBpmChanged;
            chartCore.TestPlaybackChanged -= HandleTestPlaybackChanged;
        }
    }

    private void HandleTestMsChanged(double timelineMs)
    {
        if (chartCore.IsTestPlaying)
        {
            ApplyTimelinePosition(timelineMs);
        }
    }

    private void HandleBpmChanged(double _)
    {
        if (chartCore.IsTestPlaying)
        {
            ApplyTimelinePosition(chartCore.TestMs);
        }
    }

    private void HandleTestPlaybackChanged(bool isPlaying)
    {
        if (isPlaying)
        {
            ApplyTimelinePosition(chartCore.TestMs);
        }
        else
        {
            ResetTestView();
        }
    }

    /// <summary>테스트 재생 중의 타임라인 시간을 카메라와 가이드 위치에 반영합니다.</summary>
    private void ApplyTimelinePosition(double timelineMs)
    {
        double measureProgress = CalculateMeasureProgress(
            timelineMs + chartCore.StartCorrectionMs,
            chartCore.Bpm);
        ChartPositionY = (float)(measureProgress * ChartPositionPerMeasure);
        CameraPositionY = (float)(measureProgress * CameraPositionPerMeasure);

        GuideGenerate.SetReferenceY(ChartPositionY);
        Vector3 cameraPosition = moveCameraTransform.position;
        cameraPosition.y = CameraPositionY;
        moveCameraTransform.position = cameraPosition;
    }

    /// <summary>비테스트 카메라는 0으로 복귀시키고 선택 기준은 현재 스크롤에 맞춥니다.</summary>
    private void ResetTestView()
    {
        ChartPositionY = 0f;
        CameraPositionY = 0f;
        GuideGenerate.SetReferenceFromScrollY(chartScroll.ScrollY);

        Vector3 cameraPosition = moveCameraTransform.position;
        cameraPosition.y = 0f;
        moveCameraTransform.position = cameraPosition;
    }

    /// <summary>
    /// 음악 시간을 현재까지 경과한 마디 수로 변환합니다.
    /// </summary>
    private double CalculateMeasureProgress(double audioMs, double bpm)
    {
        double beatCount = audioMs * bpm / MillisecondsPerMinute;
        return beatCount / beatsPerMeasure;
    }
}

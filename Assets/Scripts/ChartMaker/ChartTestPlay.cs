using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChartTestPlay : MonoBehaviour
{
    private const double MillisecondsPerMinute = 60000d;
    private const float CameraPositionCorrection = 102.6401f;

    [Header("Movement")]
    [SerializeField] private Transform moveCameraTranform;
    [SerializeField, Min(1)] private int beatsPerMeasure = 4;
    [SerializeField, Min(1f)] private float measureHeight = 160f;

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

        if (moveCameraTranform == null)
        {
            Debug.LogError("ChartTestPlay requires a Move Transform.", this);
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (!chartCore.IsTestPlaying) return;

        ChartPositionY = (float)CalculateChartPositionY(
            chartCore.CorrectedTestMs,
            chartCore.Bpm);
        CameraPositionY = ChartPositionY * CameraPositionCorrection / 160f;

        float fieldScaleY = Mathf.Abs(moveCameraTranform.localScale.y);

        GuideGenerate.SetReferenceY(ChartPositionY);
        moveCameraTranform.position = new Vector2(0, CameraPositionY);
    }

    private float CalculateChartPositionY(double audioMs, double bpm)
    {
        double beatCount = audioMs * bpm / MillisecondsPerMinute;
        double measureCount = beatCount / beatsPerMeasure;
        return (float)(measureCount * measureHeight);

        // 102.6401
    }
}

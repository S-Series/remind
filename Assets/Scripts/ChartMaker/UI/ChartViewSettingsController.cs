using System;
using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChartViewSettingsController : MonoBehaviour
{
    public const float DefaultHiSpeed = 2.5f;

    [SerializeField] private ChartScroll chartScroll;
    [SerializeField] private TMP_InputField viewPageInput;
    [SerializeField] private TMP_InputField hiSpeedInput;
    [SerializeField] private Transform chartPreviewNoteField;
    [SerializeField, Min(0.1f)] private float minimumHiSpeed = 0.1f;
    [SerializeField, Min(0.1f)] private float maximumHiSpeed = 20f;

    private Vector3 previewNoteFieldBaseScale;
    private float hiSpeed = DefaultHiSpeed;
    private bool initialized;

    public int ViewPage { get; private set; }
    public float HiSpeed => hiSpeed;

    private void Awake()
    {
        if (!chartScroll ||
            !viewPageInput ||
            !hiSpeedInput ||
            !chartPreviewNoteField)
        {
            Debug.LogError(
                "ChartViewSettingsController requires ChartScroll, View Page, " +
                "Hi-Speed, and the Preview NoteField.",
                this);
            enabled = false;
            return;
        }

        minimumHiSpeed = Mathf.Max(0.1f, minimumHiSpeed);
        maximumHiSpeed = Mathf.Max(minimumHiSpeed, maximumHiSpeed);
        previewNoteFieldBaseScale = chartPreviewNoteField.localScale;
        viewPageInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        hiSpeedInput.contentType = TMP_InputField.ContentType.DecimalNumber;
        ApplyHiSpeed(DefaultHiSpeed);
        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            return;
        }

        chartScroll.ScrollYChanged += HandleScrollYChanged;
        viewPageInput.onEndEdit.AddListener(HandleViewPageEdited);
        hiSpeedInput.onEndEdit.AddListener(HandleHiSpeedEdited);
    }

    private void Start()
    {
        if (initialized)
        {
            RefreshViewPage(chartScroll.ScrollY);
        }
    }

    private void OnDisable()
    {
        if (!initialized)
        {
            return;
        }

        chartScroll.ScrollYChanged -= HandleScrollYChanged;
        viewPageInput.onEndEdit.RemoveListener(HandleViewPageEdited);
        hiSpeedInput.onEndEdit.RemoveListener(HandleHiSpeedEdited);
    }

    private void HandleScrollYChanged(float scrollY)
    {
        RefreshViewPage(scrollY);
    }

    private void HandleViewPageEdited(string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int requestedPage))
        {
            RefreshViewPage(chartScroll.ScrollY);
            return;
        }

        GuideGenerate guideGenerate = GuideGenerate.Instance;

        if (!guideGenerate ||
            guideGenerate.ScrollToChartRatio <= Mathf.Epsilon)
        {
            RefreshViewPage(chartScroll.ScrollY);
            return;
        }

        int page = Mathf.Clamp(
            requestedPage,
            0,
            ChartHolder.MaximumMeasureNumber);
        float chartY = page * guideGenerate.MeasureHeight;
        float scrollY = -chartY / guideGenerate.ScrollToChartRatio;
        chartScroll.SetScrollY(scrollY);
        RefreshViewPage(chartScroll.ScrollY);
    }

    private void HandleHiSpeedEdited(string value)
    {
        if (!TryParseFloat(value, out float requestedHiSpeed))
        {
            RefreshHiSpeedText();
            return;
        }

        ApplyHiSpeed(requestedHiSpeed);
    }

    private void RefreshViewPage(float scrollY)
    {
        GuideGenerate guideGenerate = GuideGenerate.Instance;

        if (!guideGenerate || guideGenerate.MeasureHeight <= Mathf.Epsilon)
        {
            ViewPage = 0;
        }
        else
        {
            float chartY = Mathf.Max(
                0f,
                -scrollY * guideGenerate.ScrollToChartRatio);
            ViewPage = Mathf.Clamp(
                Mathf.FloorToInt(
                    chartY / guideGenerate.MeasureHeight + 0.0001f),
                0,
                ChartHolder.MaximumMeasureNumber);
        }

        viewPageInput.SetTextWithoutNotify(
            ViewPage.ToString(CultureInfo.InvariantCulture));
    }

    private void ApplyHiSpeed(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            value = DefaultHiSpeed;
        }

        hiSpeed = Mathf.Clamp(
            Mathf.Round(value * 100f) / 100f,
            minimumHiSpeed,
            maximumHiSpeed);
        float speedScale = hiSpeed / DefaultHiSpeed;
        Vector3 noteFieldScale = previewNoteFieldBaseScale;
        noteFieldScale.y *= speedScale;
        chartPreviewNoteField.localScale = noteFieldScale;
        chartScroll.SetPreviewHighSpeedScale(speedScale);
        RefreshHiSpeedText();
    }

    private void RefreshHiSpeedText()
    {
        hiSpeedInput.SetTextWithoutNotify(
            hiSpeed.ToString("0.00", CultureInfo.InvariantCulture));
    }

    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(
                   value,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out result) ||
               float.TryParse(
                   value,
                   NumberStyles.Float,
                   CultureInfo.CurrentCulture,
                   out result);
    }
}

using System.Globalization;
using REmind.Common.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("REmind/Chart Maker/Chart Data Popup")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class ChartDataPopup : PopupContext
{
    private static readonly CultureInfo Invariant =
        CultureInfo.InvariantCulture;

    [Header("View")]
    [SerializeField] private ChartDataPopupView popupPrefab;

    [SerializeField] private ChartCore chartCore;
    [SerializeField] private ChartMakerTopMenuController topMenuController;

    [Header("Auto Correction")]
    [SerializeField, Range(1, 30)] private int maximumScanDurationSeconds = 10;
    [SerializeField, Min(256)] private int fftSize = 2048;
    [SerializeField, Min(1)] private int hopSize = 256;
    [SerializeField, Range(1f, 12f)] private float peakThresholdMadMultiplier = 6f;
    [SerializeField, Min(0f)] private float minimumOnsetDistanceMs = 80f;
    [SerializeField, Min(1f)] private float maximumMatchDistanceMs = 120f;

    private TMP_InputField bpmInput;
    private TMP_InputField musicStartCorrectionInput;
    private TMP_Text errorText;
    private RawImage spectrumImage;
    private Button backdropButton;
    private Button closeButton;
    private Button autoCorrectionButton;
    private Button openChartButton;
    private Button openMusicButton;
    private Button cancelButton;
    private Button applyButton;
    private Texture2D spectrumTexture;

    protected override GameObject InitialSelection =>
        bpmInput ? bpmInput.gameObject : null;

    /// <summary>Chart Data Prefab을 생성하고 입력 이벤트를 연결합니다.</summary>
    protected override GameObject BuildPopup()
    {
        ResolveReferences();

        if (!popupPrefab)
        {
            Debug.LogError(
                "ChartDataPopup requires a ChartDataPopupView prefab.",
                this);
            return null;
        }

        if (!chartCore)
        {
            Debug.LogError(
                "ChartDataPopup requires ChartCore.",
                this);
            return null;
        }

        Canvas canvas = GetComponentInParent<Canvas>();

        if (!canvas)
        {
            Debug.LogError(
                "ChartDataPopup must be placed under a Canvas.",
                this);
            return null;
        }

        ChartDataPopupView view = Instantiate(
            popupPrefab,
            canvas.transform,
            false);
        GameObject root = view.gameObject;
        root.name = popupPrefab.gameObject.name;
        view.SetLayerRecursively(canvas.gameObject.layer);

        if (!view.TryResolveReferences(out string viewError))
        {
            Debug.LogError(viewError, view);
            Destroy(root);
            return null;
        }

        bpmInput = view.BpmInput;
        musicStartCorrectionInput = view.MusicStartCorrectionInput;
        errorText = view.ErrorText;
        spectrumImage = view.SpectrumImage;
        backdropButton = view.BackdropButton;
        closeButton = view.CloseButton;
        autoCorrectionButton = view.AutoCorrectionButton;
        openChartButton = view.OpenChartButton;
        openMusicButton = view.OpenMusicButton;
        cancelButton = view.CancelButton;
        applyButton = view.ApplyButton;

        backdropButton.onClick.AddListener(Close);
        closeButton.onClick.AddListener(Close);
        autoCorrectionButton.onClick.AddListener(AutoAdjustCorrection);
        openChartButton.onClick.AddListener(OpenChartFile);
        openMusicButton.onClick.AddListener(OpenMusicFile);
        cancelButton.onClick.AddListener(Close);
        applyButton.onClick.AddListener(ApplyValuesAndClose);
        bpmInput.onSubmit.AddListener(HandleSubmit);
        musicStartCorrectionInput.onSubmit.AddListener(HandleSubmit);
        chartCore.BpmChanged += HandleBpmChanged;
        chartCore.StartCorrectionMsChanged += HandleStartCorrectionChanged;
        chartCore.AudioClipChanged += HandleAudioClipChanged;

        if (topMenuController)
        {
            topMenuController.ChartOpened += HandleChartOpened;
        }

        return root;
    }

    protected override void OnOpening()
    {
        PopulateFields();
        RefreshSpectrumPreview();
    }

    protected override void OnDestroy()
    {
        if (chartCore)
        {
            chartCore.BpmChanged -= HandleBpmChanged;
            chartCore.StartCorrectionMsChanged -= HandleStartCorrectionChanged;
            chartCore.AudioClipChanged -= HandleAudioClipChanged;
        }

        if (topMenuController)
        {
            topMenuController.ChartOpened -= HandleChartOpened;
        }

        DestroySpectrumTexture();
        base.OnDestroy();
    }

    private void ResolveReferences()
    {
        if (!chartCore)
        {
            chartCore = ChartCore.Instance != null
                ? ChartCore.Instance
                : FindFirstObjectByType<ChartCore>();
        }

        if (!topMenuController)
        {
            topMenuController =
                FindFirstObjectByType<ChartMakerTopMenuController>();
        }
    }

    /// <summary>현재 ChartCore 값을 입력창에 채우고 이전 오류를 초기화합니다.</summary>
    private void PopulateFields()
    {
        bpmInput.SetTextWithoutNotify(
            chartCore.Bpm.ToString("R", Invariant));
        musicStartCorrectionInput.SetTextWithoutNotify(
            FormatDisplayCorrection(chartCore.StartCorrectionMs));
        SetError(null);
    }

    private void HandleBpmChanged(double value)
    {
        if (IsOpen && bpmInput)
        {
            bpmInput.SetTextWithoutNotify(value.ToString("R", Invariant));
        }
    }

    private void HandleStartCorrectionChanged(double value)
    {
        if (IsOpen && musicStartCorrectionInput)
        {
            musicStartCorrectionInput.SetTextWithoutNotify(
                FormatDisplayCorrection(value));
        }
    }

    private void HandleAudioClipChanged(AudioClip _)
    {
        if (IsOpen)
        {
            RefreshSpectrumPreview();
        }
    }

    private void HandleChartOpened()
    {
        if (!IsOpen)
        {
            return;
        }

        PopulateFields();
        RefreshSpectrumPreview();
    }

    /// <summary>입력값을 검증한 뒤 채보의 기준 BPM과 음악 시작 보정값에 적용합니다.</summary>
    private void ApplyValuesAndClose()
    {
        ApplyValues(closeAfterApply: true);
    }

    private bool ApplyValues(bool closeAfterApply)
    {
        if (!TryParseFinite(bpmInput.text, out double bpm) || bpm <= 0d)
        {
            SetError("BPM must be greater than zero.");
            SelectInput(bpmInput);
            return false;
        }

        if (!TryParseFinite(
                musicStartCorrectionInput.text,
                out double correctionMs))
        {
            SetError("Music Start Correction must be a valid number.");
            SelectInput(musicStartCorrectionInput);
            return false;
        }

        chartCore.SetBpm(bpm);
        chartCore.SetStartCorrectionMs(-correctionMs);
        SetError(null);

        if (closeAfterApply)
        {
            Close();
        }

        return true;
    }

    private void HandleSubmit(string _)
    {
        ApplyValues(closeAfterApply: false);
    }

    /// <summary>팝업을 유지한 채 기존 채보 파일 열기 흐름을 실행합니다.</summary>
    private void OpenChartFile()
    {
        if (!topMenuController)
        {
            SetError("Chart file menu controller was not found.");
            return;
        }

        topMenuController.RequestOpenChartFile();
    }

    /// <summary>팝업을 유지한 채 기존 음악 파일 선택 흐름을 실행합니다.</summary>
    private void OpenMusicFile()
    {
        if (!topMenuController)
        {
            SetError("Music file menu controller was not found.");
            return;
        }

        topMenuController.RequestOpenMusicFile();
    }

    /// <summary>채보 유무와 관계없이 음악의 첫 onset으로 시작 보정값을 계산합니다.</summary>
    private void AutoAdjustCorrection()
    {
        AudioClip clip = chartCore.AudioSource != null
            ? chartCore.AudioSource.clip
            : null;

        if (!clip)
        {
            SetError("Load a music file before running auto correction.");
            return;
        }

        AudioOnsetOffsetAnalyzer.Settings settings =
            new AudioOnsetOffsetAnalyzer.Settings(
                maximumScanDurationSeconds,
                fftSize,
                hopSize,
                peakThresholdMadMultiplier,
                minimumOnsetDistanceMs,
                maximumMatchDistanceMs);

        if (!AudioOnsetOffsetAnalyzer.TryAnalyzeFirstOnset(
                clip,
                settings,
                out AudioOnsetOffsetAnalyzer.Result result))
        {
            SetError("No clear audio onset was found near the beginning.");
            return;
        }

        musicStartCorrectionInput.SetTextWithoutNotify(
            FormatDisplayCorrection(result.ChartCorrectionMs));
        double displayCorrectionMs = -result.ChartCorrectionMs;
        Debug.Log(
            $"Recommended audio offset: {result.AudioOffsetMs:+0.###;-0.###;0}ms, " +
            $"displayed correction: {displayCorrectionMs:+0.###;-0.###;0}ms " +
            $"({result.DetectedOnsetCount} audio onsets detected).",
            this);
        SetError(null);
    }

    private void RefreshSpectrumPreview()
    {
        if (!spectrumImage)
        {
            return;
        }

        AudioClip clip = chartCore != null && chartCore.AudioSource != null
            ? chartCore.AudioSource.clip
            : null;

        DestroySpectrumTexture();

        AudioOnsetOffsetAnalyzer.Settings settings =
            new AudioOnsetOffsetAnalyzer.Settings(
                maximumScanDurationSeconds,
                fftSize,
                hopSize,
                peakThresholdMadMultiplier,
                minimumOnsetDistanceMs,
                maximumMatchDistanceMs);

        if (AudioOnsetOffsetAnalyzer.TryCreateSpectrogramTexture(
                clip,
                settings,
                384,
                72,
                out spectrumTexture))
        {
            spectrumImage.texture = spectrumTexture;
            spectrumImage.color = Color.white;
            return;
        }

        spectrumImage.texture = Texture2D.whiteTexture;
        spectrumImage.color = new Color(0.1f, 0.11f, 0.14f, 1f);
    }

    private void DestroySpectrumTexture()
    {
        if (!spectrumTexture)
        {
            return;
        }

        Destroy(spectrumTexture);
        spectrumTexture = null;
    }

    private void SetError(string message)
    {
        bool hasError = !string.IsNullOrWhiteSpace(message);
        errorText.text = hasError ? message : string.Empty;
        errorText.gameObject.SetActive(hasError);
    }

    private static void SelectInput(TMP_InputField input)
    {
        if (!input)
        {
            return;
        }

        EventSystem.current?.SetSelectedGameObject(input.gameObject);
        input.ActivateInputField();
        input.Select();
    }

    private static bool TryParseFinite(string value, out double result)
    {
        bool parsed = double.TryParse(
                value,
                NumberStyles.Float,
                Invariant,
                out result) ||
            double.TryParse(value, out result);
        return parsed && !double.IsNaN(result) && !double.IsInfinity(result);
    }

    private static string FormatDisplayCorrection(double internalCorrectionMs)
    {
        return (-internalCorrectionMs).ToString(
            "+0.###;-0.###;0",
            Invariant);
    }

}

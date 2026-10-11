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
    private TMP_InputField levelInput;
    private TMP_InputField authorInput;
    private TMP_InputField extraNameInput;
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
        BuildDisplayMetadataInputs(view);
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
        levelInput.onSubmit.AddListener(HandleSubmit);
        authorInput.onSubmit.AddListener(HandleSubmit);
        extraNameInput.onSubmit.AddListener(HandleSubmit);
        chartCore.BpmChanged += HandleBpmChanged;
        chartCore.StartCorrectionMsChanged += HandleStartCorrectionChanged;
        chartCore.AudioClipChanged += HandleAudioClipChanged;
        chartCore.AudioLoadFailed += HandleAudioLoadFailed;

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
            chartCore.AudioLoadFailed -= HandleAudioLoadFailed;
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
        levelInput.SetTextWithoutNotify(ChartEffectDocumentState.Level.ToString(Invariant));
        authorInput.SetTextWithoutNotify(ChartEffectDocumentState.Author ?? string.Empty);
        extraNameInput.SetTextWithoutNotify(ChartEffectDocumentState.ExtraName ?? string.Empty);
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

    private void HandleAudioLoadFailed(string message)
    {
        if (IsOpen)
        {
            SetError(message);
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
        if (chartCore.IsTestPlaying ||
            chartCore.IsTestPlaybackStartInProgress)
        {
            SetError("Stop Preview before changing BPM or music correction.");
            return false;
        }

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

        if (!int.TryParse(levelInput.text, NumberStyles.Integer, Invariant,
                out int level) || level < 0)
        {
            SetError("Level must be a non-negative whole number.");
            SelectInput(levelInput);
            return false;
        }
        string author = authorInput.text?.Trim();
        if (string.IsNullOrWhiteSpace(author))
        {
            SetError("Chart author is required.");
            SelectInput(authorInput);
            return false;
        }
        string extraName = extraNameInput.text?.Trim() ?? string.Empty;

        chartCore.SetBpm(bpm);
        chartCore.SetStartCorrectionMs(-correctionMs);
        ChartEffectDocumentState.Level = level;
        ChartEffectDocumentState.Author = author;
        ChartEffectDocumentState.ExtraName = extraName;
        ChartManager.NotifyChartChanged();
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

    private void BuildDisplayMetadataInputs(ChartDataPopupView view)
    {
        Transform window = view.transform.Find("Window");
        RectTransform windowRect = (RectTransform)window;
        windowRect.sizeDelta = new Vector2(windowRect.sizeDelta.x, 640f);
        Move("Title", 292f);
        Move("Close", 292f);
        Move("BPM Label", 233f);
        Move("BPM Input", 233f);
        Move("Music Start Correction (ms) Label", 180f);
        Move("Music Start Correction Input", 180f);
        Move("Open Chart", 131f);
        Move("Open Music", 131f);
        Move("Auto Correction", 131f);
        Move("Spectrum Preview", -110f);
        Move("Error", -173f);
        Move("Cancel", -278f);
        Move("Apply", -278f);

        TMP_Text labelTemplate = window.Find("BPM Label").GetComponent<TMP_Text>();
        levelInput = AddInput("Level", 78f, TMP_InputField.ContentType.IntegerNumber);
        authorInput = AddInput("Author", 25f, TMP_InputField.ContentType.Standard);
        extraNameInput = AddInput("Extra Name", -28f, TMP_InputField.ContentType.Standard);

        void Move(string name, float y)
        {
            RectTransform item = (RectTransform)window.Find(name);
            Vector2 position = item.anchoredPosition;
            position.y = y;
            item.anchoredPosition = position;
        }

        TMP_InputField AddInput(string name, float y,
            TMP_InputField.ContentType contentType)
        {
            TMP_Text label = Instantiate(labelTemplate, window);
            label.gameObject.name = name + " Label";
            label.text = name;
            ((RectTransform)label.transform).anchoredPosition = new Vector2(-135f, y);
            TMP_InputField input = Instantiate(bpmInput, window);
            input.gameObject.name = name + " Input";
            input.contentType = contentType;
            input.characterLimit = 0;
            input.SetTextWithoutNotify(string.Empty);
            ((RectTransform)input.transform).anchoredPosition = new Vector2(105f, y);
            return input;
        }
    }

}

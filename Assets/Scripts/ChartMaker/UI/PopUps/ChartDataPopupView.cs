using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public sealed class ChartDataPopupView : MonoBehaviour
{
    private const string PopupSortingLayerName = "UI";

    [Header("Inputs")]
    [SerializeField] private TMP_InputField bpmInput;
    [SerializeField] private TMP_InputField musicStartCorrectionInput;

    [Header("Display")]
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private RawImage spectrumImage;

    [Header("Actions")]
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button autoCorrectionButton;
    [SerializeField] private Button openChartButton;
    [SerializeField] private Button openMusicButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button applyButton;

    public TMP_InputField BpmInput => bpmInput;
    public TMP_InputField MusicStartCorrectionInput =>
        musicStartCorrectionInput;
    public TMP_Text ErrorText => errorText;
    public RawImage SpectrumImage => spectrumImage;
    public Button BackdropButton => backdropButton;
    public Button CloseButton => closeButton;
    public Button AutoCorrectionButton => autoCorrectionButton;
    public Button OpenChartButton => openChartButton;
    public Button OpenMusicButton => openMusicButton;
    public Button CancelButton => cancelButton;
    public Button ApplyButton => applyButton;

    public bool TryResolveReferences(out string error)
    {
        ResolveReferences();
        ApplyCanvasSorting();

        if (bpmInput &&
            musicStartCorrectionInput &&
            errorText &&
            spectrumImage &&
            backdropButton &&
            closeButton &&
            autoCorrectionButton &&
            openChartButton &&
            openMusicButton &&
            cancelButton &&
            applyButton)
        {
            error = null;
            return true;
        }

        error =
            "Chart Data Popup prefab is missing one or more required UI references.";
        return false;
    }

    public void SetLayerRecursively(int layer)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ApplyCanvasSorting();
    }

    /// <summary>프리팹과 런타임 인스턴스가 UI Sorting Layer를 사용하도록 보정합니다.</summary>
    public void ApplyCanvasSorting()
    {
        Canvas popupCanvas = GetComponent<Canvas>();

        if (!popupCanvas)
        {
            return;
        }

        popupCanvas.overrideSorting = true;
        popupCanvas.sortingLayerName = PopupSortingLayerName;
    }

    private void ResolveReferences()
    {
        bpmInput = Resolve(bpmInput, "Window/BPM Input");
        musicStartCorrectionInput = Resolve(
            musicStartCorrectionInput,
            "Window/Music Start Correction Input");
        errorText = Resolve(errorText, "Window/Error");
        spectrumImage = Resolve(spectrumImage, "Window/Spectrum Preview");
        backdropButton = Resolve(backdropButton, "Backdrop");
        closeButton = Resolve(closeButton, "Window/Close");
        autoCorrectionButton = Resolve(
            autoCorrectionButton,
            "Window/Auto Correction");
        openChartButton = Resolve(openChartButton, "Window/Open Chart");
        openMusicButton = Resolve(openMusicButton, "Window/Open Music");
        cancelButton = Resolve(cancelButton, "Window/Cancel");
        applyButton = Resolve(applyButton, "Window/Apply");
    }

    private T Resolve<T>(T current, string path)
        where T : Component
    {
        if (current)
        {
            return current;
        }

        Transform child = transform.Find(path);
        return child ? child.GetComponent<T>() : null;
    }
}
